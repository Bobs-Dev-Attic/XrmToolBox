namespace SecurityRoleViewer.Models
{
    public class RolePrivilegeInfo
    {
        public string RoleName { get; set; }
        public string EntityName { get; set; }
        public string PrivilegeName { get; set; }
        public string PrivilegeType { get; set; }
        public int Depth { get; set; }
        public int ObjectTypeCode { get; set; }
        public string Category { get; set; }

        public string AccessLevelLabel
        {
            get
            {
                switch (Depth)
                {
                    case 0: return "None";
                    case 1: return "User";
                    case 2: return "Business Unit";
                    case 4: return "Parent-Child BU";
                    case 8: return "Organization";
                    default: return "Unknown";
                }
            }
        }
    }
}
