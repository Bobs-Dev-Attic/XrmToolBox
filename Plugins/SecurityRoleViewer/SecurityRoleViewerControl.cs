using Microsoft.Xrm.Sdk;
using SecurityRoleViewer.Export;
using SecurityRoleViewer.Models;
using SecurityRoleViewer.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Args;
using XrmToolBox.Extensibility.Interfaces;

namespace SecurityRoleViewer
{
    public partial class SecurityRoleViewerControl : PluginControlBase, IGitHubPlugin, IHelpPlugin, IStatusBarMessenger
    {
        private static readonly string[] PrivilegeColumns =
            { "Create", "Read", "Write", "Delete", "Append", "AppendTo", "Assign", "Share" };

        private static readonly (int Depth, string Label)[] AccessLevels =
        {
            (8, "Organization"),
            (4, "Parent-Child BU"),
            (2, "Business Unit"),
            (1, "User"),
            (0, "None")
        };

        private SecurityRoleService _roleService;
        private List<Entity> _allRoles;
        private readonly Dictionary<Guid, List<RolePrivilegeInfo>> _loadedPrivileges
            = new Dictionary<Guid, List<RolePrivilegeInfo>>();
        private Dictionary<string, string> _entityDisplayNames;
        private bool _showDisplayNames;
        private readonly Dictionary<int, ToolStripMenuItem> _levelMenuItems
            = new Dictionary<int, ToolStripMenuItem>();
        private readonly ToolStripMenuItem[] _columnMenuItems
            = new ToolStripMenuItem[PrivilegeColumns.Length];

        public SecurityRoleViewerControl()
        {
            InitializeComponent();
            BuildLevelFilterMenu();
            BuildColumnFilterMenu();
            Load += SecurityRoleViewerControl_Load;
        }

        private void SecurityRoleViewerControl_Load(object sender, EventArgs e)
        {
            // Default the role list to ~20% of the width; the plugin docks at a
            // larger width than the designer surface, so set this proportionally.
            var target = (int)(splitContainer1.Width * 0.2);
            if (target >= splitContainer1.Panel1MinSize
                && target <= splitContainer1.Width - splitContainer1.Panel2MinSize)
            {
                splitContainer1.SplitterDistance = target;
            }
        }

        private void BuildLevelFilterMenu()
        {
            foreach (var level in AccessLevels)
            {
                var item = new ToolStripMenuItem(level.Label)
                {
                    Checked = true,
                    CheckOnClick = true,
                    Tag = level.Depth
                };
                item.CheckedChanged += (s, e) => RebuildMatrixPanel();
                _levelMenuItems[level.Depth] = item;
                tsddLevels.DropDownItems.Add(item);
            }
        }

        private void BuildColumnFilterMenu()
        {
            for (int c = 0; c < PrivilegeColumns.Length; c++)
            {
                var item = new ToolStripMenuItem(ColumnLabel(PrivilegeColumns[c]))
                {
                    Checked = true,
                    CheckOnClick = true,
                    Tag = c
                };
                item.CheckedChanged += (s, e) => RebuildMatrixPanel();
                _columnMenuItems[c] = item;
                tsddColumns.DropDownItems.Add(item);
            }
        }

        private static string ColumnLabel(string privilegeType)
            => privilegeType == "AppendTo" ? "Append To" : privilegeType;

        private string GetEntityLabel(string logicalName)
        {
            if (!_showDisplayNames || _entityDisplayNames == null) return logicalName;
            return _entityDisplayNames.TryGetValue(logicalName, out var display) ? display : logicalName;
        }

        private HashSet<int> GetVisibleDepths()
        {
            var set = new HashSet<int>();
            foreach (var kv in _levelMenuItems)
                if (kv.Value.Checked)
                    set.Add(kv.Key);
            return set;
        }

        private bool[] GetVisibleColumns()
        {
            var visible = new bool[PrivilegeColumns.Length];
            for (int c = 0; c < PrivilegeColumns.Length; c++)
                visible[c] = _columnMenuItems[c].Checked;
            return visible;
        }

        public event EventHandler<StatusBarMessageEventArgs> SendMessageToStatusBar;

        public string RepositoryName => "XrmToolBox";
        public string UserName => "MscrmTools";
        public string HelpUrl => "https://github.com/MscrmTools/XrmToolBox";

        private void tsbLoadRoles_Click(object sender, EventArgs e)
        {
            ExecuteMethod(LoadRoles);
        }

