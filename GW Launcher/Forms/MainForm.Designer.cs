namespace GW_Launcher.Forms;

partial class MainForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.listViewAccounts = new System.Windows.Forms.ListView();
            this.columnHeaderName = new System.Windows.Forms.ColumnHeader();
            this.columnHeaderStatus = new System.Windows.Forms.ColumnHeader();
            this.contextMenuStripAccounts = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItemRefreshAccounts = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMenuItemAddNew = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemEditSelected = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemRemoveSelected = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemLaunchSelected = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMenuItemLaunchGWInstance = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemUpdateAllClients = new System.Windows.Forms.ToolStripMenuItem();
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStripAccounts.SuspendLayout();
            this.SuspendLayout();
            // 
            // listViewAccounts
            // 
            this.listViewAccounts.BackColor = System.Drawing.SystemColors.Window;
            this.listViewAccounts.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderName,
            this.columnHeaderStatus});
            this.listViewAccounts.ContextMenuStrip = this.contextMenuStripAccounts;
            this.listViewAccounts.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewAccounts.FullRowSelect = true;
            this.listViewAccounts.Location = new System.Drawing.Point(0, 0);
            this.listViewAccounts.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.listViewAccounts.Name = "listViewAccounts";
            this.listViewAccounts.Size = new System.Drawing.Size(204, 312);
            this.listViewAccounts.TabIndex = 0;
            this.listViewAccounts.UseCompatibleStateImageBehavior = false;
            this.listViewAccounts.View = System.Windows.Forms.View.Details;
            this.listViewAccounts.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.ListViewAccounts_MouseDoubleClick);
            // 
            // columnHeaderName
            // 
            this.columnHeaderName.Text = "Name";
            this.columnHeaderName.Width = 140;
            // 
            // columnHeaderStatus
            // 
            this.columnHeaderStatus.Text = "Status";
            // 
            // contextMenuStripAccounts
            // 
            this.contextMenuStripAccounts.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemRefreshAccounts,
            this.toolStripSeparator3,
            this.toolStripMenuItemAddNew,
            this.toolStripMenuItemEditSelected,
            this.toolStripMenuItemRemoveSelected,
            this.toolStripMenuItemLaunchSelected,
            this.toolStripSeparator1,
            this.toolStripMenuItemLaunchGWInstance,
            this.toolStripMenuItemUpdateAllClients});
            this.contextMenuStripAccounts.Name = "contextMenuStripAccounts";
            this.contextMenuStripAccounts.Size = new System.Drawing.Size(211, 170);
            this.contextMenuStripAccounts.Text = "Options.";
            // 
            // toolStripMenuItemRefreshAccounts
            // 
            this.toolStripMenuItemRefreshAccounts.Name = "toolStripMenuItemRefreshAccounts";
            this.toolStripMenuItemRefreshAccounts.Size = new System.Drawing.Size(210, 22);
            this.toolStripMenuItemRefreshAccounts.Text = "Refresh Accounts";
            this.toolStripMenuItemRefreshAccounts.Click += new System.EventHandler(this.ToolStripMenuItemRefreshAccounts_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(207, 6);
            // 
            // toolStripMenuItemAddNew
            // 
            this.toolStripMenuItemAddNew.Name = "toolStripMenuItemAddNew";
            this.toolStripMenuItemAddNew.Size = new System.Drawing.Size(210, 22);
            this.toolStripMenuItemAddNew.Text = "Add New";
            this.toolStripMenuItemAddNew.Click += new System.EventHandler(this.ToolStripMenuItemAddNew_Click);
            // 
            // toolStripMenuItemEditSelected
            // 
            this.toolStripMenuItemEditSelected.Name = "toolStripMenuItemEditSelected";
            this.toolStripMenuItemEditSelected.Size = new System.Drawing.Size(210, 22);
            this.toolStripMenuItemEditSelected.Text = "Edit Selected";
            this.toolStripMenuItemEditSelected.Click += new System.EventHandler(this.ToolStripMenuItemEditSelected_Click);
            // 
            // toolStripMenuItemRemoveSelected
            // 
            this.toolStripMenuItemRemoveSelected.Name = "toolStripMenuItemRemoveSelected";
            this.toolStripMenuItemRemoveSelected.Size = new System.Drawing.Size(210, 22);
            this.toolStripMenuItemRemoveSelected.Text = "Remove Selected";
            this.toolStripMenuItemRemoveSelected.Click += new System.EventHandler(this.ToolStripMenuItemRemoveSelected_Click);
            // 
            // toolStripMenuItemLaunchSelected
            // 
            this.toolStripMenuItemLaunchSelected.Name = "toolStripMenuItemLaunchSelected";
            this.toolStripMenuItemLaunchSelected.Size = new System.Drawing.Size(210, 22);
            this.toolStripMenuItemLaunchSelected.Text = "Launch Selected";
            this.toolStripMenuItemLaunchSelected.Click += new System.EventHandler(this.ToolStripMenuItemLaunchSelected_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(207, 6);
            // 
            // toolStripMenuItemLaunchGWInstance
            // 
            this.toolStripMenuItemLaunchGWInstance.Name = "toolStripMenuItemLaunchGWInstance";
            this.toolStripMenuItemLaunchGWInstance.Size = new System.Drawing.Size(210, 22);
            this.toolStripMenuItemLaunchGWInstance.Text = "Launch Default GW Client";
            this.toolStripMenuItemLaunchGWInstance.Click += new System.EventHandler(this.ToolStripMenuItemLaunchGWInstance_Click);
            // 
            // toolStripMenuItemUpdateAllClients
            // 
            this.toolStripMenuItemUpdateAllClients.Name = "toolStripMenuItemUpdateAllClients";
            this.toolStripMenuItemUpdateAllClients.Size = new System.Drawing.Size(210, 22);
            this.toolStripMenuItemUpdateAllClients.Text = "Update All Clients";
            this.toolStripMenuItemUpdateAllClients.Click += new System.EventHandler(this.ToolStripMenuItemUpdateAllClients_Click);
            // 
            // notifyIcon
            // 
            this.notifyIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon.Icon")));
            this.notifyIcon.Text = "GW Launcher";
            this.notifyIcon.Visible = true;
            this.notifyIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(this.NotifyIcon_MouseClick);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(204, 312);
            this.Controls.Add(this.listViewAccounts);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(220, 351);
            this.Name = "MainForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "GW Launcher";
            this.TopMost = true;
            this.Deactivate += new System.EventHandler(this.MainForm_Deactivate);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.contextMenuStripAccounts.ResumeLayout(false);
            this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.ListView listViewAccounts;
    private System.Windows.Forms.ColumnHeader columnHeaderName;
    private System.Windows.Forms.ColumnHeader columnHeaderStatus;
    private System.Windows.Forms.ContextMenuStrip contextMenuStripAccounts;
    private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemAddNew;
    private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemRemoveSelected;
    private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemLaunchGWInstance;
    private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemRefreshAccounts;
    private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemEditSelected;
    private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
    private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemLaunchSelected;
    private System.Windows.Forms.NotifyIcon notifyIcon;
    private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
    private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemUpdateAllClients;
}
