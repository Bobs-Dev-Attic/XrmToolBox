using DataDictionaryBuilder.Export;
using DataDictionaryBuilder.Models;
using DataDictionaryBuilder.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Args;
using XrmToolBox.Extensibility.Interfaces;

namespace DataDictionaryBuilder
{
    public class DataDictionaryBuilderControl : PluginControlBase, IGitHubPlugin, IHelpPlugin, IStatusBarMessenger
    {
        private readonly ToolStrip _toolStrip = new ToolStrip();
        private readonly ToolStripButton _loadButton = new ToolStripButton("Load Entities");
        private readonly ToolStripButton _customOnlyButton = new ToolStripButton("Custom entities only");
        private readonly ToolStripButton _systemAttributesButton = new ToolStripButton("Include system attributes");
        private readonly ToolStripDropDownButton _exportButton = new ToolStripDropDownButton("Export");

        private readonly SplitContainer _split = new SplitContainer();
        private readonly TextBox _findBox = new TextBox();
        private readonly ListView _entityList = new ListView();
        private readonly TabControl _tabs = new TabControl();
        private readonly Label _emptyLabel = new Label();

        private MetadataDocumentationService _service;
        private List<EntityListItem> _allEntities = new List<EntityListItem>();
        private readonly Dictionary<string, EntityDetail> _details
            = new Dictionary<string, EntityDetail>(StringComparer.OrdinalIgnoreCase);
        private bool _suppressCheck;

        public event EventHandler<StatusBarMessageEventArgs> SendMessageToStatusBar;

        public string RepositoryName => "XrmToolBox";
        public string UserName => "MscrmTools";
        public string HelpUrl => "https://github.com/MscrmTools/XrmToolBox";

        public DataDictionaryBuilderControl()
        {
            InitializeUi();
            Load += (s, e) => ApplySplit();
        }

        private void InitializeUi()
        {
            _toolStrip.GripStyle = ToolStripGripStyle.Hidden;

            _loadButton.Click += LoadButton_Click;

            _customOnlyButton.CheckOnClick = true;
            _customOnlyButton.Checked = true;
            _customOnlyButton.ToolTipText = "List only custom entities";

            _systemAttributesButton.CheckOnClick = true;
            _systemAttributesButton.ToolTipText = "Include system attributes in the Attributes tab";

            _exportButton.Enabled = false;
            _exportButton.DropDownItems.Add("CSV files", null, ExportCsv_Click);
            _exportButton.DropDownItems.Add("Markdown", null, ExportMarkdown_Click);
            _exportButton.DropDownItems.Add("Mermaid ERD", null, ExportMermaid_Click);

            _toolStrip.Items.Add(_loadButton);
            _toolStrip.Items.Add(new ToolStripSeparator());
            _toolStrip.Items.Add(_customOnlyButton);
            _toolStrip.Items.Add(_systemAttributesButton);
            _toolStrip.Items.Add(new ToolStripSeparator());
            _toolStrip.Items.Add(_exportButton);

            // Left: find box + checkbox entity list.
            _findBox.Dock = DockStyle.Top;
            _findBox.TextChanged += (s, e) => PopulateEntityList();

            var findLabel = new Label
            {
                Dock = DockStyle.Top,
                Text = "Find entity:",
                AutoSize = false,
                Height = 18,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 0, 0)
            };

            _entityList.Dock = DockStyle.Fill;
            _entityList.CheckBoxes = true;
            _entityList.View = View.Details;
            _entityList.HeaderStyle = ColumnHeaderStyle.None;
            _entityList.FullRowSelect = true;
            _entityList.MultiSelect = false;
            _entityList.HideSelection = false;
            _entityList.ShowItemToolTips = true;
            _entityList.Columns.Add(new ColumnHeader { Text = "Entity", Width = 240 });
            _entityList.ItemChecked += EntityList_ItemChecked;
            _entityList.Resize += (s, e) => ResizeEntityColumn();

            // Dock order: list fills, then find box, then its label sits on top.
            var leftPanel = new Panel { Dock = DockStyle.Fill };
            leftPanel.Controls.Add(_entityList);
            leftPanel.Controls.Add(_findBox);
            leftPanel.Controls.Add(findLabel);

            // Right: per-entity tabs + empty-state label.
            _tabs.Dock = DockStyle.Fill;
            _tabs.Visible = false;

            _emptyLabel.Dock = DockStyle.Fill;
            _emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            _emptyLabel.ForeColor = Color.Gray;
            _emptyLabel.Font = new Font("Segoe UI", 10F);
            _emptyLabel.Text = "Load entities, then check one or more to view their metadata.";

            _split.Dock = DockStyle.Fill;
            _split.Panel1MinSize = 160;
            _split.Panel2MinSize = 320;
            _split.Size = new Size(900, 520); // valid SplitterDistance during init
            _split.SplitterDistance = 200;
            _split.Panel1.Controls.Add(leftPanel);
            _split.Panel2.Controls.Add(_tabs);
            _split.Panel2.Controls.Add(_emptyLabel);

