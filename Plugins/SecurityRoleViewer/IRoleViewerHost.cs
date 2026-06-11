using Microsoft.Xrm.Sdk;
using System;
using XrmToolBox.Extensibility;

namespace SecurityRoleViewer
{
    /// <summary>
    /// The bridge a hosted child view (e.g. the User/Team Roles tab) uses to reach
    /// the plugin's connection and async/error plumbing without being a plugin itself.
    /// </summary>
    internal interface IRoleViewerHost
    {
        IOrganizationService Service { get; }

        void RunWork(WorkAsyncInfo info);

        void ShowError(Exception error, string context);
    }
}
