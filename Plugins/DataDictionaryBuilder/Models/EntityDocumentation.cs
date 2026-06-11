using System.Collections.Generic;

namespace DataDictionaryBuilder.Models
{
    public class EntityDocumentation
    {
        public string LogicalName { get; set; }
        public string DisplayName { get; set; }
        public string SchemaName { get; set; }
        public string Description { get; set; }
        public string OwnershipType { get; set; }
        public bool IsCustomEntity { get; set; }
        public bool IsIntersect { get; set; }
        public string PrimaryIdAttribute { get; set; }
        public string PrimaryNameAttribute { get; set; }
        public List<AttributeDocumentation> Attributes { get; } = new List<AttributeDocumentation>();
    }
}
