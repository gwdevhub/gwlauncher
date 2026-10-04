namespace GW_Launcher.Forms
{
	partial class SettingsForm
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
			this.groupBoxGeneral = new System.Windows.Forms.GroupBox();
			this.checkBoxLaunchMinimized = new System.Windows.Forms.CheckBox();
			this.checkBoxKeepInSystemTray = new System.Windows.Forms.CheckBox();
			this.labelPassword = new System.Windows.Forms.Label();
			this.textBoxPassword = new System.Windows.Forms.TextBox();
			this.checkBoxProtectAccounts = new System.Windows.Forms.CheckBox();
			this.checkBoxShowPassword = new System.Windows.Forms.CheckBox();
			this.groupBoxUpdates = new System.Windows.Forms.GroupBox();
			this.checkBoxAutoUpdate = new System.Windows.Forms.CheckBox();
			this.checkBoxCheckForUpdates = new System.Windows.Forms.CheckBox();
			this.groupBoxAdvanced = new System.Windows.Forms.GroupBox();
			this.numericUpDownTimeout = new System.Windows.Forms.NumericUpDown();
			this.labelTimeout = new System.Windows.Forms.Label();
			this.labelDescProtectAccounts = new System.Windows.Forms.Label();
			this.labelDescLaunchMinimized = new System.Windows.Forms.Label();
			this.labelDescKeepInSystemTray = new System.Windows.Forms.Label();
			this.labelDescCheckForUpdates = new System.Windows.Forms.Label();
			this.labelDescAutoUpdate = new System.Windows.Forms.Label();
			this.labelDescTimeout = new System.Windows.Forms.Label();
			this.buttonOK = new System.Windows.Forms.Button();
			this.buttonCancel = new System.Windows.Forms.Button();
			this.groupBoxGeneral.SuspendLayout();
			this.groupBoxUpdates.SuspendLayout();
			this.groupBoxAdvanced.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownTimeout)).BeginInit();
			this.SuspendLayout();
			// 
			// groupBoxGeneral
			// 
			this.groupBoxGeneral.Controls.Add(this.labelDescKeepInSystemTray);
			this.groupBoxGeneral.Controls.Add(this.labelDescLaunchMinimized);
			this.groupBoxGeneral.Controls.Add(this.labelDescProtectAccounts);
			this.groupBoxGeneral.Controls.Add(this.checkBoxLaunchMinimized);
			this.groupBoxGeneral.Controls.Add(this.checkBoxKeepInSystemTray);
			this.groupBoxGeneral.Controls.Add(this.labelPassword);
			this.groupBoxGeneral.Controls.Add(this.textBoxPassword);
			this.groupBoxGeneral.Controls.Add(this.checkBoxProtectAccounts);
			this.groupBoxGeneral.Controls.Add(this.checkBoxShowPassword);
			this.groupBoxGeneral.Location = new System.Drawing.Point(12, 12);
			this.groupBoxGeneral.Name = "groupBoxGeneral";
			this.groupBoxGeneral.Size = new System.Drawing.Size(360, 208);
			this.groupBoxGeneral.TabIndex = 0;
			this.groupBoxGeneral.TabStop = false;
			this.groupBoxGeneral.Text = "General";
			//
			// labelPassword
			//
			this.labelPassword.AutoSize = true;
			this.labelPassword.Location = new System.Drawing.Point(33, 67);
			this.labelPassword.Name = "labelPassword";
			this.labelPassword.Size = new System.Drawing.Size(101, 15);
			this.labelPassword.TabIndex = 1;
			this.labelPassword.Text = "Master password:";
			//
			// textBoxPassword
			//
			this.textBoxPassword.Location = new System.Drawing.Point(140, 64);
			this.textBoxPassword.Name = "textBoxPassword";
			this.textBoxPassword.Size = new System.Drawing.Size(200, 23);
			this.textBoxPassword.TabIndex = 2;
			this.textBoxPassword.UseSystemPasswordChar = true;
			//
			// checkBoxProtectAccounts
			//
			this.checkBoxProtectAccounts.AutoSize = true;
			this.checkBoxProtectAccounts.Location = new System.Drawing.Point(15, 22);
			this.checkBoxProtectAccounts.Name = "checkBoxProtectAccounts";
			this.checkBoxProtectAccounts.Size = new System.Drawing.Size(185, 19);
			this.checkBoxProtectAccounts.TabIndex = 0;
			this.checkBoxProtectAccounts.Text = "Password protect Accounts.json";
			this.checkBoxProtectAccounts.UseVisualStyleBackColor = true;
			this.checkBoxProtectAccounts.CheckedChanged += new System.EventHandler(this.CheckBoxProtectAccounts_CheckedChanged);
			//
			// checkBoxShowPassword
			//
			this.checkBoxShowPassword.AutoSize = true;
			this.checkBoxShowPassword.Location = new System.Drawing.Point(140, 93);
			this.checkBoxShowPassword.Name = "checkBoxShowPassword";
			this.checkBoxShowPassword.Size = new System.Drawing.Size(133, 19);
			this.checkBoxShowPassword.TabIndex = 3;
			this.checkBoxShowPassword.Text = "Show password";
			this.checkBoxShowPassword.UseVisualStyleBackColor = true;
			this.checkBoxShowPassword.CheckedChanged += new System.EventHandler(this.CheckBoxShowPassword_CheckedChanged);
			//
			// checkBoxLaunchMinimized
			//
			this.checkBoxLaunchMinimized.AutoSize = true;
			this.checkBoxLaunchMinimized.Location = new System.Drawing.Point(15, 168);
			this.checkBoxLaunchMinimized.Name = "checkBoxLaunchMinimized";
			this.checkBoxLaunchMinimized.Size = new System.Drawing.Size(116, 19);
			this.checkBoxLaunchMinimized.TabIndex = 5;
			this.checkBoxLaunchMinimized.Text = "Launch minimised";
			this.checkBoxLaunchMinimized.UseVisualStyleBackColor = true;
			//
			// checkBoxKeepInSystemTray
			//
			this.checkBoxKeepInSystemTray.AutoSize = true;
			this.checkBoxKeepInSystemTray.Location = new System.Drawing.Point(15, 122);
			this.checkBoxKeepInSystemTray.Name = "checkBoxKeepInSystemTray";
			this.checkBoxKeepInSystemTray.Size = new System.Drawing.Size(130, 19);
			this.checkBoxKeepInSystemTray.TabIndex = 4;
			this.checkBoxKeepInSystemTray.Text = "Keep in system tray";
			this.checkBoxKeepInSystemTray.UseVisualStyleBackColor = true;
			this.checkBoxKeepInSystemTray.CheckedChanged += new System.EventHandler(this.CheckBoxKeepInSystemTray_CheckedChanged);
			//
			// groupBoxUpdates
			//
			this.groupBoxUpdates.Controls.Add(this.labelDescAutoUpdate);
			this.groupBoxUpdates.Controls.Add(this.labelDescCheckForUpdates);
			this.groupBoxUpdates.Controls.Add(this.checkBoxAutoUpdate);
			this.groupBoxUpdates.Controls.Add(this.checkBoxCheckForUpdates);
			this.groupBoxUpdates.Location = new System.Drawing.Point(12, 226);
			this.groupBoxUpdates.Name = "groupBoxUpdates";
			this.groupBoxUpdates.Size = new System.Drawing.Size(360, 106);
			this.groupBoxUpdates.TabIndex = 1;
			this.groupBoxUpdates.TabStop = false;
			this.groupBoxUpdates.Text = "Updates";
			// 
			// checkBoxAutoUpdate
			// 
			this.checkBoxAutoUpdate.AutoSize = true;
			this.checkBoxAutoUpdate.Location = new System.Drawing.Point(15, 66);
			this.checkBoxAutoUpdate.Name = "checkBoxAutoUpdate";
			this.checkBoxAutoUpdate.Size = new System.Drawing.Size(90, 19);
			this.checkBoxAutoUpdate.TabIndex = 1;
			this.checkBoxAutoUpdate.Text = "Auto update";
			this.checkBoxAutoUpdate.UseVisualStyleBackColor = true;
			// 
			// checkBoxCheckForUpdates
			// 
			this.checkBoxCheckForUpdates.AutoSize = true;
			this.checkBoxCheckForUpdates.Location = new System.Drawing.Point(15, 22);
			this.checkBoxCheckForUpdates.Name = "checkBoxCheckForUpdates";
			this.checkBoxCheckForUpdates.Size = new System.Drawing.Size(123, 19);
			this.checkBoxCheckForUpdates.TabIndex = 0;
			this.checkBoxCheckForUpdates.Text = "Check for updates";
			this.checkBoxCheckForUpdates.UseVisualStyleBackColor = true;
			this.checkBoxCheckForUpdates.CheckedChanged += new System.EventHandler(this.CheckBoxCheckForUpdates_CheckedChanged);
			// 
			// groupBoxAdvanced
			// 
			this.groupBoxAdvanced.Controls.Add(this.labelDescTimeout);
			this.groupBoxAdvanced.Controls.Add(this.numericUpDownTimeout);
			this.groupBoxAdvanced.Controls.Add(this.labelTimeout);
			this.groupBoxAdvanced.Location = new System.Drawing.Point(12, 338);
			this.groupBoxAdvanced.Name = "groupBoxAdvanced";
			this.groupBoxAdvanced.Size = new System.Drawing.Size(360, 76);
			this.groupBoxAdvanced.TabIndex = 2;
			this.groupBoxAdvanced.TabStop = false;
			this.groupBoxAdvanced.Text = "Advanced";
			// 
			// numericUpDownTimeout
			// 
			this.numericUpDownTimeout.Location = new System.Drawing.Point(220, 23);
			this.numericUpDownTimeout.Maximum = new decimal(new int[] {
			60000,
			0,
			0,
			0});
			this.numericUpDownTimeout.Minimum = new decimal(new int[] {
			1000,
			0,
			0,
			0});
			this.numericUpDownTimeout.Name = "numericUpDownTimeout";
			this.numericUpDownTimeout.Size = new System.Drawing.Size(80, 23);
			this.numericUpDownTimeout.TabIndex = 1;
			this.numericUpDownTimeout.Value = new decimal(new int[] {
			5000,
			0,
			0,
			0});
			// 
			// labelTimeout
			// 
			this.labelTimeout.AutoSize = true;
			this.labelTimeout.Location = new System.Drawing.Point(15, 25);
			this.labelTimeout.Name = "labelTimeout";
			this.labelTimeout.Size = new System.Drawing.Size(169, 15);
			this.labelTimeout.TabIndex = 0;
			this.labelTimeout.Text = "Timeout on mod launch (ms):";
			//
			// labelDescProtectAccounts
			//
			this.labelDescProtectAccounts.AutoSize = false;
			this.labelDescProtectAccounts.Font = new System.Drawing.Font("Segoe UI", 8F);
			this.labelDescProtectAccounts.ForeColor = System.Drawing.SystemColors.GrayText;
			this.labelDescProtectAccounts.Location = new System.Drawing.Point(33, 42);
			this.labelDescProtectAccounts.Name = "labelDescProtectAccounts";
			this.labelDescProtectAccounts.Size = new System.Drawing.Size(307, 14);
			this.labelDescProtectAccounts.Text = "Encrypts saved accounts; asked for at startup.";
			//
			// labelDescLaunchMinimized
			//
			this.labelDescLaunchMinimized.AutoSize = false;
			this.labelDescLaunchMinimized.Font = new System.Drawing.Font("Segoe UI", 8F);
			this.labelDescLaunchMinimized.ForeColor = System.Drawing.SystemColors.GrayText;
			this.labelDescLaunchMinimized.Location = new System.Drawing.Point(33, 188);
			this.labelDescLaunchMinimized.Name = "labelDescLaunchMinimized";
			this.labelDescLaunchMinimized.Size = new System.Drawing.Size(307, 14);
			this.labelDescLaunchMinimized.Text = "Start hidden in the tray instead of showing the window.";
			//
			// labelDescKeepInSystemTray
			//
			this.labelDescKeepInSystemTray.AutoSize = false;
			this.labelDescKeepInSystemTray.Font = new System.Drawing.Font("Segoe UI", 8F);
			this.labelDescKeepInSystemTray.ForeColor = System.Drawing.SystemColors.GrayText;
			this.labelDescKeepInSystemTray.Location = new System.Drawing.Point(33, 142);
			this.labelDescKeepInSystemTray.Name = "labelDescKeepInSystemTray";
			this.labelDescKeepInSystemTray.Size = new System.Drawing.Size(307, 14);
			this.labelDescKeepInSystemTray.Text = "Run from a tray icon; closing hides it to the tray.";
			//
			// labelDescCheckForUpdates
			//
			this.labelDescCheckForUpdates.AutoSize = false;
			this.labelDescCheckForUpdates.Font = new System.Drawing.Font("Segoe UI", 8F);
			this.labelDescCheckForUpdates.ForeColor = System.Drawing.SystemColors.GrayText;
			this.labelDescCheckForUpdates.Location = new System.Drawing.Point(33, 42);
			this.labelDescCheckForUpdates.Name = "labelDescCheckForUpdates";
			this.labelDescCheckForUpdates.Size = new System.Drawing.Size(307, 14);
			this.labelDescCheckForUpdates.Text = "Look for new launcher and Gw.exe versions at startup.";
			//
			// labelDescAutoUpdate
			//
			this.labelDescAutoUpdate.AutoSize = false;
			this.labelDescAutoUpdate.Font = new System.Drawing.Font("Segoe UI", 8F);
			this.labelDescAutoUpdate.ForeColor = System.Drawing.SystemColors.GrayText;
			this.labelDescAutoUpdate.Location = new System.Drawing.Point(33, 86);
			this.labelDescAutoUpdate.Name = "labelDescAutoUpdate";
			this.labelDescAutoUpdate.Size = new System.Drawing.Size(307, 14);
			this.labelDescAutoUpdate.Text = "Install launcher updates without asking first.";
			//
			// labelDescTimeout
			//
			this.labelDescTimeout.AutoSize = false;
			this.labelDescTimeout.Font = new System.Drawing.Font("Segoe UI", 8F);
			this.labelDescTimeout.ForeColor = System.Drawing.SystemColors.GrayText;
			this.labelDescTimeout.Location = new System.Drawing.Point(15, 52);
			this.labelDescTimeout.Name = "labelDescTimeout";
			this.labelDescTimeout.Size = new System.Drawing.Size(325, 14);
			this.labelDescTimeout.Text = "How long to wait for a mod DLL to load into the game.";
			// 
			// buttonOK
			// 
			this.buttonOK.Location = new System.Drawing.Point(216, 424);
			this.buttonOK.Name = "buttonOK";
			this.buttonOK.Size = new System.Drawing.Size(75, 28);
			this.buttonOK.TabIndex = 3;
			this.buttonOK.Text = "OK";
			this.buttonOK.UseVisualStyleBackColor = true;
			this.buttonOK.Click += new System.EventHandler(this.ButtonOK_Click);
			// 
			// buttonCancel
			// 
			this.buttonCancel.Location = new System.Drawing.Point(297, 424);
			this.buttonCancel.Name = "buttonCancel";
			this.buttonCancel.Size = new System.Drawing.Size(75, 28);
			this.buttonCancel.TabIndex = 4;
			this.buttonCancel.Text = "Cancel";
			this.buttonCancel.UseVisualStyleBackColor = true;
			this.buttonCancel.Click += new System.EventHandler(this.ButtonCancel_Click);
			// 
			// SettingsForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(384, 464);
			this.Controls.Add(this.buttonCancel);
			this.Controls.Add(this.buttonOK);
			this.Controls.Add(this.groupBoxAdvanced);
			this.Controls.Add(this.groupBoxUpdates);
			this.Controls.Add(this.groupBoxGeneral);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "SettingsForm";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "GW Launcher - Settings";
			this.groupBoxGeneral.ResumeLayout(false);
			this.groupBoxGeneral.PerformLayout();
			this.groupBoxUpdates.ResumeLayout(false);
			this.groupBoxUpdates.PerformLayout();
			this.groupBoxAdvanced.ResumeLayout(false);
			this.groupBoxAdvanced.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownTimeout)).EndInit();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.GroupBox groupBoxGeneral;
		private System.Windows.Forms.CheckBox checkBoxLaunchMinimized;
		private System.Windows.Forms.CheckBox checkBoxKeepInSystemTray;
		private System.Windows.Forms.Label labelPassword;
		private System.Windows.Forms.TextBox textBoxPassword;
		private System.Windows.Forms.CheckBox checkBoxProtectAccounts;
		private System.Windows.Forms.CheckBox checkBoxShowPassword;
		private System.Windows.Forms.GroupBox groupBoxUpdates;
		private System.Windows.Forms.CheckBox checkBoxAutoUpdate;
		private System.Windows.Forms.CheckBox checkBoxCheckForUpdates;
		private System.Windows.Forms.GroupBox groupBoxAdvanced;
		private System.Windows.Forms.NumericUpDown numericUpDownTimeout;
		private System.Windows.Forms.Label labelTimeout;
		private System.Windows.Forms.Label labelDescProtectAccounts;
		private System.Windows.Forms.Label labelDescLaunchMinimized;
		private System.Windows.Forms.Label labelDescKeepInSystemTray;
		private System.Windows.Forms.Label labelDescCheckForUpdates;
		private System.Windows.Forms.Label labelDescAutoUpdate;
		private System.Windows.Forms.Label labelDescTimeout;
		private System.Windows.Forms.Button buttonOK;
		private System.Windows.Forms.Button buttonCancel;
	}
}
