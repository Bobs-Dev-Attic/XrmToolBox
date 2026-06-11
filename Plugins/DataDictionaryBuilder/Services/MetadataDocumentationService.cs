using DataDictionaryBuilder.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataDictionaryBuilder.Services
{
    public class MetadataDocumentationService
    {
        private readonly IOrganizationService _service;

        public MetadataDocumentationService(IOrganizationService service)
        {
            _service = service;
        }

        public DictionaryDocument Build(bool customOnly, bool includeSystemAttributes)
        {
            var request = new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity | EntityFilters.Attributes | EntityFilters.Relationships,
                RetrieveAsIfPublished = true
            };

            var response = (RetrieveAllEntitiesResponse)_service.Execute(request);
            var entities = response.EntityMetadata
                .Where(e => !customOnly || e.IsCustomEntity == true)
                .Where(e => !string.IsNullOrEmpty(e.LogicalName))
                .OrderBy(e => Label(e.DisplayName, e.LogicalName))
                .ToList();

            var includedNames = new HashSet<string>(entities.Select(e => e.LogicalName), StringComparer.OrdinalIgnoreCase);
            var document = new DictionaryDocument();

            foreach (var entity in entities)
            {
                var entityDoc = new EntityDocumentation
                {
                    LogicalName = entity.LogicalName,
                    DisplayName = Label(entity.DisplayName, entity.LogicalName),
                    SchemaName = entity.SchemaName,
                    Description = Label(entity.Description, string.Empty),
                    OwnershipType = entity.OwnershipType?.ToString() ?? string.Empty,
                    IsCustomEntity = entity.IsCustomEntity == true,
                    IsIntersect = entity.IsIntersect == true,
                    PrimaryIdAttribute = entity.PrimaryIdAttribute,
                    PrimaryNameAttribute = entity.PrimaryNameAttribute
                };

                foreach (var attribute in entity.Attributes
                    .Where(a => includeSystemAttributes || a.IsCustomAttribute == true || a.IsPrimaryId == true || a.IsPrimaryName == true)
                    .Where(a => !string.IsNullOrEmpty(a.LogicalName))
                    .OrderBy(a => Label(a.DisplayName, a.LogicalName)))
                {
                    entityDoc.Attributes.Add(BuildAttribute(entity, attribute));
                }

                document.Entities.Add(entityDoc);
                AddRelationships(document.Relationships, entity, includedNames);
            }

            document.Relationships.Sort((left, right) => string.Compare(left.SchemaName, right.SchemaName, StringComparison.OrdinalIgnoreCase));
            return document;
        }

        private static AttributeDocumentation BuildAttribute(EntityMetadata entity, AttributeMetadata attribute)
        {
            var stringAttribute = attribute as StringAttributeMetadata;
            var lookupAttribute = attribute as LookupAttributeMetadata;

            return new AttributeDocumentation
            {
                EntityLogicalName = entity.LogicalName,
                EntityDisplayName = Label(entity.DisplayName, entity.LogicalName),
                LogicalName = attribute.LogicalName,
                DisplayName = Label(attribute.DisplayName, attribute.LogicalName),
                SchemaName = attribute.SchemaName,
                AttributeType = attribute.AttributeTypeName?.Value ?? attribute.AttributeType?.ToString() ?? string.Empty,
                Description = Label(attribute.Description, string.Empty),
                IsPrimaryId = attribute.IsPrimaryId == true,
                IsPrimaryName = attribute.IsPrimaryName == true,
                IsRequired = attribute.RequiredLevel?.Value == AttributeRequiredLevel.ApplicationRequired
                    || attribute.RequiredLevel?.Value == AttributeRequiredLevel.SystemRequired,
                IsValidForCreate = attribute.IsValidForCreate == true,
                IsValidForUpdate = attribute.IsValidForUpdate == true,
                IsValidForRead = attribute.IsValidForRead == true,
                MaxLength = stringAttribute?.MaxLength,
                Targets = lookupAttribute?.Targets == null ? string.Empty : string.Join(", ", lookupAttribute.Targets),
                Options = GetOptions(attribute)
            };
        }

        private static void AddRelationships(
            List<RelationshipDocumentation> relationships,
            EntityMetadata entity,
            HashSet<string> includedNames)
        {
            foreach (var relationship in entity.ManyToOneRelationships ?? Enumerable.Empty<OneToManyRelationshipMetadata>())
            {
                if (!includedNames.Contains(relationship.ReferencingEntity)
                    || !includedNames.Contains(relationship.ReferencedEntity))
                    continue;

                relationships.Add(new RelationshipDocumentation
                {
                    SchemaName = relationship.SchemaName,
                    RelationshipType = "Many-to-one",
                    ReferencingEntity = relationship.ReferencingEntity,
                    ReferencingAttribute = relationship.ReferencingAttribute,
                    ReferencedEntity = relationship.ReferencedEntity,
                    ReferencedAttribute = relationship.ReferencedAttribute
                });
            }

            foreach (var relationship in entity.ManyToManyRelationships ?? Enumerable.Empty<ManyToManyRelationshipMetadata>())
            {
                if (!string.Equals(relationship.Entity1LogicalName, entity.LogicalName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!includedNames.Contains(relationship.Entity1LogicalName)
                    || !includedNames.Contains(relationship.Entity2LogicalName))
                    continue;

                relationships.Add(new RelationshipDocumentation
                {
                    SchemaName = relationship.SchemaName,
                    RelationshipType = "Many-to-many",
                    Entity1LogicalName = relationship.Entity1LogicalName,
                    Entity2LogicalName = relationship.Entity2LogicalName,
                    IntersectEntityName = relationship.IntersectEntityName
                });
            }
        }

        private static string GetOptions(AttributeMetadata attribute)
        {
            var enumAttribute = attribute as EnumAttributeMetadata;
            if (enumAttribute?.OptionSet?.Options == null)
                return string.Empty;

            return string.Join("; ", enumAttribute.OptionSet.Options
                .OrderBy(o => o.Value)
                .Select(o => (o.Value?.ToString() ?? string.Empty) + "=" + Label(o.Label, string.Empty)));
        }

        private static string Label(Label label, string fallback)
        {
            return label?.UserLocalizedLabel?.Label
                ?? label?.LocalizedLabels?.FirstOrDefault()?.Label
                ?? fallback
                ?? string.Empty;
        }
    }
}
