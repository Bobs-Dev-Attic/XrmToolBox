namespace SecurityRoleViewer
{
    partial class UserTeamRolesControl
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
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbLoadUsers = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripLabelFind = new System.Windows.Forms.ToolStripLabel();
            this.tstUserSearch = new System.Windows.Forms.ToolStripTextBox();
            this.tsddStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsddLicensed = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsddTeamsFilter = new System.Windows.Forms.ToolStripDropDownButton();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.lvPrincipals = new System.Windows.Forms.ListView();
            this.colPrincipal = new System.Windows.Forms.ColumnHeader();
            this.matrixPanel = new SecurityRoleViewer.PrivilegeMatrixTabsControl();

            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();

            // toolStrip1
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.tsddBusinessUnits,
                this.toolStripSeparator1,
                this.tsbLoadUsers,
                this.toolStripSeparator2,
                this.toolStripLabelFind,
                this.tstUserSearch,
                this.tsddStatus,
                this.tsddLicensed,
                this.tsddTeamsFilter
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
            this.tsddBusinessUnits.ToolTipText = "Limit Load Users to the checked business units";

            // tsbLoadUsers
            this.tsbLoadUsers.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbLoadUsers.Name = "tsbLoadUsers";
            this.tsbLoadUsers.Text = "Load Users";
            this.tsbLoadUsers.Click += new System.EventHandler(this.tsbLoadUsers_Click);

            // toolStripLabelFind
            this.toolStripLabelFind.Name = "toolStripLabelFind";
            this.toolStripLabelFind.Text = "Find:";

            // tstUserSearch
            this.tstUserSearch.Name = "tstUserSearch";
            this.tstUserSearch.Size = new System.Drawing.Size(150, 25);
            this.tstUserSearch.ToolTipText = "Search name, username, or email";
            this.tstUserSearch.TextChanged += new System.EventHandler(this.UserFilterChanged);

            // tsddStatus
            this.tsddStatus.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsddStatus.Name = "tsddStatus";
            this.tsddStatus.Text = "Status";
            this.tsddStatus.ShowDropDownArrow = true;

            // tsddLicensed
            this.tsddLicensed.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsddLicensed.Name = "tsddLicensed";
            this.tsddLicensed.Text = "Licensed";
            this.tsddLicensed.ShowDropDownArrow = true;

            // tsddTeamsFilter
            this.tsddTeamsFilter.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsddTeamsFilter.Name = "tsddTeamsFilter";
            this.tsddTeamsFilter.Text = "Teams";
            this.tsddTeamsFilter.ShowDropDownArrow = true;
            this.tsddTeamsFilter.Enabled = false;
            this.tsddTeamsFilter.ToolTipText = "Show only users who belong to the checked teams";

            // splitContainer1
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 25);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Panel1MinSize = 120;
            this.splitContainer1.SplitterDistance = 180;

            // lvPrincipals (Panel1)
            this.lvPrincipals.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lvPrincipals.Name = "lvPrincipals";
            this.lvPrincipals.CheckBoxes = true;
            this.lvPrincipals.View = System.Windows.Forms.View.Details;
            this.lvPrincipals.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.lvPrincipals.FullRowSelect = true;
            this.lvPrincipals.MultiSelect = false;
            this.lvPrincipals.HideSelection = false;
            this.lvPrincipals.ShowGroups = true;
            this.lvPrincipals.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
                this.colPrincipal
            });
            this.lvPrincipals.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.lvPrincipals_ItemChecked);
            this.lvPrincipals.Resize += new System.EventHandler(this.lvPrincipals_Resize);
            this.splitContainer1.Panel1.Controls.Add(this.lvPrincipals);

            // colPrincipal
            this.colPrincipal.Text = "Name";
            this.colPrincipal.Width = 180;

            // matrixPanel (Panel2)
            this.matrixPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.matrixPanel.Name = "matrixPanel";
            this.splitContainer1.Panel2.Controls.Add(this.matrixPanel);

            // UserTeamRolesControl
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.Name = "UserTeamRolesControl";
            this.Size = new System.Drawing.Size(900, 550);

            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripDropDownButton tsddBusinessUnits;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton tsbLoadUsers;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripLabel toolStripLabelFind;
        private System.Windows.Forms.ToolStripTextBox tstUserSearch;
        private System.Windows.Forms.ToolStripDropDownButton tsddStatus;
        private System.Windows.Forms.ToolStripDropDownButton tsddLicensed;
        private System.Windows.Forms.ToolStripDropDownButton tsddTeamsFilter;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListView lvPrincipals;
        private System.Windows.Forms.ColumnHeader colPrincipal;
        private SecurityRoleViewer.PrivilegeMatrixTabsControl matrixPanel;
    }
}
