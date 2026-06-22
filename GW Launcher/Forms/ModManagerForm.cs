using System.Windows.Forms.VisualStyles;

namespace GW_Launcher.Forms;

public partial class ModManagerForm : Form
{
    private readonly Account _account;
    private bool _refreshing;

    public ModManagerForm(Account account)
    {
        _account = account;

        InitializeComponent();

        Text = $@"Mod Manager for {_account.Name}";
    }

    private void RefreshUI()
    {
        _refreshing = true;
        listViewAvailableMods.Items.Clear();

        _account.mods.Sort((a, b) => string.CompareOrdinal(a.fileName, b.fileName));
        foreach (var mod in _account.mods)
        {
            var item = new ListViewItem(new[]
            {
                Path.GetFileName(mod.fileName),
                Path.GetDirectoryName(mod.fileName) ?? string.Empty
            }, mod.fileName)
            {
                Checked = mod.active,
                Tag = mod
            };

            AddToTypeGroup(item, mod.type);
            listViewAvailableMods.Items.Add(item);
        }

        foreach (var plugin in ModManager.GetPluginFolderMods(_account))
        {
            var item = new ListViewItem(new[]
            {
                Path.GetFileName(plugin.filePath),
                Path.GetDirectoryName(plugin.filePath) ?? string.Empty
            }, plugin.filePath)
            {
                Checked = true,
                ForeColor = SystemColors.GrayText,
                ToolTipText =
                    $"Auto-loaded from the plugins folder at:\n{plugin.sourceFolder}\n\n" +
                    "This entry is read-only. Remove the file from that folder to stop loading it."
            };

            AddToTypeGroup(item, plugin.type);
            listViewAvailableMods.Items.Add(item);
        }

        _refreshing = false;
    }

    private void AddToTypeGroup(ListViewItem item, ModType type)
    {
        switch (type)
        {
            case ModType.kModTypeTexmod:
                listViewAvailableMods.Groups[0].Items.Add(item);
                break;

            case ModType.kModTypeDLL:
                listViewAvailableMods.Groups[1].Items.Add(item);
                break;
        }
    }

    private void ModManager_Load(object sender, EventArgs e)
    {
        RefreshUI();
    }

    private void ListViewAvailableMods_SizeChanged(object sender, EventArgs e)
    {
        var remaining = listViewAvailableMods.ClientSize.Width - columnHeaderName.Width;
        if (remaining > 0)
        {
            columnHeaderPath.Width = remaining;
        }
    }

    private void ListViewAvailableMods_ItemChecked(object sender, ItemCheckedEventArgs e)
    {
        if (_refreshing)
        {
            return;
        }

        if (e.Item.Tag is not Mod mod)
        {
            // Side-loaded plugin folder rows are read-only and always loaded.
            if (!e.Item.Checked)
            {
                e.Item.Checked = true;
            }

            return;
        }

        Program.Mutex.WaitOne();
        mod.active = e.Item.Checked;
        Refresh();
        Program.Accounts.Save();
        Program.Mutex.ReleaseMutex();
    }

    private void ToolStripMenuItemAddMod_Click(object sender, EventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = @"Select mod files to use",
            Filter = @"Mod files (*.dll;*.zip;*.tpf)|*.dll;*.zip;*.tpf",
            Multiselect = true
        };

        if (openFileDialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        if (!Program.Mutex.WaitOne(10000))
        {
            return;
        }

        foreach (var fileName in openFileDialog.FileNames)
        {
            if (_account.mods.Any(m => Path.GetFullPath(m.fileName) == Path.GetFullPath(fileName)))
            {
                continue;
            }

            var mod = new Mod
            {
                fileName = fileName,
                active = true
            };

            switch (openFileDialog.FileName.Split('.').Last())
            {
                case "dll":
                    mod.type = ModType.kModTypeDLL;
                    break;
                case "zip":
                case "tpf":
                    mod.type = ModType.kModTypeTexmod;
                    break;
            }

            _account.mods.Add(mod);
        }

        Program.Accounts.Save();
        RefreshUI();

        Program.Mutex.ReleaseMutex();
    }

    private void ToolStripMenuItemRemoveSelected_Click(object sender, EventArgs e)
    {
        if (!Program.Mutex.WaitOne(10000))
        {
            return;
        }

        var modsToRemove = listViewAvailableMods.SelectedItems
            .Cast<ListViewItem>()
            .Select(item => item.Tag)
            .OfType<Mod>()
            .ToList();
        foreach (var mod in modsToRemove)
        {
            _account.mods.Remove(mod);
        }

        Program.Accounts.Save();
        RefreshUI();
        Program.Mutex.ReleaseMutex();
    }

    private void ListViewAvailableMods_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
    {
        if (e.ColumnIndex == 0)
        {
            e.DrawBackground();
            var allActive = _account.mods.All(a => a.active);

            CheckBoxRenderer.DrawCheckBox(e.Graphics,
                new Point(e.Bounds.Left + 4, e.Bounds.Top + 4),
                allActive ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal);
        }
        else
        {
            e.DrawDefault = true;
        }
    }

    private void ListViewAvailableMods_DrawItem(object sender, DrawListViewItemEventArgs e)
    {
        e.DrawDefault = true;
    }

    private void ListViewAvailableMods_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
    {
        e.DrawDefault = true;
    }

    private void ListViewAvailableMods_ColumnClick(object sender, ColumnClickEventArgs e)
    {
        if (e.Column != 0)
        {
            return;
        }

        var value = _account.mods.Any(a => a.active == false);

        listViewAvailableMods.Columns[e.Column].Tag = value;
        foreach (ListViewItem item in listViewAvailableMods.Items)
        {
            if (item.Tag is Mod)
            {
                item.Checked = value;
            }
        }

        listViewAvailableMods.Invalidate();
    }
}
