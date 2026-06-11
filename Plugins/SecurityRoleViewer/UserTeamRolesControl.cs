using Microsoft.Xrm.Sdk;
using SecurityRoleViewer.Models;
using SecurityRoleViewer.Rendering;
using SecurityRoleViewer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using XrmToolBox.Extensibility;

namespace SecurityRoleViewer
{
    /// <summary>
    /// The "User/Team Roles" tab: pick a user or team on the left, see the effective
    /// (combined, including team-inherited) entity privilege matrix on the right.
    /// Hosted inside the plugin via <see cref="IRoleViewerHost"/>.
    /// </summary>
    public partial class UserTeamRolesControl : UserControl
    {
        private enum PrincipalKind { User, Team }

        private sealed class PrincipalItem
        {
            public PrincipalKind Kind { get; }
            public Guid Id { get; }
            public string Name { get; }

            public PrincipalItem(PrincipalKind kind, Guid id, string name)
            {
                Kind = kind;
                Id = id;
                Name = name;
            }
        }

        private IRoleViewerHost _host;
        private SecurityRoleService _service;
        private Dictionary<string, string> _entityDisplayNames
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // The selected principal's effective privileges (concatenated across all of
        // its roles); cached so filter changes re-render without re-querying.
        private List<RolePrivilegeInfo> _currentPrivileges;

        private bool _showLogicalNames;
        private bool _suppressSelect;

        private readonly Dictionary<int, ToolStripMenuItem> _levelMenuItems
            = new Dictionary<int, ToolStripMenuItem>();
        private readonly ToolStripMenuItem[] _columnMenuItems
            = new ToolStripMenuItem[MatrixRendering.PrivilegeColumns.Length];

        public UserTeamRolesControl()
        {
            InitializeComponent();
            BuildLevelFilterMenu();
            BuildColumnFilterMenu();
        }

        internal IRoleViewerHost Host
        {
            get => _host;
            set => _host = value;
        }

        // ---- connection / business units --------------------------------------

        /// <summary>Populates the BU dropdown from the parent's single BU load.</summary>
        public void SetBusinessUnits(List<Entity> businessUnits)
        {
            tsddBusinessUnits.DropDownItems.Clear();

            foreach (var bu in businessUnits)
            {
                var id = bu.GetAttributeValue<Guid>("businessunitid");
                var name = bu.GetAttributeValue<string>("name");

                tsddBusinessUnits.DropDownItems.Add(new ToolStripMenuItem(
                    string.IsNullOrEmpty(name) ? id.ToString() : name)
                {
                    Checked = true,
                    CheckOnClick = true,
                    Tag = id
                });
            }

            tsddBusinessUnits.Enabled = tsddBusinessUnits.DropDownItems.Count > 0;
        }

        /// <summary>Clears loaded principals and the matrix (e.g. on reconnect).</summary>
        public void Reset()
        {
            _service = null;
            _entityDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _currentPrivileges = null;

            _suppressSelect = true;
            lvPrincipals.Items.Clear();
            lvPrincipals.Groups.Clear();
            _suppressSelect = false;

            ShowMatrix(null);
        }

        private List<Guid> GetCheckedBusinessUnitIds()
        {
            var ids = new List<Guid>();
            foreach (ToolStripMenuItem item in tsddBusinessUnits.DropDownItems)
                if (item.Checked && item.Tag is Guid id)
                    ids.Add(id);
            return ids;
        }

        // ---- load users + teams ----------------------------------------------