        private void LoadRoles()
        {
            _roleService = new SecurityRoleService(Service);
            _loadedPrivileges.Clear();

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading security roles...",
                Work = (w, args) =>
                {
                    args.Result = _roleService.GetRoles();
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Loading Roles");
                        return;
                    }

                    _allRoles = (List<Entity>)args.Result;
                    _entityDisplayNames = null;
                    _showDisplayNames = false;
                    tsbDisplayNames.Checked = false;
                    tsbDisplayNames.Enabled = true;
                    PopulateRoleList();
                    tsbExport.Enabled = false;
                    RebuildMatrixPanel();

                    SendMessageToStatusBar?.Invoke(this,
                        new StatusBarMessageEventArgs($"Loaded {_allRoles.Count} roles"));
                }
            });
        }

        private void PopulateRoleList()
        {
            clbRoles.Items.Clear();
            var searchText = tstSearch.Text?.Trim() ?? "";

            foreach (var role in _allRoles)
            {
                var roleName = role.GetAttributeValue<string>("name") ?? "";
                var roleId = role.GetAttributeValue<Guid>("roleid");
                var buRef = role.GetAttributeValue<EntityReference>("businessunitid");
                var buName = buRef?.Name ?? "";

                if (!string.IsNullOrEmpty(searchText)
                    && roleName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var displayName = string.IsNullOrEmpty(buName)
                    ? roleName
                    : $"{roleName} ({buName})";

                var item = new RoleListItem(roleId, roleName, displayName);
                bool wasChecked = _loadedPrivileges.ContainsKey(roleId);
                clbRoles.Items.Add(item, wasChecked);
            }
        }

        private void clbRoles_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            var item = clbRoles.Items[e.Index] as RoleListItem;
            if (item == null) return;

            if (e.NewValue == CheckState.Checked)
            {
                if (!_loadedPrivileges.ContainsKey(item.RoleId))
                {
                    LoadRolePrivileges(item.RoleId, item.RoleName);
                    return;
                }
            }
            else
            {
                _loadedPrivileges.Remove(item.RoleId);
            }

            BeginInvoke((Action)RebuildMatrixPanel);
        }

        private void LoadRolePrivileges(Guid roleId, string roleName)
        {
            if (_roleService == null) return;

            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Loading privileges for {roleName}...",
                Work = (w, args) =>
                {
                    args.Result = _roleService.GetRolePrivileges(roleId, roleName);
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Loading Privileges");
                        return;
                    }

                    _loadedPrivileges[roleId] = (List<RolePrivilegeInfo>)args.Result;
                    tsbExport.Enabled = _loadedPrivileges.Count > 0;
                    RebuildMatrixPanel();

                    SendMessageToStatusBar?.Invoke(this,
                        new StatusBarMessageEventArgs(
                            $"Loaded {_loadedPrivileges[roleId].Count} privileges for {roleName}"));
                }
            });
        }

        private void RebuildMatrixPanel()
        {
            tabRoles.SuspendLayout();

            var selectedTabName = tabRoles.SelectedTab?.Name;

            foreach (TabPage page in tabRoles.TabPages)
            {
                foreach (Control c in page.Controls)
                    c.Dispose();
            }
            tabRoles.TabPages.Clear();

            var checkedRoleIds = new List<Guid>();
            for (int i = 0; i < clbRoles.Items.Count; i++)
            {
                if (clbRoles.GetItemChecked(i))
                {
                    var item = clbRoles.Items[i] as RoleListItem;
                    if (item != null && _loadedPrivileges.ContainsKey(item.RoleId))
                        checkedRoleIds.Add(item.RoleId);
                }
            }

            if (checkedRoleIds.Count == 0)
            {
                tabRoles.ResumeLayout();
                tabRoles.Visible = false;
                lblEmpty.Visible = true;
                tsbExport.Enabled = false;
                return;
            }

            lblEmpty.Visible = false;
            tabRoles.Visible = true;
            tsbExport.Enabled = true;

            var keyword = tstEntitySearch.Text?.Trim() ?? "";
            var visibleDepths = GetVisibleDepths();
            var visibleColumns = GetVisibleColumns();

            foreach (var roleId in checkedRoleIds)
            {
                var privileges = _loadedPrivileges[roleId];
                var roleName = privileges.FirstOrDefault()?.RoleName ?? "Unknown";

                var page = new TabPage(roleName)
                {
                    Name = "tab_" + roleId.ToString("N"),
                    ToolTipText = roleName,
                    UseVisualStyleBackColor = true,
                    Padding = new Padding(3)
                };

                var rows = BuildEntityRows(privileges, keyword, visibleDepths, visibleColumns);

                var grid = CreateMatrixGrid(rows, visibleColumns);
                grid.Dock = DockStyle.Fill;
                grid.ScrollBars = ScrollBars.Both;

                page.Controls.Add(grid);
                tabRoles.TabPages.Add(page);

                if (page.Name == selectedTabName)
                    tabRoles.SelectedTab = page;
            }

            tabRoles.ResumeLayout();
        }

        private List<EntityRow> BuildEntityRows(
            List<RolePrivilegeInfo> privileges, string keyword,
            HashSet<int> visibleDepths, bool[] visibleColumns)
        {
            var rows = new List<EntityRow>();

            var entityGroups = privileges
                .Where(p => !string.IsNullOrEmpty(p.EntityName))
                .GroupBy(p => p.EntityName)
                .OrderBy(g => GetEntityLabel(g.Key));

            foreach (var group in entityGroups)
            {
                var label = GetEntityLabel(group.Key);
                if (keyword.Length > 0
                    && label.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0
                    && group.Key.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var byType = group
                    .GroupBy(p => p.PrivilegeType)
                    .ToDictionary(g => g.Key, g => g.Max(p => p.Depth));

                var depths = new int[PrivilegeColumns.Length];
                bool anyVisible = false;
                for (int c = 0; c < PrivilegeColumns.Length; c++)
                {
                    int depth = byType.TryGetValue(PrivilegeColumns[c], out var d) ? d : 0;
                    if (visibleDepths.Contains(depth))
                    {
                        depths[c] = depth;
                        if (visibleColumns[c])
                            anyVisible = true;
                    }
                    else
                    {
                        depths[c] = -1; // hidden by level filter
                    }
                }

                if (!anyVisible)
                    continue;

                rows.Add(new EntityRow { EntityName = label, Depths = depths });
            }

            return rows;
        }

        private DataGridView CreateMatrixGrid(List<EntityRow> entityRows, bool[] visibleColumns)
        {
            var grid = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                DefaultCellStyle = { SelectionBackColor = Color.White, SelectionForeColor = Color.Black },
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(230, 230, 230)
            };

            var entityCol = new DataGridViewTextBoxColumn
            {
                HeaderText = "Entity",
                Name = "colEntity",
                Width = 180,
                SortMode = DataGridViewColumnSortMode.Automatic
            };
            grid.Columns.Add(entityCol);

            // Only build the privilege columns the user has chosen to show; keep
            // the original column index so cell values map back to the depth array.
            var columnMap = new List<int>();
            for (int c = 0; c < PrivilegeColumns.Length; c++)
            {
                if (!visibleColumns[c])
                    continue;

                var col = new DataGridViewTextBoxColumn
                {
                    HeaderText = ColumnLabel(PrivilegeColumns[c]),
                    Name = "col" + PrivilegeColumns[c],
                    Width = 65,
                    SortMode = DataGridViewColumnSortMode.Automatic,
                    DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
                };
                grid.Columns.Add(col);
                columnMap.Add(c);
            }

            foreach (var entityRow in entityRows)
            {
                var row = new DataGridViewRow();
                row.CreateCells(grid);
                row.Cells[0].Value = entityRow.EntityName;

                for (int k = 0; k < columnMap.Count; k++)
                    row.Cells[k + 1].Value = entityRow.Depths[columnMap[k]];

                grid.Rows.Add(row);
            }

            grid.CellPainting += MatrixGrid_CellPainting;
            return grid;
        }

        private void MatrixGrid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 1) return;
            if (!(e.Value is int depth)) return;

            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

            // -1 is a level filtered out of view: leave the cell blank.
            if (depth < 0)
            {
                e.Handled = true;
                return;
            }

            int size = 15;
            int x = e.CellBounds.X + (e.CellBounds.Width - size) / 2;
            int y = e.CellBounds.Y + (e.CellBounds.Height - size) / 2;
            var rect = new Rectangle(x, y, size, size);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Native Dataverse access-level icons are "pie clock" glyphs: the
            // colored wedge grows with the access level (none -> org). The start
            // angle is offset per level to match the native icon orientation.
            Color color;
            float sweep;
            float start;
            switch (depth)
            {
                case 1:  color = Color.FromArgb(237, 162, 0);  sweep = 90f;  start = 0f; break;   // User - amber, +60 CW
                case 2:  color = Color.FromArgb(240, 199, 0);  sweep = 180f; start = 0f;   break;   // Business Unit - gold, +90 CW
                case 4:  color = Color.FromArgb(76, 175, 80);  sweep = 270f; start = -75f; break;   // Parent-Child BU - green, +15 CW
                case 8:  color = Color.FromArgb(46, 155, 50);  sweep = 360f; start = -90f; break;   // Organization - full green
                default: color = Color.FromArgb(200, 70, 70);  sweep = 0f;   start = -90f; break;   // None - red ring
            }

            // White base so partially-filled wedges read as a pie chart.
            using (var baseBrush = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(baseBrush, rect);

            if (sweep >= 360f)
            {
                using (var brush = new SolidBrush(color))
                    e.Graphics.FillEllipse(brush, rect);
            }
            else if (sweep > 0f)
            {
                using (var brush = new SolidBrush(color))
                    e.Graphics.FillPie(brush, rect, start, sweep);
            }

            // Colored outline ring around the whole glyph.
            using (var pen = new Pen(color, 1.4f))
                e.Graphics.DrawEllipse(pen, rect);

            e.Handled = true;
        }

        private void tstSearch_TextChanged(object sender, EventArgs e)
        {
            if (_allRoles != null)
                PopulateRoleList();
        }

        private void tstEntitySearch_TextChanged(object sender, EventArgs e)
        {
            if (_allRoles != null)
                RebuildMatrixPanel();
        }

        private void tsbDisplayNames_Click(object sender, EventArgs e)
        {
            if (tsbDisplayNames.Checked && _entityDisplayNames == null)
            {
                tsbDisplayNames.Enabled = false;
                WorkAsync(new WorkAsyncInfo
                {
                    Message = "Loading entity display names...",
                    Work = (w, args) => args.Result = _roleService.GetEntityDisplayNames(),
                    PostWorkCallBack = args =>
                    {
                        tsbDisplayNames.Enabled = true;
                        if (args.Error != null)
                        {
                            tsbDisplayNames.Checked = false;
                            ShowErrorDialog(args.Error, "Loading Display Names");
                            return;
                        }
                        _entityDisplayNames = (Dictionary<string, string>)args.Result;
                        _showDisplayNames = true;
                        RebuildMatrixPanel();
                    }
                });
                return;
            }

            _showDisplayNames = tsbDisplayNames.Checked;
            RebuildMatrixPanel();
        }

        private List<RolePrivilegeInfo> CollectCheckedPrivileges()
        {
            var all = new List<RolePrivilegeInfo>();
            for (int i = 0; i < clbRoles.Items.Count; i++)
            {
                if (clbRoles.GetItemChecked(i))
                {
                    var item = clbRoles.Items[i] as RoleListItem;
                    if (item != null && _loadedPrivileges.ContainsKey(item.RoleId))
                        all.AddRange(_loadedPrivileges[item.RoleId]);
                }
            }
            return all;
        }

        private void tsbExportCsv_Click(object sender, EventArgs e)
        {
            var privileges = CollectCheckedPrivileges();
            if (privileges.Count == 0)
            {
                MessageBox.Show("No privileges loaded to export. Select a role first.",
                    "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.FileName = "SecurityRolePrivileges.csv";
                dialog.Filter = "CSV files (*.csv)|*.csv";
                dialog.Title = "Export Role Privileges";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    CsvExporter.Export(dialog.FileName, privileges);
                    MessageBox.Show($"Exported privileges to:\n{dialog.FileName}",
                        "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void tsbExportExcel_Click(object sender, EventArgs e)
        {
            var privileges = CollectCheckedPrivileges();
            if (privileges.Count == 0)
            {
                MessageBox.Show("No privileges loaded to export. Select a role first.",
                    "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.FileName = "SecurityRolePrivileges.xlsx";
                dialog.Filter = "Excel files (*.xlsx)|*.xlsx";
                dialog.Title = "Export Role Privileges";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    ExcelExporter.Export(dialog.FileName, privileges);
                    MessageBox.Show($"Exported privileges to:\n{dialog.FileName}",
                        "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private class RoleListItem
        {
            public Guid RoleId { get; }
            public string RoleName { get; }
            private readonly string _displayName;

            public RoleListItem(Guid roleId, string roleName, string displayName)
            {
                RoleId = roleId;
                RoleName = roleName;
                _displayName = displayName;
            }

            public override string ToString() => _displayName;
        }

        private class EntityRow
        {
            public string EntityName { get; set; }
            public int[] Depths { get; set; }
        }
    }
}
