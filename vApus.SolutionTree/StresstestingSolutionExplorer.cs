﻿/*
 * Copyright 2009 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using vApus.Util;
using WeifenLuo.WinFormsUI.Docking;

namespace vApus.SolutionTree
{
    public partial class StresstestingSolutionExplorer : DockablePanel
    {
        #region Fields
        private Keys _hotkey = Keys.None;
        #endregion

        #region Constructor
        public StresstestingSolutionExplorer()
        {
            InitializeComponent();
            if (this.IsHandleCreated)
                Init();
            else
                this.HandleCreated += new EventHandler(StresstestingSolutionExplorer_HandleCreated);
        }
        #endregion

        #region Functions
        private void StresstestingSolutionExplorer_HandleCreated(object sender, EventArgs e)
        {
            Init();
        }
        private void Init()
        {
            Solution.ActiveSolutionChanged += new EventHandler<ActiveSolutionChangedEventArgs>(Solution_ActiveSolutionChanged);
            SolutionComponent.SolutionComponentChanged += new EventHandler<SolutionComponentChangedEventArgs>(SolutionComponent_SolutionComponentChanged);
        }
        #region SolutionEventHandling
        private void Solution_ActiveSolutionChanged(object sender, ActiveSolutionChangedEventArgs e)
        {
            if (e.ToBeLoaded)
            {
                tvw.Nodes.Clear();
                tvw.Nodes.AddRange(Solution.ActiveSolution.GetTreeNodes().ToArray());

                //Applying images/expanding.
                foreach (TreeNode node in tvw.Nodes)
                    RefreshTreeNode(node);

                tvw.Select();
                tvw.SelectedNode = tvw.Nodes[0];
            }
        }
        private void SetTreeNodeImage(TreeNode node)
        {
            SolutionComponent component = node.Tag as SolutionComponent;
            string componentTypeName = component.GetType().Name;
            if (!tvw.ImageList.Images.Keys.Contains(componentTypeName))
            {
                Image image = component.GetImage();
                if (image != null)
                    tvw.ImageList.Images.Add(componentTypeName, image);
            }
            node.ImageIndex = tvw.ImageList.Images.Keys.IndexOf(componentTypeName);
            node.SelectedImageIndex = node.ImageIndex;
        }
        private void SolutionComponent_SolutionComponentChanged(object sender, SolutionComponentChangedEventArgs e)
        {
            SolutionComponent solutionComponent = sender as SolutionComponent;
            TreeNode node;
            switch (e.__DoneAction)
            {
                case SolutionComponentChangedEventArgs.DoneAction.Added:
                    node = FindNodeByTag(solutionComponent);
                    List<TreeNode> childNodes = solutionComponent.GetChildNodes();
                    if (childNodes.Count != 0)
                        //Added one?
                        if ((bool)e.Arg)
                        {
                            TreeNode childNode = childNodes[childNodes.Count - 1];
                            node.Nodes.Add(childNode);
                            childNode.ExpandAll();
                            RefreshTreeNode(node);
                            tvw.SelectedNode = childNode;
                            if (childNode.Tag is LabeledBaseItem && (childNode.Tag as LabeledBaseItem).Label.Length == 0)
                                try
                                {
                                    childNode.BeginEdit();
                                }
                                catch
                                {
                                    //ignore
                                }
                        }
                        else
                        {
                            node.Nodes.Clear();
                            node.Nodes.AddRange(childNodes.ToArray());
                            RefreshTreeNode(node);
                        }
                    break;
                case SolutionComponentChangedEventArgs.DoneAction.Edited:
                    tvw.SelectedNode = FindNodeByTag(solutionComponent);
                    RefreshTreeNode(tvw.SelectedNode);
                    break;
                case SolutionComponentChangedEventArgs.DoneAction.Cleared:
                    node = FindNodeByTag(solutionComponent);
                    if (node != null)
                    {
                        node.Nodes.Clear();
                        RefreshTreeNode(node);
                    }
                    break;
                case SolutionComponentChangedEventArgs.DoneAction.Removed:
                    node = FindNodeByTag(e.Arg as SolutionComponent);
                    if (node != null)
                    {
                        TreeNode parentNode = node.Parent;
                        tvw.Nodes.Remove(node);
                        RefreshTreeNode(parentNode);
                    }
                    break;
            }
        }
        public TreeNode FindNodeByTag(SolutionComponent tag)
        {
            TreeNode foundNode = null;
            foreach (TreeNode node in tvw.Nodes)
            {
                foundNode = node.Tag == tag ? node : FindChildNodeByTag(node, tag);
                if (foundNode != null)
                    break;
            }
            return foundNode;
        }
        public TreeNode FindChildNodeByTag(TreeNode parentNode, SolutionComponent tag)
        {
            TreeNode foundNode = null;
            foreach (TreeNode node in parentNode.Nodes)
            {
                foundNode = node.Tag == tag ? node : FindChildNodeByTag(node, tag);
                if (foundNode != null)
                    break;
            }
            return foundNode;
        }
        #endregion
        /// <summary>
        /// Returns true or false if it was able to refresh the selected node (false if being null that is).
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private bool RefreshTreeNode(TreeNode node)
        {
            //Node == null can occur when closing a solution (the leave from commmon property controls will result in calling this method).
            if (node != null)
            {
                bool succes = true;
                node.Text = node.Tag.ToString();
                SetTreeNodeImage(node);
                node.Expand();
                foreach (TreeNode childNode in node.Nodes)
                    if (!RefreshChildNode(childNode))
                        succes = false;
                return succes;
            }
            return false;
        }
        private bool RefreshChildNode(TreeNode childNode)
        {
            if (childNode != null)
            {
                childNode.Text = childNode.Tag.ToString();
                SetTreeNodeImage(childNode);
                foreach (TreeNode node in childNode.Nodes)
                    RefreshChildNode(node);
                return true;
            }
            return false;
        }

        #region LabelEdit
        private void tvw_BeforeLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (e.Node.Tag is LabeledBaseItem)
            {
                tvw.BeforeLabelEdit -= tvw_BeforeLabelEdit;
                tvw.AfterLabelEdit -= tvw_AfterLabelEdit;
                e.Node.Text = (e.Node.Tag as LabeledBaseItem).Label;
                Thread t = new Thread(NodeBeginEdit);
                t.IsBackground = true;
                t.Start(e.Node as object);
            }
            else
            {
                e.CancelEdit = true;
            }
        }
        private void NodeBeginEdit(object o)
        {
            SynchronizationContextWrapper.SynchronizationContext.Send(delegate
            {
                try
                {
                    (o as TreeNode).BeginEdit();
                }
                catch
                {
                    TreeNode node = o as TreeNode;
                    LabeledBaseItem item = node.Tag as LabeledBaseItem;
                    node.Text = item.ToString();
                }
                tvw.AfterLabelEdit += tvw_AfterLabelEdit;
                tvw.BeforeLabelEdit += tvw_BeforeLabelEdit;
            }, null);
        }
        private void tvw_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (e.Node.Tag is LabeledBaseItem)
            {
                Thread t = new Thread(AfterEdit);
                t.IsBackground = true;
                t.Start(e.Node as object);
            }
            else
            {
                e.CancelEdit = true;
            }
        }
        private void AfterEdit(object o)
        {
            SynchronizationContextWrapper.SynchronizationContext.Send(delegate
            {
                try
                {
                    TreeNode node = o as TreeNode;
                    LabeledBaseItem item = node.Tag as LabeledBaseItem;
                    if (item.Label != node.Text)
                    {
                        try
                        {
                            item.Label = node.Text;
                            if (RefreshTreeNode(tvw.SelectedNode))
                            {
                                SolutionComponent.SolutionComponentChanged -= SolutionComponent_SolutionComponentChanged;
                                item.InvokeSolutionComponentChangedEvent(SolutionComponentChangedEventArgs.DoneAction.Edited);
                                SolutionComponent.SolutionComponentChanged += SolutionComponent_SolutionComponentChanged;
                            }
                        }
                        catch { }
                    }
                    //A leave and enter will otherwise lead in resulting a wrong label of the node.
                    node.Text = item.ToString();
                }
                catch { }
            }, null);
        }

        private void tvw_Leave(object sender, EventArgs e)
        {
            tvw.LabelEdit = false;
        }

        private void tvw_Enter(object sender, EventArgs e)
        {
            tvw.LabelEdit = true;
        }
        #endregion

        #region Hotkeys
        private void tvw_KeyDown(object sender, KeyEventArgs e)
        {
            if (tvw.SelectedNode != null)
            {
                if (e.KeyCode == (Keys.Control & Keys.C) || e.KeyCode == (Keys.ControlKey & Keys.C))
                { }
                //Modifiers, this was a hard one.
                switch (e.KeyCode)
                {
                    case Keys.ControlKey:
                        _hotkey = _hotkey | Keys.Control;
                        break;
                    case Keys.ShiftKey:
                        _hotkey = _hotkey | Keys.Shift;
                        break;
                    default:
                        _hotkey |= e.KeyCode;
                        break;
                }
                //Handle rename immediatly
                if (_hotkey == Keys.F2)
                    tvw.SelectedNode.BeginEdit();
                else
                    (tvw.SelectedNode.Tag as SolutionComponent).HandleHotkey(_hotkey);
            }
        }
        private void tvw_KeyUp(object sender, KeyEventArgs e)
        {
            _hotkey = Keys.None;
        }
        #endregion

        private void tvw_DoubleClick(object sender, EventArgs e)
        {
            if (tvw.SelectedNode != null)
                (tvw.SelectedNode.Tag as SolutionComponent).HandleDoubleClick();
        }
        #endregion
    }
}
