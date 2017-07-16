﻿namespace GW_Launcher
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
            this.labelPw = new System.Windows.Forms.Label();
            this.textBoxChar = new System.Windows.Forms.TextBox();
            this.labelChar = new System.Windows.Forms.Label();
            this.checkBoxDatFix = new System.Windows.Forms.CheckBox();
            this.buttonDone = new System.Windows.Forms.Button();
            this.labelGWPath = new System.Windows.Forms.Label();
            this.textBoxPath = new System.Windows.Forms.TextBox();
            this.buttonDialogPath = new System.Windows.Forms.Button();
            this.labelExtraArgs = new System.Windows.Forms.Label();
            this.textBoxExArgs = new System.Windows.Forms.TextBox();
            this.labelAlias = new System.Windows.Forms.Label();
            this.textBoxAlias = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // labelEmail
            // 
            this.labelEmail.AutoSize = true;
            this.labelEmail.Location = new System.Drawing.Point(12, 41);
            this.labelEmail.Name = "labelEmail";
            this.labelEmail.Size = new System.Drawing.Size(35, 13);
            this.labelEmail.TabIndex = 0;
            this.labelEmail.Text = "Email:";
            // 
            // textBoxEmail
            // 
            this.textBoxEmail.Location = new System.Drawing.Point(54, 38);
            this.textBoxEmail.Name = "textBoxEmail";
            this.textBoxEmail.Size = new System.Drawing.Size(149, 20);
            this.textBoxEmail.TabIndex = 1;
            // 
            // textBoxPassword
            // 
            this.textBoxPassword.Location = new System.Drawing.Point(54, 64);
            this.textBoxPassword.Name = "textBoxPassword";
            this.textBoxPassword.PasswordChar = '*';
            this.textBoxPassword.Size = new System.Drawing.Size(149, 20);
            this.textBoxPassword.TabIndex = 2;
            // 
            // labelPw
            // 
            this.labelPw.AutoSize = true;
            this.labelPw.Location = new System.Drawing.Point(12, 67);
            this.labelPw.Name = "labelPw";
            this.labelPw.Size = new System.Drawing.Size(33, 13);
            this.labelPw.TabIndex = 3;
            this.labelPw.Text = "Pass:";
            // 
            // textBoxChar
            // 
            this.textBoxChar.Location = new System.Drawing.Point(54, 90);
            this.textBoxChar.Name = "textBoxChar";
            this.textBoxChar.Size = new System.Drawing.Size(149, 20);
            this.textBoxChar.TabIndex = 4;
            // 
            // labelChar
            // 
            this.labelChar.AutoSize = true;
            this.labelChar.Location = new System.Drawing.Point(12, 93);
            this.labelChar.Name = "labelChar";
            this.labelChar.Size = new System.Drawing.Size(32, 13);
            this.labelChar.TabIndex = 5;
            this.labelChar.Text = "Char:";
            // 
            // checkBoxDatFix
            // 
            this.checkBoxDatFix.AutoSize = true;
            this.checkBoxDatFix.Location = new System.Drawing.Point(12, 173);
            this.checkBoxDatFix.Name = "checkBoxDatFix";
            this.checkBoxDatFix.Size = new System.Drawing.Size(104, 17);
            this.checkBoxDatFix.TabIndex = 6;
            this.checkBoxDatFix.Text = "Apply .dat Patch";
            this.checkBoxDatFix.UseVisualStyleBackColor = true;
            // 
            // buttonDone
            // 
            this.buttonDone.Location = new System.Drawing.Point(128, 170);
            this.buttonDone.Name = "buttonDone";
            this.buttonDone.Size = new System.Drawing.Size(75, 23);
            this.buttonDone.TabIndex = 7;
            this.buttonDone.Text = "Add";
            this.buttonDone.UseCompatibleTextRendering = true;
            this.buttonDone.UseVisualStyleBackColor = true;
            this.buttonDone.Click += new System.EventHandler(this.buttonDone_Click);
            // 
            // labelGWPath
            // 
            this.labelGWPath.AutoSize = true;
            this.labelGWPath.Location = new System.Drawing.Point(12, 119);
            this.labelGWPath.Name = "labelGWPath";
            this.labelGWPath.Size = new System.Drawing.Size(32, 13);
            this.labelGWPath.TabIndex = 8;
            this.labelGWPath.Text = "Path:";
            // 
            // textBoxPath
            // 
            this.textBoxPath.Location = new System.Drawing.Point(54, 116);
            this.textBoxPath.Name = "textBoxPath";
            this.textBoxPath.Size = new System.Drawing.Size(109, 20);
            this.textBoxPath.TabIndex = 9;
            // 
            // buttonDialogPath
            // 
            this.buttonDialogPath.Location = new System.Drawing.Point(169, 115);
            this.buttonDialogPath.Name = "buttonDialogPath";
            this.buttonDialogPath.Size = new System.Drawing.Size(34, 22);
            this.buttonDialogPath.TabIndex = 11;
            this.buttonDialogPath.Text = "...";
            this.buttonDialogPath.UseVisualStyleBackColor = true;
            this.buttonDialogPath.Click += new System.EventHandler(this.buttonDialogPath_Click);
            // 
            // labelExtraArgs
            // 
            this.labelExtraArgs.AutoSize = true;
            this.labelExtraArgs.Location = new System.Drawing.Point(2, 144);
            this.labelExtraArgs.Name = "labelExtraArgs";
            this.labelExtraArgs.Size = new System.Drawing.Size(46, 13);
            this.labelExtraArgs.TabIndex = 12;
            this.labelExtraArgs.Text = "Ex Args:";
            // 
            // textBoxExArgs
            // 
            this.textBoxExArgs.Location = new System.Drawing.Point(54, 141);
            this.textBoxExArgs.Name = "textBoxExArgs";
            this.textBoxExArgs.Size = new System.Drawing.Size(149, 20);
            this.textBoxExArgs.TabIndex = 13;
            // 
            // labelAlias
            // 
            this.labelAlias.AutoSize = true;
            this.labelAlias.Location = new System.Drawing.Point(12, 15);
            this.labelAlias.Name = "labelAlias";
            this.labelAlias.Size = new System.Drawing.Size(32, 13);
            this.labelAlias.TabIndex = 14;
            this.labelAlias.Text = "Alias:";
            // 
            // textBoxAlias
            // 
            this.textBoxAlias.Location = new System.Drawing.Point(54, 12);
            this.textBoxAlias.Name = "textBoxAlias";
            this.textBoxAlias.Size = new System.Drawing.Size(149, 20);
            this.textBoxAlias.TabIndex = 15;
            // 
            // AddAccountForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(215, 205);
            this.Controls.Add(this.textBoxAlias);
            this.Controls.Add(this.labelAlias);
            this.Controls.Add(this.textBoxExArgs);
            this.Controls.Add(this.labelExtraArgs);
            this.Controls.Add(this.buttonDialogPath);
            this.Controls.Add(this.textBoxPath);
            this.Controls.Add(this.labelGWPath);
            this.Controls.Add(this.buttonDone);
            this.Controls.Add(this.checkBoxDatFix);
            this.Controls.Add(this.labelChar);
            this.Controls.Add(this.textBoxChar);
            this.Controls.Add(this.labelPw);
            this.Controls.Add(this.textBoxPassword);
            this.Controls.Add(this.textBoxEmail);
            this.Controls.Add(this.labelEmail);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "AddAccountForm";
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
        private System.Windows.Forms.Label labelPw;
        private System.Windows.Forms.TextBox textBoxChar;
        private System.Windows.Forms.Label labelChar;
        private System.Windows.Forms.CheckBox checkBoxDatFix;
        private System.Windows.Forms.Label labelGWPath;
        private System.Windows.Forms.TextBox textBoxPath;
        private System.Windows.Forms.Button buttonDialogPath;
        private System.Windows.Forms.Label labelExtraArgs;
        private System.Windows.Forms.TextBox textBoxExArgs;
        private System.Windows.Forms.Label labelAlias;
        private System.Windows.Forms.TextBox textBoxAlias;
        public System.Windows.Forms.Button buttonDone;
    }
}