using Microsoft.Xrm.Sdk;
using SecurityRoleViewer.Export;
using SecurityRoleViewer.Models;
using SecurityRoleViewer.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Args;
using XrmToolBox.Extensibility.Interfaces;

namespace SecurityRoleViewer
{
    public partial class SecurityRoleViewerControl : PluginControlBase, IGitHubPlugin, IHelpPlugin, IStatusBarMessenger
    {
        private SecurityRoleService _roleService;
        private List<Entity> _allRoles;
        private Guid _selectedRoleId;
        private string _selectedRoleName;
        private List<RolePrivilegeInfo> _currentPrivileges;

        public SecurityRoleViewerControl()
        {
            InitializeComponent();
            tscbFilter.SelectedIndex = 0;
        }

        public event EventHandler<StatusBarMessageEventArgs> SendMessageToStatusBar;

        public string RepositoryName => "XrmToolBox";
        public string UserName => "MscrmTools";
        public string HelpUrl => "https://github.com/MscrmTools/XrmToolBox";

        private void tsbLoadRoles_Click(object sender, EventArgs e)
        {
            ExecuteMethod(LoadRoles);
        }

        private void LoadRoles()
        {
            _roleService = new SecurityRoleService(Service);

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading security roles...",
                Work = (w, args) =>
                {
                    args.Result = _roleService.GetRoles();
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Loading Roles");
                        return;
                    }

                    _allRoles = (List<Entity>)args.Result;
                    BuildTree(_allRoles);
                    tsbExport.Enabled = false;

                    SendMessageToStatusBar?.Invoke(this,
                        new StatusBarMessageEventArgs($"Loaded {_allRoles.Count} roles"));
                }
            });
        }

        private void BuildTree(List<Entity> roles)
        {
            tvRoles.BeginUpdate();
            tvRoles.Nodes.Clear();

            var searchText = tstSearch.Text?.Trim() ?? "";
            var filterLevel = tscbFilter.SelectedItem?.ToString() ?? "All";

            foreach (var role in roles)
            {
                var roleName = role.GetAttributeValue<string>("name") ?? "";
                var roleId = role.GetAttributeValue<Guid>("roleid");
                var buRef = role.GetAttributeValue<EntityReference>("businessunitid");
                var buName = buRef?.Name ?? "";

                var displayName = string.IsNullOrEmpty(buName)
                    ? roleName
                    : $"{roleName} ({buName})";

                if (_roleService != null && _roleService.IsCached(roleId))
                {
                    var privileges = _roleService.GetRolePrivileges(roleId, roleName);
                    var filtered = ApplyFilters(privileges, searchText, filterLevel);

                    if (!string.IsNullOrEmpty(searchText) && filtered.Count == 0
                        && roleName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    var roleNode = new TreeNode(displayName) { Tag = roleId };
                    AddCategoryNodes(roleNode, filtered);
                    tvRoles.Nodes.Add(roleNode);
                }
                else
                {
                    if (!string.IsNullOrEmpty(searchText)
                        && roleName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    var roleNode = new TreeNode(displayName) { Tag = roleId };
                    tvRoles.Nodes.Add(roleNode);
                }
            }

            tvRoles.EndUpdate();
        }

        private void AddCategoryNodes(TreeNode roleNode, List<RolePrivilegeInfo> privileges)
        {
            var groups = privileges
                .GroupBy(p => p.Category)
                .OrderBy(g => g.Key == "Core Entities" ? 0 : g.Key == "Custom Entities" ? 1 : 2);

            foreach (var group in groups)
            {
                var categoryNode = new TreeNode(group.Key);

                var entityGroups = group.GroupBy(p => string.IsNullOrEmpty(p.EntityName) ? p.PrivilegeName : p.EntityName);
                foreach (var entityGroup in entityGroups.OrderBy(eg => eg.Key))
                {
                    var entityNode = new TreeNode(entityGroup.Key)
                    {
                        Tag = entityGroup.ToList()
                    };
                    categoryNode.Nodes.Add(entityNode);
                }

                roleNode.Nodes.Add(categoryNode);
            }
        }

        private List<RolePrivilegeInfo> ApplyFilters(List<RolePrivilegeInfo> privileges, string searchText, string filterLevel)
        {
            var result = privileges.AsEnumerable();

            if (!string.IsNullOrEmpty(searchText))
            {
                result = result.Where(p =>
                    p.EntityName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    p.PrivilegeName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (filterLevel != "All")
            {
                result = result.Where(p => p.AccessLevelLabel == filterLevel);
            }

            return result.ToList();
        }

        private void tvRoles_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Tag is List<RolePrivilegeInfo> entityPrivileges)
            {
                PopulateGrid(entityPrivileges);
                lblDetailHeader.Text = e.Node.Text;
                return;
            }

            if (e.Node.Tag is Guid roleId)
            {
                _selectedRoleId = roleId;
                _selectedRoleName = e.Node.Text;
                tsbExport.Enabled = true;

                LoadRolePrivileges(roleId, e.Node);
            }
        }

        private void LoadRolePrivileges(Guid roleId, TreeNode roleNode)
        {
            if (_roleService == null) return;

            var roleName = roleNode.Text;

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

                    _currentPrivileges = (List<RolePrivilegeInfo>)args.Result;

                    var searchText = tstSearch.Text?.Trim() ?? "";
                    var filterLevel = tscbFilter.SelectedItem?.ToString() ?? "All";
                    var filtered = ApplyFilters(_currentPrivileges, searchText, filterLevel);

                    roleNode.Nodes.Clear();
                    AddCategoryNodes(roleNode, filtered);
                    roleNode.Expand();

                    tsbExport.Enabled = true;
                    dgvPrivileges.Rows.Clear();
                    lblDetailHeader.Text = $"{roleName} - {filtered.Count} privileges";

                    SendMessageToStatusBar?.Invoke(this,
                        new StatusBarMessageEventArgs($"Loaded {_currentPrivileges.Count} privileges for {roleName}"));
                }
            });
        }

        private void PopulateGrid(List<RolePrivilegeInfo> privileges)
        {
            dgvPrivileges.Rows.Clear();

            foreach (var p in privileges)
            {
                dgvPrivileges.Rows.Add(p.PrivilegeName, p.AccessLevelLabel);
            }
        }

        private void dgvPrivileges_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex != 1 || e.Value == null) return;

            var level = e.Value.ToString();
            switch (level)
            {
                case "Organization":
                    e.CellStyle.ForeColor = Color.DarkGreen;
                    e.CellStyle.Font = new Font(dgvPrivileges.Font, FontStyle.Bold);
                    break;
                case "Parent-Child BU":
                    e.CellStyle.ForeColor = Color.Blue;
                    break;
                case "Business Unit":
                    e.CellStyle.ForeColor = Color.Goldenrod;
                    break;
                case "User":
                    e.CellStyle.ForeColor = Color.Orange;
                    break;
                case "None":
                    e.CellStyle.ForeColor = Color.Gray;
                    break;
            }
        }

        private void tstSearch_TextChanged(object sender, EventArgs e)
        {
            if (_allRoles != null)
                BuildTree(_allRoles);
        }

        private void tscbFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_allRoles != null)
                BuildTree(_allRoles);
        }

        private void tsbExport_Click(object sender, EventArgs e)
        {
            if (_currentPrivileges == null || _currentPrivileges.Count == 0)
            {
                MessageBox.Show("No privileges loaded to export. Select a role first.",
                    "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                var safeName = string.Join("_", _selectedRoleName.Split(
                    System.IO.Path.GetInvalidFileNameChars()));
                dialog.FileName = $"{safeName}_Privileges.csv";
                dialog.Filter = "CSV files (*.csv)|*.csv";
                dialog.Title = "Export Role Privileges";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    CsvExporter.Export(dialog.FileName, _currentPrivileges);
                    MessageBox.Show($"Exported {_currentPrivileges.Count} privileges to:\n{dialog.FileName}",
                        "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }
}
