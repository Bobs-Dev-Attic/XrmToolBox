namespace DataDictionaryBuilder.Models
{
    public class RelationshipDocumentation
    {
        public string SchemaName { get; set; }
        public string RelationshipType { get; set; }
        public string ReferencingEntity { get; set; }
        public string ReferencingAttribute { get; set; }
        public string ReferencedEntity { get; set; }
        public string ReferencedAttribute { get; set; }
        public string Entity1LogicalName { get; set; }
        public string Entity2LogicalName { get; set; }
        public string IntersectEntityName { get; set; }
    }
}
