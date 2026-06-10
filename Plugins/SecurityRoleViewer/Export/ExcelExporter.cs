using SecurityRoleViewer.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SecurityRoleViewer.Export
{
    // Generates an Excel XML Spreadsheet 2003 (.xls) file — no external dependencies.
    // Excel opens these natively; the first open may prompt a format-confirmation dialog.
    public static class ExcelExporter
    {
        private static readonly string[] PrivilegeTypes =
            { "Create", "Read", "Write", "Delete", "Append", "AppendTo", "Assign", "Share" };

        public static void Export(string filePath, List<RolePrivilegeInfo> privileges)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
            sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
            sb.AppendLine("  xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
            sb.AppendLine("  xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
            sb.AppendLine("  xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");

            AppendStyles(sb);

            var byRole = privileges
                .Where(p => !string.IsNullOrEmpty(p.EntityName))
                .GroupBy(p => p.RoleName)
                .OrderBy(g => g.Key);

            foreach (var roleGroup in byRole)
                AppendWorksheet(sb, roleGroup.Key, roleGroup.ToList());

            sb.AppendLine("</Workbook>");

            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        private static void AppendStyles(StringBuilder sb)
        {
            sb.AppendLine("  <Styles>");
            sb.AppendLine("    <Style ss:ID=\"Default\" ss:Name=\"Normal\"/>");
            // s1 = header: blue bg, white bold text, centered
            AppendStyle(sb, "s1", "#4472C4", "#FFFFFF", bold: true);
            // s2-s5 = access levels matching the in-app pie colours
            AppendStyle(sb, "s2", "#EDA200", "#000000", bold: false); // User - amber
            AppendStyle(sb, "s3", "#F0C700", "#000000", bold: false); // Business Unit - gold
            AppendStyle(sb, "s4", "#4CAF50", "#FFFFFF", bold: false); // Parent-Child BU - green
            AppendStyle(sb, "s5", "#2E9B32", "#FFFFFF", bold: false); // Organization - dark green
            sb.AppendLine("  </Styles>");
        }

        private static void AppendStyle(StringBuilder sb, string id, string bgColor, string fontColor, bool bold)
        {
            sb.AppendLine($"    <Style ss:ID=\"{id}\">");
            sb.AppendLine("      <Alignment ss:Horizontal=\"Center\"/>");
            sb.Append($"      <Font ss:FontName=\"Calibri\" ss:Color=\"{fontColor}\"");
            if (bold) sb.Append(" ss:Bold=\"1\"");
            sb.AppendLine("/>");
            sb.AppendLine($"      <Interior ss:Color=\"{bgColor}\" ss:Pattern=\"Solid\"/>");
            sb.AppendLine("    </Style>");
        }

        private static void AppendWorksheet(StringBuilder sb, string roleName, List<RolePrivilegeInfo> privileges)
        {
            sb.AppendLine($"  <Worksheet ss:Name=\"{X(SanitizeSheetName(roleName))}\">");
            sb.AppendLine("    <Table>");
            sb.AppendLine("      <Column ss:Width=\"160\"/>");
            for (int i = 0; i < PrivilegeTypes.Length; i++)
                sb.AppendLine("      <Column ss:Width=\"90\"/>");

            // Header row
            sb.AppendLine("      <Row>");
            Cell(sb, "Entity", "s1");
            foreach (var pt in PrivilegeTypes)
                Cell(sb, pt == "AppendTo" ? "Append To" : pt, "s1");
            sb.AppendLine("      </Row>");

            // Data rows
            foreach (var entityGroup in privileges.GroupBy(p => p.EntityName).OrderBy(g => g.Key))
            {
                var byType = entityGroup
                    .GroupBy(p => p.PrivilegeType)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Depth).First().Depth);

                sb.AppendLine("      <Row>");
                Cell(sb, entityGroup.Key, null);
                foreach (var pt in PrivilegeTypes)
                {
                    int depth = byType.TryGetValue(pt, out var d) ? d : 0;
                    Cell(sb, DepthLabel(depth), DepthStyle(depth));
                }
                sb.AppendLine("      </Row>");
            }

            sb.AppendLine("    </Table>");

            // Freeze first row
            sb.AppendLine("    <WorksheetOptions xmlns=\"urn:schemas-microsoft-com:office:excel\">");
            sb.AppendLine("      <FreezePanes/><FrozenNoSplit/>");
            sb.AppendLine("      <SplitHorizontal>1</SplitHorizontal>");
            sb.AppendLine("      <TopRowBottomPane>1</TopRowBottomPane>");
            sb.AppendLine("      <ActivePane>2</ActivePane>");
            sb.AppendLine("    </WorksheetOptions>");

            sb.AppendLine("  </Worksheet>");
        }

        private static void Cell(StringBuilder sb, string value, string styleId)
        {
            var s = styleId != null ? $" ss:StyleID=\"{styleId}\"" : "";
            if (string.IsNullOrEmpty(value))
                sb.AppendLine($"        <Cell{s}/>");
            else
                sb.AppendLine($"        <Cell{s}><Data ss:Type=\"String\">{X(value)}</Data></Cell>");
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

        private static string DepthStyle(int depth)
        {
            switch (depth)
            {
                case 1: return "s2";
                case 2: return "s3";
                case 4: return "s4";
                case 8: return "s5";
                default: return null;
            }
        }

        private static string X(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
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
