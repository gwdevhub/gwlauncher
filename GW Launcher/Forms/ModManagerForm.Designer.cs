namespace GW_Launcher.Forms;

partial class ModManagerForm
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
            System.Windows.Forms.ListViewGroup ListViewGroupTextureMod = new System.Windows.Forms.ListViewGroup("Texture Mods", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroupDLL = new System.Windows.Forms.ListViewGroup("DLLs", System.Windows.Forms.HorizontalAlignment.Left);
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItemAddMod = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemRemoveSelected = new System.Windows.Forms.ToolStripMenuItem();
            this.columnHeaderName = new System.Windows.Forms.ColumnHeader();
            this.columnHeaderPath = new System.Windows.Forms.ColumnHeader();
            this.listViewAvailableMods = new System.Windows.Forms.ListView();
            this.contextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // contextMenuStrip
            // 
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemAddMod,
            this.toolStripMenuItemRemoveSelected});
            this.contextMenuStrip.Name = "contextMenuStrip";
            this.contextMenuStrip.Size = new System.Drawing.Size(165, 48);
            // 
            // toolStripMenuItemAddMod
            // 
            this.toolStripMenuItemAddMod.Name = "toolStripMenuItemAddMod";
            this.toolStripMenuItemAddMod.Size = new System.Drawing.Size(164, 22);
            this.toolStripMenuItemAddMod.Text = "Add Mod";
            this.toolStripMenuItemAddMod.Click += new System.EventHandler(this.ToolStripMenuItemAddMod_Click);
            // 
            // toolStripMenuItemRemoveSelected
            // 
            this.toolStripMenuItemRemoveSelected.Name = "toolStripMenuItemRemoveSelected";
            this.toolStripMenuItemRemoveSelected.Size = new System.Drawing.Size(164, 22);
            this.toolStripMenuItemRemoveSelected.Text = "Remove Selected";
            this.toolStripMenuItemRemoveSelected.Click += new System.EventHandler(this.ToolStripMenuItemRemoveSelected_Click);
            // 
            // columnHeaderName
            // 
            this.columnHeaderName.Text = "Name";
            this.columnHeaderName.Width = 160;
            // 
            // columnHeaderPath
            // 
            this.columnHeaderPath.Text = "Full Path";
            this.columnHeaderPath.Width = 229;
            // 
            // listViewAvailableMods
            // 
            this.listViewAvailableMods.CheckBoxes = true;
            this.listViewAvailableMods.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderName,
            this.columnHeaderPath});
            this.listViewAvailableMods.ContextMenuStrip = this.contextMenuStrip;
            this.listViewAvailableMods.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewAvailableMods.FullRowSelect = true;
            ListViewGroupTextureMod.Header = "Texture Mods";
            ListViewGroupTextureMod.Name = "ListViewGroupTextureMod";
            listViewGroupDLL.Header = "DLLs";
            listViewGroupDLL.Name = "listViewGroupDLL";
            this.listViewAvailableMods.Groups.AddRange(new System.Windows.Forms.ListViewGroup[] {
            ListViewGroupTextureMod,
            listViewGroupDLL});
            this.listViewAvailableMods.Location = new System.Drawing.Point(0, 0);
            this.listViewAvailableMods.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.listViewAvailableMods.Name = "listViewAvailableMods";
            this.listViewAvailableMods.OwnerDraw = true;
            this.listViewAvailableMods.Size = new System.Drawing.Size(458, 351);
            this.listViewAvailableMods.TabIndex = 0;
            this.listViewAvailableMods.UseCompatibleStateImageBehavior = false;
            this.listViewAvailableMods.View = System.Windows.Forms.View.Details;
            this.listViewAvailableMods.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.ListViewAvailableMods_ColumnClick);
            this.listViewAvailableMods.DrawColumnHeader += new System.Windows.Forms.DrawListViewColumnHeaderEventHandler(this.ListViewAvailableMods_DrawColumnHeader);
            this.listViewAvailableMods.DrawItem += new System.Windows.Forms.DrawListViewItemEventHandler(this.ListViewAvailableMods_DrawItem);
            this.listViewAvailableMods.DrawSubItem += new System.Windows.Forms.DrawListViewSubItemEventHandler(this.ListViewAvailableMods_DrawSubItem);
            this.listViewAvailableMods.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.ListViewAvailableMods_ItemChecked);
            // 
            // ModManager
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(458, 351);
            this.Controls.Add(this.listViewAvailableMods);
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Name = "ModManager";
            this.Text = "Mod Manager";
            this.Load += new System.EventHandler(this.ModManager_Load);
            this.contextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);

    }

    #endregion
    private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
    private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemAddMod;
    private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemRemoveSelected;
    private System.Windows.Forms.ColumnHeader columnHeaderName;
    private System.Windows.Forms.ColumnHeader columnHeaderPath;
    private System.Windows.Forms.ListView listViewAvailableMods;
}
