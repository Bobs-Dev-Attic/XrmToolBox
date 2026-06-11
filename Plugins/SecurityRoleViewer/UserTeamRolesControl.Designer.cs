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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.lvPrincipals = new System.Windows.Forms.ListView();
            this.colPrincipal = new System.Windows.Forms.ColumnHeader();
            this.tsFilters = new System.Windows.Forms.ToolStrip();
            this.toolStripLabelFilters = new System.Windows.Forms.ToolStripLabel();
            this.toolStripLabelEntity = new System.Windows.Forms.ToolStripLabel();
            this.tstEntitySearch = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.tsddLevels = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsddColumns = new System.Windows.Forms.ToolStripDropDownButton();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbEntityLabel = new System.Windows.Forms.ToolStripButton();
            this.pnlMatrix = new System.Windows.Forms.Panel();
            this.lblEmpty = new System.Windows.Forms.Label();

            this.toolStrip1.SuspendLayout();
            this.tsFilters.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();

            // toolStrip1
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.tsddBusinessUnits,
                this.toolStripSeparator1,
                this.tsbLoadUsers
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

            // splitContainer1
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 25);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Panel1MinSize = 120;
            this.splitContainer1.SplitterDistance = 200;

            // lvPrincipals (Panel1)
            this.lvPrincipals.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lvPrincipals.Name = "lvPrincipals";
            this.lvPrincipals.View = System.Windows.Forms.View.Details;
            this.lvPrincipals.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.lvPrincipals.FullRowSelect = true;
            this.lvPrincipals.MultiSelect = false;
            this.lvPrincipals.HideSelection = false;
            this.lvPrincipals.ShowGroups = true;
            this.lvPrincipals.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
                this.colPrincipal
            });
            this.lvPrincipals.SelectedIndexChanged += new System.EventHandler(this.lvPrincipals_SelectedIndexChanged);
            this.lvPrincipals.Resize += new System.EventHandler(this.lvPrincipals_Resize);
            this.splitContainer1.Panel1.Controls.Add(this.lvPrincipals);

            // colPrincipal
            this.colPrincipal.Text = "Name";
            this.colPrincipal.Width = 180;

            // tsFilters
            this.tsFilters.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.toolStripLabelFilters,
                this.toolStripLabelEntity,
                this.tstEntitySearch,
                this.toolStripSeparator2,
                this.tsddLevels,
                this.tsddColumns,
                this.toolStripSeparator3,
                this.tsbEntityLabel
            });
            this.tsFilters.Dock = System.Windows.Forms.DockStyle.Top;
            this.tsFilters.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.tsFilters.Name = "tsFilters";
            this.tsFilters.BackColor = System.Drawing.SystemColors.Control;

            // toolStripLabelFilters
            this.toolStripLabelFilters.Name = "toolStripLabelFilters";
            this.toolStripLabelFilters.Text = "Filters:";
            this.toolStripLabelFilters.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);

            // toolStripLabelEntity
            this.toolStripLabelEntity.Name = "toolStripLabelEntity";
            this.toolStripLabelEntity.Text = "Entity:";

            // tstEntitySearch
            this.tstEntitySearch.Name = "tstEntitySearch";
            this.tstEntitySearch.Size = new System.Drawing.Size(160, 25);
            this.tstEntitySearch.TextChanged += new System.EventHandler(this.tstEntitySearch_TextChanged);

            // tsddLevels
            this.tsddLevels.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsddLevels.Name = "tsddLevels";
            this.tsddLevels.Text = "Access Levels";
            this.tsddLevels.ShowDropDownArrow = true;

            // tsddColumns
            this.tsddColumns.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsddColumns.Name = "tsddColumns";
            this.tsddColumns.Text = "Levels";
            this.tsddColumns.ShowDropDownArrow = true;

            // tsbEntityLabel
            this.tsbEntityLabel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbEntityLabel.Name = "tsbEntityLabel";
            this.tsbEntityLabel.Text = "Logical Names";
            this.tsbEntityLabel.CheckOnClick = true;
            this.tsbEntityLabel.ToolTipText = "Show entity logical names instead of display names";
            this.tsbEntityLabel.Click += new System.EventHandler(this.tsbEntityLabel_Click);

            // pnlMatrix (Panel2)
            this.pnlMatrix.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlMatrix.Name = "pnlMatrix";

            // lblEmpty (Panel2)
            this.lblEmpty.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblEmpty.Name = "lblEmpty";
            this.lblEmpty.Text = "Load users, then select a user or team to view their effective privileges";
            this.lblEmpty.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblEmpty.ForeColor = System.Drawing.Color.Gray;
            this.lblEmpty.Font = new System.Drawing.Font("Segoe UI", 10F);

            this.splitContainer1.Panel2.Controls.Add(this.pnlMatrix);
            this.splitContainer1.Panel2.Controls.Add(this.lblEmpty);
            this.splitContainer1.Panel2.Controls.Add(this.tsFilters);

            // UserTeamRolesControl
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.Name = "UserTeamRolesControl";
            this.Size = new System.Drawing.Size(900, 550);

            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.tsFilters.ResumeLayout(false);
            this.tsFilters.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripDropDownButton tsddBusinessUnits;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton tsbLoadUsers;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListView lvPrincipals;
        private System.Windows.Forms.ColumnHeader colPrincipal;
        private System.Windows.Forms.ToolStrip tsFilters;
        private System.Windows.Forms.ToolStripLabel toolStripLabelFilters;
        private System.Windows.Forms.ToolStripLabel toolStripLabelEntity;
        private System.Windows.Forms.ToolStripTextBox tstEntitySearch;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripDropDownButton tsddLevels;
        private System.Windows.Forms.ToolStripDropDownButton tsddColumns;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripButton tsbEntityLabel;
        private System.Windows.Forms.Panel pnlMatrix;
        private System.Windows.Forms.Label lblEmpty;
    }
}
