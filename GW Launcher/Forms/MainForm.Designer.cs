﻿using GW_Launcher.Properties;

namespace GW_Launcher.Forms;

partial class MainForm
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private IContainer components = null;

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
        components = new Container();
        ComponentResourceManager resources = new ComponentResourceManager(typeof(MainForm));
        listViewAccounts = new ListView();
        columnHeaderName = new ColumnHeader();
        columnHeaderStatus = new ColumnHeader();
        contextMenuStripAccounts = new ContextMenuStrip(components);
        toolStripMenuItemRefreshAccounts = new ToolStripMenuItem();
        toolStripSeparator3 = new ToolStripSeparator();
        toolStripMenuItemAddNew = new ToolStripMenuItem();
        toolStripMenuItemEditSelected = new ToolStripMenuItem();
        toolStripMenuItemRemoveSelected = new ToolStripMenuItem();
        toolStripMenuItemLaunchSelected = new ToolStripMenuItem();
        toolStripSeparator1 = new ToolStripSeparator();
        toolStripMenuItemLaunchGWInstance = new ToolStripMenuItem();
        toolStripMenuItemUpdateAllClients = new ToolStripMenuItem();
        notifyIcon = new NotifyIcon(components);
        contextMenuStripAccounts.SuspendLayout();
        SuspendLayout();
        // 
        // listViewAccounts
        // 
        listViewAccounts.BackColor = SystemColors.Window;
        listViewAccounts.Columns.AddRange(new ColumnHeader[] { columnHeaderName, columnHeaderStatus });
        listViewAccounts.ContextMenuStrip = contextMenuStripAccounts;
        listViewAccounts.Dock = DockStyle.Fill;
        listViewAccounts.FullRowSelect = true;
        listViewAccounts.Location = new Point(0, 0);
        listViewAccounts.Margin = new Padding(4, 3, 4, 3);
        listViewAccounts.Name = "listViewAccounts";
        listViewAccounts.Size = new Size(204, 312);
        listViewAccounts.TabIndex = 0;
        listViewAccounts.UseCompatibleStateImageBehavior = false;
        listViewAccounts.View = View.Details;
        listViewAccounts.MouseDoubleClick += ListViewAccounts_MouseDoubleClick;
        // 
        // columnHeaderName
        // 
        columnHeaderName.Text = "Name";
        columnHeaderName.Width = 140;
        // 
        // columnHeaderStatus
        // 
        columnHeaderStatus.Text = "Status";
        // 
        // contextMenuStripAccounts
        // 
        contextMenuStripAccounts.Items.AddRange(new ToolStripItem[] { toolStripMenuItemRefreshAccounts, toolStripSeparator3, toolStripMenuItemAddNew, toolStripMenuItemEditSelected, toolStripMenuItemRemoveSelected, toolStripMenuItemLaunchSelected, toolStripSeparator1, toolStripMenuItemLaunchGWInstance, toolStripMenuItemUpdateAllClients });
        contextMenuStripAccounts.Name = "contextMenuStripAccounts";
        contextMenuStripAccounts.Size = new Size(211, 170);
        contextMenuStripAccounts.Text = "Options.";
        // 
        // toolStripMenuItemRefreshAccounts
        // 
        toolStripMenuItemRefreshAccounts.Name = "toolStripMenuItemRefreshAccounts";
        toolStripMenuItemRefreshAccounts.Size = new Size(210, 22);
        toolStripMenuItemRefreshAccounts.Text = "Refresh Accounts";
        toolStripMenuItemRefreshAccounts.Click += ToolStripMenuItemRefreshAccounts_Click;
        // 
        // toolStripSeparator3
        // 
        toolStripSeparator3.Name = "toolStripSeparator3";
        toolStripSeparator3.Size = new Size(207, 6);
        // 
        // toolStripMenuItemAddNew
        // 
        toolStripMenuItemAddNew.Name = "toolStripMenuItemAddNew";
        toolStripMenuItemAddNew.Size = new Size(210, 22);
        toolStripMenuItemAddNew.Text = "Add New";
        toolStripMenuItemAddNew.Click += ToolStripMenuItemAddNew_Click;
        // 
        // toolStripMenuItemEditSelected
        // 
        toolStripMenuItemEditSelected.Name = "toolStripMenuItemEditSelected";
        toolStripMenuItemEditSelected.Size = new Size(210, 22);
        toolStripMenuItemEditSelected.Text = "Edit Selected";
        toolStripMenuItemEditSelected.Click += ToolStripMenuItemEditSelected_Click;
        // 
        // toolStripMenuItemRemoveSelected
        // 
        toolStripMenuItemRemoveSelected.Name = "toolStripMenuItemRemoveSelected";
        toolStripMenuItemRemoveSelected.Size = new Size(210, 22);
        toolStripMenuItemRemoveSelected.Text = "Remove Selected";
        toolStripMenuItemRemoveSelected.Click += ToolStripMenuItemRemoveSelected_Click;
        // 
        // toolStripMenuItemLaunchSelected
        // 
        toolStripMenuItemLaunchSelected.Name = "toolStripMenuItemLaunchSelected";
        toolStripMenuItemLaunchSelected.Size = new Size(210, 22);
        toolStripMenuItemLaunchSelected.Text = "Launch Selected";
        toolStripMenuItemLaunchSelected.Click += ToolStripMenuItemLaunchSelected_Click;
        // 
        // toolStripSeparator1
        // 
        toolStripSeparator1.Name = "toolStripSeparator1";
        toolStripSeparator1.Size = new Size(207, 6);
        // 
        // toolStripMenuItemLaunchGWInstance
        // 
        toolStripMenuItemLaunchGWInstance.Name = "toolStripMenuItemLaunchGWInstance";
        toolStripMenuItemLaunchGWInstance.Size = new Size(210, 22);
        toolStripMenuItemLaunchGWInstance.Text = "Launch Default GW Client";
        toolStripMenuItemLaunchGWInstance.Click += ToolStripMenuItemLaunchGWInstance_Click;
        // 
        // toolStripMenuItemUpdateAllClients
        // 
        toolStripMenuItemUpdateAllClients.Name = "toolStripMenuItemUpdateAllClients";
        toolStripMenuItemUpdateAllClients.Size = new Size(210, 22);
        toolStripMenuItemUpdateAllClients.Text = "Update All Clients";
        toolStripMenuItemUpdateAllClients.Click += ToolStripMenuItemUpdateAllClients_Click;
        // 
        // notifyIcon
        // 
        notifyIcon.Text = "GW Launcher";
        notifyIcon.Visible = true;
        notifyIcon.MouseClick += NotifyIcon_MouseClick;
        notifyIcon.Icon = (Icon)Resources.gwlauncher_ico;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(204, 312);
        Controls.Add(listViewAccounts);
        Icon = (Icon)Resources.gwlauncher_ico;
        Margin = new Padding(4, 3, 4, 3);
        MaximizeBox = false;
        MinimizeBox = false;
        MinimumSize = new Size(220, 351);
        Name = "MainForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "GW Launcher";
        TopMost = true;
        Deactivate += MainForm_Deactivate;
        Load += MainForm_Load;
        contextMenuStripAccounts.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private ListView listViewAccounts;
    private ColumnHeader columnHeaderName;
    private ColumnHeader columnHeaderStatus;
    private ContextMenuStrip contextMenuStripAccounts;
    private ToolStripMenuItem toolStripMenuItemAddNew;
    private ToolStripMenuItem toolStripMenuItemRemoveSelected;
    private ToolStripMenuItem toolStripMenuItemLaunchGWInstance;
    private ToolStripMenuItem toolStripMenuItemRefreshAccounts;
    private ToolStripMenuItem toolStripMenuItemEditSelected;
    private ToolStripSeparator toolStripSeparator1;
    private ToolStripMenuItem toolStripMenuItemLaunchSelected;
    private NotifyIcon notifyIcon;
    private ToolStripSeparator toolStripSeparator3;
    private ToolStripMenuItem toolStripMenuItemUpdateAllClients;
}
