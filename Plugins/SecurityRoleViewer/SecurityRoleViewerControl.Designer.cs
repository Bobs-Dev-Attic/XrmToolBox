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
            this.tsddBusinessUnits = new System.Windows.Forms.ToolStripDropDownButton();
            this.toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbLoadRoles = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this.tstSearch = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbExport = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsbExportCsv = new System.Windows.Forms.ToolStripMenuItem();
            this.tsbExportExcel = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.lvRoles = new System.Windows.Forms.ListView();
            this.colRole = new System.Windows.Forms.ColumnHeader();
            this.matrixPanel = new SecurityRoleViewer.PrivilegeMatrixTabsControl();
            this.tabsTop = new System.Windows.Forms.TabControl();
            this.tabRolePermissions = new System.Windows.Forms.TabPage();
            this.tabUserTeamRoles = new System.Windows.Forms.TabPage();
            this.utrControl = new SecurityRoleViewer.UserTeamRolesControl();

            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabsTop.SuspendLayout();
            this.tabRolePermissions.SuspendLayout();
            this.tabUserTeamRoles.SuspendLayout();
            this.SuspendLayout();

            // toolStrip1
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.tsddBusinessUnits,
                this.toolStripSeparator6,
                this.tsbLoadRoles,
                this.toolStripSeparator1,
                this.toolStripLabel1,
                this.tstSearch,
                this.toolStripSeparator2,
                this.tsbExport
            });
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(900, 25);

            // tsddBusinessUnits
            this.tsddBusinessUnits.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsddBusinessUnits.Name = "tsddBusinessUnits";
            this.tsddBusinessUnits.Text = "Business Units";
            this.tsddBusinessUnits.ShowDropDownArrow = true;
            this.tsddBusinessUnits.Enabled = false;
            this.tsddBusinessUnits.ToolTipText = "Limit Load Roles to the checked business units";

            // tsbLoadRoles
            this.tsbLoadRoles.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbLoadRoles.Name = "tsbLoadRoles";
            this.tsbLoadRoles.Text = "Load Roles";
            this.tsbLoadRoles.Click += new System.EventHandler(this.tsbLoadRoles_Click);

            // toolStripLabel1
            this.toolStripLabel1.Name = "toolStripLabel1";
            this.toolStripLabel1.Text = "Find role:";

            // tstSearch
            this.tstSearch.Name = "tstSearch";
            this.tstSearch.Size = new System.Drawing.Size(150, 25);
            this.tstSearch.TextChanged += new System.EventHandler(this.tstSearch_TextChanged);

            // tsbExport
            this.tsbExport.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbExport.Enabled = false;
            this.tsbExport.Name = "tsbExport";
            this.tsbExport.Text = "Export";
            this.tsbExport.ShowDropDownArrow = true;
            this.tsbExport.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.tsbExportCsv,
                this.tsbExportExcel
            });

            // tsbExportCsv
            this.tsbExportCsv.Name = "tsbExportCsv";
            this.tsbExportCsv.Text = "Export CSV";
            this.tsbExportCsv.Click += new System.EventHandler(this.tsbExportCsv_Click);

            // tsbExportExcel
            this.tsbExportExcel.Name = "tsbExportExcel";
            this.tsbExportExcel.Text = "Export Excel";
            this.tsbExportExcel.Click += new System.EventHandler(this.tsbExportExcel_Click);

            // splitContainer1
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 25);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Panel1MinSize = 120;
            this.splitContainer1.SplitterDistance = 180;

            // lvRoles (Panel1)
            this.lvRoles.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lvRoles.Name = "lvRoles";
            this.lvRoles.CheckBoxes = true;
            this.lvRoles.View = System.Windows.Forms.View.Details;
            this.lvRoles.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.lvRoles.FullRowSelect = true;
            this.lvRoles.MultiSelect = false;
            this.lvRoles.HideSelection = false;
            this.lvRoles.ShowGroups = true;
            this.lvRoles.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
                this.colRole
            });
            this.lvRoles.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.lvRoles_ItemChecked);
            this.lvRoles.Resize += new System.EventHandler(this.lvRoles_Resize);
            this.splitContainer1.Panel1.Controls.Add(this.lvRoles);

            // colRole
            this.colRole.Text = "Role";
            this.colRole.Width = 160;

            // matrixPanel (Panel2)
            this.matrixPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.matrixPanel.Name = "matrixPanel";
            this.splitContainer1.Panel2.Controls.Add(this.matrixPanel);

            // tabRolePermissions
            this.tabRolePermissions.Controls.Add(this.splitContainer1);
            this.tabRolePermissions.Controls.Add(this.toolStrip1);
            this.tabRolePermissions.Name = "tabRolePermissions";
            this.tabRolePermissions.Text = "Role Permissions";
            this.tabRolePermissions.UseVisualStyleBackColor = true;
            this.tabRolePermissions.Padding = new System.Windows.Forms.Padding(3);

            // tabUserTeamRoles
            this.utrControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.utrControl.Name = "utrControl";
            this.tabUserTeamRoles.Controls.Add(this.utrControl);
            this.tabUserTeamRoles.Name = "tabUserTeamRoles";
            this.tabUserTeamRoles.Text = "User/Team Roles";
            this.tabUserTeamRoles.UseVisualStyleBackColor = true;
            this.tabUserTeamRoles.Padding = new System.Windows.Forms.Padding(3);

            // tabsTop
            this.tabsTop.Controls.Add(this.tabRolePermissions);
            this.tabsTop.Controls.Add(this.tabUserTeamRoles);
            this.tabsTop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabsTop.Name = "tabsTop";
            this.tabsTop.SelectedIndex = 0;

            // SecurityRoleViewerControl
            this.Controls.Add(this.tabsTop);
            this.Name = "SecurityRoleViewerControl";
            this.Size = new System.Drawing.Size(900, 550);

            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tabRolePermissions.ResumeLayout(false);
            this.tabRolePermissions.PerformLayout();
            this.tabUserTeamRoles.ResumeLayout(false);
            this.tabsTop.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripDropDownButton tsddBusinessUnits;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator6;
        private System.Windows.Forms.ToolStripButton tsbLoadRoles;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripLabel toolStripLabel1;
        private System.Windows.Forms.ToolStripTextBox tstSearch;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripDropDownButton tsbExport;
        private System.Windows.Forms.ToolStripMenuItem tsbExportCsv;
        private System.Windows.Forms.ToolStripMenuItem tsbExportExcel;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListView lvRoles;
        private System.Windows.Forms.ColumnHeader colRole;
        private SecurityRoleViewer.PrivilegeMatrixTabsControl matrixPanel;
        private System.Windows.Forms.TabControl tabsTop;
        private System.Windows.Forms.TabPage tabRolePermissions;
        private System.Windows.Forms.TabPage tabUserTeamRoles;
        private SecurityRoleViewer.UserTeamRolesControl utrControl;
    }
}
