namespace GW_Launcher.Forms
{
	partial class CryptPassForm
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
            groupBox1 = new GroupBox();
            checkBoxDontAsk = new CheckBox();
            buttonEnter = new Button();
            textBoxPassword = new TextBox();
            groupBox1.SuspendLayout();
            SuspendLayout();
            // 
            // groupBox1
            // 
            groupBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            groupBox1.Controls.Add(checkBoxDontAsk);
            groupBox1.Controls.Add(buttonEnter);
            groupBox1.Controls.Add(textBoxPassword);
            groupBox1.Location = new Point(14, 14);
            groupBox1.Margin = new Padding(4, 3, 4, 3);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new Padding(4, 3, 4, 3);
            groupBox1.Size = new Size(264, 123);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Specify GWLauncher Master Password";
            // 
            // checkBoxDontAsk
            // 
            checkBoxDontAsk.AutoSize = true;
            checkBoxDontAsk.Location = new Point(7, 52);
            checkBoxDontAsk.Margin = new Padding(4, 3, 4, 3);
            checkBoxDontAsk.Name = "checkBoxDontAsk";
            checkBoxDontAsk.Size = new Size(202, 19);
            checkBoxDontAsk.TabIndex = 2;
            checkBoxDontAsk.Text = "Don't ask again until I next log on";
            checkBoxDontAsk.UseVisualStyleBackColor = true;
            // 
            // buttonEnter
            // 
            buttonEnter.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            buttonEnter.Location = new Point(169, 79);
            buttonEnter.Margin = new Padding(4, 3, 4, 3);
            buttonEnter.Name = "buttonEnter";
            buttonEnter.Size = new Size(88, 27);
            buttonEnter.TabIndex = 1;
            buttonEnter.Text = "Enter";
            buttonEnter.UseVisualStyleBackColor = true;
            buttonEnter.Click += ButtonEnter_Click;
            // 
            // textBoxPassword
            // 
            textBoxPassword.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBoxPassword.Location = new Point(7, 22);
            textBoxPassword.Margin = new Padding(4, 3, 4, 3);
            textBoxPassword.Name = "textBoxPassword";
            textBoxPassword.Size = new Size(249, 23);
            textBoxPassword.TabIndex = 0;
            textBoxPassword.UseSystemPasswordChar = true;
            textBoxPassword.KeyPress += TextBoxPassword_KeyPress;
            // 
            // CryptPassForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(292, 169);
            Controls.Add(groupBox1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Margin = new Padding(4, 3, 4, 3);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "CryptPassForm";
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "GW Launcher - Master Password";
            TopMost = true;
            FormClosing += CryptPassForm_FormClosing;
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.CheckBox checkBoxDontAsk;
		private System.Windows.Forms.Button buttonEnter;
		private System.Windows.Forms.TextBox textBoxPassword;
	}
}
