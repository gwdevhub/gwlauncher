﻿namespace GW_Launcher.Forms
{
    partial class AddAccountForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddAccountForm));
            this.labelEmail = new System.Windows.Forms.Label();
            this.textBoxEmail = new System.Windows.Forms.TextBox();
            this.textBoxPassword = new System.Windows.Forms.TextBox();
            this.labelPassword = new System.Windows.Forms.Label();
            this.textBoxCharacter = new System.Windows.Forms.TextBox();
            this.labelCharacter = new System.Windows.Forms.Label();
            this.checkBoxDatFix = new System.Windows.Forms.CheckBox();
            this.buttonDone = new System.Windows.Forms.Button();
            this.labelPath = new System.Windows.Forms.Label();
            this.textBoxPath = new System.Windows.Forms.TextBox();
            this.buttonDialogPath = new System.Windows.Forms.Button();
            this.labelExtraArguments = new System.Windows.Forms.Label();
            this.textBoxExtraArguments = new System.Windows.Forms.TextBox();
            this.buttonTogglePasswordVisibility = new System.Windows.Forms.Button();
            this.checkBoxElevated = new System.Windows.Forms.CheckBox();
            this.buttonMods = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // labelEmail
            // 
            this.labelEmail.AutoSize = true;
            this.labelEmail.Location = new System.Drawing.Point(23, 15);
            this.labelEmail.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelEmail.Name = "labelEmail";
            this.labelEmail.Size = new System.Drawing.Size(39, 15);
            this.labelEmail.TabIndex = 0;
            this.labelEmail.Text = "Email:";
            // 
            // textBoxEmail
            // 
            this.textBoxEmail.Location = new System.Drawing.Point(70, 12);
            this.textBoxEmail.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.textBoxEmail.Name = "textBoxEmail";
            this.textBoxEmail.Size = new System.Drawing.Size(168, 23);
            this.textBoxEmail.TabIndex = 1;
            // 
            // textBoxPassword
            // 
            this.textBoxPassword.Location = new System.Drawing.Point(70, 41);
            this.textBoxPassword.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.textBoxPassword.Name = "textBoxPassword";
            this.textBoxPassword.PasswordChar = '*';
            this.textBoxPassword.Size = new System.Drawing.Size(135, 23);
            this.textBoxPassword.TabIndex = 2;
            // 
            // labelPassword
            // 
            this.labelPassword.AutoSize = true;
            this.labelPassword.Location = new System.Drawing.Point(29, 45);
            this.labelPassword.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelPassword.Name = "labelPassword";
            this.labelPassword.Size = new System.Drawing.Size(33, 15);
            this.labelPassword.TabIndex = 3;
            this.labelPassword.Text = "Pass:";
            // 
            // textBoxCharacter
            // 
            this.textBoxCharacter.Location = new System.Drawing.Point(70, 70);
            this.textBoxCharacter.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.textBoxCharacter.Name = "textBoxCharacter";
            this.textBoxCharacter.Size = new System.Drawing.Size(168, 23);
            this.textBoxCharacter.TabIndex = 4;
            // 
            // labelCharacter
            // 
            this.labelCharacter.AutoSize = true;
            this.labelCharacter.Location = new System.Drawing.Point(27, 73);
            this.labelCharacter.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelCharacter.Name = "labelCharacter";
            this.labelCharacter.Size = new System.Drawing.Size(35, 15);
            this.labelCharacter.TabIndex = 5;
            this.labelCharacter.Text = "Char:";
            // 
            // checkBoxDatFix
            // 
            this.checkBoxDatFix.AutoSize = true;
            this.checkBoxDatFix.Location = new System.Drawing.Point(13, 164);
            this.checkBoxDatFix.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.checkBoxDatFix.Name = "checkBoxDatFix";
            this.checkBoxDatFix.Size = new System.Drawing.Size(113, 19);
            this.checkBoxDatFix.TabIndex = 6;
            this.checkBoxDatFix.Text = "Apply .dat Patch";
            this.checkBoxDatFix.UseVisualStyleBackColor = true;
            this.checkBoxDatFix.CheckedChanged += new System.EventHandler(this.CheckBoxDatFix_CheckedChanged);
            // 
            // buttonDone
            // 
            this.buttonDone.Location = new System.Drawing.Point(163, 190);
            this.buttonDone.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.buttonDone.Name = "buttonDone";
            this.buttonDone.Size = new System.Drawing.Size(75, 23);
            this.buttonDone.TabIndex = 7;
            this.buttonDone.Text = "Add";
            this.buttonDone.UseCompatibleTextRendering = true;
            this.buttonDone.UseVisualStyleBackColor = true;
            this.buttonDone.Click += new System.EventHandler(this.ButtonDone_Click);
            // 
            // labelPath
            // 
            this.labelPath.AutoSize = true;
            this.labelPath.Location = new System.Drawing.Point(28, 107);
            this.labelPath.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelPath.Name = "labelPath";
            this.labelPath.Size = new System.Drawing.Size(34, 15);
            this.labelPath.TabIndex = 8;
            this.labelPath.Text = "Path:";
            // 
            // textBoxPath
            // 
            this.textBoxPath.Location = new System.Drawing.Point(70, 103);
            this.textBoxPath.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.textBoxPath.Name = "textBoxPath";
            this.textBoxPath.Size = new System.Drawing.Size(135, 23);
            this.textBoxPath.TabIndex = 9;
            // 
            // buttonDialogPath
            // 
            this.buttonDialogPath.Location = new System.Drawing.Point(213, 103);
            this.buttonDialogPath.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.buttonDialogPath.Name = "buttonDialogPath";
            this.buttonDialogPath.Size = new System.Drawing.Size(25, 23);
            this.buttonDialogPath.TabIndex = 11;
            this.buttonDialogPath.Text = "...";
            this.buttonDialogPath.UseVisualStyleBackColor = true;
            this.buttonDialogPath.Click += new System.EventHandler(this.ButtonDialogPath_Click);
            // 
            // labelExtraArguments
            // 
            this.labelExtraArguments.AutoSize = true;
            this.labelExtraArguments.Location = new System.Drawing.Point(13, 135);
            this.labelExtraArguments.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelExtraArguments.Name = "labelExtraArguments";
            this.labelExtraArguments.Size = new System.Drawing.Size(49, 15);
            this.labelExtraArguments.TabIndex = 12;
            this.labelExtraArguments.Text = "Ex Args:";
            // 
            // textBoxExtraArguments
            // 
            this.textBoxExtraArguments.Location = new System.Drawing.Point(70, 132);
            this.textBoxExtraArguments.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.textBoxExtraArguments.Name = "textBoxExtraArguments";
            this.textBoxExtraArguments.Size = new System.Drawing.Size(168, 23);
            this.textBoxExtraArguments.TabIndex = 13;
            // 
            // buttonTogglePasswordVisibility
            // 
            this.buttonTogglePasswordVisibility.Location = new System.Drawing.Point(213, 41);
            this.buttonTogglePasswordVisibility.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.buttonTogglePasswordVisibility.Name = "buttonTogglePasswordVisibility";
            this.buttonTogglePasswordVisibility.Size = new System.Drawing.Size(25, 23);
            this.buttonTogglePasswordVisibility.TabIndex = 14;
            this.buttonTogglePasswordVisibility.Text = "*";
            this.buttonTogglePasswordVisibility.UseVisualStyleBackColor = true;
            this.buttonTogglePasswordVisibility.Click += new System.EventHandler(this.ButtonTogglePasswordVisibility_Click);
            // 
            // checkBoxElevated
            // 
            this.checkBoxElevated.AutoSize = true;
            this.checkBoxElevated.Location = new System.Drawing.Point(13, 193);
            this.checkBoxElevated.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.checkBoxElevated.Name = "checkBoxElevated";
            this.checkBoxElevated.Size = new System.Drawing.Size(94, 19);
            this.checkBoxElevated.TabIndex = 15;
            this.checkBoxElevated.Text = "Run elevated";
            this.checkBoxElevated.UseVisualStyleBackColor = true;
            // 
            // buttonMods
            // 
            this.buttonMods.Location = new System.Drawing.Point(163, 161);
            this.buttonMods.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.buttonMods.Name = "buttonMods";
            this.buttonMods.Size = new System.Drawing.Size(75, 23);
            this.buttonMods.TabIndex = 16;
            this.buttonMods.Text = "Mods";
            this.buttonMods.UseVisualStyleBackColor = true;
            this.buttonMods.Click += new System.EventHandler(this.ButtonMods_Click);
            // 
            // AddAccountForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(251, 243);
            this.Controls.Add(this.buttonMods);
            this.Controls.Add(this.checkBoxElevated);
            this.Controls.Add(this.buttonTogglePasswordVisibility);
            this.Controls.Add(this.textBoxExtraArguments);
            this.Controls.Add(this.labelExtraArguments);
            this.Controls.Add(this.buttonDialogPath);
            this.Controls.Add(this.textBoxPath);
            this.Controls.Add(this.labelPath);
            this.Controls.Add(this.buttonDone);
            this.Controls.Add(this.checkBoxDatFix);
            this.Controls.Add(this.labelCharacter);
            this.Controls.Add(this.textBoxCharacter);
            this.Controls.Add(this.labelPassword);
            this.Controls.Add(this.textBoxPassword);
            this.Controls.Add(this.textBoxEmail);
            this.Controls.Add(this.labelEmail);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.MinimumSize = new System.Drawing.Size(267, 0);
            this.Name = "AddAccountForm";
            this.Padding = new System.Windows.Forms.Padding(0, 0, 0, 9);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Add Account";
            this.Load += new System.EventHandler(this.AddAccountForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelEmail;
        private System.Windows.Forms.TextBox textBoxEmail;
        private System.Windows.Forms.TextBox textBoxPassword;
        private System.Windows.Forms.Label labelPassword;
        private System.Windows.Forms.TextBox textBoxCharacter;
        private System.Windows.Forms.Label labelCharacter;
        private System.Windows.Forms.CheckBox checkBoxDatFix;
        private System.Windows.Forms.Button buttonDone;
        private System.Windows.Forms.Label labelPath;
        private System.Windows.Forms.TextBox textBoxPath;
        private System.Windows.Forms.Button buttonDialogPath;
        private System.Windows.Forms.Label labelExtraArguments;
        private System.Windows.Forms.TextBox textBoxExtraArguments;
        private System.Windows.Forms.Button buttonTogglePasswordVisibility;
        private System.Windows.Forms.CheckBox checkBoxElevated;
        private System.Windows.Forms.Button buttonMods;
    }
}
