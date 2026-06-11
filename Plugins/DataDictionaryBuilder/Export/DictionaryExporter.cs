using DataDictionaryBuilder.Models;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace DataDictionaryBuilder.Export
{
    public static class DictionaryExporter
    {
        public static void ExportCsv(DictionaryDocument document, string folder)
        {
            Directory.CreateDirectory(folder);
            WriteEntitiesCsv(document, Path.Combine(folder, "entities.csv"));
            WriteAttributesCsv(document, Path.Combine(folder, "attributes.csv"));
            WriteRelationshipsCsv(document, Path.Combine(folder, "relationships.csv"));
        }

        public static void ExportMarkdown(DictionaryDocument document, string path)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Dataverse Data Dictionary");
            builder.AppendLine();
            builder.AppendLine("| Entity | Logical name | Ownership | Custom | Description |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var entity in document.Entities)
            {
                builder.AppendLine($"| {EscapeMarkdown(entity.DisplayName)} | `{EscapeMarkdown(entity.LogicalName)}` | {EscapeMarkdown(entity.OwnershipType)} | {YesNo(entity.IsCustomEntity)} | {EscapeMarkdown(entity.Description)} |");
            }

            foreach (var entity in document.Entities)
            {
                builder.AppendLine();
                builder.AppendLine("## " + entity.DisplayName);
                builder.AppendLine();
                builder.AppendLine("| Column | Logical name | Type | Required | Create | Update | Description |");
                builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
                foreach (var attribute in entity.Attributes)
                {
                    builder.AppendLine($"| {EscapeMarkdown(attribute.DisplayName)} | `{EscapeMarkdown(attribute.LogicalName)}` | {EscapeMarkdown(attribute.AttributeType)} | {YesNo(attribute.IsRequired)} | {YesNo(attribute.IsValidForCreate)} | {YesNo(attribute.IsValidForUpdate)} | {EscapeMarkdown(attribute.Description)} |");
                }
            }

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        public static void ExportMermaidErd(DictionaryDocument document, string path)
        {
            var builder = new StringBuilder();
            builder.AppendLine("erDiagram");
            foreach (var entity in document.Entities)
            {
                builder.AppendLine("  " + MermaidName(entity.LogicalName) + " {");
                foreach (var attribute in entity.Attributes
                    .Where(a => a.IsPrimaryId || a.IsPrimaryName || a.IsRequired)
                    .Take(12))
                {
                    builder.AppendLine($"    {MermaidType(attribute.AttributeType)} {MermaidName(attribute.LogicalName)}");
                }
                builder.AppendLine("  }");
            }

            foreach (var relationship in document.Relationships)
            {
                if (relationship.RelationshipType == "Many-to-one")
                {
                    builder.AppendLine($"  {MermaidName(relationship.ReferencedEntity)} ||--o{{ {MermaidName(relationship.ReferencingEntity)} : \"{EscapeMermaid(relationship.ReferencingAttribute)}\"");
                }
                else
                {
                    builder.AppendLine($"  {MermaidName(relationship.Entity1LogicalName)} }}o--o{{ {MermaidName(relationship.Entity2LogicalName)} : \"{EscapeMermaid(relationship.IntersectEntityName)}\"");
                }
            }

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        private static void WriteEntitiesCsv(DictionaryDocument document, string path)
        {
            var builder = new StringBuilder();
            builder.AppendLine("DisplayName,LogicalName,SchemaName,OwnershipType,IsCustomEntity,IsIntersect,PrimaryIdAttribute,PrimaryNameAttribute,Description");
            foreach (var entity in document.Entities)
            {
                builder.AppendLine(string.Join(",", Csv(entity.DisplayName), Csv(entity.LogicalName), Csv(entity.SchemaName),
                    Csv(entity.OwnershipType), Csv(entity.IsCustomEntity), Csv(entity.IsIntersect),
                    Csv(entity.PrimaryIdAttribute), Csv(entity.PrimaryNameAttribute), Csv(entity.Description)));
            }
            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        private static void WriteAttributesCsv(DictionaryDocument document, string path)
        {
            var builder = new StringBuilder();
            builder.AppendLine("EntityDisplayName,EntityLogicalName,DisplayName,LogicalName,SchemaName,AttributeType,IsPrimaryId,IsPrimaryName,IsRequired,IsValidForCreate,IsValidForUpdate,IsValidForRead,MaxLength,Targets,Options,Description");
            foreach (var attribute in document.Entities.SelectMany(e => e.Attributes))
            {
                builder.AppendLine(string.Join(",", Csv(attribute.EntityDisplayName), Csv(attribute.EntityLogicalName),
                    Csv(attribute.DisplayName), Csv(attribute.LogicalName), Csv(attribute.SchemaName),
                    Csv(attribute.AttributeType), Csv(attribute.IsPrimaryId), Csv(attribute.IsPrimaryName),
                    Csv(attribute.IsRequired), Csv(attribute.IsValidForCreate), Csv(attribute.IsValidForUpdate),
                    Csv(attribute.IsValidForRead), Csv(attribute.MaxLength), Csv(attribute.Targets),
                    Csv(attribute.Options), Csv(attribute.Description)));
            }
            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        private static void WriteRelationshipsCsv(DictionaryDocument document, string path)
        {
            var builder = new StringBuilder();
            builder.AppendLine("SchemaName,RelationshipType,ReferencingEntity,ReferencingAttribute,ReferencedEntity,ReferencedAttribute,Entity1LogicalName,Entity2LogicalName,IntersectEntityName");
            foreach (var relationship in document.Relationships)
            {
                builder.AppendLine(string.Join(",", Csv(relationship.SchemaName), Csv(relationship.RelationshipType),
                    Csv(relationship.ReferencingEntity), Csv(relationship.ReferencingAttribute),
                    Csv(relationship.ReferencedEntity), Csv(relationship.ReferencedAttribute),
                    Csv(relationship.Entity1LogicalName), Csv(relationship.Entity2LogicalName),
                    Csv(relationship.IntersectEntityName)));
            }
            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        private static string Csv(object value)
        {
            var text = value?.ToString() ?? string.Empty;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private static string YesNo(bool value) => value ? "Yes" : "No";

        private static string EscapeMarkdown(string value)
        {
            return (value ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }

        private static string MermaidName(string value)
        {
            return string.IsNullOrEmpty(value) ? "unknown" : value.Replace("-", "_").Replace(".", "_");
        }

        private static string MermaidType(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "string";

            var lower = value.ToLowerInvariant();
            if (lower.Contains("int") || lower.Contains("decimal") || lower.Contains("money") || lower.Contains("double"))
                return "number";
            if (lower.Contains("datetime"))
                return "datetime";
            if (lower.Contains("bool"))
                return "boolean";
            return "string";
        }

        private static string EscapeMermaid(string value)
        {
            return (value ?? string.Empty).Replace("\"", "'");
        }
    }
}
