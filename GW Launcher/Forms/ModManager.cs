namespace GW_Launcher.Forms;

public partial class ModManager : Form
{
    private readonly Account _account;

    public ModManager(Account account)
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

    private void listViewAvailableMods_ItemChecked(object sender, ItemCheckedEventArgs e)
    {
        var mod = _account.mods[e.Item.Index];
        mod.active = e.Item.Checked;
        Program.accounts.Save();
    }

    private void ToolStripMenuItemAddMod_Click(object sender, EventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = @"Select Mod File to Use",
            Filter = @"Mod Files (*.dll;*.zip;*.tpf)|*.dll;*.zip;*.tpf|All files (*.*)|*.*",
            Multiselect = true
        };

        if (openFileDialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        foreach (var fileName in openFileDialog.FileNames)
        {
            if (_account.mods.Any(m => Path.GetFileName(m.fileName) == Path.GetFileName(fileName)))
            {
                continue;
            }

            var mod = new Mod
            {
                fileName = fileName,
                active = false
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
    }

    private void ToolStripMenuItemRemoveSelected_Click(object sender, EventArgs e)
    {
        Program.mutex.WaitOne();
        var list = listViewAvailableMods.SelectedIndices.Cast<int>().ToList().OrderByDescending(i => i);
        foreach (var index in list)
        {
            _account.mods.RemoveAt(index);
        }

        Program.accounts.Save();
        RefreshUI();
        Program.mutex.ReleaseMutex();
    }
}
