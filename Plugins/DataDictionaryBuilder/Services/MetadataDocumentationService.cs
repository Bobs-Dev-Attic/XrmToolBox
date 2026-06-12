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

        /// <summary>Fast, lightweight entity list (entity-level metadata only), each
        /// stamped with a category for the toolbar filter.</summary>
        public List<EntityListItem> GetEntities()
        {
            var request = new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            };

            var response = (RetrieveAllEntitiesResponse)_service.Execute(request);
            return response.EntityMetadata
                .Where(e => !string.IsNullOrEmpty(e.LogicalName))
                .Select(e => new EntityListItem
                {
                    LogicalName = e.LogicalName,
                    DisplayName = Label(e.DisplayName, e.LogicalName),
                    IsCustom = e.IsCustomEntity == true,
                    Category = Categorize(e)
                })
                .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // First match wins. A custom activity falls under Custom (checked first).
        private static string Categorize(EntityMetadata entity)
        {
            if (entity.IsIntersect == true || entity.IsLogicalEntity == true) return "Misc";
            if (entity.IsCustomEntity == true) return "Custom";
            if (entity.IsActivity == true) return "Activity";
            return "System";
        }

        /// <summary>Full metadata for one entity, loaded on demand when it is checked.</summary>
        public EntityDetail GetEntityDetail(string logicalName, bool includeSystemAttributes)
        {
            var request = new RetrieveEntityRequest
            {
                LogicalName = logicalName,
                EntityFilters = EntityFilters.Entity | EntityFilters.Attributes | EntityFilters.Relationships,
                RetrieveAsIfPublished = true
            };

            var entity = ((RetrieveEntityResponse)_service.Execute(request)).EntityMetadata;

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

            foreach (var attribute in (entity.Attributes ?? new AttributeMetadata[0])
                .Where(a => includeSystemAttributes || a.IsCustomAttribute == true || a.IsPrimaryId == true || a.IsPrimaryName == true)
                .Where(a => !string.IsNullOrEmpty(a.LogicalName))
                .OrderBy(a => Label(a.DisplayName, a.LogicalName), StringComparer.OrdinalIgnoreCase))
            {
                entityDoc.Attributes.Add(BuildAttribute(entity, attribute));
            }

            return new EntityDetail
            {
                Entity = entityDoc,
                Relationships = BuildRelationships(entity)
            };
        }

        // Every 1:N, N:1, and N:N relationship the entity participates in.
        private static List<RelationshipDocumentation> BuildRelationships(EntityMetadata entity)
        {
            var list = new List<RelationshipDocumentation>();

            foreach (var r in entity.OneToManyRelationships ?? Enumerable.Empty<OneToManyRelationshipMetadata>())
                list.Add(new RelationshipDocumentation
                {
                    SchemaName = r.SchemaName,
                    RelationshipType = "One-to-many",
                    ReferencingEntity = r.ReferencingEntity,
                    ReferencingAttribute = r.ReferencingAttribute,
                    ReferencedEntity = r.ReferencedEntity,
                    ReferencedAttribute = r.ReferencedAttribute
                });

            foreach (var r in entity.ManyToOneRelationships ?? Enumerable.Empty<OneToManyRelationshipMetadata>())
                list.Add(new RelationshipDocumentation
                {
                    SchemaName = r.SchemaName,
                    RelationshipType = "Many-to-one",
                    ReferencingEntity = r.ReferencingEntity,
                    ReferencingAttribute = r.ReferencingAttribute,
                    ReferencedEntity = r.ReferencedEntity,
                    ReferencedAttribute = r.ReferencedAttribute
                });

            foreach (var r in entity.ManyToManyRelationships ?? Enumerable.Empty<ManyToManyRelationshipMetadata>())
                list.Add(new RelationshipDocumentation
                {
                    SchemaName = r.SchemaName,
                    RelationshipType = "Many-to-many",
                    Entity1LogicalName = r.Entity1LogicalName,
                    Entity2LogicalName = r.Entity2LogicalName,
                    IntersectEntityName = r.IntersectEntityName
                });

            list.Sort((a, b) => string.Compare(a.SchemaName, b.SchemaName, StringComparison.OrdinalIgnoreCase));
            return list;
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
