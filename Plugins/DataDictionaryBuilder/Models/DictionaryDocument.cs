using System.Collections.Generic;

namespace DataDictionaryBuilder.Models
{
    public class DictionaryDocument
    {
        public List<EntityDocumentation> Entities { get; } = new List<EntityDocumentation>();
        public List<RelationshipDocumentation> Relationships { get; } = new List<RelationshipDocumentation>();
    }

    /// <summary>Lightweight entry for the entity checklist (no attributes/relationships).</summary>
    public class EntityListItem
    {
        public string LogicalName { get; set; }
        public string DisplayName { get; set; }
        public bool IsCustom { get; set; }
        public string Category { get; set; }
    }

    /// <summary>An entity's full metadata, loaded on demand when it is checked.</summary>
    public class EntityDetail
    {
        public EntityDocumentation Entity { get; set; }
        public List<RelationshipDocumentation> Relationships { get; set; } = new List<RelationshipDocumentation>();
    }
}
