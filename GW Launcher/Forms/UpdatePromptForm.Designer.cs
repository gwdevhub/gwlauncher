namespace GW_Launcher.Forms
{
    partial class UpdatePromptForm
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
            labelMessage = new Label();
            labelChangelog = new Label();
            textBoxChangelog = new TextBox();
            linkLabelRelease = new LinkLabel();
            buttonUpdate = new Button();
            buttonNotNow = new Button();
            SuspendLayout();
            //
            // labelMessage
            //
            labelMessage.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelMessage.Location = new Point(12, 12);
            labelMessage.Name = "labelMessage";
            labelMessage.Size = new Size(460, 50);
            labelMessage.TabIndex = 0;
            labelMessage.Text = "A different build of GW Launcher is available.";
            //
            // labelChangelog
            //
            labelChangelog.AutoSize = true;
            labelChangelog.Location = new Point(12, 68);
            labelChangelog.Name = "labelChangelog";
            labelChangelog.Size = new Size(74, 15);
            labelChangelog.TabIndex = 1;
            labelChangelog.Text = "What's new:";
            //
            // textBoxChangelog
            //
            textBoxChangelog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBoxChangelog.BackColor = SystemColors.Window;
            textBoxChangelog.Location = new Point(12, 86);
            textBoxChangelog.Multiline = true;
            textBoxChangelog.Name = "textBoxChangelog";
            textBoxChangelog.ReadOnly = true;
            textBoxChangelog.ScrollBars = ScrollBars.Vertical;
            textBoxChangelog.Size = new Size(460, 220);
            textBoxChangelog.TabIndex = 2;
            textBoxChangelog.TabStop = false;
            textBoxChangelog.Enter += TextBoxChangelog_Enter;
            //
            // linkLabelRelease
            //
            linkLabelRelease.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            linkLabelRelease.AutoSize = true;
            linkLabelRelease.Location = new Point(12, 320);
            linkLabelRelease.Name = "linkLabelRelease";
            linkLabelRelease.Size = new Size(130, 15);
            linkLabelRelease.TabIndex = 3;
            linkLabelRelease.TabStop = true;
            linkLabelRelease.Text = "View release on GitHub";
            linkLabelRelease.LinkClicked += LinkLabelRelease_LinkClicked;
            //
            // buttonUpdate
            //
            buttonUpdate.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonUpdate.DialogResult = DialogResult.Yes;
            buttonUpdate.Location = new Point(291, 314);
            buttonUpdate.Name = "buttonUpdate";
            buttonUpdate.Size = new Size(88, 27);
            buttonUpdate.TabIndex = 4;
            buttonUpdate.Text = "Update";
            buttonUpdate.UseVisualStyleBackColor = true;
            //
            // buttonNotNow
            //
            buttonNotNow.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonNotNow.DialogResult = DialogResult.No;
            buttonNotNow.Location = new Point(384, 314);
            buttonNotNow.Name = "buttonNotNow";
            buttonNotNow.Size = new Size(88, 27);
            buttonNotNow.TabIndex = 5;
            buttonNotNow.Text = "Not now";
            buttonNotNow.UseVisualStyleBackColor = true;
            //
            // UpdatePromptForm
            //
            AcceptButton = buttonUpdate;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = buttonNotNow;
            ClientSize = new Size(484, 353);
            Controls.Add(buttonNotNow);
            Controls.Add(buttonUpdate);
            Controls.Add(linkLabelRelease);
            Controls.Add(textBoxChangelog);
            Controls.Add(labelChangelog);
            Controls.Add(labelMessage);
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new Size(400, 300);
            Name = "UpdatePromptForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "GW Launcher";
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelMessage;
        private System.Windows.Forms.Label labelChangelog;
        private System.Windows.Forms.TextBox textBoxChangelog;
        private System.Windows.Forms.LinkLabel linkLabelRelease;
        private System.Windows.Forms.Button buttonUpdate;
        private System.Windows.Forms.Button buttonNotNow;
    }
}
