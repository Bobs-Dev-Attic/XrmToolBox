using McTools.Xrm.Connection;
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
        private Dictionary<string, string> _entityDisplayNames
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, List<RolePrivilegeInfo>> _loadedPrivileges
            = new Dictionary<Guid, List<RolePrivilegeInfo>>();
        private readonly Dictionary<int, ToolStripMenuItem> _levelMenuItems
            = new Dictionary<int, ToolStripMenuItem>();
        private readonly ToolStripMenuItem[] _columnMenuItems
            = new ToolStripMenuItem[PrivilegeColumns.Length];

        // Entity column shows display names by default; the toolbar toggle flips
        // this to show logical names instead (the other name moves to the tooltip).
        private bool _showLogicalNames;

        // Guards the ListView ItemChecked handler while we repopulate the list, so
        // restoring checkboxes during a rebuild doesn't trigger privilege loads.
        private bool _suppressItemCheck;

        // Roles are grouped into these categories, shown in this order; empty
        // categories are hidden. See CategorizeRole for the assignment rule.
        private static readonly string[] CategoryOrder = { "Custom", "System", "Core", "Misc" };

        // Cross-role comparison driven by checkboxes on the role tab headers.
        private enum CompareMode { None, Combined, Same, Different }

        // Role ids whose tabs are checked for comparison; survives tab rebuilds.
        private readonly HashSet<Guid> _compareRoleIds = new HashSet<Guid>();
        private CompareMode _compareMode = CompareMode.None;

        // Per "entityLogical|privilegeType" cell, computed over the compared roles:
        // the highest level granted, and whether every compared role is identical.
        private readonly Dictionary<string, int> _compareMax
            = new Dictionary<string, int>();
        private readonly Dictionary<string, bool> _compareAllEqual
            = new Dictionary<string, bool>();

        private static readonly Color SameHighlight = Color.FromArgb(208, 240, 208);
        private static readonly Color DifferentHighlight = Color.FromArgb(252, 232, 196);
        private static readonly Color CombinedHighlight = Color.FromArgb(205, 226, 250);

        public SecurityRoleViewerControl()
        {
            InitializeComponent();
            BuildLevelFilterMenu();
            BuildColumnFilterMenu();
            BuildCompareMenu();
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

        private static string CompareModeLabel(CompareMode mode)
        {
            switch (mode)
            {
                case CompareMode.Combined: return "Combined (max)";
                case CompareMode.Same: return "Same";
                case CompareMode.Different: return "Different";
                default: return "Off";
            }
        }

        private void BuildCompareMenu()
        {
            var modes = new[]
            {
                CompareMode.None, CompareMode.Combined, CompareMode.Same, CompareMode.Different
            };
            foreach (var mode in modes)
            {
                var item = new ToolStripMenuItem(CompareModeLabel(mode))
                {
                    Tag = mode,
                    Checked = mode == CompareMode.None
                };
                item.Click += CompareModeItem_Click;
                tsddCompare.DropDownItems.Add(item);
            }
        }

        private void CompareModeItem_Click(object sender, EventArgs e)
        {
            _compareMode = (CompareMode)((ToolStripMenuItem)sender).Tag;
            foreach (ToolStripMenuItem item in tsddCompare.DropDownItems)
                item.Checked = (CompareMode)item.Tag == _compareMode;

            tsddCompare.Text = _compareMode == CompareMode.None
                ? "Compare"
                : "Compare: " + CompareModeLabel(_compareMode);

            InvalidateMatrixGrids();
        }

        // Reflects the current comparison selection in the toolbar and recomputes
        // the highlight data. Called whenever the checked-tab set changes.
        private void UpdateComparisonState()
        {
            bool canCompare = _compareRoleIds.Count >= 2;
            tsddCompare.Visible = canCompare;

            if (!canCompare && _compareMode != CompareMode.None)
            {
                _compareMode = CompareMode.None;
                foreach (ToolStripMenuItem item in tsddCompare.DropDownItems)
                    item.Checked = (CompareMode)item.Tag == CompareMode.None;
                tsddCompare.Text = "Compare";
            }

            ComputeComparison();
            InvalidateMatrixGrids();
        }

        // Builds the per-cell max/all-equal maps across the compared roles, treating
        // an entity a role doesn't list as None (0).
        private void ComputeComparison()
        {
            _compareMax.Clear();
            _compareAllEqual.Clear();

            var roleCells = new List<Dictionary<string, int>>();
            foreach (var roleId in _compareRoleIds)
            {
                if (!_loadedPrivileges.TryGetValue(roleId, out var privileges))
                    continue;

                var cells = new Dictionary<string, int>();
                foreach (var p in privileges)
                {
                    if (string.IsNullOrEmpty(p.EntityName))
                        continue;
                    var key = p.EntityName + "|" + p.PrivilegeType;
                    if (!cells.TryGetValue(key, out var existing) || p.Depth > existing)
                        cells[key] = p.Depth;
                }
                roleCells.Add(cells);
            }

            if (roleCells.Count < 2)
                return;

            var allKeys = new HashSet<string>();
            foreach (var cells in roleCells)
                foreach (var key in cells.Keys)
                    allKeys.Add(key);

            foreach (var key in allKeys)
            {
                int max = 0;
                int first = 0;
                bool firstSet = false;
                bool allEqual = true;

                foreach (var cells in roleCells)
                {
                    int depth = cells.TryGetValue(key, out var d) ? d : 0;
                    if (!firstSet) { first = depth; firstSet = true; }
                    else if (depth != first) allEqual = false;
                    if (depth > max) max = depth;
                }

                _compareMax[key] = max;
                _compareAllEqual[key] = allEqual;
            }
        }

        // The highlight colour for one privilege cell, or null when comparison is
        // off, the grid's role isn't selected, or the cell doesn't qualify.
        private Color? GetCompareHighlight(DataGridView grid, int rowIndex, int colIndex, int depth)
        {
            if (_compareMode == CompareMode.None || grid == null)
                return null;
            if (!(grid.Tag is Guid roleId) || !_compareRoleIds.Contains(roleId))
                return null;

            var entity = grid.Rows[rowIndex].Tag as string;
            if (string.IsNullOrEmpty(entity))
                return null;

            var columnName = grid.Columns[colIndex].Name ?? "";
            var privType = columnName.StartsWith("col") ? columnName.Substring(3) : columnName;
            var key = entity + "|" + privType;

            switch (_compareMode)
            {
                case CompareMode.Same:
                    return _compareAllEqual.TryGetValue(key, out var eqS) && eqS
                        ? SameHighlight : (Color?)null;
                case CompareMode.Different:
                    return _compareAllEqual.TryGetValue(key, out var eqD) && !eqD
                        ? DifferentHighlight : (Color?)null;
                case CompareMode.Combined:
                    return _compareMax.TryGetValue(key, out var mx) && depth == mx && depth > 0
                        ? CombinedHighlight : (Color?)null;
                default:
                    return null;
            }
        }

        private void InvalidateMatrixGrids()
        {
            foreach (TabPage page in tabRoles.TabPages)
                foreach (Control c in page.Controls)
                    if (c is DataGridView grid)
                        grid.Invalidate();
        }

        private static Rectangle GetTabCheckBoxBounds(Rectangle tabRect)
        {
            const int size = 14;
            return new Rectangle(
                tabRect.Left + 5,
                tabRect.Top + (tabRect.Height - size) / 2,
                size, size);
        }

        private void tabRoles_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= tabRoles.TabPages.Count)
                return;

            var page = tabRoles.TabPages[e.Index];
            var tabRect = tabRoles.GetTabRect(e.Index);
            bool selected = (e.State & DrawItemState.Selected) != 0;

            using (var back = new SolidBrush(selected ? SystemColors.Window : SystemColors.Control))
                e.Graphics.FillRectangle(back, tabRect);

            var box = GetTabCheckBoxBounds(tabRect);
            bool isChecked = page.Tag is Guid roleId && _compareRoleIds.Contains(roleId);
            CheckBoxRenderer.DrawCheckBox(e.Graphics, box.Location,
                isChecked
                    ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                    : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal);

            var textRect = new Rectangle(
                box.Right + 3, tabRect.Top,
                tabRect.Right - box.Right - 6, tabRect.Height);
            TextRenderer.DrawText(e.Graphics, page.Text, tabRoles.Font, textRect,
                SystemColors.ControlText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void tabRoles_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            for (int i = 0; i < tabRoles.TabPages.Count; i++)
            {
                if (!GetTabCheckBoxBounds(tabRoles.GetTabRect(i)).Contains(e.Location))
                    continue;

                if (tabRoles.TabPages[i].Tag is Guid roleId)
                {
                    if (!_compareRoleIds.Remove(roleId))
                        _compareRoleIds.Add(roleId);

                    UpdateComparisonState();
                    tabRoles.Invalidate(); // repaint the header checkboxes
                }
                return;
            }
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

        // Populate the Business Units dropdown as soon as a connection is available,
        // so the user can narrow the role load before clicking Load Roles.
        public override void UpdateConnection(
            IOrganizationService newService, ConnectionDetail detail,
            string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);
            LoadBusinessUnits();
        }

        private void LoadBusinessUnits()
        {
            if (Service == null) return;

            var service = new SecurityRoleService(Service);
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading business units...",
                Work = (w, args) => args.Result = service.GetBusinessUnits(),
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Loading Business Units");
                        return;
                    }
                    PopulateBusinessUnitMenu((List<Entity>)args.Result);
                }
            });
        }

        private void PopulateBusinessUnitMenu(List<Entity> businessUnits)
        {
            tsddBusinessUnits.DropDownItems.Clear();

            foreach (var bu in businessUnits)
            {
                var id = bu.GetAttributeValue<Guid>("businessunitid");
                var name = bu.GetAttributeValue<string>("name");

                var item = new ToolStripMenuItem(string.IsNullOrEmpty(name) ? id.ToString() : name)
                {
                    Checked = true,        // all checked by default; a lone BU is checked too
                    CheckOnClick = true,
                    Tag = id
                };
                tsddBusinessUnits.DropDownItems.Add(item);
            }

            tsddBusinessUnits.Enabled = tsddBusinessUnits.DropDownItems.Count > 0;
        }

        private List<Guid> GetCheckedBusinessUnitIds()
        {
            var ids = new List<Guid>();
            foreach (ToolStripMenuItem item in tsddBusinessUnits.DropDownItems)
                if (item.Checked && item.Tag is Guid id)
                    ids.Add(id);
            return ids;
        }

        private void tsbLoadRoles_Click(object sender, EventArgs e)
        {
            ExecuteMethod(LoadRoles);
        }

        private void LoadRoles()
        {
            _roleService = new SecurityRoleService(Service);
            _loadedPrivileges.Clear();

            // Read the BU selection on the UI thread before the background work.
            // All-checked (or nothing loaded) means no filter, i.e. load every role.
            var checkedBuIds = GetCheckedBusinessUnitIds();
            IList<Guid> buFilter =
                checkedBuIds.Count == 0 || checkedBuIds.Count == tsddBusinessUnits.DropDownItems.Count
                    ? null
                    : checkedBuIds;

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading security roles...",
                Work = (w, args) =>
                {
                    var roles = _roleService.GetRoles(buFilter);

                    // Resolve entity display names alongside the roles. If the caller
                    // lacks metadata read access we still want the roles to load, so
                    // failures here fall back to an empty map (logical names shown).
                    Dictionary<string, string> displayNames;
                    try
                    {
                        displayNames = _roleService.GetEntityDisplayNames();
                    }
                    catch
                    {
                        displayNames = new Dictionary<string, string>(
                            StringComparer.OrdinalIgnoreCase);
                    }

                    args.Result = new LoadResult
                    {
                        Roles = roles,
                        DisplayNames = displayNames
                    };
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Loading Roles");
                        return;
                    }

                    var result = (LoadResult)args.Result;
                    _allRoles = result.Roles;
                    _entityDisplayNames = result.DisplayNames;
                    PopulateRoleList();
                    tsbExport.Enabled = false;
                    RebuildMatrixPanel();

                    SendMessageToStatusBar?.Invoke(this,
                        new StatusBarMessageEventArgs($"Loaded {_allRoles.Count} roles"));
                }
            });
        }

        // Assigns a role to a category. First match wins:
        //   Core   - backed by a role template (built-in baseline roles)
        //   System - managed (shipped by a solution)
        //   Custom - unmanaged (created/customized in this org)
        //   Misc   - flags unreadable (defensive fallback)
        private static string CategorizeRole(Entity role)
        {
            if (role.GetAttributeValue<EntityReference>("roletemplateid") != null)
                return "Core";

            if (role.Contains("ismanaged") && role["ismanaged"] is bool managed)
                return managed ? "System" : "Custom";

            return "Misc";
        }

        private void PopulateRoleList()
        {
            var searchText = tstSearch.Text?.Trim() ?? "";

            // Bucket matching roles by category first, so we can emit groups in the
            // configured order and skip the empty ones.
            var buckets = CategoryOrder.ToDictionary(c => c, c => new List<ListViewItem>());

            foreach (var role in _allRoles)
            {
                var roleName = role.GetAttributeValue<string>("name") ?? "";

                if (!string.IsNullOrEmpty(searchText)
                    && roleName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var roleId = role.GetAttributeValue<Guid>("roleid");
                var buName = role.GetAttributeValue<EntityReference>("businessunitid")?.Name ?? "";
                var label = string.IsNullOrEmpty(buName) ? roleName : $"{roleName} ({buName})";

                var item = new ListViewItem(label)
                {
                    Tag = new RoleListItem(roleId, roleName, label),
                    Checked = _loadedPrivileges.ContainsKey(roleId)
                };
                buckets[CategorizeRole(role)].Add(item);
            }

            _suppressItemCheck = true;
            lvRoles.BeginUpdate();
            lvRoles.Items.Clear();
            lvRoles.Groups.Clear();

            foreach (var category in CategoryOrder)
            {
                var items = buckets[category];
                if (items.Count == 0)
                    continue;

                var group = new ListViewGroup(category, $"{category} ({items.Count})");
                lvRoles.Groups.Add(group);
                foreach (var item in items)
                {
                    item.Group = group;
                    lvRoles.Items.Add(item);
                }
            }

            ResizeRoleColumn();
            lvRoles.EndUpdate();
            _suppressItemCheck = false;

            // Add the native collapse/expand chevrons once the groups exist.
            NativeMethods.MakeGroupsCollapsible(lvRoles);
        }

        private void ResizeRoleColumn()
        {
            if (lvRoles.Columns.Count > 0)
                lvRoles.Columns[0].Width = Math.Max(60, lvRoles.ClientSize.Width - 4);
        }

        private void lvRoles_Resize(object sender, EventArgs e)
        {
            ResizeRoleColumn();
        }

        private void lvRoles_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_suppressItemCheck) return;

            var item = e.Item.Tag as RoleListItem;
            if (item == null) return;

            if (e.Item.Checked)
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
            foreach (ListViewItem lvItem in lvRoles.CheckedItems)
            {
                var item = lvItem.Tag as RoleListItem;
                if (item != null && _loadedPrivileges.ContainsKey(item.RoleId))
                    checkedRoleIds.Add(item.RoleId);
            }

            // Drop comparison selections for roles that no longer have a tab.
            _compareRoleIds.IntersectWith(checkedRoleIds);

            if (checkedRoleIds.Count == 0)
            {
                tabRoles.ResumeLayout();
                tabRoles.Visible = false;
                lblEmpty.Visible = true;
                tsbExport.Enabled = false;
                UpdateComparisonState();
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
                    Padding = new Padding(3),
                    Tag = roleId
                };

                var rows = BuildEntityRows(privileges, keyword, visibleDepths, visibleColumns);

                var grid = CreateMatrixGrid(rows, visibleColumns);
                grid.Dock = DockStyle.Fill;
                grid.ScrollBars = ScrollBars.Both;
                grid.Tag = roleId;

                page.Controls.Add(grid);
                tabRoles.TabPages.Add(page);

                if (page.Name == selectedTabName)
                    tabRoles.SelectedTab = page;
            }

            tabRoles.ResumeLayout();

            // Refresh the Compare toolbar + highlight data for the rebuilt tabs.
            UpdateComparisonState();
        }

        private List<EntityRow> BuildEntityRows(
            List<RolePrivilegeInfo> privileges, string keyword,
            HashSet<int> visibleDepths, bool[] visibleColumns)
        {
            var rows = new List<EntityRow>();

            var entityGroups = privileges
                .Where(p => !string.IsNullOrEmpty(p.EntityName))
                .GroupBy(p => p.EntityName)
                .OrderBy(g => ResolveDisplayName(g.Key), StringComparer.OrdinalIgnoreCase);

            foreach (var group in entityGroups)
            {
                var logicalName = group.Key;
                var displayName = ResolveDisplayName(logicalName);

                // Match the keyword against both the display name and the logical
                // name so either spelling finds the entity.
                if (keyword.Length > 0
                    && displayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0
                    && logicalName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
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
                        // A row is kept only if it has a visible cell in a visible column.
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

                rows.Add(new EntityRow
                {
                    EntityName = logicalName,
                    DisplayName = displayName,
                    Depths = depths
                });
            }

            return rows;
        }

        private string ResolveDisplayName(string logicalName)
        {
            if (!string.IsNullOrEmpty(logicalName)
                && _entityDisplayNames.TryGetValue(logicalName, out var displayName)
                && !string.IsNullOrEmpty(displayName))
                return displayName;

            return logicalName;
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
                // Logical name on the row backs cross-role comparison lookups.
                row.Tag = entityRow.EntityName;
                // The toggle decides which name fills the cell; the other one moves
                // to the tooltip so both are always reachable.
                if (_showLogicalNames)
                {
                    row.Cells[0].Value = entityRow.EntityName;
                    row.Cells[0].ToolTipText = "Display name: " + entityRow.DisplayName;
                }
                else
                {
                    row.Cells[0].Value = entityRow.DisplayName;
                    row.Cells[0].ToolTipText = "Logical name: " + entityRow.EntityName;
                }

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

            // When a comparison mode is active, tint qualifying cells of the
            // checked tabs; otherwise paint the normal cell background.
            var highlight = depth >= 0
                ? GetCompareHighlight(sender as DataGridView, e.RowIndex, e.ColumnIndex, depth)
                : null;

            if (highlight.HasValue)
            {
                using (var fill = new SolidBrush(highlight.Value))
                    e.Graphics.FillRectangle(fill, e.CellBounds);
                e.Paint(e.CellBounds, DataGridViewPaintParts.Border);
            }
            else
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
            }

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

        private void tsbEntityLabel_Click(object sender, EventArgs e)
        {
            _showLogicalNames = tsbEntityLabel.Checked;
            RebuildMatrixPanel();
        }

        private List<RolePrivilegeInfo> CollectCheckedPrivileges()
        {
            var all = new List<RolePrivilegeInfo>();
            foreach (ListViewItem lvItem in lvRoles.CheckedItems)
            {
                var item = lvItem.Tag as RoleListItem;
                if (item != null && _loadedPrivileges.ContainsKey(item.RoleId))
                    all.AddRange(_loadedPrivileges[item.RoleId]);
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
                dialog.FileName = $"SecurityRolePrivileges_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
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
                dialog.FileName = $"SecurityRolePrivileges_{DateTime.Now:yyyyMMdd_HHmmss}.xls";
                dialog.Filter = "Excel files (*.xls)|*.xls";
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
            public string DisplayName { get; set; }
            public int[] Depths { get; set; }
        }

        private class LoadResult
        {
            public List<Entity> Roles { get; set; }
            public Dictionary<string, string> DisplayNames { get; set; }
        }
    }
}
