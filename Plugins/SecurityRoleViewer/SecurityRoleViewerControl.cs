using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using SecurityRoleViewer.Export;
using SecurityRoleViewer.Rendering;
using SecurityRoleViewer.Models;
using SecurityRoleViewer.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Args;
using XrmToolBox.Extensibility.Interfaces;

namespace SecurityRoleViewer
{
    public partial class SecurityRoleViewerControl : PluginControlBase, IGitHubPlugin, IHelpPlugin, IStatusBarMessenger, IRoleViewerHost
    {
        private SecurityRoleService _roleService;
        private List<Entity> _allRoles;
        private Dictionary<string, string> _entityDisplayNames
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, List<RolePrivilegeInfo>> _loadedPrivileges
            = new Dictionary<Guid, List<RolePrivilegeInfo>>();
        // Guards the ListView ItemChecked handler while we repopulate the list, so
        // restoring checkboxes during a rebuild doesn't trigger privilege loads.
        private bool _suppressItemCheck;

        // Roles are grouped into these categories, shown in this order; empty
        // categories are hidden. See CategorizeRole for the assignment rule.
        private static readonly string[] CategoryOrder = { "Custom", "System", "Core", "Misc" };

        public SecurityRoleViewerControl()
        {
            InitializeComponent();
            utrControl.Host = this;
            Load += SecurityRoleViewerControl_Load;
        }

        // --- IRoleViewerHost: lets the hosted User/Team Roles tab reuse the
        //     plugin's connection and async/error plumbing.
        IOrganizationService IRoleViewerHost.Service => Service;

        void IRoleViewerHost.RunWork(WorkAsyncInfo info) => WorkAsync(info);

        void IRoleViewerHost.ShowError(Exception error, string context)
            => ShowErrorDialog(error, context);

        private void SecurityRoleViewerControl_Load(object sender, EventArgs e)
        {
            // Default the role list to ~20% of the width; the plugin docks at a
            // larger width than the designer surface, so set this proportionally.
            var target = (int)(splitContainer1.Width * 0.2);
            if (target >= splitContainer1.Panel1MinSize
                && target <= splitContainer1.Width - splitContainer1.Panel2MinSize)
            {
                splitContainer1.SplitterDistance = target;
            }
        }

        public event EventHandler<StatusBarMessageEventArgs> SendMessageToStatusBar;

        public string RepositoryName => "XrmToolBox";
        public string UserName => "MscrmTools";
        public string HelpUrl => "https://github.com/MscrmTools/XrmToolBox";

        // Populate the Business Units dropdown as soon as a connection is available,
        // so the user can narrow the role load before clicking Load Roles.
        public override void UpdateConnection(
            IOrganizationService newService, ConnectionDetail detail,
            string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);
            utrControl.Reset();
            LoadBusinessUnits();
        }

