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
            this.addNewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeSelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.launchGWInstanceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.addNewToolStripMenuItem,
            this.removeSelectedToolStripMenuItem,
            this.launchGWInstanceToolStripMenuItem});
            this.contextMenuStripAccounts.Name = "contextMenuStripAccounts";
            this.contextMenuStripAccounts.Size = new System.Drawing.Size(198, 70);
            this.contextMenuStripAccounts.Text = "Options.";
            // 
            // addNewToolStripMenuItem
            // 
            this.addNewToolStripMenuItem.Name = "addNewToolStripMenuItem";
            this.addNewToolStripMenuItem.Size = new System.Drawing.Size(197, 22);
            this.addNewToolStripMenuItem.Text = "Add New";
            this.addNewToolStripMenuItem.Click += new System.EventHandler(this.addNewToolStripMenuItem_Click);
            // 
            // removeSelectedToolStripMenuItem
            // 
            this.removeSelectedToolStripMenuItem.Name = "removeSelectedToolStripMenuItem";
            this.removeSelectedToolStripMenuItem.Size = new System.Drawing.Size(197, 22);
            this.removeSelectedToolStripMenuItem.Text = "Remove Selected";
            this.removeSelectedToolStripMenuItem.Click += new System.EventHandler(this.removeSelectedToolStripMenuItem_Click);
            // 
            // launchGWInstanceToolStripMenuItem
            // 
            this.launchGWInstanceToolStripMenuItem.Name = "launchGWInstanceToolStripMenuItem";
            this.launchGWInstanceToolStripMenuItem.Size = new System.Drawing.Size(197, 22);
            this.launchGWInstanceToolStripMenuItem.Text = "Launch No-Account GW";
            this.launchGWInstanceToolStripMenuItem.Click += new System.EventHandler(this.launchGWInstanceToolStripMenuItem_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(184, 270);
            this.Controls.Add(this.listViewAccounts);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MainForm";
            this.Text = "GW Launcher";
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
    }
}

