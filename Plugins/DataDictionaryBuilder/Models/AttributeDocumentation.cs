namespace DataDictionaryBuilder.Models
{
    public class AttributeDocumentation
    {
        public string EntityLogicalName { get; set; }
        public string EntityDisplayName { get; set; }
        public string LogicalName { get; set; }
        public string DisplayName { get; set; }
        public string SchemaName { get; set; }
        public string AttributeType { get; set; }
        public string Description { get; set; }
        public bool IsPrimaryId { get; set; }
        public bool IsPrimaryName { get; set; }
        public bool IsRequired { get; set; }
        public bool IsValidForCreate { get; set; }
        public bool IsValidForUpdate { get; set; }
        public bool IsValidForRead { get; set; }
        public int? MaxLength { get; set; }
        public string Targets { get; set; }
        public string Options { get; set; }
    }
}
