﻿/*
 * Copyright 2012 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;

namespace vApus.Util
{
    public partial class EnumValueControl : BaseValueControl, IValueControl
    {
        public EnumValueControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// This inits the control with event handling.
        /// </summary>
        /// <param name="value"></param>
        public void Init(BaseValueControl.Value value)
        {
            base.__Value = value;

            //Only take the value into account, the other properties are taken care off.
            //Keep control recycling in mind.
            ComboBox cbo = null;

            if (base.ValueControl == null)
            {
                cbo = new ComboBox();
                cbo.Dock = DockStyle.Fill;
                cbo.DropDownStyle = ComboBoxStyle.DropDownList;
                cbo.FlatStyle = FlatStyle.Flat;
                cbo.BackColor = Color.White;

                cbo.SelectedIndexChanged += new EventHandler(cbo_SelectedIndexChanged);
                cbo.Leave += new EventHandler(cbo_Leave);
                cbo.KeyUp += new KeyEventHandler(cbo_KeyUp);
            }
            else
            {
                cbo = base.ValueControl as ComboBox;
            }

            cbo.SelectedIndexChanged -= cbo_SelectedIndexChanged;
            //Extract all the values.
            Type valueType = value.GetType();
            foreach (Enum e in Enum.GetValues(valueType))
            {
                //The description value will be used instead of the tostring of the enum, if any.
                DescriptionAttribute[] attr = valueType.GetField(e.ToString()).GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];
                cbo.Items.Add(attr.Length != 0 ? attr[0].Description : e.ToString());
            }

            DescriptionAttribute[] attr2 = valueType.GetField(value.ToString()).GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];
            cbo.SelectedItem = attr2.Length > 0 ? attr2[0].Description : value.ToString();

            cbo.SelectedIndexChanged += cbo_SelectedIndexChanged;
        }
        private void cbo_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox cbo = base.ValueControl as ComboBox;
            base.HandleValueChanged(ExtractValue(cbo));
        }
        private void cbo_KeyUp(object sender, KeyEventArgs e)
        {
            ComboBox cbo = base.ValueControl as ComboBox;
            base.HandleKeyUp(e.KeyCode, ExtractValue(cbo));
        }
        private void cbo_Leave(object sender, EventArgs e)
        {
            ComboBox cbo = base.ValueControl as ComboBox;
            base.HandleValueChanged(ExtractValue(cbo));
        }

        private object ExtractValue(ComboBox cbo)
        {
            Type valueType = base.__Value.GetType();
            foreach (Enum e in Enum.GetValues(valueType))
            {
                DescriptionAttribute[] attr = valueType.GetField(e.ToString()).GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[];
                if (cbo.SelectedItem.ToString() == (attr.Length != 0 ? attr[0].Description : e.ToString()))
                    return e;
            }
            return null;
        }
    }
}