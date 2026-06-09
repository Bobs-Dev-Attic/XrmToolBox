namespace SecurityRoleViewer
{
    partial class SecurityRoleViewerControl
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.tsbLoadRoles = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this.tstSearch = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripLabel2 = new System.Windows.Forms.ToolStripLabel();
            this.tscbFilter = new System.Windows.Forms.ToolStripComboBox();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbExport = new System.Windows.Forms.ToolStripButton();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tvRoles = new System.Windows.Forms.TreeView();
            this.dgvPrivileges = new System.Windows.Forms.DataGridView();
            this.colPrivilege = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAccessLevel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.lblDetailHeader = new System.Windows.Forms.Label();

            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPrivileges)).BeginInit();
            this.SuspendLayout();

            // toolStrip1
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.tsbLoadRoles,
                this.toolStripSeparator1,
                this.toolStripLabel1,
                this.tstSearch,
                this.toolStripSeparator2,
                this.toolStripLabel2,
                this.tscbFilter,
                this.toolStripSeparator3,
                this.tsbExport
            });
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(800, 25);

            // tsbLoadRoles
            this.tsbLoadRoles.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbLoadRoles.Name = "tsbLoadRoles";
            this.tsbLoadRoles.Text = "Load Roles";
            this.tsbLoadRoles.Click += new System.EventHandler(this.tsbLoadRoles_Click);

            // toolStripLabel1
            this.toolStripLabel1.Name = "toolStripLabel1";
            this.toolStripLabel1.Text = "Search:";

            // tstSearch
            this.tstSearch.Name = "tstSearch";
            this.tstSearch.Size = new System.Drawing.Size(180, 25);
            this.tstSearch.TextChanged += new System.EventHandler(this.tstSearch_TextChanged);

            // toolStripLabel2
            this.toolStripLabel2.Name = "toolStripLabel2";
            this.toolStripLabel2.Text = "Filter:";

            // tscbFilter
            this.tscbFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.tscbFilter.Items.AddRange(new object[] {
                "All",
                "Organization",
                "Parent-Child BU",
                "Business Unit",
                "User",
                "None"
            });
            this.tscbFilter.Name = "tscbFilter";
            this.tscbFilter.Size = new System.Drawing.Size(130, 25);
            this.tscbFilter.SelectedIndexChanged += new System.EventHandler(this.tscbFilter_SelectedIndexChanged);

            // tsbExport
            this.tsbExport.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbExport.Enabled = false;
            this.tsbExport.Name = "tsbExport";
            this.tsbExport.Text = "Export CSV";
            this.tsbExport.Click += new System.EventHandler(this.tsbExport_Click);

            // splitContainer1
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 25);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.SplitterDistance = 280;

            // tvRoles (Panel1)
            this.tvRoles.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tvRoles.Name = "tvRoles";
            this.tvRoles.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.tvRoles_AfterSelect);
            this.splitContainer1.Panel1.Controls.Add(this.tvRoles);

            // Panel2 layout
            this.lblDetailHeader = new System.Windows.Forms.Label();
            this.lblDetailHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblDetailHeader.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblDetailHeader.Height = 30;
            this.lblDetailHeader.Name = "lblDetailHeader";
            this.lblDetailHeader.Text = "Select an entity from the tree";
            this.lblDetailHeader.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblDetailHeader.Padding = new System.Windows.Forms.Padding(4, 0, 0, 0);

            // dgvPrivileges
            this.dgvPrivileges.AllowUserToAddRows = false;
            this.dgvPrivileges.AllowUserToDeleteRows = false;
            this.dgvPrivileges.ReadOnly = true;
            this.dgvPrivileges.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvPrivileges.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvPrivileges.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.colPrivilege,
                this.colAccessLevel
            });
            this.dgvPrivileges.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvPrivileges.Name = "dgvPrivileges";
            this.dgvPrivileges.RowHeadersVisible = false;
            this.dgvPrivileges.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvPrivileges.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.dgvPrivileges_CellFormatting);

            // colPrivilege
            this.colPrivilege.HeaderText = "Privilege";
            this.colPrivilege.Name = "colPrivilege";
            this.colPrivilege.FillWeight = 60;

            // colAccessLevel
            this.colAccessLevel.HeaderText = "Access Level";
            this.colAccessLevel.Name = "colAccessLevel";
            this.colAccessLevel.FillWeight = 40;

            this.splitContainer1.Panel2.Controls.Add(this.dgvPrivileges);
            this.splitContainer1.Panel2.Controls.Add(this.lblDetailHeader);

            // SecurityRoleViewerControl
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.Name = "SecurityRoleViewerControl";
            this.Size = new System.Drawing.Size(800, 500);

            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvPrivileges)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton tsbLoadRoles;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripLabel toolStripLabel1;
        private System.Windows.Forms.ToolStripTextBox tstSearch;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripLabel toolStripLabel2;
        private System.Windows.Forms.ToolStripComboBox tscbFilter;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripButton tsbExport;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TreeView tvRoles;
        private System.Windows.Forms.DataGridView dgvPrivileges;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPrivilege;
        private System.Windows.Forms.DataGridViewTextBoxColumn colAccessLevel;
        private System.Windows.Forms.Label lblDetailHeader;
    }
}
