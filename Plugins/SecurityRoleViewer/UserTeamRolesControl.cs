using Microsoft.Xrm.Sdk;
using SecurityRoleViewer.Models;
using SecurityRoleViewer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using XrmToolBox.Extensibility;

namespace SecurityRoleViewer
{
    /// <summary>
    /// The "User/Team Roles" tab: a filterable, checkable list of users and teams on
    /// the left; checking a principal opens a tab on the right showing its effective
    /// (combined, team-inherited) privilege matrix via the shared matrix panel.
    /// </summary>
    public partial class UserTeamRolesControl : UserControl
    {
        private enum PrincipalKind { User, Team }
        private enum StatusFilter { Enabled, Disabled, All }
        private enum LicenseFilter { All, Licensed, Unlicensed }

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

        private List<Entity> _allUsers = new List<Entity>();
        private List<Entity> _allTeams = new List<Entity>();
        // userId -> team ids the user belongs to (for the Teams membership filter).
        private Dictionary<Guid, HashSet<Guid>> _userTeams = new Dictionary<Guid, HashSet<Guid>>();

        // Effective privileges per checked principal; feeds the matrix tabs.
        private readonly Dictionary<Guid, List<RolePrivilegeInfo>> _principalPrivileges
            = new Dictionary<Guid, List<RolePrivilegeInfo>>();

        private StatusFilter _statusFilter = StatusFilter.Enabled;
        private LicenseFilter _licenseFilter = LicenseFilter.All;
        private bool _suppressCheck;

        public UserTeamRolesControl()
        {
            InitializeComponent();
            BuildStatusMenu();
            BuildLicensedMenu();
            Load += (s, e) => ApplySplit();
        }

        internal IRoleViewerHost Host
        {
            get => _host;
            set => _host = value;
        }

        private void ApplySplit()
        {
            // Left list ~20%, matrix ~80%.
            var target = (int)(splitContainer1.Width * 0.2);
            if (target >= splitContainer1.Panel1MinSize
                && target <= splitContainer1.Width - splitContainer1.Panel2MinSize)
            {
                splitContainer1.SplitterDistance = target;
            }
        }

        // ---- connection / business units --------------------------------------

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

        public void Reset()
        {
            _service = null;
            _entityDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _allUsers = new List<Entity>();
            _allTeams = new List<Entity>();
            _userTeams = new Dictionary<Guid, HashSet<Guid>>();
            _principalPrivileges.Clear();

            tsddTeamsFilter.DropDownItems.Clear();
            tsddTeamsFilter.Enabled = false;

            _suppressCheck = true;
            lvPrincipals.Items.Clear();
            lvPrincipals.Groups.Clear();
            _suppressCheck = false;

            matrixPanel.SetSources(new List<MatrixSource>(), ResolveDisplayName);
        }

        private List<Guid> GetCheckedBusinessUnitIds()
        {
            var ids = new List<Guid>();
            foreach (ToolStripMenuItem item in tsddBusinessUnits.DropDownItems)
                if (item.Checked && item.Tag is Guid id)
                    ids.Add(id);
            return ids;
        }

        // ---- load users + teams + memberships ---------------------------------

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
                    var memberships = _service.GetTeamMemberships();

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
                        Memberships = memberships,
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
                    _allUsers = result.Users;
                    _allTeams = result.Teams;
                    _userTeams = BuildUserTeamMap(result.Memberships);
                    _principalPrivileges.Clear();

