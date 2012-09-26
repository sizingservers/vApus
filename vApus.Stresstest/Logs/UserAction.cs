﻿/*
 * Copyright 2009 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using vApus.SolutionTree;
using vApus.Util;

namespace vApus.Stresstest
{
    [DisplayName("User Action"), Serializable]
    public class UserAction : LabeledBaseItem
    {
        #region Fields
        private int _occurance = 1;
        private bool _pinned;

        #endregion

        #region Properties
        [ReadOnly(true)]
        [SavableCloneable]
        [Description("How many times this user action occures in the log. Action and Log Entry Distribution in the stresstest determines how this value will be used.")]
        public int Occurance
        {
            get { return _occurance; }
            set
            {
                if (_occurance < 0)
                    throw new ArgumentOutOfRangeException("occurance");
                _occurance = value;
            }
        }
        [ReadOnly(true)]
        [SavableCloneable]
        [Description("To pin this user action in place.")]
        public bool Pinned
        {
            get { return _pinned; }
            set { _pinned = value; }
        }
        #endregion

        #region Constructors
        public UserAction()
            : base()
        {
            ShowInGui = false;
        }
        public UserAction(string label)
            : this()
        {
            Label = label;
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="beginTokenDelimiter">Needed to dermine parameter tokens</param>
        /// <param name="endTokenDelimiter">Needed to dermine parameter tokens</param>
        /// <param name="chosenNextValueParametersForLScope">Can be an empty hash set but may not be null, used to store all these values for the right scope.</param>
        /// <param name="generateWhileTestingParameterTokens">The tokens and parameters that must generate values while stresstesting.</param>
        /// <returns></returns>
        internal List<StringTree> GetParameterizedStructure(string beginTokenDelimiter, string endTokenDelimiter,
                                                                   HashSet<BaseParameter> chosenNextValueParametersForLScope,
                                                                   Dictionary<string, BaseParameter> generateWhileTestingParameterTokens)
        {
            List<StringTree> parameterizedStructure = new List<StringTree>();
            HashSet<BaseParameter> chosenNextValueParametersForUAScope = new HashSet<BaseParameter>();

            foreach (LogEntry logEntry in this)
                parameterizedStructure.Add(logEntry.GetParameterizedStructure(beginTokenDelimiter, endTokenDelimiter,
                    chosenNextValueParametersForLScope, chosenNextValueParametersForUAScope,
                    generateWhileTestingParameterTokens));
            return parameterizedStructure;
        }

        public UserAction Clone(bool setParent = true)
        {
            UserAction userAction = new UserAction(Label);
            if (setParent)
                userAction.Parent = Parent;
            userAction.Occurance = _occurance;
            userAction.Pinned = _pinned;

            foreach (LogEntry entry in this)
                userAction.AddWithoutInvokingEvent(entry.Clone(), false);

            return userAction;
        }
    }
}
