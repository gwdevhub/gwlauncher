namespace GW_Launcher
{
    partial class ModManager
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.ListViewGroup listViewGroup1 = new System.Windows.Forms.ListViewGroup("Texmods", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup2 = new System.Windows.Forms.ListViewGroup("DLLs", System.Windows.Forms.HorizontalAlignment.Left);
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addModToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeSelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.columnHeaderName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderFullName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.listViewAvailableMods = new System.Windows.Forms.ListView();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addModToolStripMenuItem,
            this.removeSelectedToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(165, 48);
            // 
            // addModToolStripMenuItem
            // 
            this.addModToolStripMenuItem.Name = "addModToolStripMenuItem";
            this.addModToolStripMenuItem.Size = new System.Drawing.Size(164, 22);
            this.addModToolStripMenuItem.Text = "Add Mod";
            this.addModToolStripMenuItem.Click += new System.EventHandler(this.addModToolStripMenuItem_Click);
            // 
            // removeSelectedToolStripMenuItem
            // 
            this.removeSelectedToolStripMenuItem.Name = "removeSelectedToolStripMenuItem";
            this.removeSelectedToolStripMenuItem.Size = new System.Drawing.Size(164, 22);
            this.removeSelectedToolStripMenuItem.Text = "Remove Selected";
            this.removeSelectedToolStripMenuItem.Click += new System.EventHandler(this.removeSelectedToolStripMenuItem_Click);
            // 
            // columnHeaderName
            // 
            this.columnHeaderName.Text = "Name";
            this.columnHeaderName.Width = 160;
            // 
            // columnHeaderFullName
            // 
            this.columnHeaderFullName.Text = "Full Path";
            this.columnHeaderFullName.Width = -2;
            // 
            // listViewAvailableMods
            // 
            this.listViewAvailableMods.CheckBoxes = true;
            this.listViewAvailableMods.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderName,
            this.columnHeaderFullName});
            this.listViewAvailableMods.ContextMenuStrip = this.contextMenuStrip1;
            this.listViewAvailableMods.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewAvailableMods.FullRowSelect = true;
            listViewGroup1.Header = "Texmods";
            listViewGroup1.Name = "listViewGroupTexMods";
            listViewGroup2.Header = "DLLs";
            listViewGroup2.Name = "listViewGroupDLLs";
            this.listViewAvailableMods.Groups.AddRange(new System.Windows.Forms.ListViewGroup[] {
            listViewGroup1,
            listViewGroup2});
            this.listViewAvailableMods.HideSelection = false;
            this.listViewAvailableMods.Location = new System.Drawing.Point(0, 0);
            this.listViewAvailableMods.Name = "listViewAvailableMods";
            this.listViewAvailableMods.Size = new System.Drawing.Size(393, 304);
            this.listViewAvailableMods.TabIndex = 0;
            this.listViewAvailableMods.UseCompatibleStateImageBehavior = false;
            this.listViewAvailableMods.View = System.Windows.Forms.View.Details;
            this.listViewAvailableMods.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.listViewAvailableMods_ItemChecked);
            this.listViewAvailableMods.SelectedIndexChanged += new System.EventHandler(this.listViewAvailableMods_SelectedIndexChanged);
            // 
            // ModManager
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(393, 304);
            this.Controls.Add(this.listViewAvailableMods);
            this.Name = "ModManager";
            this.Text = "Mod Manager";
            this.Load += new System.EventHandler(this.TexmodManager_Load);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem addModToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeSelectedToolStripMenuItem;
        private System.Windows.Forms.ColumnHeader columnHeaderName;
        private System.Windows.Forms.ColumnHeader columnHeaderFullName;
        private System.Windows.Forms.ListView listViewAvailableMods;
    }
}