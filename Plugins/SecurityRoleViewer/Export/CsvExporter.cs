using SecurityRoleViewer.Models;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SecurityRoleViewer.Export
{
    public static class CsvExporter
    {
        public static void Export(string filePath, List<RolePrivilegeInfo> privileges)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Role,Category,Entity,Privilege,AccessLevel");

            foreach (var p in privileges)
            {
                sb.AppendLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\"",
                    Escape(p.RoleName),
                    Escape(p.Category),
                    Escape(p.EntityName),
                    Escape(p.PrivilegeName),
                    Escape(p.AccessLevelLabel)));
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
