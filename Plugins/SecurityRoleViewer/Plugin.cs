using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace SecurityRoleViewer
{
    [Export(typeof(IXrmToolBoxPlugin)),
    ExportMetadata("Name", "Security Role Viewer"),
    ExportMetadata("Description", "View security role privileges by entity with filtering and CSV export"),
    ExportMetadata("SmallImageBase64", PluginImages.Small),
    ExportMetadata("BigImageBase64", PluginImages.Big),
    ExportMetadata("BackgroundColor", "#4A90D9"),
    ExportMetadata("PrimaryFontColor", "#FFFFFF"),
    ExportMetadata("SecondaryFontColor", "#E0E0E0")]
    public class Plugin : PluginBase
    {
        public override IXrmToolBoxPluginControl GetControl()
        {
            return new SecurityRoleViewerControl();
        }
    }
}
