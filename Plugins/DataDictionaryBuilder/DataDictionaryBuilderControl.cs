using DataDictionaryBuilder.Export;
using DataDictionaryBuilder.Models;
using DataDictionaryBuilder.Services;
using System;
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
        private readonly ToolStripButton _loadButton = new ToolStripButton("Load Metadata");
        private readonly ToolStripDropDownButton _exportButton = new ToolStripDropDownButton("Export");
        private readonly CheckBox _customOnlyCheckBox = new CheckBox();
        private readonly CheckBox _systemAttributesCheckBox = new CheckBox();
        private readonly Label _summaryLabel = new Label();
        private readonly DataGridView _entitiesGrid = new DataGridView();
        private readonly DataGridView _attributesGrid = new DataGridView();
        private readonly DataGridView _relationshipsGrid = new DataGridView();
        private DictionaryDocument _document;

        public event EventHandler<StatusBarMessageEventArgs> SendMessageToStatusBar;

        public string RepositoryName => "XrmToolBox";
        public string UserName => "MscrmTools";
        public string HelpUrl => "https://github.com/MscrmTools/XrmToolBox";

        public DataDictionaryBuilderControl()
        {
            InitializeUi();
        }

        private void InitializeUi()
        {
            _toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            _loadButton.Click += LoadButton_Click;
            _exportButton.Enabled = false;
            _exportButton.DropDownItems.Add("CSV files", null, ExportCsv_Click);
            _exportButton.DropDownItems.Add("Markdown", null, ExportMarkdown_Click);
            _exportButton.DropDownItems.Add("Mermaid ERD", null, ExportMermaid_Click);
            _toolStrip.Items.Add(_loadButton);
            _toolStrip.Items.Add(new ToolStripSeparator());
            _toolStrip.Items.Add(_exportButton);

            _customOnlyCheckBox.Text = "Custom entities only";
            _customOnlyCheckBox.Checked = true;
            _customOnlyCheckBox.AutoSize = true;
            _customOnlyCheckBox.Margin = new Padding(12, 8, 8, 4);

            _systemAttributesCheckBox.Text = "Include system attributes";
            _systemAttributesCheckBox.AutoSize = true;
            _systemAttributesCheckBox.Margin = new Padding(12, 8, 8, 4);

            _summaryLabel.AutoSize = true;
            _summaryLabel.Margin = new Padding(12, 9, 8, 4);
            _summaryLabel.Text = "Load metadata to build a data dictionary.";

            var optionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight
            };
            optionsPanel.Controls.Add(_customOnlyCheckBox);
            optionsPanel.Controls.Add(_systemAttributesCheckBox);
            optionsPanel.Controls.Add(_summaryLabel);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(CreateTab("Entities", _entitiesGrid));
            tabs.TabPages.Add(CreateTab("Attributes", _attributesGrid));
            tabs.TabPages.Add(CreateTab("Relationships", _relationshipsGrid));

            Controls.Add(tabs);
            Controls.Add(optionsPanel);
            Controls.Add(_toolStrip);
        }

        private static TabPage CreateTab(string title, DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            grid.BackgroundColor = SystemColors.Window;

            var tab = new TabPage(title);
            tab.Controls.Add(grid);
            return tab;
        }

        private void LoadButton_Click(object sender, EventArgs e)
        {
            ExecuteMethod(LoadMetadata);
        }

        private void LoadMetadata()
        {
            ToggleCommands(false);
            SendMessage("Loading Dataverse metadata...");

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading metadata...",
                Work = (worker, args) =>
                {
                    var service = new MetadataDocumentationService(Service);
                    args.Result = service.Build(_customOnlyCheckBox.Checked, _systemAttributesCheckBox.Checked);
                },
                PostWorkCallBack = args =>
                {
                    ToggleCommands(true);

                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Unable to build data dictionary");
                        SendMessage("Metadata load failed.");
                        return;
                    }

                    _document = (DictionaryDocument)args.Result;
                    BindDocument();
                    SendMessage("Metadata loaded.");
                }
            });
        }

        private void BindDocument()
        {
            _entitiesGrid.DataSource = null;
            _attributesGrid.DataSource = null;
            _relationshipsGrid.DataSource = null;

            _entitiesGrid.DataSource = _document.Entities;
            _attributesGrid.DataSource = _document.Entities.Count == 0
                ? null
                : new BindingSource { DataSource = _document.Entities.SelectMany(e => e.Attributes).ToList() };
            _relationshipsGrid.DataSource = _document.Relationships;

            _summaryLabel.Text = $"{_document.Entities.Count} entities, {_document.Entities.SelectMany(e => e.Attributes).Count()} attributes, {_document.Relationships.Count} relationships";
            _exportButton.Enabled = true;
        }

        private void ExportCsv_Click(object sender, EventArgs e)
        {
            if (!TryPickFolder(out var folder))
                return;

            DictionaryExporter.ExportCsv(_document, folder);
            OpenFolder(folder);
        }

        private void ExportMarkdown_Click(object sender, EventArgs e)
        {
            if (!TryPickFile("Markdown files (*.md)|*.md", "data-dictionary.md", out var path))
                return;

            DictionaryExporter.ExportMarkdown(_document, path);
            OpenFolder(Path.GetDirectoryName(path));
        }

        private void ExportMermaid_Click(object sender, EventArgs e)
        {
            if (!TryPickFile("Mermaid files (*.mmd)|*.mmd|Markdown files (*.md)|*.md", "erd.mmd", out var path))
                return;

            DictionaryExporter.ExportMermaidErd(_document, path);
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

        private void ToggleCommands(bool enabled)
        {
            _loadButton.Enabled = enabled;
            _customOnlyCheckBox.Enabled = enabled;
            _systemAttributesCheckBox.Enabled = enabled;
            _exportButton.Enabled = enabled && _document != null;
        }

        private void SendMessage(string message)
        {
            SendMessageToStatusBar?.Invoke(this, new StatusBarMessageEventArgs(message));
        }
    }
}
