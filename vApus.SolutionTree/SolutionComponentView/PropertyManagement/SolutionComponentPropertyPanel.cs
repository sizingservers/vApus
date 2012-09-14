﻿/*
 * Copyright 2012 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using vApus.Util;
using System.ComponentModel;

namespace vApus.SolutionTree
{
    /// <summary>
    /// This is a standard panel to edit a property of the types: string, char, bool, all numeric types and array or list of those.
    /// Furthermore, a single object having a parent of the type IEnumerable (GetParent() in vApus.Util.ObjectExtension), can be displayed also.
    /// The type of the property must be one of the above, or else an exception will be thrown. 
    /// Or else you can always make your own control derived from "BaseSolutionComponentPropertyControl".
    /// The value of the property may not be null or an exception will be thrown.
    /// </summary>
    public partial class SolutionComponentPropertyPanel : ValueControlPanel
    {

        #region Fields
        private SolutionComponent _solutionComponent;
        private bool _solutionComponentTypeChanged;
        //Can be sorted, kept here.
        private List<PropertyInfo> _properties;

        private LinkLabel _showHideAdvancedSettings = new LinkLabel();
        private bool _showAdvancedSettings = false;
        #endregion

        [DefaultValue(true)]
        public new bool AutoSelectControl
        {
            get { return base.AutoSelectControl; }
            set { base.AutoSelectControl = value; }
        }

        /// <summary>
        /// Set the gui if the panel is empty.
        /// </summary>
        public SolutionComponent SolutionComponent
        {
            get { return _solutionComponent; }
            set
            {
                if (_solutionComponent != value)
                {
                    this.ValueChanged -= SolutionComponentPropertyPanel_ValueChanged;

                    _solutionComponentTypeChanged = _solutionComponent == null || _solutionComponent.GetType() != value.GetType();
                    _solutionComponent = value;
                    SetGui(true);
                    _solutionComponentTypeChanged = false;

                    this.ValueChanged += SolutionComponentPropertyPanel_ValueChanged;
                }
            }
        }

        #region Constructors
        /// <summary>
        /// This is a standard panel to edit a property of the types: string, char, bool, all numeric types and array or list of those.
        /// Furthermore, a single object having a parent of the type IEnumerable (GetParent() in vApus.Util.ObjectExtension), can be displayed also.
        /// The type of the property must be one of the above, or else an exception will be thrown. 
        /// Or else you can always make your own control derived from "BaseSolutionComponentPropertyControl".
        /// The value of the property may not be null or an exception will be thrown.
        /// </summary>
        public SolutionComponentPropertyPanel()
        {
            InitializeComponent();

            _showHideAdvancedSettings.Text = "Show/Hide Advanced Settings";
            _showHideAdvancedSettings.AutoSize = true;
            _showHideAdvancedSettings.Click += new EventHandler(_showHideAdvancedSettings_Click);
            _showHideAdvancedSettings.KeyUp += new KeyEventHandler(_showHideAdvancedSettings_KeyUp);

            if (this.IsHandleCreated)
                SetGui(true);
            else
                this.HandleCreated += new EventHandler(SolutionComponentPropertyPanel_HandleCreated);

            this.ValueChanged += new EventHandler<ValueChangedEventArgs>(SolutionComponentPropertyPanel_ValueChanged);
        }

        #endregion

        #region Functions
        private void _showHideAdvancedSettings_Click(object sender, EventArgs e)
        {
            _showAdvancedSettings = !_showAdvancedSettings;
            Refresh();
        }
        private void _showHideAdvancedSettings_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                _showAdvancedSettings = !_showAdvancedSettings;
                Refresh();
            }
        }
        private void SolutionComponentPropertyPanel_ValueChanged(object sender, ValueControlPanel.ValueChangedEventArgs e)
        {
            if (Solution.ActiveSolution != null)
                SetValue(e.Index, e.NewValue, e.OldValue, true);
        }
        private void SetValue(int index, object newValue, object oldValue, bool invokeEvent)
        {
            //Nothing can be null, this is solved this way.
            if (oldValue is BaseItem && newValue == null)
            {
                if ((oldValue as BaseItem).IsEmpty)
                    return;
                var empty = BaseItem.Empty(oldValue.GetType(), oldValue.GetParent() as SolutionComponent);
                empty.SetParent(oldValue.GetParent());
                _properties[index].SetValue(_solutionComponent, empty, null);
            }
            else
            {
                _properties[index].SetValue(_solutionComponent, newValue, null);
            }
            //Very needed, for when leaving when disposed, or key up == enter while creating.
            if (invokeEvent)
                try
                {
                    //var attributes = _propertyInfo.GetCustomAttributes(typeof(SavableCloneableAttribute), true);
                    //if (attributes.Length != 0)
                    _solutionComponent.InvokeSolutionComponentChangedEvent(SolutionComponentChangedEventArgs.DoneAction.Edited);
                }
                catch { }
        }
        private void SolutionComponentPropertyPanel_HandleCreated(object sender, EventArgs e)
        {
            SetGui(true);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="collapse">not on refresh</param>
        private void SetGui(bool collapse)
        {
            if (_solutionComponent != null && IsHandleCreated)
            {
                bool locked = _locked;
                bool showHideAdvancedSettingsControl = false;
                //if (_solutionComponentTypeChanged || _properties == null)
                //{
                _properties = new List<PropertyInfo>();
                foreach (PropertyInfo propertyInfo in _solutionComponent.GetType().GetProperties())
                {
                    object[] attributes = propertyInfo.GetCustomAttributes(typeof(PropertyControlAttribute), true);
                    PropertyControlAttribute propertyControlAttribute = (attributes.Length == 0) ? null : (attributes[0] as PropertyControlAttribute);
                    if (propertyControlAttribute != null)
                        if (propertyControlAttribute.AdvancedProperty)
                        {
                            showHideAdvancedSettingsControl = true;
                            if (_showAdvancedSettings) //Show advanced settings only if chosen to.
                                _properties.Add(propertyInfo);
                        }
                        else
                        {
                            _properties.Add(propertyInfo);
                        }
                }
                _properties.Sort(PropertyInfoComparer.GetInstance());
                _properties.Sort(PropertyInfoDisplayIndexComparer.GetInstance());

                BaseValueControl.Value[] values = new BaseValueControl.Value[_properties.Count];
                for (int i = 0; i != values.Length; i++)
                {
                    PropertyInfo propertyInfo = _properties[i];

                    object value = _properties[i].GetValue(_solutionComponent, null);

                    object[] attributes = propertyInfo.GetCustomAttributes(typeof(DisplayNameAttribute), true);
                    string label = (attributes.Length != 0) ? (attributes[0] as DisplayNameAttribute).DisplayName : propertyInfo.Name;

                    //for dynamic descriptions you can choose to call SetDescription however usage of the description attribute is adviced.
                    string description = value.GetDescription();
                    if (description == null)
                    {
                        attributes = propertyInfo.GetCustomAttributes(typeof(DescriptionAttribute), true);
                        description = (attributes.Length != 0) ? (attributes[0] as DescriptionAttribute).Description : string.Empty;
                    }

                    attributes = propertyInfo.GetCustomAttributes(typeof(ReadOnlyAttribute), true);
                    bool isReadOnly = !propertyInfo.CanWrite || (attributes.Length > 0 && (attributes[0] as ReadOnlyAttribute).IsReadOnly);

                    attributes = propertyInfo.GetCustomAttributes(typeof(SavableCloneableAttribute), true);
                    bool isEncrypted = (attributes.Length != 0 && (attributes[0] as SavableCloneableAttribute).Encrypt);


                    values[i] = new BaseValueControl.Value { __Value = value, Description = description, IsEncrypted = isEncrypted, IsReadOnly = isReadOnly, Label = label };
                }

                base.SetValues(values);

                if (_locked)
                    base.Lock();

                if (showHideAdvancedSettingsControl)
                    this.Controls.Add(_showHideAdvancedSettings);
                //}
                //else //Recycle controls
                //{
                //    object[] values = new object[_properties.Count];
                //    for (int i = 0; i != values.Length; i++)
                //    {
                //        values[i] = _properties[i].GetValue(_solutionComponent, null);
                //        base.SetDescriptionAt(i, values[i].GetDescription());
                //    }

                //   base.Set__Values(collapse, values);
                //}
            }
        }
        /// <summary>
        /// This is used in the solution component view manager, please implement this always.
        /// </summary>
        public override void Refresh()
        {
            base.Refresh();
            SetGui(false);
        }
        #endregion
    }
}
