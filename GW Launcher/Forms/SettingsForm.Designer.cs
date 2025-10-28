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
			this.checkBoxEncrypt = new System.Windows.Forms.CheckBox();
			this.groupBoxUpdates = new System.Windows.Forms.GroupBox();
			this.checkBoxAutoUpdate = new System.Windows.Forms.CheckBox();
			this.checkBoxCheckForUpdates = new System.Windows.Forms.CheckBox();
			this.groupBoxAdvanced = new System.Windows.Forms.GroupBox();
			this.numericUpDownTimeout = new System.Windows.Forms.NumericUpDown();
			this.labelTimeout = new System.Windows.Forms.Label();
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
			this.groupBoxGeneral.Controls.Add(this.checkBoxLaunchMinimized);
			this.groupBoxGeneral.Controls.Add(this.checkBoxEncrypt);
			this.groupBoxGeneral.Location = new System.Drawing.Point(12, 12);
			this.groupBoxGeneral.Name = "groupBoxGeneral";
			this.groupBoxGeneral.Size = new System.Drawing.Size(360, 80);
			this.groupBoxGeneral.TabIndex = 0;
			this.groupBoxGeneral.TabStop = false;
			this.groupBoxGeneral.Text = "General";
			// 
			// checkBoxLaunchMinimized
			// 
			this.checkBoxLaunchMinimized.AutoSize = true;
			this.checkBoxLaunchMinimized.Location = new System.Drawing.Point(15, 48);
			this.checkBoxLaunchMinimized.Name = "checkBoxLaunchMinimized";
			this.checkBoxLaunchMinimized.Size = new System.Drawing.Size(116, 19);
			this.checkBoxLaunchMinimized.TabIndex = 1;
			this.checkBoxLaunchMinimized.Text = "Launch minimized";
			this.checkBoxLaunchMinimized.UseVisualStyleBackColor = true;
			// 
			// checkBoxEncrypt
			// 
			this.checkBoxEncrypt.AutoSize = true;
			this.checkBoxEncrypt.Location = new System.Drawing.Point(15, 22);
			this.checkBoxEncrypt.Name = "checkBoxEncrypt";
			this.checkBoxEncrypt.Size = new System.Drawing.Size(149, 19);
			this.checkBoxEncrypt.TabIndex = 0;
			this.checkBoxEncrypt.Text = "Encrypt account storage";
			this.checkBoxEncrypt.UseVisualStyleBackColor = true;
			this.checkBoxEncrypt.CheckedChanged += new System.EventHandler(this.CheckBoxEncrypt_CheckedChanged);
			// 
			// groupBoxUpdates
			// 
			this.groupBoxUpdates.Controls.Add(this.checkBoxAutoUpdate);
			this.groupBoxUpdates.Controls.Add(this.checkBoxCheckForUpdates);
			this.groupBoxUpdates.Location = new System.Drawing.Point(12, 98);
			this.groupBoxUpdates.Name = "groupBoxUpdates";
			this.groupBoxUpdates.Size = new System.Drawing.Size(360, 80);
			this.groupBoxUpdates.TabIndex = 1;
			this.groupBoxUpdates.TabStop = false;
			this.groupBoxUpdates.Text = "Updates";
			// 
			// checkBoxAutoUpdate
			// 
			this.checkBoxAutoUpdate.AutoSize = true;
			this.checkBoxAutoUpdate.Location = new System.Drawing.Point(15, 48);
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
			this.groupBoxAdvanced.Controls.Add(this.numericUpDownTimeout);
			this.groupBoxAdvanced.Controls.Add(this.labelTimeout);
			this.groupBoxAdvanced.Location = new System.Drawing.Point(12, 184);
			this.groupBoxAdvanced.Name = "groupBoxAdvanced";
			this.groupBoxAdvanced.Size = new System.Drawing.Size(360, 60);
			this.groupBoxAdvanced.TabIndex = 2;
			this.groupBoxAdvanced.TabStop = false;
			this.groupBoxAdvanced.Text = "Advanced";
			// 
			// numericUpDownTimeout
			// 
			this.numericUpDownTimeout.Location = new System.Drawing.Point(190, 23);
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
			// buttonOK
			// 
			this.buttonOK.Location = new System.Drawing.Point(216, 260);
			this.buttonOK.Name = "buttonOK";
			this.buttonOK.Size = new System.Drawing.Size(75, 28);
			this.buttonOK.TabIndex = 3;
			this.buttonOK.Text = "OK";
			this.buttonOK.UseVisualStyleBackColor = true;
			this.buttonOK.Click += new System.EventHandler(this.ButtonOK_Click);
			// 
			// buttonCancel
			// 
			this.buttonCancel.Location = new System.Drawing.Point(297, 260);
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
			this.ClientSize = new System.Drawing.Size(384, 300);
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
		private System.Windows.Forms.CheckBox checkBoxEncrypt;
		private System.Windows.Forms.GroupBox groupBoxUpdates;
		private System.Windows.Forms.CheckBox checkBoxAutoUpdate;
		private System.Windows.Forms.CheckBox checkBoxCheckForUpdates;
		private System.Windows.Forms.GroupBox groupBoxAdvanced;
		private System.Windows.Forms.NumericUpDown numericUpDownTimeout;
		private System.Windows.Forms.Label labelTimeout;
		private System.Windows.Forms.Button buttonOK;
		private System.Windows.Forms.Button buttonCancel;
	}
}
