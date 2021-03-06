﻿/*
 * 2012 Sizing Servers Lab, affiliated with IT bachelor degree NMCT
 * University College of West-Flanders, Department GKG (www.sizingservers.be, www.nmct.be, www.howest.be/en)
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using Microsoft.Win32;
using RandomUtils;
using RandomUtils.Log;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace vApus.Util {
    public partial class WindowsFirewallAutoUpdatePanel : Panel {
        public enum Status {
            AllDisabled = 0,
            WindowsFirewallEnabled = 1,
            WindowsAutoUpdateEnabled = 2,
            AllEnabled = 3
        }

        private delegate void ApplyDel();

        #region Fields
        private readonly BackgroundWorkQueue _backgroundWorkQueue = new BackgroundWorkQueue();
        private readonly ApplyDel _applyCallback;
        private Status _status;
        private bool _canCheckStatus = true;
        #endregion

        #region Properties
        [DllImport("user32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int LockWindowUpdate(IntPtr hWnd);
        public Status __Status { get { return _status; } }
        #endregion

        #region Constructor
        public WindowsFirewallAutoUpdatePanel() {
            InitializeComponent();
            _applyCallback = ApplyCallback;
            _backgroundWorkQueue.OnWorkItemProcessed += _activeObject_OnResult;
            HandleCreated += DisableFirewallAutoUpdatePanel_HandleCreated;
        }
        #endregion

        #region Functions
        private void DisableFirewallAutoUpdatePanel_HandleCreated(object sender, EventArgs e) {
            CheckStatus();
        }

        public Status CheckStatus() {
            if (!_canCheckStatus) return _status;
            _status = Status.AllDisabled;

            //Firewall
            EvaluateValue(
                Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\SharedAccess\Parameters\FirewallPolicy\DomainProfile",
                    "EnableFirewall", 0), 0, Status.WindowsFirewallEnabled);
            EvaluateValue(
                Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\SharedAccess\Parameters\FirewallPolicy\PublicProfile",
                    "EnableFirewall", 0), 0, Status.WindowsFirewallEnabled);
            EvaluateValue(
                Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\SharedAccess\Parameters\FirewallPolicy\StandardProfile",
                    "EnableFirewall", 0), 0, Status.WindowsFirewallEnabled);
            //windows update
            EvaluateValue(
                Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update",
                    "AUOptions", 1), 1, Status.WindowsAutoUpdateEnabled);

            if (Handle != null)
                SetGui();
            return _status;
        }

        private void EvaluateValue(object value, int validValue, Status append) {
            try {
                if ((int)value != validValue)
                    _status |= append;
            } catch (Exception ex) {
                Loggers.Log(Level.Error, "Failed checking if the firewall and Windows auto update are enabled or not!\nCould not find a registry key.", ex, new object[] { value, validValue, append });
            }
        }

        private void SetGui() {
            LockWindowUpdate(this.Handle);

            rdbFirewallOn.CheckedChanged -= rdb_CheckedChanged;
            rdbUpdateOn.CheckedChanged -= rdb_CheckedChanged;

            pnlFirewall.BackColor = pnlUpdate.BackColor = Color.LightGreen;
            rdbFirewallOff.Checked = rdbUpdateOff.Checked = true;
            btnDisableAll.Enabled = false;

            switch (_status) {
                case Status.WindowsFirewallEnabled:
                    pnlFirewall.BackColor = Color.Orange;
                    rdbFirewallOn.Checked = btnDisableAll.Enabled = true;
                    break;
                case Status.WindowsAutoUpdateEnabled:
                    pnlUpdate.BackColor = Color.Orange;
                    rdbUpdateOn.Checked = btnDisableAll.Enabled = true;
                    break;
                case Status.AllEnabled:
                    pnlFirewall.BackColor = pnlUpdate.BackColor = Color.Orange;
                    rdbFirewallOn.Checked = rdbUpdateOn.Checked = btnDisableAll.Enabled = true;
                    break;
            }

            rdbFirewallOn.CheckedChanged += rdb_CheckedChanged;
            rdbUpdateOn.CheckedChanged += rdb_CheckedChanged;

            LockWindowUpdate(IntPtr.Zero);
        }

        private void btnDisableAll_Click(object sender, EventArgs e) {
            rdbFirewallOn.CheckedChanged -= rdb_CheckedChanged;
            rdbUpdateOn.CheckedChanged -= rdb_CheckedChanged;

            rdbFirewallOff.Checked = rdbUpdateOff.Checked = true;

            rdbFirewallOn.CheckedChanged += rdb_CheckedChanged;
            rdbUpdateOn.CheckedChanged += rdb_CheckedChanged;

            Disable();
        }

        private void rdb_CheckedChanged(object sender, EventArgs e) {
            Disable();
        }

        private void Disable() {
            _canCheckStatus = false;
            groupBox.Enabled = false;
            btnDisableAll.Enabled = false;
            btnDisableAll.Text = "Wait...";
            _backgroundWorkQueue.EnqueueWorkItem(_applyCallback);
        }

        private void ApplyCallback() {
            try {
                //Disable or enable Windows Firewall
                int value = rdbFirewallOff.Checked ? 0 : 1;
                Registry.SetValue(
                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\SharedAccess\Parameters\FirewallPolicy\DomainProfile",
                    "EnableFirewall", value, RegistryValueKind.DWord);
                Registry.SetValue(
                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\SharedAccess\Parameters\FirewallPolicy\PublicProfile",
                    "EnableFirewall", value, RegistryValueKind.DWord);
                Registry.SetValue(
                    @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\SharedAccess\Parameters\FirewallPolicy\StandardProfile",
                    "EnableFirewall", value, RegistryValueKind.DWord);

                //Restarting the process
                StartProcess("NET", "STOP MpsSvc");
                StartProcess("NET", "START MpsSvc");
            } catch (Exception ex) {
                Loggers.Log(Level.Error, "Failed enabling or disabling the firewall!\nCould not find a registry key.", ex);
            }

            try {
                //Disabling Auto Update
                int value = rdbUpdateOff.Checked ? 1 : 4;
                Registry.SetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update",
                    "AUOptions", value, RegistryValueKind.DWord);

                //Restarting the process
                StartProcess("NET", "STOP wuauserv");
                StartProcess("NET", "START wuauserv");
            } catch (Exception ex) {
                Loggers.Log(Level.Error, "Failed enabling or disabling Windows auto update!\nCould not find a registry key.", ex);
            }
            _canCheckStatus = true;
        }

        private void _activeObject_OnResult(object sender, BackgroundWorkQueue.OnWorkItemProcessedEventArgs e) {
            SynchronizationContextWrapper.SynchronizationContext.Send(delegate {
                btnDisableAll.Text = "Disable all";
                groupBox.Enabled = true;
                CheckStatus();
            }, null);
        }

        private void StartProcess(string process, string arguments) {
            Process p = null;
            try {
                var startInfo = new ProcessStartInfo(process, arguments);
                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p = Process.Start(startInfo);
                p.WaitForExit();
            } catch {
            }
            if (p != null)
                try {
                    p.Dispose();
                } catch {
                    //Already gone?
                }
            p = null;
        }

        public override string ToString() {
            return "Windows firewall & update";
        }
        #endregion
    }
}