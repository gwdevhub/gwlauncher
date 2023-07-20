using System.Windows.Forms.VisualStyles;

namespace GW_Launcher.Forms;

public partial class ModManagerForm : Form
{
    private readonly Account _account;

    public ModManagerForm(Account account)
    {
        _account = account;

        InitializeComponent();

        Text = $@"Mod Manager for {_account.Name}";
    }

    private void RefreshUI()
    {
        listViewAvailableMods.Items.Clear();

        _account.mods.Sort((a, b) => string.CompareOrdinal(a.fileName, b.fileName));
        foreach (var mod in _account.mods)
        {
            var name = Path.GetFileName(mod.fileName);
            var path = Path.GetDirectoryName(mod.fileName);
            var item = new ListViewItem(new[]
            {
                name,
                path ?? string.Empty
            }, mod.fileName)
            {
                Checked = mod.active
            };

            switch (mod.type)
            {
                case ModType.kModTypeTexmod:
                    listViewAvailableMods.Groups[0].Items.Add(item);
                    break;

                case ModType.kModTypeDLL:
                    listViewAvailableMods.Groups[1].Items.Add(item);
                    break;
            }

            listViewAvailableMods.Items.Add(item);
        }
    }

    private void ModManager_Load(object sender, EventArgs e)
    {
        RefreshUI();
    }

    private void ListViewAvailableMods_ItemChecked(object sender, ItemCheckedEventArgs e)
    {
        Program.mutex.WaitOne();
        var mod = _account.mods[e.Item.Index];
        mod.active = e.Item.Checked;
        Refresh();
        Program.accounts.Save();
        Program.mutex.ReleaseMutex();
    }

    private void ToolStripMenuItemAddMod_Click(object sender, EventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = @"Select mod files to use",
            Filter = @"Mod files (*.dll;*.zip;*.tpf)|",
            Multiselect = true
        };

        if (openFileDialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        if (!Program.mutex.WaitOne(10000))
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

        Program.accounts.Save();
        RefreshUI();

        Program.mutex.ReleaseMutex();
    }

    private void ToolStripMenuItemRemoveSelected_Click(object sender, EventArgs e)
    {
        if (!Program.mutex.WaitOne(10000))
        {
            return;
        }

        var list = listViewAvailableMods.SelectedIndices.Cast<int>().ToList().OrderByDescending(i => i);
        foreach (var index in list)
        {
            _account.mods.RemoveAt(index);
        }

        Program.accounts.Save();
        RefreshUI();
        Program.mutex.ReleaseMutex();
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
            item.Checked = value;
        }

        listViewAvailableMods.Invalidate();
    }
}
