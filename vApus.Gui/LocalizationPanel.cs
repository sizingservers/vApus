﻿/*
 * Copyright 2010 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using vApus.Gui.Properties;
using vApus.Util;

namespace vApus.Gui {
    public partial class LocalizationPanel : Panel {
        public LocalizationPanel() {
            InitializeComponent();
            HandleCreated += LocalizationPanel_HandleCreated;
        }

        private void LocalizationPanel_HandleCreated(object sender, EventArgs e) {
            if (cboCulture.Items.Count == 0)
                foreach (CultureInfo info in CultureInfo.GetCultures(CultureTypes.SpecificCultures)) {
                    object o = info.DisplayName;
                    o.SetTag(info);
                    cboCulture.Items.Add(o);
                }

            string culture = Settings.Default.Culture;
            if (culture == null || culture == string.Empty) {
                culture = Thread.CurrentThread.CurrentCulture.ToString();
                Settings.Default.Culture = culture;
                Settings.Default.Save();
            }
            foreach (Object o in cboCulture.Items)
                if (o.GetTag().ToString() == culture) {
                    cboCulture.SelectedItem = o;
                    break;
                }
        }

        private void btnSet_Click(object sender, EventArgs e) {
            var cultureInfo = cboCulture.SelectedItem.GetTag() as CultureInfo;
            //Use ISO 8601 for DateTime formatting.
            cultureInfo.DateTimeFormat.ShortDatePattern = "yyyy'-'MM'-'dd";
            cultureInfo.DateTimeFormat.LongTimePattern = "HH':'mm':'ss'.'fff";
            Thread.CurrentThread.CurrentCulture = cultureInfo;


            Settings.Default.Culture = Thread.CurrentThread.CurrentCulture.ToString();
            Settings.Default.Save();
            btnSet.Enabled = false;
        }

        private void cbo_SelectedIndexChanged(object sender, EventArgs e) {
            btnSet.Enabled = (cboCulture.SelectedItem.GetTag().ToString() != Thread.CurrentThread.CurrentCulture.ToString());
        }

        public override string ToString() {
            return "Localization";
        }
    }
}