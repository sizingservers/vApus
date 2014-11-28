﻿namespace vApus.Stresstest {
    partial class ExportToExcelDialog {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ExportToExcelDialog));
            this.btnExportToExcel = new System.Windows.Forms.Button();
            this.lblDescription = new System.Windows.Forms.Label();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.pictureBox2 = new System.Windows.Forms.PictureBox();
            this.label1 = new System.Windows.Forms.Label();
            this.pnlBorderStresstest = new System.Windows.Forms.Panel();
            this.cboStresstest = new System.Windows.Forms.ComboBox();
            this.saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            this.tvw = new System.Windows.Forms.TreeView();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
            this.pnlBorderStresstest.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnExportToExcel
            // 
            this.btnExportToExcel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnExportToExcel.AutoSize = true;
            this.btnExportToExcel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnExportToExcel.BackColor = System.Drawing.SystemColors.Control;
            this.btnExportToExcel.FlatAppearance.BorderColor = System.Drawing.Color.DimGray;
            this.btnExportToExcel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExportToExcel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnExportToExcel.Location = new System.Drawing.Point(555, 584);
            this.btnExportToExcel.MaximumSize = new System.Drawing.Size(9999, 24);
            this.btnExportToExcel.Name = "btnExportToExcel";
            this.btnExportToExcel.Size = new System.Drawing.Size(117, 24);
            this.btnExportToExcel.TabIndex = 1;
            this.btnExportToExcel.Text = "Export to Excel...";
            this.btnExportToExcel.UseVisualStyleBackColor = false;
            this.btnExportToExcel.Click += new System.EventHandler(this.btnExportToExcel_Click);
            // 
            // lblDescription
            // 
            this.lblDescription.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblDescription.AutoSize = true;
            this.lblDescription.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDescription.Location = new System.Drawing.Point(12, 13);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(503, 16);
            this.lblDescription.TabIndex = 8;
            this.lblDescription.Text = "Charts and data will be exported to one or more Excel files in a newly created zi" +
    "p file.";
            // 
            // pictureBox2
            // 
            this.pictureBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.pictureBox2.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox2.Image")));
            this.pictureBox2.Location = new System.Drawing.Point(15, 588);
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.Size = new System.Drawing.Size(16, 16);
            this.pictureBox2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.pictureBox2.TabIndex = 18;
            this.pictureBox2.TabStop = false;
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(37, 588);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(119, 16);
            this.label1.TabIndex = 17;
            this.label1.Text = "Select a Stresstest";
            // 
            // pnlBorderStresstest
            // 
            this.pnlBorderStresstest.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlBorderStresstest.BackColor = System.Drawing.Color.Silver;
            this.pnlBorderStresstest.Controls.Add(this.cboStresstest);
            this.pnlBorderStresstest.Location = new System.Drawing.Point(162, 584);
            this.pnlBorderStresstest.Name = "pnlBorderStresstest";
            this.pnlBorderStresstest.Size = new System.Drawing.Size(332, 23);
            this.pnlBorderStresstest.TabIndex = 0;
            // 
            // cboStresstest
            // 
            this.cboStresstest.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cboStresstest.BackColor = System.Drawing.Color.White;
            this.cboStresstest.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboStresstest.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cboStresstest.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboStresstest.FormattingEnabled = true;
            this.cboStresstest.Location = new System.Drawing.Point(1, 1);
            this.cboStresstest.Margin = new System.Windows.Forms.Padding(3, 4, 3, 3);
            this.cboStresstest.Name = "cboStresstest";
            this.cboStresstest.Size = new System.Drawing.Size(330, 21);
            this.cboStresstest.TabIndex = 0;
            this.cboStresstest.SelectedIndexChanged += new System.EventHandler(this.cboStresstest_SelectedIndexChanged);
            // 
            // saveFileDialog
            // 
            this.saveFileDialog.Filter = "Zip file|*.zip";
            this.saveFileDialog.Title = "Give the name of the zip file that will hold the Excel files. Those files will be" +
    " prefixed with the given name.";
            // 
            // tvw
            // 
            this.tvw.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tvw.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.tvw.CheckBoxes = true;
            this.tvw.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tvw.Location = new System.Drawing.Point(15, 60);
            this.tvw.Name = "tvw";
            this.tvw.PathSeparator = "\'/\'";
            this.tvw.Size = new System.Drawing.Size(657, 518);
            this.tvw.TabIndex = 25;
            this.tvw.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.tvw_AfterCheck);
            // 
            // ExportToExcelDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(684, 619);
            this.Controls.Add(this.tvw);
            this.Controls.Add(this.pictureBox2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.pnlBorderStresstest);
            this.Controls.Add(this.lblDescription);
            this.Controls.Add(this.btnExportToExcel);
            this.DataBindings.Add(new System.Windows.Forms.Binding("Tag", global::vApus.Stresstest.Properties.Settings.Default, "SelectedGoals", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ExportToExcelDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Tag = global::vApus.Stresstest.Properties.Settings.Default.SelectedGoals;
            this.Text = "Export to Excel";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ExportToExcelDialog_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
            this.pnlBorderStresstest.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnExportToExcel;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.PictureBox pictureBox2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel pnlBorderStresstest;
        private System.Windows.Forms.ComboBox cboStresstest;
        private System.Windows.Forms.SaveFileDialog saveFileDialog;
        private System.Windows.Forms.TreeView tvw;
    }
}