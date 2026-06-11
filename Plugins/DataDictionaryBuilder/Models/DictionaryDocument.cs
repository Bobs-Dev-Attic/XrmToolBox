using System.Collections.Generic;

namespace DataDictionaryBuilder.Models
{
    public class DictionaryDocument
    {
        public List<EntityDocumentation> Entities { get; } = new List<EntityDocumentation>();
        public List<RelationshipDocumentation> Relationships { get; } = new List<RelationshipDocumentation>();
    }
}
