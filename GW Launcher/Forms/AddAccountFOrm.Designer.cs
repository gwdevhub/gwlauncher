namespace GW_Launcher
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
            this.button1 = new System.Windows.Forms.Button();
            this.checkBoxElevated = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // labelEmail
            // 
            this.labelEmail.AutoSize = true;
            this.labelEmail.Location = new System.Drawing.Point(20, 23);
            this.labelEmail.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelEmail.Name = "labelEmail";
            this.labelEmail.Size = new System.Drawing.Size(52, 20);
            this.labelEmail.TabIndex = 0;
            this.labelEmail.Text = "Email:";
            // 
            // textBoxEmail
            // 
            this.textBoxEmail.Location = new System.Drawing.Point(81, 18);
            this.textBoxEmail.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.textBoxEmail.Name = "textBoxEmail";
            this.textBoxEmail.Size = new System.Drawing.Size(222, 26);
            this.textBoxEmail.TabIndex = 1;
            // 
            // textBoxPassword
            // 
            this.textBoxPassword.Location = new System.Drawing.Point(81, 58);
            this.textBoxPassword.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.textBoxPassword.Name = "textBoxPassword";
            this.textBoxPassword.PasswordChar = '*';
            this.textBoxPassword.Size = new System.Drawing.Size(182, 26);
            this.textBoxPassword.TabIndex = 2;
            // 
            // labelPw
            // 
            this.labelPw.AutoSize = true;
            this.labelPw.Location = new System.Drawing.Point(18, 63);
            this.labelPw.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelPw.Name = "labelPw";
            this.labelPw.Size = new System.Drawing.Size(48, 20);
            this.labelPw.TabIndex = 3;
            this.labelPw.Text = "Pass:";
            // 
            // textBoxChar
            // 
            this.textBoxChar.Location = new System.Drawing.Point(81, 98);
            this.textBoxChar.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.textBoxChar.Name = "textBoxChar";
            this.textBoxChar.Size = new System.Drawing.Size(222, 26);
            this.textBoxChar.TabIndex = 4;
            // 
            // labelChar
            // 
            this.labelChar.AutoSize = true;
            this.labelChar.Location = new System.Drawing.Point(18, 103);
            this.labelChar.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelChar.Name = "labelChar";
            this.labelChar.Size = new System.Drawing.Size(47, 20);
            this.labelChar.TabIndex = 5;
            this.labelChar.Text = "Char:";
            // 
            // checkBoxDatFix
            // 
            this.checkBoxDatFix.AutoSize = true;
            this.checkBoxDatFix.Location = new System.Drawing.Point(18, 226);
            this.checkBoxDatFix.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.checkBoxDatFix.Name = "checkBoxDatFix";
            this.checkBoxDatFix.Size = new System.Drawing.Size(150, 24);
            this.checkBoxDatFix.TabIndex = 6;
            this.checkBoxDatFix.Text = "Apply .dat Patch";
            this.checkBoxDatFix.UseVisualStyleBackColor = true;
            // 
            // buttonDone
            // 
            this.buttonDone.Location = new System.Drawing.Point(191, 247);
            this.buttonDone.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.buttonDone.Name = "buttonDone";
            this.buttonDone.Size = new System.Drawing.Size(112, 35);
            this.buttonDone.TabIndex = 7;
            this.buttonDone.Text = "Add";
            this.buttonDone.UseCompatibleTextRendering = true;
            this.buttonDone.UseVisualStyleBackColor = true;
            this.buttonDone.Click += new System.EventHandler(this.buttonDone_Click);
            // 
            // labelGWPath
            // 
            this.labelGWPath.AutoSize = true;
            this.labelGWPath.Location = new System.Drawing.Point(18, 143);
            this.labelGWPath.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelGWPath.Name = "labelGWPath";
            this.labelGWPath.Size = new System.Drawing.Size(46, 20);
            this.labelGWPath.TabIndex = 8;
            this.labelGWPath.Text = "Path:";
            // 
            // textBoxPath
            // 
            this.textBoxPath.Location = new System.Drawing.Point(81, 138);
            this.textBoxPath.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.textBoxPath.Name = "textBoxPath";
            this.textBoxPath.Size = new System.Drawing.Size(162, 26);
            this.textBoxPath.TabIndex = 9;
            // 
            // buttonDialogPath
            // 
            this.buttonDialogPath.Location = new System.Drawing.Point(254, 137);
            this.buttonDialogPath.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.buttonDialogPath.Name = "buttonDialogPath";
            this.buttonDialogPath.Size = new System.Drawing.Size(51, 34);
            this.buttonDialogPath.TabIndex = 11;
            this.buttonDialogPath.Text = "...";
            this.buttonDialogPath.UseVisualStyleBackColor = true;
            this.buttonDialogPath.Click += new System.EventHandler(this.buttonDialogPath_Click);
            // 
            // labelExtraArgs
            // 
            this.labelExtraArgs.AutoSize = true;
            this.labelExtraArgs.Location = new System.Drawing.Point(3, 182);
            this.labelExtraArgs.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelExtraArgs.Name = "labelExtraArgs";
            this.labelExtraArgs.Size = new System.Drawing.Size(68, 20);
            this.labelExtraArgs.TabIndex = 12;
            this.labelExtraArgs.Text = "Ex Args:";
            // 
            // textBoxExArgs
            // 
            this.textBoxExArgs.Location = new System.Drawing.Point(81, 177);
            this.textBoxExArgs.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.textBoxExArgs.Name = "textBoxExArgs";
            this.textBoxExArgs.Size = new System.Drawing.Size(222, 26);
            this.textBoxExArgs.TabIndex = 13;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(274, 58);
            this.button1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(30, 31);
            this.button1.TabIndex = 14;
            this.button1.Text = "*";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // checkBoxElevated
            // 
            this.checkBoxElevated.AutoSize = true;
            this.checkBoxElevated.Location = new System.Drawing.Point(18, 258);
            this.checkBoxElevated.Name = "checkBoxElevated";
            this.checkBoxElevated.Size = new System.Drawing.Size(129, 24);
            this.checkBoxElevated.TabIndex = 15;
            this.checkBoxElevated.Text = "Run elevated";
            this.checkBoxElevated.UseVisualStyleBackColor = true;
            // 
            // AddAccountForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(322, 294);
            this.Controls.Add(this.checkBoxElevated);
            this.Controls.Add(this.button1);
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
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
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
        private System.Windows.Forms.Button buttonDone;
        private System.Windows.Forms.Label labelGWPath;
        private System.Windows.Forms.TextBox textBoxPath;
        private System.Windows.Forms.Button buttonDialogPath;
        private System.Windows.Forms.Label labelExtraArgs;
        private System.Windows.Forms.TextBox textBoxExArgs;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.CheckBox checkBoxElevated;
    }
}