        private void LoadBusinessUnits()
        {
            if (Service == null) return;

            var service = new SecurityRoleService(Service);
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading business units...",
                Work = (w, args) => args.Result = service.GetBusinessUnits(),
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Loading Business Units");
                        return;
                    }
                    PopulateBusinessUnitMenu((List<Entity>)args.Result);
                }
            });
        }

        private void PopulateBusinessUnitMenu(List<Entity> businessUnits)
        {
            tsddBusinessUnits.DropDownItems.Clear();

            foreach (var bu in businessUnits)
            {
                var id = bu.GetAttributeValue<Guid>("businessunitid");
                var name = bu.GetAttributeValue<string>("name");

                var item = new ToolStripMenuItem(string.IsNullOrEmpty(name) ? id.ToString() : name)
                {
                    Checked = true,        // all checked by default; a lone BU is checked too
                    CheckOnClick = true,
                    Tag = id
                };
                tsddBusinessUnits.DropDownItems.Add(item);
            }

            tsddBusinessUnits.Enabled = tsddBusinessUnits.DropDownItems.Count > 0;

            // Share the same BU list with the User/Team Roles tab.
            utrControl.SetBusinessUnits(businessUnits);
        }

        private List<Guid> GetCheckedBusinessUnitIds()
        {
            var ids = new List<Guid>();
            foreach (ToolStripMenuItem item in tsddBusinessUnits.DropDownItems)
                if (item.Checked && item.Tag is Guid id)
                    ids.Add(id);
            return ids;
        }

        private void tsbLoadRoles_Click(object sender, EventArgs e)
        {
            ExecuteMethod(LoadRoles);
        }

        private void LoadRoles()
        {
            _roleService = new SecurityRoleService(Service);
            _loadedPrivileges.Clear();

            // Read the BU selection on the UI thread before the background work.
            // All-checked (or nothing loaded) means no filter, i.e. load every role.
            var checkedBuIds = GetCheckedBusinessUnitIds();
            IList<Guid> buFilter =
                checkedBuIds.Count == 0 || checkedBuIds.Count == tsddBusinessUnits.DropDownItems.Count
                    ? null
                    : checkedBuIds;

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading security roles...",
                Work = (w, args) =>
                {
                    var roles = _roleService.GetRoles(buFilter);

                    // Resolve entity display names alongside the roles. If the caller
                    // lacks metadata read access we still want the roles to load, so
                    // failures here fall back to an empty map (logical names shown).
                    Dictionary<string, string> displayNames;
                    try
                    {
                        displayNames = _roleService.GetEntityDisplayNames();
                    }
                    catch
                    {
                        displayNames = new Dictionary<string, string>(
                            StringComparer.OrdinalIgnoreCase);
                    }

                    args.Result = new LoadResult
                    {
                        Roles = roles,
                        DisplayNames = displayNames
                    };
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Loading Roles");
                        return;
                    }

                    var result = (LoadResult)args.Result;
                    _allRoles = result.Roles;
                    _entityDisplayNames = result.DisplayNames;
                    PopulateRoleList();
                    tsbExport.Enabled = false;
                    RefreshMatrixSources();

                    SendMessageToStatusBar?.Invoke(this,
                        new StatusBarMessageEventArgs($"Loaded {_allRoles.Count} roles"));
                }
            });
        }

        // Assigns a role to a category. First match wins:
        //   Core   - backed by a role template (built-in baseline roles)
        //   System - managed (shipped by a solution)
        //   Custom - unmanaged (created/customized in this org)
        //   Misc   - flags unreadable (defensive fallback)
        private static string CategorizeRole(Entity role)
        {
            if (role.GetAttributeValue<EntityReference>("roletemplateid") != null)
                return "Core";

            if (role.Contains("ismanaged") && role["ismanaged"] is bool managed)
                return managed ? "System" : "Custom";

            return "Misc";
        }

        private void PopulateRoleList()
        {
            var searchText = tstSearch.Text?.Trim() ?? "";

            // Bucket matching roles by category first, so we can emit groups in the
            // configured order and skip the empty ones.
            var buckets = CategoryOrder.ToDictionary(c => c, c => new List<ListViewItem>());

            foreach (var role in _allRoles)
            {
                var roleName = role.GetAttributeValue<string>("name") ?? "";

                if (!string.IsNullOrEmpty(searchText)
                    && roleName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var roleId = role.GetAttributeValue<Guid>("roleid");
                var buName = role.GetAttributeValue<EntityReference>("businessunitid")?.Name ?? "";
                var label = string.IsNullOrEmpty(buName) ? roleName : $"{roleName} ({buName})";

                var item = new ListViewItem(label)
                {
                    Tag = new RoleListItem(roleId, roleName, label),
                    Checked = _loadedPrivileges.ContainsKey(roleId)
                };
                buckets[CategorizeRole(role)].Add(item);
            }

            _suppressItemCheck = true;
            lvRoles.BeginUpdate();
            lvRoles.Items.Clear();
            lvRoles.Groups.Clear();

            foreach (var category in CategoryOrder)
            {
                var items = buckets[category];
                if (items.Count == 0)
                    continue;

                var group = new ListViewGroup(category, $"{category} ({items.Count})");
                lvRoles.Groups.Add(group);
                foreach (var item in items)
                {
                    item.Group = group;
                    lvRoles.Items.Add(item);
                }
            }

            ResizeRoleColumn();
            lvRoles.EndUpdate();
            _suppressItemCheck = false;

            // Add the native collapse/expand chevrons once the groups exist.
            NativeMethods.MakeGroupsCollapsible(lvRoles);
        }

        private void ResizeRoleColumn()
        {
            if (lvRoles.Columns.Count > 0)
                lvRoles.Columns[0].Width = Math.Max(60, lvRoles.ClientSize.Width - 4);
        }

        private void lvRoles_Resize(object sender, EventArgs e)
        {
            ResizeRoleColumn();
        }

        private void lvRoles_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_suppressItemCheck) return;

            var item = e.Item.Tag as RoleListItem;
            if (item == null) return;

            if (e.Item.Checked)
            {
                if (!_loadedPrivileges.ContainsKey(item.RoleId))
                {
                    LoadRolePrivileges(item.RoleId, item.RoleName);
                    return;
                }
            }
            else
            {
                _loadedPrivileges.Remove(item.RoleId);
            }

            BeginInvoke((Action)RefreshMatrixSources);
        }

        private void LoadRolePrivileges(Guid roleId, string roleName)
        {
            if (_roleService == null) return;

            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Loading privileges for {roleName}...",
                Work = (w, args) =>
                {
                    args.Result = _roleService.GetRolePrivileges(roleId, roleName);
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Loading Privileges");
                        return;
                    }

                    _loadedPrivileges[roleId] = (List<RolePrivilegeInfo>)args.Result;
                    tsbExport.Enabled = _loadedPrivileges.Count > 0;
                    RefreshMatrixSources();

                    SendMessageToStatusBar?.Invoke(this,
                        new StatusBarMessageEventArgs(
                            $"Loaded {_loadedPrivileges[roleId].Count} privileges for {roleName}"));
                }
            });
        }

        // Feeds the shared matrix panel with one source per checked role.
        private void RefreshMatrixSources()
        {
            var sources = new List<MatrixSource>();
            foreach (ListViewItem lvItem in lvRoles.CheckedItems)
            {
                var item = lvItem.Tag as RoleListItem;
                if (item != null && _loadedPrivileges.TryGetValue(item.RoleId, out var privileges))
                    sources.Add(new MatrixSource(item.RoleId, item.RoleName, privileges));
            }

            matrixPanel.SetSources(sources, ResolveDisplayName);
            tsbExport.Enabled = sources.Count > 0;
        }

        private string ResolveDisplayName(string logicalName)
        {
            if (!string.IsNullOrEmpty(logicalName)
                && _entityDisplayNames.TryGetValue(logicalName, out var displayName)
                && !string.IsNullOrEmpty(displayName))
                return displayName;

            return logicalName;
        }

        private void tstSearch_TextChanged(object sender, EventArgs e)
        {
            if (_allRoles != null)
                PopulateRoleList();
        }

        private List<RolePrivilegeInfo> CollectCheckedPrivileges()
        {
            var all = new List<RolePrivilegeInfo>();
            foreach (ListViewItem lvItem in lvRoles.CheckedItems)
            {
                var item = lvItem.Tag as RoleListItem;
                if (item != null && _loadedPrivileges.ContainsKey(item.RoleId))
                    all.AddRange(_loadedPrivileges[item.RoleId]);
            }
            return all;
        }

        private void tsbExportCsv_Click(object sender, EventArgs e)
        {
            var privileges = CollectCheckedPrivileges();
            if (privileges.Count == 0)
            {
                MessageBox.Show("No privileges loaded to export. Select a role first.",
                    "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.FileName = $"SecurityRolePrivileges_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                dialog.Filter = "CSV files (*.csv)|*.csv";
                dialog.Title = "Export Role Privileges";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    CsvExporter.Export(dialog.FileName, privileges);
                    MessageBox.Show($"Exported privileges to:\n{dialog.FileName}",
                        "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void tsbExportExcel_Click(object sender, EventArgs e)
        {
            var privileges = CollectCheckedPrivileges();
            if (privileges.Count == 0)
            {
                MessageBox.Show("No privileges loaded to export. Select a role first.",
                    "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.FileName = $"SecurityRolePrivileges_{DateTime.Now:yyyyMMdd_HHmmss}.xls";
                dialog.Filter = "Excel files (*.xls)|*.xls";
                dialog.Title = "Export Role Privileges";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    ExcelExporter.Export(dialog.FileName, privileges);
                    MessageBox.Show($"Exported privileges to:\n{dialog.FileName}",
                        "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private class RoleListItem
        {
            public Guid RoleId { get; }
            public string RoleName { get; }
            private readonly string _displayName;

            public RoleListItem(Guid roleId, string roleName, string displayName)
            {
                RoleId = roleId;
                RoleName = roleName;
                _displayName = displayName;
            }

            public override string ToString() => _displayName;
        }

        private class LoadResult
        {
            public List<Entity> Roles { get; set; }
            public Dictionary<string, string> DisplayNames { get; set; }
        }
    }
}
