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

        private SecurityRoleService _roleService;
        private List<Entity> _allRoles;
        private readonly Dictionary<Guid, List<RolePrivilegeInfo>> _loadedPrivileges
            = new Dictionary<Guid, List<RolePrivilegeInfo>>();

        public SecurityRoleViewerControl()
        {
            InitializeComponent();
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
                    PopulateRoleList();
                    tsbExport.Enabled = false;
                    pnlMatrix.Controls.Clear();

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
            pnlMatrix.SuspendLayout();
            pnlMatrix.Controls.Clear();

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
                var lbl = new System.Windows.Forms.Label
                {
                    Text = "Check one or more roles to view privileges",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.Gray,
                    Font = new Font("Segoe UI", 10F)
                };
                pnlMatrix.Controls.Add(lbl);
                pnlMatrix.ResumeLayout();
                tsbExport.Enabled = false;
                return;
            }

            tsbExport.Enabled = true;
            int yOffset = 0;

            foreach (var roleId in checkedRoleIds)
            {
                var privileges = _loadedPrivileges[roleId];
                var roleName = privileges.FirstOrDefault()?.RoleName ?? "Unknown";

                var header = new System.Windows.Forms.Label
                {
                    Text = roleName,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    Height = 28,
                    Width = pnlMatrix.ClientSize.Width - 20,
                    Location = new Point(4, yOffset),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };
                pnlMatrix.Controls.Add(header);
                yOffset += 30;

                var entityPrivileges = privileges
                    .Where(p => !string.IsNullOrEmpty(p.EntityName))
                    .GroupBy(p => p.EntityName)
                    .OrderBy(g => g.Key)
                    .ToList();

                var grid = CreateMatrixGrid(entityPrivileges);
                grid.Location = new Point(4, yOffset);
                grid.Width = pnlMatrix.ClientSize.Width - 20;
                grid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                int rowHeight = grid.RowTemplate.Height;
                int headerHeight = grid.ColumnHeadersHeight;
                int gridHeight = headerHeight + (entityPrivileges.Count * rowHeight) + 4;
                grid.Height = Math.Min(gridHeight, 500);
                grid.ScrollBars = gridHeight > 500 ? ScrollBars.Vertical : ScrollBars.None;

                pnlMatrix.Controls.Add(grid);
                yOffset += grid.Height + 12;
            }

            pnlMatrix.ResumeLayout();
        }

        private DataGridView CreateMatrixGrid(List<IGrouping<string, RolePrivilegeInfo>> entityGroups)
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
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            grid.Columns.Add(entityCol);

            foreach (var privType in PrivilegeColumns)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    HeaderText = privType,
                    Name = "col" + privType,
                    Width = 65,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
                };
                grid.Columns.Add(col);
            }

            foreach (var entityGroup in entityGroups)
            {
                var row = new DataGridViewRow();
                row.CreateCells(grid);
                row.Cells[0].Value = entityGroup.Key;

                var privsByType = entityGroup.ToDictionary(p => p.PrivilegeType, p => p.Depth);
                for (int c = 0; c < PrivilegeColumns.Length; c++)
                {
                    int depth = 0;
                    if (privsByType.TryGetValue(PrivilegeColumns[c], out var d))
                        depth = d;
                    row.Cells[c + 1].Value = depth;
                }

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

            int size = 16;
            int x = e.CellBounds.X + (e.CellBounds.Width - size) / 2;
            int y = e.CellBounds.Y + (e.CellBounds.Height - size) / 2;
            var rect = new Rectangle(x, y, size, size);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            switch (depth)
            {
                case 0:
                    using (var pen = new Pen(Color.FromArgb(200, 60, 60), 1.5f))
                        e.Graphics.DrawEllipse(pen, rect);
                    break;
                case 1:
                    using (var brush = new SolidBrush(Color.FromArgb(240, 200, 50)))
                        e.Graphics.FillEllipse(brush, rect);
                    break;
                case 2:
                    using (var brush = new SolidBrush(Color.FromArgb(160, 200, 60)))
                        e.Graphics.FillEllipse(brush, rect);
                    break;
                case 4:
                    using (var brush = new SolidBrush(Color.FromArgb(80, 180, 80)))
                    {
                        e.Graphics.FillEllipse(brush, rect);
                        using (var pen = new Pen(Color.White, 1.5f))
                        {
                            e.Graphics.DrawLine(pen,
                                x + 4, y + size / 2,
                                x + size / 2 - 1, y + size - 5);
                            e.Graphics.DrawLine(pen,
                                x + size / 2 - 1, y + size - 5,
                                x + size - 4, y + 4);
                        }
                    }
                    break;
                case 8:
                    using (var brush = new SolidBrush(Color.FromArgb(40, 150, 40)))
                        e.Graphics.FillEllipse(brush, rect);
                    break;
            }

            e.Handled = true;
        }

        private void tstSearch_TextChanged(object sender, EventArgs e)
        {
            if (_allRoles != null)
                PopulateRoleList();
        }

        private void tsbExport_Click(object sender, EventArgs e)
        {
            var allPrivileges = new List<RolePrivilegeInfo>();
            for (int i = 0; i < clbRoles.Items.Count; i++)
            {
                if (clbRoles.GetItemChecked(i))
                {
                    var item = clbRoles.Items[i] as RoleListItem;
                    if (item != null && _loadedPrivileges.ContainsKey(item.RoleId))
                        allPrivileges.AddRange(_loadedPrivileges[item.RoleId]);
                }
            }

            if (allPrivileges.Count == 0)
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
                    CsvExporter.Export(dialog.FileName, allPrivileges);
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
    }
}
