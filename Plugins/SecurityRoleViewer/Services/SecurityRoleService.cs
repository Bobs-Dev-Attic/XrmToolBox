using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SecurityRoleViewer.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SecurityRoleViewer.Services
{
    public class SecurityRoleService
    {
        private static readonly string[] StandardPrivilegeTypes =
            { "Create", "Read", "Write", "Delete", "Append", "AppendTo", "Assign", "Share" };

        private readonly IOrganizationService _service;
        private readonly Dictionary<Guid, List<RolePrivilegeInfo>> _cache
            = new Dictionary<Guid, List<RolePrivilegeInfo>>();

        public bool IsCached(Guid roleId) => _cache.ContainsKey(roleId);

        public SecurityRoleService(IOrganizationService service)
        {
            _service = service;
        }

        public List<Entity> GetRoles()
        {
            var query = new QueryExpression("role")
            {
                ColumnSet = new ColumnSet("name", "roleid", "businessunitid"),
                Orders = { new OrderExpression("name", OrderType.Ascending) }
            };

            var results = _service.RetrieveMultiple(query);
            return results.Entities.ToList();
        }

        public List<RolePrivilegeInfo> GetRolePrivileges(Guid roleId, string roleName)
        {
            if (_cache.TryGetValue(roleId, out var cached))
                return cached;

            var fetchXml = $@"
<fetch>
  <entity name='roleprivileges'>
    <attribute name='privilegedepthmask' />
    <filter>
      <condition attribute='roleid' operator='eq' value='{roleId}' />
    </filter>
    <link-entity name='privilege' from='privilegeid' to='privilegeid' alias='priv'>
      <attribute name='name' />
      <attribute name='accessright' />
      <link-entity name='privilegeobjecttypecodes' from='privilegeid' to='privilegeid' alias='otc' link-type='outer'>
        <attribute name='objecttypecode' />
      </link-entity>
    </link-entity>
  </entity>
</fetch>";

            var results = _service.RetrieveMultiple(new FetchExpression(fetchXml));

            var privileges = new List<RolePrivilegeInfo>();
            foreach (var entity in results.Entities)
            {
                var privName = entity.GetAttributeValue<AliasedValue>("priv.name")?.Value?.ToString() ?? "";
                var depthMask = entity.GetAttributeValue<int>("privilegedepthmask");
                var otcValue = entity.GetAttributeValue<AliasedValue>("otc.objecttypecode")?.Value;
                var accessRight = entity.GetAttributeValue<AliasedValue>("priv.accessright")?.Value;

                int objectTypeCode = -1;
                string entityName = "";
                string category = "Miscellaneous";

                if (otcValue != null)
                {
                    entityName = otcValue.ToString();
                    category = entityName.Contains("_") ? "Custom Entities" : "Core Entities";
                    objectTypeCode = entityName.Contains("_") ? 10000 : 1;
                }

                int depth = 0;
                if (depthMask >= 8) depth = 8;
                else if (depthMask >= 4) depth = 4;
                else if (depthMask >= 2) depth = 2;
                else if (depthMask >= 1) depth = 1;

                var privilegeType = MapPrivilegeType(privName, accessRight);

                privileges.Add(new RolePrivilegeInfo
                {
                    RoleName = roleName,
                    EntityName = entityName,
                    PrivilegeName = privName,
                    PrivilegeType = privilegeType,
                    Depth = depth,
                    ObjectTypeCode = objectTypeCode,
                    Category = category
                });
            }

            privileges = privileges
                .Where(p => !string.IsNullOrEmpty(p.EntityName) || p.Category == "Miscellaneous")
                .GroupBy(p => new { p.EntityName, p.PrivilegeType })
                .Select(g => g.OrderByDescending(p => p.Depth).First())
                .OrderBy(p => p.Category)
                .ThenBy(p => p.EntityName)
                .ThenBy(p => p.PrivilegeName)
                .ToList();

            _cache[roleId] = privileges;
            return privileges;
        }

        private static string MapPrivilegeType(string privilegeName, object accessRight)
        {
            if (accessRight != null)
            {
                var ar = Convert.ToInt32(accessRight);
                switch (ar)
                {
                    case 1: return "Read";
                    case 2: return "Write";
                    case 4: return "Append";
                    case 16: return "Create";
                    case 32: return "Delete";
                    case 65536: return "AppendTo";
                    case 524288: return "Assign";
                    case 262144: return "Share";
                }
            }

            if (string.IsNullOrEmpty(privilegeName)) return "Other";

            var lower = privilegeName.ToLowerInvariant();
            if (lower.StartsWith("prvcreate")) return "Create";
            if (lower.StartsWith("prvread")) return "Read";
            if (lower.StartsWith("prvwrite")) return "Write";
            if (lower.StartsWith("prvdelete")) return "Delete";
            if (lower.StartsWith("prvappendto")) return "AppendTo";
            if (lower.StartsWith("prvappend")) return "Append";
            if (lower.StartsWith("prvassign")) return "Assign";
            if (lower.StartsWith("prvshare")) return "Share";

            return "Other";
        }

        public void ClearCache()
        {
            _cache.Clear();
        }
    }
}
