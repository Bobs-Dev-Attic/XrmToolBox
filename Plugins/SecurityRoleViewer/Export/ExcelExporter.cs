using OfficeOpenXml;
using OfficeOpenXml.Style;
using SecurityRoleViewer.Models;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace SecurityRoleViewer.Export
{
    public static class ExcelExporter
    {
        private static readonly string[] PrivilegeTypes =
            { "Create", "Read", "Write", "Delete", "Append", "AppendTo", "Assign", "Share" };

        public static void Export(string filePath, List<RolePrivilegeInfo> privileges)
        {
            using (var package = new ExcelPackage())
            {
                var byRole = privileges
                    .Where(p => !string.IsNullOrEmpty(p.EntityName))
                    .GroupBy(p => p.RoleName)
                    .OrderBy(g => g.Key);

                foreach (var roleGroup in byRole)
                    WriteSheet(package.Workbook.Worksheets.Add(SanitizeSheetName(roleGroup.Key)), roleGroup.ToList());

                package.SaveAs(new FileInfo(filePath));
            }
        }

        private static void WriteSheet(ExcelWorksheet ws, List<RolePrivilegeInfo> privileges)
        {
            ws.Cells[1, 1].Value = "Entity";
            for (int c = 0; c < PrivilegeTypes.Length; c++)
                ws.Cells[1, c + 2].Value = PrivilegeTypes[c] == "AppendTo" ? "Append To" : PrivilegeTypes[c];

            using (var hdr = ws.Cells[1, 1, 1, PrivilegeTypes.Length + 1])
            {
                hdr.Style.Font.Bold = true;
                hdr.Style.Fill.PatternType = ExcelFillStyle.Solid;
                hdr.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 114, 196));
                hdr.Style.Font.Color.SetColor(Color.White);
                hdr.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            ws.View.FreezePanes(2, 2);

            int row = 2;
            foreach (var entityGroup in privileges.GroupBy(p => p.EntityName).OrderBy(g => g.Key))
            {
                ws.Cells[row, 1].Value = entityGroup.Key;

                var byType = entityGroup
                    .GroupBy(p => p.PrivilegeType)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Depth).First().Depth);

                for (int c = 0; c < PrivilegeTypes.Length; c++)
                {
                    int depth = byType.TryGetValue(PrivilegeTypes[c], out var d) ? d : 0;
                    var cell = ws.Cells[row, c + 2];
                    cell.Value = DepthLabel(depth);
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ApplyCellColor(cell, depth);
                }

                row++;
            }

            ws.Cells.AutoFitColumns();
            if (ws.Column(1).Width < 20) ws.Column(1).Width = 20;
        }

        private static void ApplyCellColor(ExcelRange cell, int depth)
        {
            Color bg;
            Color fg;
            switch (depth)
            {
                case 1: bg = Color.FromArgb(237, 162, 0);  fg = Color.Black; break; // User - amber
                case 2: bg = Color.FromArgb(240, 199, 0);  fg = Color.Black; break; // Business Unit - gold
                case 4: bg = Color.FromArgb(76, 175, 80);  fg = Color.White; break; // Parent-Child BU - green
                case 8: bg = Color.FromArgb(46, 155, 50);  fg = Color.White; break; // Organization - dark green
                default: return;                                                      // None - no fill
            }
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(bg);
            cell.Style.Font.Color.SetColor(fg);
        }

        private static string DepthLabel(int depth)
        {
            switch (depth)
            {
                case 1: return "User";
                case 2: return "Business Unit";
                case 4: return "Parent-Child BU";
                case 8: return "Organization";
                default: return "";
            }
        }

        private static string SanitizeSheetName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Role";
            foreach (var c in new[] { ':', '\\', '/', '?', '*', '[', ']' })
                name = name.Replace(c.ToString(), "_");
            return name.Length > 31 ? name.Substring(0, 31) : name;
        }
    }
}
