namespace GW_Launcher
{
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
            this.columnName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.contextMenuStripAccounts = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.refreshAccountsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.addNewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editSelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeSelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.launchSelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.launchGWInstanceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStripAccounts.SuspendLayout();
            this.SuspendLayout();
            // 
            // listViewAccounts
            // 
            this.listViewAccounts.BackColor = System.Drawing.SystemColors.Window;
            this.listViewAccounts.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnName,
            this.columnStatus});
            this.listViewAccounts.ContextMenuStrip = this.contextMenuStripAccounts;
            this.listViewAccounts.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewAccounts.FullRowSelect = true;
            this.listViewAccounts.Location = new System.Drawing.Point(0, 0);
            this.listViewAccounts.Name = "listViewAccounts";
            this.listViewAccounts.Size = new System.Drawing.Size(184, 270);
            this.listViewAccounts.TabIndex = 0;
            this.listViewAccounts.UseCompatibleStateImageBehavior = false;
            this.listViewAccounts.View = System.Windows.Forms.View.Details;
            this.listViewAccounts.ItemDrag += new System.Windows.Forms.ItemDragEventHandler(this.listViewAccounts_ItemDrag);
            this.listViewAccounts.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listViewAccounts_MouseDoubleClick);
            // 
            // columnName
            // 
            this.columnName.Text = "Name";
            this.columnName.Width = 120;
            // 
            // columnStatus
            // 
            this.columnStatus.Text = "Status";
            // 
            // contextMenuStripAccounts
            // 
            this.contextMenuStripAccounts.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.refreshAccountsToolStripMenuItem,
            this.toolStripSeparator2,
            this.addNewToolStripMenuItem,
            this.editSelectedToolStripMenuItem,
            this.removeSelectedToolStripMenuItem,
            this.launchSelectedToolStripMenuItem,
            this.toolStripSeparator1,
            this.launchGWInstanceToolStripMenuItem});
            this.contextMenuStripAccounts.Name = "contextMenuStripAccounts";
            this.contextMenuStripAccounts.Size = new System.Drawing.Size(211, 148);
            this.contextMenuStripAccounts.Text = "Options.";
            // 
            // refreshAccountsToolStripMenuItem
            // 
            this.refreshAccountsToolStripMenuItem.Name = "refreshAccountsToolStripMenuItem";
            this.refreshAccountsToolStripMenuItem.Size = new System.Drawing.Size(210, 22);
            this.refreshAccountsToolStripMenuItem.Text = "Refresh Accounts";
            this.refreshAccountsToolStripMenuItem.Click += new System.EventHandler(this.refreshAccountsToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(207, 6);
            // 
            // addNewToolStripMenuItem
            // 
            this.addNewToolStripMenuItem.Name = "addNewToolStripMenuItem";
            this.addNewToolStripMenuItem.Size = new System.Drawing.Size(210, 22);
            this.addNewToolStripMenuItem.Text = "Add New";
            this.addNewToolStripMenuItem.Click += new System.EventHandler(this.addNewToolStripMenuItem_Click);
            // 
            // editSelectedToolStripMenuItem
            // 
            this.editSelectedToolStripMenuItem.Name = "editSelectedToolStripMenuItem";
            this.editSelectedToolStripMenuItem.Size = new System.Drawing.Size(210, 22);
            this.editSelectedToolStripMenuItem.Text = "Edit Selected";
            this.editSelectedToolStripMenuItem.Click += new System.EventHandler(this.editSelectedToolStripMenuItem_Click);
            // 
            // removeSelectedToolStripMenuItem
            // 
            this.removeSelectedToolStripMenuItem.Name = "removeSelectedToolStripMenuItem";
            this.removeSelectedToolStripMenuItem.Size = new System.Drawing.Size(210, 22);
            this.removeSelectedToolStripMenuItem.Text = "Remove Selected";
            this.removeSelectedToolStripMenuItem.Click += new System.EventHandler(this.removeSelectedToolStripMenuItem_Click);
            // 
            // launchSelectedToolStripMenuItem
            // 
            this.launchSelectedToolStripMenuItem.Name = "launchSelectedToolStripMenuItem";
            this.launchSelectedToolStripMenuItem.Size = new System.Drawing.Size(210, 22);
            this.launchSelectedToolStripMenuItem.Text = "Launch Selected";
            this.launchSelectedToolStripMenuItem.Click += new System.EventHandler(this.launchSelectedToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(207, 6);
            // 
            // launchGWInstanceToolStripMenuItem
            // 
            this.launchGWInstanceToolStripMenuItem.Name = "launchGWInstanceToolStripMenuItem";
            this.launchGWInstanceToolStripMenuItem.Size = new System.Drawing.Size(210, 22);
            this.launchGWInstanceToolStripMenuItem.Text = "Launch Default GW Client";
            this.launchGWInstanceToolStripMenuItem.Click += new System.EventHandler(this.launchGWInstanceToolStripMenuItem_Click);
            // 
            // notifyIcon
            // 
            this.notifyIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon.Icon")));
            this.notifyIcon.Text = "GW Launcher";
            this.notifyIcon.Visible = true;
            this.notifyIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(this.notifyIcon_MouseClick);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(184, 270);
            this.Controls.Add(this.listViewAccounts);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
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
        private System.Windows.Forms.ColumnHeader columnName;
        private System.Windows.Forms.ColumnHeader columnStatus;
        private System.Windows.Forms.ContextMenuStrip contextMenuStripAccounts;
        private System.Windows.Forms.ToolStripMenuItem addNewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeSelectedToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem launchGWInstanceToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem refreshAccountsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem editSelectedToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem launchSelectedToolStripMenuItem;
        private System.Windows.Forms.NotifyIcon notifyIcon;
    }
}

