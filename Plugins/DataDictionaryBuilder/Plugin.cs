using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace DataDictionaryBuilder
{
    [Export(typeof(IXrmToolBoxPlugin)),
    ExportMetadata("Name", "Data Dictionary Builder"),
    ExportMetadata("Description", "Generate Dataverse data dictionaries and Mermaid ERD diagrams from metadata"),
    ExportMetadata("SmallImageBase64", null),
    ExportMetadata("BigImageBase64", null),
    ExportMetadata("BackgroundColor", "#2D6A4F"),
    ExportMetadata("PrimaryFontColor", "#FFFFFF"),
    ExportMetadata("SecondaryFontColor", "#D8F3DC")]
    public class Plugin : PluginBase
    {
        public override IXrmToolBoxPluginControl GetControl()
        {
            return new DataDictionaryBuilderControl();
        }
    }
}
