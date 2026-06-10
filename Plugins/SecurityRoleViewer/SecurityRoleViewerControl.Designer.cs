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
            this.tsbExport = new System.Windows.Forms.ToolStripButton();
            this.tsFilters = new System.Windows.Forms.ToolStrip();
            this.toolStripLabelFilters = new System.Windows.Forms.ToolStripLabel();
            this.toolStripLabelEntity = new System.Windows.Forms.ToolStripLabel();
            this.tstEntitySearch = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.tsddLevels = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsddColumns = new System.Windows.Forms.ToolStripDropDownButton();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.clbRoles = new System.Windows.Forms.CheckedListBox();
            this.tabRoles = new System.Windows.Forms.TabControl();
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
            this.tsbExport.Text = "Export CSV";
            this.tsbExport.Click += new System.EventHandler(this.tsbExport_Click);

            // tsFilters (filter strip for the selected role tabs)
            this.tsFilters.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.toolStripLabelFilters,
                this.toolStripLabelEntity,
                this.tstEntitySearch,
                this.toolStripSeparator3,
                this.tsddLevels,
                this.tsddColumns
            });
            this.tsFilters.Dock = System.Windows.Forms.DockStyle.Top;
            this.tsFilters.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.tsFilters.Name = "tsFilters";
            this.tsFilters.BackColor = System.Drawing.SystemColors.Control;

            // toolStripLabelFilters
            this.toolStripLabelFilters.Name = "toolStripLabelFilters";
            this.toolStripLabelFilters.Text = "Tab filters:";
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
            this.tsddColumns.Text = "Columns";
            this.tsddColumns.ShowDropDownArrow = true;

            // splitContainer1
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 25);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Panel1MinSize = 120;
            this.splitContainer1.SplitterDistance = 180;

            // clbRoles (Panel1)
            this.clbRoles.Dock = System.Windows.Forms.DockStyle.Fill;
            this.clbRoles.Name = "clbRoles";
            this.clbRoles.CheckOnClick = true;
            this.clbRoles.IntegralHeight = false;
            this.clbRoles.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.clbRoles_ItemCheck);
            this.splitContainer1.Panel1.Controls.Add(this.clbRoles);

            // tabRoles (Panel2)
            this.tabRoles.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabRoles.Name = "tabRoles";
            this.tabRoles.Visible = false;

            // lblEmpty (Panel2)
            this.lblEmpty.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblEmpty.Name = "lblEmpty";
            this.lblEmpty.Text = "Check one or more roles to view privileges";
            this.lblEmpty.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblEmpty.ForeColor = System.Drawing.Color.Gray;
            this.lblEmpty.Font = new System.Drawing.Font("Segoe UI", 10F);

            this.splitContainer1.Panel2.Controls.Add(this.tabRoles);
            this.splitContainer1.Panel2.Controls.Add(this.lblEmpty);
            this.splitContainer1.Panel2.Controls.Add(this.tsFilters);

            // SecurityRoleViewerControl
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.Name = "SecurityRoleViewerControl";
            this.Size = new System.Drawing.Size(900, 550);

            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.tsFilters.ResumeLayout(false);
            this.tsFilters.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton tsbLoadRoles;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripLabel toolStripLabel1;
        private System.Windows.Forms.ToolStripTextBox tstSearch;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripButton tsbExport;
        private System.Windows.Forms.ToolStrip tsFilters;
        private System.Windows.Forms.ToolStripLabel toolStripLabelFilters;
        private System.Windows.Forms.ToolStripLabel toolStripLabelEntity;
        private System.Windows.Forms.ToolStripTextBox tstEntitySearch;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripDropDownButton tsddLevels;
        private System.Windows.Forms.ToolStripDropDownButton tsddColumns;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.CheckedListBox clbRoles;
        private System.Windows.Forms.TabControl tabRoles;
        private System.Windows.Forms.Label lblEmpty;
    }
}
