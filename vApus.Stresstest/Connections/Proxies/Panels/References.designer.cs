﻿namespace vApus.Stresstest
{
    partial class References
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
            this.components = new System.ComponentModel.Container();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.openFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.lvwCustomReferences = new System.Windows.Forms.ListView();
            this.clm = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.lblDefaultReferences = new System.Windows.Forms.Label();
            this.btnAdd = new System.Windows.Forms.Button();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            // 
            // btnBrowse
            // 
            this.btnBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowse.AutoSize = true;
            this.btnBrowse.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnBrowse.BackColor = System.Drawing.Color.White;
            this.btnBrowse.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnBrowse.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnBrowse.Location = new System.Drawing.Point(329, 139);
            this.btnBrowse.MaximumSize = new System.Drawing.Size(1000, 24);
            this.btnBrowse.MinimumSize = new System.Drawing.Size(75, 0);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(75, 24);
            this.btnBrowse.TabIndex = 2;
            this.btnBrowse.Text = "Browse...";
            this.btnBrowse.UseVisualStyleBackColor = false;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // btnRemove
            // 
            this.btnRemove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRemove.AutoSize = true;
            this.btnRemove.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnRemove.BackColor = System.Drawing.Color.White;
            this.btnRemove.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRemove.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRemove.Location = new System.Drawing.Point(410, 139);
            this.btnRemove.MaximumSize = new System.Drawing.Size(1000, 24);
            this.btnRemove.MinimumSize = new System.Drawing.Size(75, 0);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(75, 24);
            this.btnRemove.TabIndex = 3;
            this.btnRemove.Text = "Remove";
            this.btnRemove.UseVisualStyleBackColor = false;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // openFileDialog
            // 
            this.openFileDialog.Filter = "Dll files|*.dll";
            this.openFileDialog.Multiselect = true;
            // 
            // lvwCustomReferences
            // 
            this.lvwCustomReferences.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lvwCustomReferences.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.clm});
            this.lvwCustomReferences.FullRowSelect = true;
            this.lvwCustomReferences.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.lvwCustomReferences.HideSelection = false;
            this.lvwCustomReferences.Location = new System.Drawing.Point(15, 25);
            this.lvwCustomReferences.Name = "lvwCustomReferences";
            this.lvwCustomReferences.Size = new System.Drawing.Size(470, 108);
            this.lvwCustomReferences.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.lvwCustomReferences.TabIndex = 0;
            this.lvwCustomReferences.UseCompatibleStateImageBehavior = false;
            this.lvwCustomReferences.View = System.Windows.Forms.View.Details;
            // 
            // clm
            // 
            this.clm.Width = 451;
            // 
            // lblDefaultReferences
            // 
            this.lblDefaultReferences.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblDefaultReferences.AutoSize = true;
            this.lblDefaultReferences.Location = new System.Drawing.Point(12, 9);
            this.lblDefaultReferences.Name = "lblDefaultReferences";
            this.lblDefaultReferences.Size = new System.Drawing.Size(292, 13);
            this.lblDefaultReferences.TabIndex = 6;
            this.lblDefaultReferences.Text = "System.dll;System.Data.dll;vApus.Util.dll;vApus.Stresstest.dll;";
            // 
            // btnAdd
            // 
            this.btnAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAdd.AutoSize = true;
            this.btnAdd.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnAdd.BackColor = System.Drawing.Color.White;
            this.btnAdd.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAdd.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnAdd.Location = new System.Drawing.Point(248, 139);
            this.btnAdd.MaximumSize = new System.Drawing.Size(1000, 24);
            this.btnAdd.MinimumSize = new System.Drawing.Size(75, 0);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(75, 24);
            this.btnAdd.TabIndex = 1;
            this.btnAdd.Text = "Add...";
            this.toolTip.SetToolTip(this.btnAdd, "From Gac");
            this.btnAdd.UseVisualStyleBackColor = false;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // References
            // 
            this.Controls.Add(this.lblDefaultReferences);
            this.Controls.Add(this.lvwCustomReferences);
            this.Controls.Add(this.btnRemove);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.btnBrowse);
            this.Name = "References";
            this.Padding = new System.Windows.Forms.Padding(9);
            this.Size = new System.Drawing.Size(497, 174);
            this.VisibleChanged += new System.EventHandler(this.References_VisibleChanged);
            this.Resize += new System.EventHandler(this.References_Resize);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.OpenFileDialog openFileDialog;
        private System.Windows.Forms.ListView lvwCustomReferences;
        private System.Windows.Forms.ColumnHeader clm;
        private System.Windows.Forms.Label lblDefaultReferences;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.ToolTip toolTip;

    }
}