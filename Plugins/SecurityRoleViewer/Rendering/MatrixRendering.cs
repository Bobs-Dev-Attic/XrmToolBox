using SecurityRoleViewer.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace SecurityRoleViewer.Rendering
{
    /// <summary>
    /// A single entity's row in the privilege matrix: its logical and display
    /// names, plus the access depth per privilege column (-1 = hidden by the
    /// level filter).
    /// </summary>
    internal class EntityRow
    {
        public string EntityName { get; set; }
        public string DisplayName { get; set; }
        public int[] Depths { get; set; }
    }

    /// <summary>
    /// Shared rendering for the entity x privilege access matrix, used by both the
    /// Role Permissions tab and the User/Team Roles tab so the two look identical.
    /// </summary>
    internal static class MatrixRendering
    {
        public static readonly string[] PrivilegeColumns =
            { "Create", "Read", "Write", "Delete", "Append", "AppendTo", "Assign", "Share" };

        // Access levels shown in the level filter, high to low.
        public static readonly (int Depth, string Label)[] AccessLevels =
        {
            (8, "Organization"),
            (4, "Parent-Child BU"),
            (2, "Business Unit"),
            (1, "User"),
            (0, "None")
        };

        public static string ColumnLabel(string privilegeType)
            => privilegeType == "AppendTo" ? "Append To" : privilegeType;

        /// <summary>
        /// Collapses raw privilege rows into one matrix row per entity, applying the
        /// entity keyword filter and the per-level visibility filter. A cell hidden
        /// by the level filter is stored as -1; a row with no visible cell in a
        /// visible column is dropped entirely.
        /// </summary>
        public static List<EntityRow> BuildEntityRows(
            IEnumerable<RolePrivilegeInfo> privileges, string keyword,
            HashSet<int> visibleDepths, bool[] visibleColumns,
            Func<string, string> resolveDisplayName)
        {
            var rows = new List<EntityRow>();

            var entityGroups = privileges
                .Where(p => !string.IsNullOrEmpty(p.EntityName))
                .GroupBy(p => p.EntityName)
                .OrderBy(g => resolveDisplayName(g.Key), StringComparer.OrdinalIgnoreCase);

            foreach (var group in entityGroups)
            {
                var logicalName = group.Key;
                var displayName = resolveDisplayName(logicalName);

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

        /// <summary>
        /// Builds a fully configured matrix grid for the given rows. The optional
        /// highlightProvider (grid, rowIndex, colIndex, depth) lets a caller tint
        /// cells (used by the Role Permissions comparison mode); pass null for none.
        /// </summary>
        public static DataGridView CreateMatrixGrid(
            List<EntityRow> entityRows, bool[] visibleColumns, bool showLogicalNames,
            Func<DataGridView, int, int, int, Color?> highlightProvider)
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
                if (showLogicalNames)
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

            grid.CellPainting += (s, e) => PaintCell((DataGridView)s, e, highlightProvider);
            return grid;
        }

        private static void PaintCell(
            DataGridView grid, DataGridViewCellPaintingEventArgs e,
            Func<DataGridView, int, int, int, Color?> highlightProvider)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 1) return;
            if (!(e.Value is int depth)) return;

            // Optional caller highlight (e.g. comparison mode); else normal background.
            var highlight = depth >= 0 && highlightProvider != null
                ? highlightProvider(grid, e.RowIndex, e.ColumnIndex, depth)
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
                case 1:  color = Color.FromArgb(237, 162, 0);  sweep = 90f;  start = 45f; break;   // User - amber, +60 CW
                case 2:  color = Color.FromArgb(240, 199, 0);  sweep = 180f; start = 0f;   break;   // Business Unit - gold, +90 CW
                case 4:  color = Color.FromArgb(76, 175, 80);  sweep = 270f; start = 45f; break;   // Parent-Child BU - green, +15 CW
                case 8:  color = Color.FromArgb(46, 155, 50);  sweep = 360f; start = -90f; break;   // Organization - full green
                default: color = Color.FromArgb(200, 70, 70);  sweep = 0f;   start = -90f; break;   // None - red ring
            }

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

            using (var pen = new Pen(color, 1.4f))
                e.Graphics.DrawEllipse(pen, rect);

            e.Handled = true;
        }
    }
}
