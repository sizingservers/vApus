﻿/*
 * Copyright 2010 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using FastColoredTextBoxNS;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using vApus.SolutionTree;
using vApus.Util;

namespace vApus.Stresstest {
    public partial class ConnectionProxyCodeView : BaseSolutionComponentView {
        [DllImport("user32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int LockWindowUpdate(int hWnd);

        #region Fields

        private readonly ConnectionProxyCode _connectionProxyCode;

        private bool _codeInitialized;
        private CSharpTextStyle _csharpTextStyle;
        private int _previousSplitterDistance;

        #endregion

        #region Constructor

        /// <summary>
        ///     Designer time only constructor
        /// </summary>
        public ConnectionProxyCodeView() {
            InitializeComponent();
        }

        public ConnectionProxyCodeView(SolutionComponent solutionComponent, params object[] args)
            : base(solutionComponent, args) {
            InitializeComponent();

            _connectionProxyCode = solutionComponent as ConnectionProxyCode;
            if (IsHandleCreated)
                SetGui();
            else
                HandleCreated += ConnectionView_HandleCreated;
            TextChanged += ConnectionProxyCodeView_TextChanged;
        }

        private void ConnectionProxyCodeView_TextChanged(object sender, EventArgs e) {
            TextChanged -= ConnectionProxyCodeView_TextChanged;
            Text = "Connection Proxy Code (" + (_connectionProxyCode.Parent as LabeledBaseItem).Label + ")";
            TextChanged += ConnectionProxyCodeView_TextChanged;
        }

        #endregion

        #region Functions

        private void ConnectionView_HandleCreated(object sender, EventArgs e) {
            HandleCreated -= ConnectionView_HandleCreated;
            SetGui();
        }

        private void SetGui() {
            _csharpTextStyle = new CSharpTextStyle(codeTextBox);
            codeTextBox.Text = (_connectionProxyCode.Parent as ConnectionProxy).BuildConnectionProxyClass();
            codeTextBox.ClearUndo();

            references.CodeTextBox = codeTextBox;
            find.CodeTextBox = codeTextBox;
            compile.ConnectionProxyCode = _connectionProxyCode;

            codeTextBox.TextChangedDelayed += codeTextBox_TextChangedDelayed;
        }

        private void codeTextBox_TextChangedDelayed(object sender, TextChangedEventArgs e) {
            if (_codeInitialized) {
                if (_connectionProxyCode.Code != codeTextBox.Text) {
                    _connectionProxyCode.Code = codeTextBox.Text;
                    _connectionProxyCode.InvokeSolutionComponentChangedEvent(
                        SolutionComponentChangedEventArgs.DoneAction.Edited);
                }
            } else {
                _codeInitialized = true;
            }
        }

        #region Tools

        private void find_FoundButtonClicked(object sender, FindAndReplace.FoundReplacedButtonClickedEventArgs e) {
            codeTextBox.ClearSelection();
            codeTextBox.SelectLine(e.LineNumber);
        }

        private void compile_CompileError(object sender, EventArgs e) {
            tcTools.SelectedIndex = 2;
        }

        private void compile_CompileErrorButtonClicked(object sender, Compile.CompileErrorButtonClickedEventArgs e) {
            codeTextBox.ClearSelection();
            codeTextBox.SelectLine(e.LineNumber);
        }

        private void btnExport_Click(object sender, EventArgs e) {
            if (sfd.ShowDialog() == DialogResult.OK) {
                using (var sw = new StreamWriter(sfd.FileName))
                    sw.Write(codeTextBox.Text);
#pragma warning disable 0168
                if (
                    MessageBox.Show("Do you want to open the file?", string.Empty, MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question) == DialogResult.Yes)
                    try {
                        Process.Start(sfd.FileName);
                    } catch (FileNotFoundException fnfe) {
                        MessageBox.Show("Could not open the file!\nThe file could not be found.", string.Empty,
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    } catch {
                        MessageBox.Show(
                            "Could not open the file!\nNo Application is associated with the 'cs' extension.",
                            string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
            }
        }

        private void btnCollapseExpand_Click(object sender, EventArgs e) {
            if (btnCollapseExpand.Text == "-") {
                btnCollapseExpand.Text = "+";
                _previousSplitterDistance = splitCode.SplitterDistance;
                splitCode.SplitterDistance = splitCode.Height - 23;
                splitCode.IsSplitterFixed = true;

                tcTools.Hide();
            } else {
                btnCollapseExpand.Text = "-";
                splitCode.SplitterDistance = _previousSplitterDistance;
                splitCode.IsSplitterFixed = false;

                tcTools.Show();
            }
        }

        private void ConnectionProxyCodeView_KeyPress(object sender, KeyPressEventArgs e) {
            if (e.KeyChar == (char)6) tcTools.SelectedIndex = 1;
        }

        #endregion

        #endregion
    }
}