        private void tsbLoadUsers_Click(object sender, EventArgs e)
        {
            if (_host?.Service == null)
            {
                MessageBox.Show("Connect to an environment first.", "Load Users",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _service = new SecurityRoleService(_host.Service);

            var checkedBuIds = GetCheckedBusinessUnitIds();
            IList<Guid> buFilter =
                checkedBuIds.Count == 0 || checkedBuIds.Count == tsddBusinessUnits.DropDownItems.Count
                    ? null
                    : checkedBuIds;

            _host.RunWork(new WorkAsyncInfo
            {
                Message = "Loading users and teams...",
                Work = (w, args) =>
                {
                    var users = _service.GetUsers(buFilter);
                    var teams = _service.GetTeams(buFilter);

                    // Display names are needed to render the matrix later; load once.
                    Dictionary<string, string> displayNames;
                    try { displayNames = _service.GetEntityDisplayNames(); }
                    catch
                    {
                        displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }

                    args.Result = new LoadResult
                    {
                        Users = users,
                        Teams = teams,
                        DisplayNames = displayNames
                    };
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        _host.ShowError(args.Error, "Loading Users and Teams");
                        return;
                    }

                    var result = (LoadResult)args.Result;
                    _entityDisplayNames = result.DisplayNames;
                    PopulatePrincipals(result.Users, result.Teams);
                }
            });
        }

        private void PopulatePrincipals(List<Entity> users, List<Entity> teams)
        {
            _suppressSelect = true;
            lvPrincipals.BeginUpdate();
            lvPrincipals.Items.Clear();
            lvPrincipals.Groups.Clear();

            AddPrincipalGroup("Users", users, "fullname", "systemuserid", PrincipalKind.User);
            AddPrincipalGroup("Teams", teams, "name", "teamid", PrincipalKind.Team);

            ResizePrincipalColumn();
            lvPrincipals.EndUpdate();
            _suppressSelect = false;

            ShowMatrix(null);
        }

        private void AddPrincipalGroup(
            string header, List<Entity> entities, string nameAttr, string idAttr, PrincipalKind kind)
        {
            if (entities == null || entities.Count == 0)
                return;

            var group = new ListViewGroup(header, $"{header} ({entities.Count})");
            lvPrincipals.Groups.Add(group);

            foreach (var entity in entities)
            {
                var id = entity.GetAttributeValue<Guid>(idAttr);
                var name = entity.GetAttributeValue<string>(nameAttr);
                if (string.IsNullOrEmpty(name)) name = id.ToString();

                lvPrincipals.Items.Add(new ListViewItem(name, group)
                {
                    Tag = new PrincipalItem(kind, id, name)
                });
            }
        }

        // ---- principal selection -> effective matrix --------------------------

        private void lvPrincipals_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressSelect || _service == null) return;
            if (lvPrincipals.SelectedItems.Count == 0) return;

            var principal = lvPrincipals.SelectedItems[0].Tag as PrincipalItem;
            if (principal == null) return;

