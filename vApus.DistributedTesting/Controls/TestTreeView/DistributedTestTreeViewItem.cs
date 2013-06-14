﻿/*
 * Copyright 2012 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using vApus.DistributedTesting.Properties;
using vApus.SolutionTree;
using vApus.Stresstest;

namespace vApus.DistributedTesting {
    [ToolboxItem(false)]
    public partial class DistributedTestTreeViewItem : UserControl, ITreeViewItem {
        #region Events

        /// <summary>
        ///     Call unfocus for the other items in the panel.
        /// </summary>
        public event EventHandler AfterSelect;

        public event EventHandler AddTileClicked;

        #endregion

        #region Fields

        private readonly DistributedTest _distributedTest = new DistributedTest();

        /// <summary>
        ///     Check if the ctrl key is pressed.
        /// </summary>
        private bool _ctrl;

        private DistributedTestMode _distributedTestMode;

        private bool _testStarted;

        #endregion

        #region Properties

        public DistributedTest DistributedTest {
            get { return _distributedTest; }
        }

        #endregion

        #region Constructor

        public DistributedTestTreeViewItem() {
            InitializeComponent();
        }

        public DistributedTestTreeViewItem(DistributedTest distributedTest)
            : this() {
            _distributedTest = distributedTest;

            chkUseRDP.Checked = _distributedTest.UseRDP;
            chkUseRDP.CheckedChanged += chkUseRDP_CheckedChanged;

            cboRunSync.SelectedIndex = (int)distributedTest.RunSynchronization;
            cboRunSync.SelectedIndexChanged += cboRunSync_SelectedIndexChanged;
        }

        #endregion

        #region Functions

        public void Unfocus() {
            BackColor = Color.Transparent;
        }

        public void SetVisibleControls() {
        }

        public void SetVisibleControls(bool visible) {
        }

        public void RefreshGui() {
        }

        public void SetDistributedTestMode(DistributedTestMode distributedTestMode) {
            _distributedTestMode = distributedTestMode;
            if (_distributedTestMode == DistributedTestMode.Edit) {
                chkUseRDP.Enabled =
                    pnlRunSync.Enabled =
                    picAddTile.Visible = true;

                picStresstestStatus.Visible = false;
            } else {
                chkUseRDP.Enabled =
                    pnlRunSync.Enabled =
                    picAddTile.Visible = false;

                _testStarted = false;

                picStresstestStatus.Image = Resources.Wait;
                toolTip.SetToolTip(picStresstestStatus, string.Empty);

                picStresstestStatus.Visible = true;
            }
        }
        /// <summary>
        /// Only call this if the tile stresstest has monitors.
        /// </summary>
        public void SetMonitoringBeforeAfter() {
            picStresstestStatus.Image = Resources.Busy;
            toolTip.SetToolTip(picStresstestStatus, "Busy Monitoring");
        }

        private void chkUseRDP_CheckedChanged(object sender, EventArgs e) {
            _distributedTest.UseRDP = chkUseRDP.Checked;
            _distributedTest.InvokeSolutionComponentChangedEvent(SolutionComponentChangedEventArgs.DoneAction.Edited);
        }

        private void cboRunSync_SelectedIndexChanged(object sender, EventArgs e) {
            _distributedTest.RunSynchronization = (RunSynchronization)cboRunSync.SelectedIndex;
            _distributedTest.InvokeSolutionComponentChangedEvent(SolutionComponentChangedEventArgs.DoneAction.Edited);
        }

        private void _Enter(object sender, EventArgs e) {
            BackColor = SystemColors.Control;
            if (AfterSelect != null)
                AfterSelect(this, null);
        }

        private void picAddTile_Click(object sender, EventArgs e) {
            Focus();
            if (AddTileClicked != null)
                AddTileClicked(this, null);
        }

        private void _KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.ControlKey)
                _ctrl = true;
        }

        private void _KeyUp(object sender, KeyEventArgs e) {
            if (_ctrl && e.KeyCode == Keys.I && AddTileClicked != null)
                AddTileClicked(this, null);
        }

        public void SetStresstestStarted() {
            //Keep the running if this code at a minimum
            if (!_testStarted) {
                _testStarted = true;

                picStresstestStatus.Image = Resources.Busy;
                foreach (Tile t in _distributedTest.Tiles)
                    if (t.Use)
                        foreach (TileStresstest ts in t)
                            if (ts.Use && ts.BasicTileStresstest.MonitorIndices.Length != 0) {
                                toolTip.SetToolTip(picStresstestStatus, "Busy Stresstesting and Monitoring");
                                return;
                            }

                toolTip.SetToolTip(picStresstestStatus, "Busy Stresstesting");
            }
        }

        #endregion
    }
}