namespace GW_Launcher.Forms
{
    partial class AddAccountForm
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
            ComponentResourceManager resources = new ComponentResourceManager(typeof(AddAccountForm));
            labelEmail = new Label();
            textBoxEmail = new TextBox();
            textBoxPassword = new TextBox();
            labelPassword = new Label();
            textBoxCharacter = new TextBox();
            labelCharacter = new Label();
            buttonDone = new Button();
            labelPath = new Label();
            textBoxPath = new TextBox();
            buttonDialogPath = new Button();
            labelExtraArguments = new Label();
            textBoxExtraArguments = new TextBox();
            buttonTogglePasswordVisibility = new Button();
            checkBoxElevated = new CheckBox();
            buttonMods = new Button();
            label1 = new Label();
            textBoxTitle = new TextBox();
            checkBoxUsePluginFolderMods = new CheckBox();
            SuspendLayout();
            // 
            // labelEmail
            // 
            labelEmail.AutoSize = true;
            labelEmail.Location = new Point(23, 45);
            labelEmail.Margin = new Padding(4, 0, 4, 0);
            labelEmail.Name = "labelEmail";
            labelEmail.Size = new Size(39, 15);
            labelEmail.TabIndex = 0;
            labelEmail.Text = "Email:";
            // 
            // textBoxEmail
            // 
            textBoxEmail.Location = new Point(70, 42);
            textBoxEmail.Margin = new Padding(4, 3, 4, 3);
            textBoxEmail.Name = "textBoxEmail";
            textBoxEmail.Size = new Size(203, 23);
            textBoxEmail.TabIndex = 1;
            // 
            // textBoxPassword
            // 
            textBoxPassword.Location = new Point(70, 71);
            textBoxPassword.Margin = new Padding(4, 3, 4, 3);
            textBoxPassword.Name = "textBoxPassword";
            textBoxPassword.PasswordChar = '*';
            textBoxPassword.Size = new Size(168, 23);
            textBoxPassword.TabIndex = 2;
            // 
            // labelPassword
            // 
            labelPassword.AutoSize = true;
            labelPassword.Location = new Point(29, 75);
            labelPassword.Margin = new Padding(4, 0, 4, 0);
            labelPassword.Name = "labelPassword";
            labelPassword.Size = new Size(33, 15);
            labelPassword.TabIndex = 3;
            labelPassword.Text = "Pass:";
            // 
            // textBoxCharacter
            // 
            textBoxCharacter.Location = new Point(70, 100);
            textBoxCharacter.Margin = new Padding(4, 3, 4, 3);
            textBoxCharacter.Name = "textBoxCharacter";
            textBoxCharacter.Size = new Size(203, 23);
            textBoxCharacter.TabIndex = 4;
            // 
            // labelCharacter
            // 
            labelCharacter.AutoSize = true;
            labelCharacter.Location = new Point(27, 103);
            labelCharacter.Margin = new Padding(4, 0, 4, 0);
            labelCharacter.Name = "labelCharacter";
            labelCharacter.Size = new Size(35, 15);
            labelCharacter.TabIndex = 5;
            labelCharacter.Text = "Char:";
            // 
            // buttonDone
            // 
            buttonDone.DialogResult = DialogResult.OK;
            buttonDone.Location = new Point(198, 214);
            buttonDone.Margin = new Padding(4, 3, 4, 3);
            buttonDone.Name = "buttonDone";
            buttonDone.Size = new Size(75, 23);
            buttonDone.TabIndex = 7;
            buttonDone.Text = "Save";
            buttonDone.UseCompatibleTextRendering = true;
            buttonDone.UseVisualStyleBackColor = true;
            buttonDone.Click += ButtonDone_Click;
            // 
            // labelPath
            // 
            labelPath.AutoSize = true;
            labelPath.Location = new Point(28, 137);
            labelPath.Margin = new Padding(4, 0, 4, 0);
            labelPath.Name = "labelPath";
            labelPath.Size = new Size(34, 15);
            labelPath.TabIndex = 8;
            labelPath.Text = "Path:";
            // 
            // textBoxPath
            // 
            textBoxPath.Location = new Point(70, 133);
            textBoxPath.Margin = new Padding(4, 3, 4, 3);
            textBoxPath.Name = "textBoxPath";
            textBoxPath.Size = new Size(168, 23);
            textBoxPath.TabIndex = 9;
            // 
            // buttonDialogPath
            // 
            buttonDialogPath.Location = new Point(248, 133);
            buttonDialogPath.Margin = new Padding(4, 3, 4, 3);
            buttonDialogPath.Name = "buttonDialogPath";
            buttonDialogPath.Size = new Size(25, 23);
            buttonDialogPath.TabIndex = 11;
            buttonDialogPath.Text = "...";
            buttonDialogPath.UseVisualStyleBackColor = true;
            buttonDialogPath.Click += ButtonDialogPath_Click;
            // 
            // labelExtraArguments
            // 
            labelExtraArguments.AutoSize = true;
            labelExtraArguments.Location = new Point(13, 165);
            labelExtraArguments.Margin = new Padding(4, 0, 4, 0);
            labelExtraArguments.Name = "labelExtraArguments";
            labelExtraArguments.Size = new Size(49, 15);
            labelExtraArguments.TabIndex = 12;
            labelExtraArguments.Text = "Ex Args:";
            // 
            // textBoxExtraArguments
            // 
            textBoxExtraArguments.Location = new Point(70, 162);
            textBoxExtraArguments.Margin = new Padding(4, 3, 4, 3);
            textBoxExtraArguments.Name = "textBoxExtraArguments";
            textBoxExtraArguments.Size = new Size(203, 23);
            textBoxExtraArguments.TabIndex = 13;
            // 
            // buttonTogglePasswordVisibility
            // 
            buttonTogglePasswordVisibility.Location = new Point(248, 71);
            buttonTogglePasswordVisibility.Margin = new Padding(4, 3, 4, 3);
            buttonTogglePasswordVisibility.Name = "buttonTogglePasswordVisibility";
            buttonTogglePasswordVisibility.Size = new Size(25, 23);
            buttonTogglePasswordVisibility.TabIndex = 14;
            buttonTogglePasswordVisibility.Text = "*";
            buttonTogglePasswordVisibility.UseVisualStyleBackColor = true;
            buttonTogglePasswordVisibility.Click += ButtonTogglePasswordVisibility_Click;
            // 
            // checkBoxElevated
            // 
            checkBoxElevated.AutoSize = true;
            checkBoxElevated.Location = new Point(13, 194);
            checkBoxElevated.Margin = new Padding(4, 3, 4, 3);
            checkBoxElevated.Name = "checkBoxElevated";
            checkBoxElevated.Size = new Size(94, 19);
            checkBoxElevated.TabIndex = 15;
            checkBoxElevated.Text = "Run elevated";
            checkBoxElevated.UseVisualStyleBackColor = true;
            // 
            // buttonMods
            // 
            buttonMods.Location = new Point(115, 214);
            buttonMods.Margin = new Padding(4, 3, 4, 3);
            buttonMods.Name = "buttonMods";
            buttonMods.Size = new Size(75, 23);
            buttonMods.TabIndex = 16;
            buttonMods.Text = "Mods";
            buttonMods.UseVisualStyleBackColor = true;
            buttonMods.Click += ButtonMods_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(30, 16);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(32, 15);
            label1.TabIndex = 0;
            label1.Text = "Title:";
            // 
            // textBoxTitle
            // 
            textBoxTitle.Location = new Point(70, 13);
            textBoxTitle.Margin = new Padding(4, 3, 4, 3);
            textBoxTitle.Name = "textBoxTitle";
            textBoxTitle.Size = new Size(203, 23);
            textBoxTitle.TabIndex = 1;
            // 
            // checkBoxUsePluginFolderMods
            // 
            checkBoxUsePluginFolderMods.AutoSize = true;
            checkBoxUsePluginFolderMods.Location = new Point(115, 194);
            checkBoxUsePluginFolderMods.Name = "checkBoxUsePluginFolderMods";
            checkBoxUsePluginFolderMods.Size = new Size(167, 19);
            checkBoxUsePluginFolderMods.TabIndex = 17;
            checkBoxUsePluginFolderMods.Text = "Use mods in plugin folders";
            checkBoxUsePluginFolderMods.UseVisualStyleBackColor = true;
            // 
            // AddAccountForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ClientSize = new Size(285, 239);
            Controls.Add(checkBoxUsePluginFolderMods);
            Controls.Add(buttonMods);
            Controls.Add(checkBoxElevated);
            Controls.Add(buttonTogglePasswordVisibility);
            Controls.Add(textBoxExtraArguments);
            Controls.Add(labelExtraArguments);
            Controls.Add(buttonDialogPath);
            Controls.Add(textBoxPath);
            Controls.Add(labelPath);
            Controls.Add(buttonDone);
            Controls.Add(labelCharacter);
            Controls.Add(textBoxCharacter);
            Controls.Add(labelPassword);
            Controls.Add(textBoxPassword);
            Controls.Add(textBoxTitle);
            Controls.Add(label1);
            Controls.Add(textBoxEmail);
            Controls.Add(labelEmail);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(4, 3, 4, 3);
            MinimumSize = new Size(267, 0);
            Name = "AddAccountForm";
            Padding = new Padding(0, 0, 0, 9);
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Add Account";
            Load += AddAccountForm_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label labelEmail;
        private TextBox textBoxEmail;
        private TextBox textBoxPassword;
        private Label labelPassword;
        private TextBox textBoxCharacter;
        private Label labelCharacter;
        private Button buttonDone;
        private Label labelPath;
        private TextBox textBoxPath;
        private Button buttonDialogPath;
        private Label labelExtraArguments;
        private TextBox textBoxExtraArguments;
        private Button buttonTogglePasswordVisibility;
        private CheckBox checkBoxElevated;
        private Button buttonMods;
        private Label label1;
        private TextBox textBoxTitle;
        private CheckBox checkBoxUsePluginFolderMods;
    }
}
