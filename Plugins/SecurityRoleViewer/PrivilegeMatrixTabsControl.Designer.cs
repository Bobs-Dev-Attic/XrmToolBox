namespace SecurityRoleViewer
{
    partial class PrivilegeMatrixTabsControl
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
            this.tsFilters = new System.Windows.Forms.ToolStrip();
            this.toolStripLabelFilters = new System.Windows.Forms.ToolStripLabel();
            this.toolStripLabelEntity = new System.Windows.Forms.ToolStripLabel();
            this.tstEntitySearch = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.tsddLevels = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsddColumns = new System.Windows.Forms.ToolStripDropDownButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbEntityLabel = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.tsddCompare = new System.Windows.Forms.ToolStripDropDownButton();
            this.tabs = new System.Windows.Forms.TabControl();
            this.lblEmpty = new System.Windows.Forms.Label();

            this.tsFilters.SuspendLayout();
            this.SuspendLayout();

            // tsFilters
            this.tsFilters.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.toolStripLabelFilters,
                this.toolStripLabelEntity,
                this.tstEntitySearch,
                this.toolStripSeparator1,
                this.tsddLevels,
                this.tsddColumns,
                this.toolStripSeparator2,
                this.tsbEntityLabel,
                this.toolStripSeparator3,
                this.tsddCompare
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
            this.tsddColumns.Text = "Levels";
            this.tsddColumns.ShowDropDownArrow = true;

            // tsbEntityLabel
            this.tsbEntityLabel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbEntityLabel.Name = "tsbEntityLabel";
            this.tsbEntityLabel.Text = "Logical Names";
            this.tsbEntityLabel.CheckOnClick = true;
            this.tsbEntityLabel.ToolTipText = "Show entity logical names instead of display names";
            this.tsbEntityLabel.Click += new System.EventHandler(this.tsbEntityLabel_Click);

            // tsddCompare (hidden until 2+ tabs are checked)
            this.tsddCompare.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsddCompare.Name = "tsddCompare";
            this.tsddCompare.Text = "Compare";
            this.tsddCompare.ShowDropDownArrow = true;
            this.tsddCompare.Visible = false;
            this.tsddCompare.ToolTipText = "Compare the checked tabs";

            // tabs
            this.tabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabs.Name = "tabs";
            this.tabs.Visible = false;
            this.tabs.DrawMode = System.Windows.Forms.TabDrawMode.OwnerDrawFixed;
            this.tabs.Padding = new System.Drawing.Point(18, 3);
            this.tabs.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.tabs_DrawItem);
            this.tabs.MouseDown += new System.Windows.Forms.MouseEventHandler(this.tabs_MouseDown);

            // lblEmpty
            this.lblEmpty.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblEmpty.Name = "lblEmpty";
            this.lblEmpty.Text = "Check one or more items to view privileges";
            this.lblEmpty.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblEmpty.ForeColor = System.Drawing.Color.Gray;
            this.lblEmpty.Font = new System.Drawing.Font("Segoe UI", 10F);

            // PrivilegeMatrixTabsControl
            this.Controls.Add(this.tabs);
            this.Controls.Add(this.lblEmpty);
            this.Controls.Add(this.tsFilters);
            this.Name = "PrivilegeMatrixTabsControl";
            this.Size = new System.Drawing.Size(700, 550);

            this.tsFilters.ResumeLayout(false);
            this.tsFilters.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.ToolStrip tsFilters;
        private System.Windows.Forms.ToolStripLabel toolStripLabelFilters;
        private System.Windows.Forms.ToolStripLabel toolStripLabelEntity;
        private System.Windows.Forms.ToolStripTextBox tstEntitySearch;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripDropDownButton tsddLevels;
        private System.Windows.Forms.ToolStripDropDownButton tsddColumns;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripButton tsbEntityLabel;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripDropDownButton tsddCompare;
        private System.Windows.Forms.TabControl tabs;
        private System.Windows.Forms.Label lblEmpty;
    }
}