            _host.RunWork(new WorkAsyncInfo
            {
                Message = $"Loading effective privileges for {principal.Name}...",
                Work = (w, args) =>
                {
                    var roleIds = ResolveEffectiveRoleIds(principal);

                    // Concatenate every role's privileges; BuildEntityRows already
                    // takes the max depth per entity/privilege, giving effective access.
                    var privileges = new List<RolePrivilegeInfo>();
                    foreach (var roleId in roleIds)
                        privileges.AddRange(_service.GetRolePrivileges(roleId, string.Empty));

                    args.Result = privileges;
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        _host.ShowError(args.Error, "Loading Effective Privileges");
                        return;
                    }

                    _currentPrivileges = (List<RolePrivilegeInfo>)args.Result;
                    RebuildMatrix();
                }
            });
        }

        private List<Guid> ResolveEffectiveRoleIds(PrincipalItem principal)
        {
            var roleIds = new HashSet<Guid>();

            if (principal.Kind == PrincipalKind.User)
            {
                foreach (var id in _service.GetUserRoleIds(principal.Id))
                    roleIds.Add(id);

                // Roles inherited through team membership.
                foreach (var teamId in _service.GetUserTeamIds(principal.Id))
                    foreach (var id in _service.GetTeamRoleIds(teamId))
                        roleIds.Add(id);
            }
            else
            {
                foreach (var id in _service.GetTeamRoleIds(principal.Id))
                    roleIds.Add(id);
            }

            return roleIds.ToList();
        }

        // ---- matrix rendering -------------------------------------------------

        private void RebuildMatrix()
        {
            if (_currentPrivileges == null)
            {
                ShowMatrix(null);
                return;
            }

            var keyword = tstEntitySearch.Text?.Trim() ?? "";
            var rows = MatrixRendering.BuildEntityRows(
                _currentPrivileges, keyword, GetVisibleDepths(), GetVisibleColumns(),
                ResolveDisplayName);

            var grid = MatrixRendering.CreateMatrixGrid(
                rows, GetVisibleColumns(), _showLogicalNames, null);
            grid.Dock = DockStyle.Fill;
            grid.ScrollBars = ScrollBars.Both;

            ShowMatrix(grid);
        }

        private void ShowMatrix(DataGridView grid)
        {
            foreach (Control c in pnlMatrix.Controls)
                c.Dispose();
            pnlMatrix.Controls.Clear();

            if (grid == null)
            {
                lblEmpty.Visible = true;
                pnlMatrix.Visible = false;
                return;
            }

            lblEmpty.Visible = false;
            pnlMatrix.Visible = true;
            pnlMatrix.Controls.Add(grid);
        }

        private string ResolveDisplayName(string logicalName)
        {
            if (!string.IsNullOrEmpty(logicalName)
                && _entityDisplayNames.TryGetValue(logicalName, out var displayName)
                && !string.IsNullOrEmpty(displayName))
                return displayName;

            return logicalName;
        }

        // ---- filter menus (mirror Role Permissions) ---------------------------

        private void BuildLevelFilterMenu()
        {
            foreach (var level in MatrixRendering.AccessLevels)
            {
                var item = new ToolStripMenuItem(level.Label)
                {
                    Checked = true,
                    CheckOnClick = true,
                    Tag = level.Depth
                };
                item.CheckedChanged += (s, e) => RebuildMatrix();
                _levelMenuItems[level.Depth] = item;
                tsddLevels.DropDownItems.Add(item);
            }
        }

        private void BuildColumnFilterMenu()
        {
            for (int c = 0; c < MatrixRendering.PrivilegeColumns.Length; c++)
            {
                var item = new ToolStripMenuItem(
                    MatrixRendering.ColumnLabel(MatrixRendering.PrivilegeColumns[c]))
                {
                    Checked = true,
                    CheckOnClick = true,
                    Tag = c
                };
                item.CheckedChanged += (s, e) => RebuildMatrix();
                _columnMenuItems[c] = item;
                tsddColumns.DropDownItems.Add(item);
            }
        }

        private HashSet<int> GetVisibleDepths()
        {
            var set = new HashSet<int>();
            foreach (var kv in _levelMenuItems)
                if (kv.Value.Checked)
                    set.Add(kv.Key);
            return set;
        }

        private bool[] GetVisibleColumns()
        {
            var visible = new bool[MatrixRendering.PrivilegeColumns.Length];
            for (int c = 0; c < MatrixRendering.PrivilegeColumns.Length; c++)
                visible[c] = _columnMenuItems[c].Checked;
            return visible;
        }

        private void tstEntitySearch_TextChanged(object sender, EventArgs e)
        {
            if (_currentPrivileges != null)
                RebuildMatrix();
        }

        private void tsbEntityLabel_Click(object sender, EventArgs e)
        {
            _showLogicalNames = tsbEntityLabel.Checked;
            if (_currentPrivileges != null)
                RebuildMatrix();
        }

        // ---- left list sizing -------------------------------------------------

        private void ResizePrincipalColumn()
        {
            if (lvPrincipals.Columns.Count > 0)
                lvPrincipals.Columns[0].Width = Math.Max(80, lvPrincipals.ClientSize.Width - 4);
        }

        private void lvPrincipals_Resize(object sender, EventArgs e)
        {
            ResizePrincipalColumn();
        }

        private sealed class LoadResult
        {
            public List<Entity> Users { get; set; }
            public List<Entity> Teams { get; set; }
            public Dictionary<string, string> DisplayNames { get; set; }
        }
    }
}