                    PopulateTeamsFilter();
                    RefilterPrincipals();
                    matrixPanel.SetSources(new List<MatrixSource>(), ResolveDisplayName);
                }
            });
        }

        private static Dictionary<Guid, HashSet<Guid>> BuildUserTeamMap(List<Entity> memberships)
        {
            var map = new Dictionary<Guid, HashSet<Guid>>();
            foreach (var m in memberships)
            {
                var userId = m.GetAttributeValue<Guid>("systemuserid");
                var teamId = m.GetAttributeValue<Guid>("teamid");
                if (userId == Guid.Empty || teamId == Guid.Empty)
                    continue;
                if (!map.TryGetValue(userId, out var teams))
                    map[userId] = teams = new HashSet<Guid>();
                teams.Add(teamId);
            }
            return map;
        }

        private void PopulateTeamsFilter()
        {
            tsddTeamsFilter.DropDownItems.Clear();
            foreach (var team in _allTeams)
            {
                var id = team.GetAttributeValue<Guid>("teamid");
                var name = team.GetAttributeValue<string>("name");
                var item = new ToolStripMenuItem(string.IsNullOrEmpty(name) ? id.ToString() : name)
                {
                    Checked = false,
                    CheckOnClick = true,
                    Tag = id
                };
                item.CheckedChanged += (s, e) => RefilterPrincipals();
                tsddTeamsFilter.DropDownItems.Add(item);
            }
            tsddTeamsFilter.Enabled = tsddTeamsFilter.DropDownItems.Count > 0;
        }

        private HashSet<Guid> GetCheckedTeamFilterIds()
        {
            var ids = new HashSet<Guid>();
            foreach (ToolStripMenuItem item in tsddTeamsFilter.DropDownItems)
                if (item.Checked && item.Tag is Guid id)
                    ids.Add(id);
            return ids;
        }

        // ---- filtering + list population --------------------------------------

        private void UserFilterChanged(object sender, EventArgs e) => RefilterPrincipals();

        private void RefilterPrincipals()
        {
            var keyword = tstUserSearch.Text?.Trim() ?? "";
            var teamFilter = GetCheckedTeamFilterIds();

            var userItems = new List<ListViewItem>();
            foreach (var user in _allUsers)
            {
                if (!PassesStatus(user) || !PassesLicense(user) || !PassesTeamFilter(user, teamFilter))
                    continue;
                if (!MatchesUserKeyword(user, keyword))
                    continue;

                var id = user.GetAttributeValue<Guid>("systemuserid");
                var name = user.GetAttributeValue<string>("fullname");
                if (string.IsNullOrEmpty(name)) name = id.ToString();
                userItems.Add(MakePrincipalItem(PrincipalKind.User, id, name));
            }

            var teamItems = new List<ListViewItem>();
            foreach (var team in _allTeams)
            {
                var id = team.GetAttributeValue<Guid>("teamid");
                var name = team.GetAttributeValue<string>("name") ?? id.ToString();
                if (keyword.Length > 0 && name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                teamItems.Add(MakePrincipalItem(PrincipalKind.Team, id, name));
            }

            _suppressCheck = true;
            lvPrincipals.BeginUpdate();
            lvPrincipals.Items.Clear();
            lvPrincipals.Groups.Clear();

            AddGroup("Users", userItems);
            AddGroup("Teams", teamItems);

            ResizePrincipalColumn();
            lvPrincipals.EndUpdate();
            _suppressCheck = false;
        }

        private ListViewItem MakePrincipalItem(PrincipalKind kind, Guid id, string name)
        {
            return new ListViewItem(name)
            {
                Tag = new PrincipalItem(kind, id, name),
                Checked = _principalPrivileges.ContainsKey(id)
            };
        }

        private void AddGroup(string header, List<ListViewItem> items)
        {
            if (items.Count == 0)
                return;

            var group = new ListViewGroup(header, $"{header} ({items.Count})");
            lvPrincipals.Groups.Add(group);
            foreach (var item in items)
            {
                item.Group = group;
                lvPrincipals.Items.Add(item);
            }
        }

        private bool PassesStatus(Entity user)
        {
            if (_statusFilter == StatusFilter.All) return true;
            bool disabled = user.GetAttributeValue<bool>("isdisabled");
            return _statusFilter == StatusFilter.Disabled ? disabled : !disabled;
        }

        private bool PassesLicense(Entity user)
        {
            if (_licenseFilter == LicenseFilter.All) return true;
            // "Licensed" proxy: accessmode Read-Write (0).
            bool licensed = user.GetAttributeValue<OptionSetValue>("accessmode")?.Value == 0;
            return _licenseFilter == LicenseFilter.Licensed ? licensed : !licensed;
        }

        private bool PassesTeamFilter(Entity user, HashSet<Guid> teamFilter)
        {
            if (teamFilter.Count == 0) return true;
            var userId = user.GetAttributeValue<Guid>("systemuserid");
            return _userTeams.TryGetValue(userId, out var teams) && teams.Overlaps(teamFilter);
        }

        private static bool MatchesUserKeyword(Entity user, string keyword)
        {
            if (keyword.Length == 0) return true;
            string[] fields =
            {
                user.GetAttributeValue<string>("fullname"),
                user.GetAttributeValue<string>("domainname"),
                user.GetAttributeValue<string>("internalemailaddress"),
                user.GetAttributeValue<string>("firstname"),
                user.GetAttributeValue<string>("lastname")
            };
            return fields.Any(f => !string.IsNullOrEmpty(f)
                && f.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // ---- check -> effective matrix sources --------------------------------

        private void lvPrincipals_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_suppressCheck || _service == null) return;

            var principal = e.Item.Tag as PrincipalItem;
            if (principal == null) return;

            if (e.Item.Checked)
            {
                if (_principalPrivileges.ContainsKey(principal.Id))
                {
                    RefreshSources();
                    return;
                }

                _host.RunWork(new WorkAsyncInfo
                {
                    Message = $"Loading effective privileges for {principal.Name}...",
                    Work = (w, args) =>
                    {
                        var roleIds = ResolveEffectiveRoleIds(principal);
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
                        _principalPrivileges[principal.Id] = (List<RolePrivilegeInfo>)args.Result;
                        RefreshSources();
                    }
                });
            }
            else
            {
                _principalPrivileges.Remove(principal.Id);
                RefreshSources();
            }
        }

        private void RefreshSources()
        {
            var sources = new List<MatrixSource>();
            foreach (ListViewItem lvItem in lvPrincipals.CheckedItems)
            {
                var principal = lvItem.Tag as PrincipalItem;
                if (principal != null && _principalPrivileges.TryGetValue(principal.Id, out var privs))
                    sources.Add(new MatrixSource(principal.Id, principal.Name, privs));
            }
            matrixPanel.SetSources(sources, ResolveDisplayName);
        }

        private List<Guid> ResolveEffectiveRoleIds(PrincipalItem principal)
        {
            var roleIds = new HashSet<Guid>();

            if (principal.Kind == PrincipalKind.User)
            {
                foreach (var id in _service.GetUserRoleIds(principal.Id))
                    roleIds.Add(id);

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

        private string ResolveDisplayName(string logicalName)
        {
            if (!string.IsNullOrEmpty(logicalName)
                && _entityDisplayNames.TryGetValue(logicalName, out var displayName)
                && !string.IsNullOrEmpty(displayName))
                return displayName;

            return logicalName;
        }

        // ---- status / licensed filter menus -----------------------------------

        private void BuildStatusMenu()
        {
            AddRadioItem(tsddStatus, "Enabled", StatusFilter.Enabled, _statusFilter,
                v => { _statusFilter = (StatusFilter)v; });
            AddRadioItem(tsddStatus, "Disabled", StatusFilter.Disabled, _statusFilter,
                v => { _statusFilter = (StatusFilter)v; });
            AddRadioItem(tsddStatus, "All", StatusFilter.All, _statusFilter,
                v => { _statusFilter = (StatusFilter)v; });
            tsddStatus.Text = "Status: Enabled";
        }

        private void BuildLicensedMenu()
        {
            AddRadioItem(tsddLicensed, "All", LicenseFilter.All, _licenseFilter,
                v => { _licenseFilter = (LicenseFilter)v; });
            AddRadioItem(tsddLicensed, "Licensed", LicenseFilter.Licensed, _licenseFilter,
                v => { _licenseFilter = (LicenseFilter)v; });
            AddRadioItem(tsddLicensed, "Non-licensed", LicenseFilter.Unlicensed, _licenseFilter,
                v => { _licenseFilter = (LicenseFilter)v; });
            tsddLicensed.Text = "Licensed: All";
        }

        // Adds one mutually-exclusive option to a dropdown; on click it updates the
        // backing field (via apply), re-checks siblings, relabels, and refilters.
        private void AddRadioItem(
            ToolStripDropDownButton button, string label, object value, object current, Action<object> apply)
        {
            var item = new ToolStripMenuItem(label)
            {
                Tag = value,
                Checked = Equals(value, current)
            };
            item.Click += (s, e) =>
            {
                apply(value);
                foreach (ToolStripMenuItem sibling in button.DropDownItems)
                    sibling.Checked = sibling == item;
                button.Text = (button == tsddStatus ? "Status: " : "Licensed: ") + label;
                RefilterPrincipals();
            };
            button.DropDownItems.Add(item);
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
            public List<Entity> Memberships { get; set; }
            public Dictionary<string, string> DisplayNames { get; set; }
        }
    }
}
