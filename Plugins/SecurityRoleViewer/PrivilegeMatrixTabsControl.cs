using SecurityRoleViewer.Models;
using SecurityRoleViewer.Rendering;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SecurityRoleViewer
{
    /// <summary>One tab's worth of data: a stable key, a header label, and the
    /// privileges whose max-per-cell defines the rendered access matrix.</summary>
    internal sealed class MatrixSource
    {
        public Guid Key { get; }
        public string Label { get; }
        public List<RolePrivilegeInfo> Privileges { get; }

        public MatrixSource(Guid key, string label, List<RolePrivilegeInfo> privileges)
        {
            Key = key;
            Label = label;
            Privileges = privileges ?? new List<RolePrivilegeInfo>();
        }
    }

    /// <summary>
    /// The shared right-hand view: a filter bar, one matrix tab per source with a
    /// checkbox in each tab header, and a Compare mode that highlights how the
    /// checked tabs relate (Combined / Same / Different). Used by both the Role
    /// Permissions tab (sources = roles) and the User/Team Roles tab (sources =
    /// users/teams, carrying effective privileges).
    /// </summary>
    public partial class PrivilegeMatrixTabsControl : UserControl
    {
        private enum CompareMode { None, Combined, Same, Different }

        private readonly Dictionary<int, ToolStripMenuItem> _levelMenuItems
            = new Dictionary<int, ToolStripMenuItem>();
        private readonly ToolStripMenuItem[] _columnMenuItems
            = new ToolStripMenuItem[MatrixRendering.PrivilegeColumns.Length];

        private List<MatrixSource> _sources = new List<MatrixSource>();
        private readonly Dictionary<Guid, List<RolePrivilegeInfo>> _privByKey
            = new Dictionary<Guid, List<RolePrivilegeInfo>>();
        private Func<string, string> _resolveDisplayName = s => s;

        private bool _showLogicalNames;

        private readonly HashSet<Guid> _compareKeys = new HashSet<Guid>();
        private CompareMode _compareMode = CompareMode.None;
        private readonly Dictionary<string, int> _compareMax = new Dictionary<string, int>();
        private readonly Dictionary<string, bool> _compareAllEqual = new Dictionary<string, bool>();

        private static readonly Color SameHighlight = Color.FromArgb(208, 240, 208);
        private static readonly Color DifferentHighlight = Color.FromArgb(252, 232, 196);
        private static readonly Color CombinedHighlight = Color.FromArgb(205, 226, 250);

        public PrivilegeMatrixTabsControl()
        {
            InitializeComponent();
            BuildLevelFilterMenu();
            BuildColumnFilterMenu();
            BuildCompareMenu();
        }

        /// <summary>Replaces the rendered tabs with one per source.</summary>
        internal void SetSources(IList<MatrixSource> sources, Func<string, string> resolveDisplayName)
        {
            _sources = sources?.ToList() ?? new List<MatrixSource>();
            _resolveDisplayName = resolveDisplayName ?? (s => s);

            _privByKey.Clear();
            foreach (var source in _sources)
                _privByKey[source.Key] = source.Privileges;

            // Drop comparison selections for keys that are no longer present.
            _compareKeys.IntersectWith(_sources.Select(s => s.Key));

            RebuildTabs();
            UpdateComparisonState();
        }

        // ---- tab building -----------------------------------------------------

        private void RebuildTabs()
        {
            tabs.SuspendLayout();

            var selectedKey = tabs.SelectedTab?.Tag as Guid?;

            foreach (TabPage page in tabs.TabPages)
                foreach (Control c in page.Controls)
                    c.Dispose();
            tabs.TabPages.Clear();

            if (_sources.Count == 0)
            {
                tabs.ResumeLayout();
                tabs.Visible = false;
                lblEmpty.Visible = true;
                return;
            }

            lblEmpty.Visible = false;
            tabs.Visible = true;

            var keyword = tstEntitySearch.Text?.Trim() ?? "";
            var visibleDepths = GetVisibleDepths();
            var visibleColumns = GetVisibleColumns();

            foreach (var source in _sources)
            {
                var page = new TabPage(source.Label)
                {
                    Name = "tab_" + source.Key.ToString("N"),
                    ToolTipText = source.Label,
                    UseVisualStyleBackColor = true,
                    Padding = new Padding(3),
                    Tag = source.Key
                };

                var rows = MatrixRendering.BuildEntityRows(
                    source.Privileges, keyword, visibleDepths, visibleColumns, _resolveDisplayName);

                var grid = MatrixRendering.CreateMatrixGrid(
                    rows, visibleColumns, _showLogicalNames, GetCompareHighlight);
                grid.Dock = DockStyle.Fill;
                grid.ScrollBars = ScrollBars.Both;
                grid.Tag = source.Key;

                page.Controls.Add(grid);
                tabs.TabPages.Add(page);

                if (selectedKey.HasValue && selectedKey.Value == source.Key)
                    tabs.SelectedTab = page;
            }

            tabs.ResumeLayout();
        }

        // ---- filter menus -----------------------------------------------------

        private void BuildLevelFilterMenu()
        {
            foreach (var level in MatrixRendering.AccessLevels)
            {
                var item = new ToolStripMenuItem(level.Label)
                {
                    Checked = true,
                    CheckOnClick = true,
                    Tag = level.Depth
                };
                item.CheckedChanged += (s, e) => RebuildTabs();
                _levelMenuItems[level.Depth] = item;
                tsddLevels.DropDownItems.Add(item);
            }
        }

        private void BuildColumnFilterMenu()
        {
            for (int c = 0; c < MatrixRendering.PrivilegeColumns.Length; c++)
            {
                var item = new ToolStripMenuItem(
                    MatrixRendering.ColumnLabel(MatrixRendering.PrivilegeColumns[c]))
                {
                    Checked = true,
                    CheckOnClick = true,
                    Tag = c
                };
                item.CheckedChanged += (s, e) => RebuildTabs();
                _columnMenuItems[c] = item;
                tsddColumns.DropDownItems.Add(item);
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
            var visible = new bool[MatrixRendering.PrivilegeColumns.Length];
            for (int c = 0; c < MatrixRendering.PrivilegeColumns.Length; c++)
                visible[c] = _columnMenuItems[c].Checked;
            return visible;
        }

        private void tstEntitySearch_TextChanged(object sender, EventArgs e) => RebuildTabs();

        private void tsbEntityLabel_Click(object sender, EventArgs e)
        {
            _showLogicalNames = tsbEntityLabel.Checked;
            RebuildTabs();
        }

        // ---- comparison -------------------------------------------------------

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

        private void UpdateComparisonState()
        {
            bool canCompare = _compareKeys.Count >= 2;
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

        private void ComputeComparison()
        {
            _compareMax.Clear();
            _compareAllEqual.Clear();

            var keyCells = new List<Dictionary<string, int>>();
            foreach (var key in _compareKeys)
            {
                if (!_privByKey.TryGetValue(key, out var privileges))
                    continue;

                var cells = new Dictionary<string, int>();
                foreach (var p in privileges)
                {
                    if (string.IsNullOrEmpty(p.EntityName))
                        continue;
                    var cellKey = p.EntityName + "|" + p.PrivilegeType;
                    if (!cells.TryGetValue(cellKey, out var existing) || p.Depth > existing)
                        cells[cellKey] = p.Depth;
                }
                keyCells.Add(cells);
            }

            if (keyCells.Count < 2)
                return;

            var allKeys = new HashSet<string>();
            foreach (var cells in keyCells)
                foreach (var k in cells.Keys)
                    allKeys.Add(k);

            foreach (var cellKey in allKeys)
            {
                int max = 0;
                int first = 0;
                bool firstSet = false;
                bool allEqual = true;

                foreach (var cells in keyCells)
                {
                    int depth = cells.TryGetValue(cellKey, out var v) ? v : 0;
                    if (!firstSet) { first = depth; firstSet = true; }
                    else if (depth != first) allEqual = false;
                    if (depth > max) max = depth;
                }

                _compareMax[cellKey] = max;
                _compareAllEqual[cellKey] = allEqual;
            }
        }

        private Color? GetCompareHighlight(DataGridView grid, int rowIndex, int colIndex, int depth)
        {
            if (_compareMode == CompareMode.None || grid == null)
                return null;
            if (!(grid.Tag is Guid key) || !_compareKeys.Contains(key))
                return null;

            var entity = grid.Rows[rowIndex].Tag as string;
            if (string.IsNullOrEmpty(entity))
                return null;

            var columnName = grid.Columns[colIndex].Name ?? "";
            var privType = columnName.StartsWith("col") ? columnName.Substring(3) : columnName;
            var cellKey = entity + "|" + privType;

            switch (_compareMode)
            {
                case CompareMode.Same:
                    return _compareAllEqual.TryGetValue(cellKey, out var eqS) && eqS
                        ? SameHighlight : (Color?)null;
                case CompareMode.Different:
                    return _compareAllEqual.TryGetValue(cellKey, out var eqD) && !eqD
                        ? DifferentHighlight : (Color?)null;
                case CompareMode.Combined:
                    return _compareMax.TryGetValue(cellKey, out var mx) && depth == mx && depth > 0
                        ? CombinedHighlight : (Color?)null;
                default:
                    return null;
            }
        }

        private void InvalidateMatrixGrids()
        {
            foreach (TabPage page in tabs.TabPages)
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

        private void tabs_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= tabs.TabPages.Count)
                return;

            var page = tabs.TabPages[e.Index];
            var tabRect = tabs.GetTabRect(e.Index);
            bool selected = (e.State & DrawItemState.Selected) != 0;

            using (var back = new SolidBrush(selected ? SystemColors.Window : SystemColors.Control))
                e.Graphics.FillRectangle(back, tabRect);

            var box = GetTabCheckBoxBounds(tabRect);
            bool isChecked = page.Tag is Guid key && _compareKeys.Contains(key);
            CheckBoxRenderer.DrawCheckBox(e.Graphics, box.Location,
                isChecked
                    ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                    : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal);

            var textRect = new Rectangle(
                box.Right + 3, tabRect.Top,
                tabRect.Right - box.Right - 6, tabRect.Height);
            TextRenderer.DrawText(e.Graphics, page.Text, tabs.Font, textRect,
                SystemColors.ControlText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void tabs_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            for (int i = 0; i < tabs.TabPages.Count; i++)
            {
                if (!GetTabCheckBoxBounds(tabs.GetTabRect(i)).Contains(e.Location))
                    continue;

                if (tabs.TabPages[i].Tag is Guid key)
                {
                    if (!_compareKeys.Remove(key))
                        _compareKeys.Add(key);

                    UpdateComparisonState();
                    tabs.Invalidate();
                }
                return;
            }
        }
    }
}
