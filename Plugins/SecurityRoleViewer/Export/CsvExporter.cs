using SecurityRoleViewer.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SecurityRoleViewer.Export
{
    public static class CsvExporter
    {
        private static readonly string[] PrivilegeTypes =
            { "Create", "Read", "Write", "Delete", "Append", "AppendTo", "Assign", "Share" };

        public static void Export(string filePath, List<RolePrivilegeInfo> privileges)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Role,Entity,Create,Read,Write,Delete,Append,AppendTo,Assign,Share");

            var grouped = privileges
                .Where(p => !string.IsNullOrEmpty(p.EntityName))
                .GroupBy(p => new { p.RoleName, p.EntityName })
                .OrderBy(g => g.Key.RoleName)
                .ThenBy(g => g.Key.EntityName);

            foreach (var group in grouped)
            {
                var privsByType = group
                    .GroupBy(p => p.PrivilegeType)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Depth).First().AccessLevelLabel);

                sb.Append($"\"{Escape(group.Key.RoleName)}\",\"{Escape(group.Key.EntityName)}\"");
                foreach (var pt in PrivilegeTypes)
                {
                    string level = "";
                    if (privsByType.TryGetValue(pt, out var l))
                        level = l;
                    sb.Append($",\"{Escape(level)}\"");
                }
                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static string Escape(string value)
        {
            if (value == null) return "";
            return value.Replace("\"", "\"\"");
        }
    }
}
