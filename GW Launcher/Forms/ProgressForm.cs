using System;
using System.Windows.Forms;

namespace GW_Launcher.Forms
{
    public partial class ProgressForm : Form
    {
        public ProgressForm()
        {
            InitializeComponent();
        }

        public void UpdateProgress(string stage, double progress)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string, double>(UpdateProgress), stage, progress);
                return;
            }

            labelStage.Text = stage;
            progressBar.Value = (int)(progress * 100);
        }
    }
}