            Controls.Add(_split);
            Controls.Add(_toolStrip);
        }

        private void ApplySplit()
        {
            var target = (int)(_split.Width * 0.2);
            var max = _split.Width - _split.Panel2MinSize;
            if (max <= _split.Panel1MinSize)
                return;

            _split.SplitterDistance = Math.Max(_split.Panel1MinSize, Math.Min(target, max));
        }

        // ---- load entity list -------------------------------------------------

        private void LoadButton_Click(object sender, EventArgs e)
        {
            ExecuteMethod(LoadEntities);
        }

        private void LoadEntities()
        {
            _service = new MetadataDocumentationService(Service);
            bool customOnly = _customOnlyButton.Checked;

            _loadButton.Enabled = false;
            SendMessage("Loading entity list...");

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading entities...",
                Work = (worker, args) => args.Result = _service.GetEntities(customOnly),
                PostWorkCallBack = args =>
                {
                    _loadButton.Enabled = true;

                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Unable to load entities");
                        SendMessage("Entity load failed.");
                        return;
                    }

                    _allEntities = (List<EntityListItem>)args.Result;
                    _details.Clear();
                    ClearTabs();
                    PopulateEntityList();
                    UpdateExportEnabled();
                    SendMessage($"Loaded {_allEntities.Count} entities.");
                }
            });
        }

        private void PopulateEntityList()
        {
            var keyword = _findBox.Text?.Trim() ?? "";

            _suppressCheck = true;
            _entityList.BeginUpdate();
            _entityList.Items.Clear();

            foreach (var entity in _allEntities)
            {
                if (keyword.Length > 0
                    && entity.DisplayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0
                    && entity.LogicalName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                _entityList.Items.Add(new ListViewItem(entity.DisplayName)
                {
                    Tag = entity,
                    ToolTipText = entity.LogicalName,
                    Checked = _details.ContainsKey(entity.LogicalName)
                });
            }

            ResizeEntityColumn();
            _entityList.EndUpdate();
            _suppressCheck = false;
        }

        private void ResizeEntityColumn()
        {
            if (_entityList.Columns.Count > 0)
                _entityList.Columns[0].Width = Math.Max(80, _entityList.ClientSize.Width - 4);
        }

        // ---- check -> lazy load -> tab ----------------------------------------

        private void EntityList_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_suppressCheck || _service == null) return;

            var entity = e.Item.Tag as EntityListItem;
            if (entity == null) return;

            if (e.Item.Checked)
            {
                if (_details.TryGetValue(entity.LogicalName, out var cached))
                {
                    AddEntityTab(cached);
                    UpdateExportEnabled();
                    return;
                }

                bool includeSystem = _systemAttributesButton.Checked;
                SendMessage($"Loading metadata for {entity.DisplayName}...");

                WorkAsync(new WorkAsyncInfo
                {
                    Message = $"Loading {entity.DisplayName}...",
                    Work = (w, args) => args.Result = _service.GetEntityDetail(entity.LogicalName, includeSystem),
                    PostWorkCallBack = args =>
                    {
                        if (args.Error != null)
                        {
                            ShowErrorDialog(args.Error, "Unable to load entity metadata");
                            return;
                        }

                        var detail = (EntityDetail)args.Result;
                        _details[entity.LogicalName] = detail;
                        AddEntityTab(detail);
                        UpdateExportEnabled();
                        SendMessage($"Loaded {entity.DisplayName}.");
                    }
                });
            }
            else
            {
                RemoveEntityTab(entity.LogicalName);
                UpdateExportEnabled();
            }
        }

        private void AddEntityTab(EntityDetail detail)
        {
            var key = detail.Entity.LogicalName;
            var name = "ent_" + key;

            // If already shown, just select it.
            foreach (TabPage existing in _tabs.TabPages)
            {
                if (existing.Name == name)
                {
                    _tabs.SelectedTab = existing;
                    ShowTabs();
                    return;
                }
            }

            var page = new TabPage(detail.Entity.DisplayName)
            {
                Name = name,
                ToolTipText = key,
                Tag = key,
                UseVisualStyleBackColor = true,
                Padding = new Padding(2)
            };

            var inner = new TabControl { Dock = DockStyle.Fill };
            inner.TabPages.Add(BuildGridTab("Entity", BuildEntityPropertyGrid(detail.Entity)));
            inner.TabPages.Add(BuildGridTab("Attributes", BuildDataGrid(detail.Entity.Attributes)));
            inner.TabPages.Add(BuildGridTab("Relationships", BuildDataGrid(detail.Relationships)));

            page.Controls.Add(inner);
            _tabs.TabPages.Add(page);
            _tabs.SelectedTab = page;
            ShowTabs();
        }

        private void RemoveEntityTab(string logicalName)
        {
            var name = "ent_" + logicalName;
            foreach (TabPage page in _tabs.TabPages)
            {
                if (page.Name != name) continue;

                _tabs.TabPages.Remove(page);
                foreach (Control c in page.Controls) c.Dispose();
                page.Dispose();
                break;
            }

            if (_tabs.TabPages.Count == 0)
            {
                _tabs.Visible = false;
                _emptyLabel.Visible = true;
            }
        }

        private void ClearTabs()
        {
            foreach (TabPage page in _tabs.TabPages)
            {
                foreach (Control c in page.Controls) c.Dispose();
            }
            _tabs.TabPages.Clear();
            _tabs.Visible = false;
            _emptyLabel.Visible = true;
        }

        private void ShowTabs()
        {
            _emptyLabel.Visible = false;
            _tabs.Visible = true;
        }

        private static TabPage BuildGridTab(string title, Control content)
        {
            content.Dock = DockStyle.Fill;
            var tab = new TabPage(title) { UseVisualStyleBackColor = true };
            tab.Controls.Add(content);
            return tab;
        }

        private static DataGridView BuildDataGrid<T>(List<T> rows)
        {
            var grid = NewGrid();
            grid.AutoGenerateColumns = true;
            grid.DataSource = rows;
            return grid;
        }

        private static DataGridView BuildEntityPropertyGrid(EntityDocumentation entity)
        {
            var rows = new List<PropertyRow>
            {
                new PropertyRow("Display name", entity.DisplayName),
                new PropertyRow("Logical name", entity.LogicalName),
                new PropertyRow("Schema name", entity.SchemaName),
                new PropertyRow("Description", entity.Description),
                new PropertyRow("Ownership", entity.OwnershipType),
                new PropertyRow("Primary id attribute", entity.PrimaryIdAttribute),
                new PropertyRow("Primary name attribute", entity.PrimaryNameAttribute),
                new PropertyRow("Custom entity", entity.IsCustomEntity ? "Yes" : "No"),
                new PropertyRow("Intersect (N:N)", entity.IsIntersect ? "Yes" : "No"),
                new PropertyRow("Attributes loaded", entity.Attributes.Count.ToString())
            };

            var grid = NewGrid();
            grid.AutoGenerateColumns = false;
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Property",
                DataPropertyName = nameof(PropertyRow.Property),
                Width = 170
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Value",
                DataPropertyName = nameof(PropertyRow.Value),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            grid.ColumnHeadersVisible = true;
            grid.DataSource = rows;
            return grid;
        }

        private static DataGridView NewGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None
            };
        }

        // ---- export (checked entities only) -----------------------------------

        private DictionaryDocument BuildCheckedDocument()
        {
            var document = new DictionaryDocument();
            var seenRelationships = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ListViewItem item in _entityList.CheckedItems)
            {
                var entity = item.Tag as EntityListItem;
                if (entity == null || !_details.TryGetValue(entity.LogicalName, out var detail))
                    continue;

                document.Entities.Add(detail.Entity);
                foreach (var relationship in detail.Relationships)
                {
                    if (seenRelationships.Add(relationship.SchemaName ?? Guid.NewGuid().ToString()))
                        document.Relationships.Add(relationship);
                }
            }

            return document;
        }

        private void UpdateExportEnabled()
        {
            _exportButton.Enabled = _entityList.CheckedItems.Count > 0 && _details.Count > 0;
        }

        private void ExportCsv_Click(object sender, EventArgs e)
        {
            if (!TryPickFolder(out var folder)) return;
            DictionaryExporter.ExportCsv(BuildCheckedDocument(), folder);
            OpenFolder(folder);
        }

        private void ExportMarkdown_Click(object sender, EventArgs e)
        {
            if (!TryPickFile("Markdown files (*.md)|*.md", "data-dictionary.md", out var path)) return;
            DictionaryExporter.ExportMarkdown(BuildCheckedDocument(), path);
            OpenFolder(Path.GetDirectoryName(path));
        }

        private void ExportMermaid_Click(object sender, EventArgs e)
        {
            if (!TryPickFile("Mermaid files (*.mmd)|*.mmd|Markdown files (*.md)|*.md", "erd.mmd", out var path)) return;
            DictionaryExporter.ExportMermaidErd(BuildCheckedDocument(), path);
            OpenFolder(Path.GetDirectoryName(path));
        }

        private static bool TryPickFolder(out string folder)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose a folder for CSV export";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    folder = dialog.SelectedPath;
                    return true;
                }
            }

            folder = null;
            return false;
        }

        private static bool TryPickFile(string filter, string fileName, out string path)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = filter;
                dialog.FileName = fileName;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    path = dialog.FileName;
                    return true;
                }
            }

            path = null;
            return false;
        }

        private static void OpenFolder(string folder)
        {
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                Process.Start("explorer.exe", folder);
        }

        private void SendMessage(string message)
        {
            SendMessageToStatusBar?.Invoke(this, new StatusBarMessageEventArgs(message));
        }

        private sealed class PropertyRow
        {
            public PropertyRow(string property, string value)
            {
                Property = property;
                Value = value;
            }

            public string Property { get; }
            public string Value { get; }
        }
    }
}
