/*
 * Copyright 2007 (c) Blancke Karen, Cavaliere Leandro, Kets Brecht, Vandroemme Dieter
 * Technical University Kortrijk, department PIH
 *  
 * Author(s):
 *    Vandroemme Dieter
 */
using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using vApus.Util;

namespace vApus.Monitor
{
    public partial class ConfigurationDialog : Form
    {
        #region Fields
        // The XmlDocument where the formatted configuration is loaded into.
        private XmlDocument _configuration;
        private StringBuilder _sb = new StringBuilder();
        #endregion

        #region Constructor
        public ConfigurationDialog(string configuration)
        {
            InitializeComponent();
            LoadConfiguration(configuration);
        }
        #endregion

        #region Functions
        private void LoadConfiguration(string configuration)
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;
                
                StringReader stringReader = new StringReader(configuration);
                _configuration = new XmlDocument();
                
                try { _configuration.Load(stringReader); }
                catch { throw; }
                finally { stringReader.Close(); }

                foreach (XmlNode node in _configuration.ChildNodes)
                {
                    if (node.Name != null && node.NodeType != XmlNodeType.Text && node.Name != "xml")
                    {
                        TreeNode treeNode = new TreeNode();
                        treeNode.Text = node.Name;
                        foreach (XmlAttribute attribute in node.Attributes)
                            treeNode.Text += " " + attribute.Name + "= " + attribute.Value;

                        tv.Nodes.Add(treeNode);
                        if (node.HasChildNodes)
                        {
                            if (node.FirstChild.NodeType == XmlNodeType.Text)
                                treeNode.Tag = node.FirstChild.Value;
                            AddNodesToTreeView(node, treeNode);
                        }
                    }
                }
                if (tv.Nodes.Count != 0)
                {
                    tv.SelectedNode = tv.Nodes[0];
                    tv.SelectedNode.Expand();
                }

                this.Cursor = Cursors.Default;
            }
            catch (Exception ex)
            {
                LogWrapper.LogByLevel("[" + this + "] " + "The configuration is not a wellformed xml.\n" + ex.ToString(), LogLevel.Error);
                this.Close();
            }
        }
        private void AddNodesToTreeView(XmlNode xmlNode, TreeNode treeNode)
        {
            foreach (XmlNode node in xmlNode.ChildNodes)
            {
                if (node.Name != null && node.NodeType != XmlNodeType.Text)
                {
                    TreeNode tn = new TreeNode();
                    tn.Text = node.Name;
                    foreach (XmlAttribute attribute in node.Attributes)
                        tn.Text += " " + attribute.Name + "= " + attribute.Value;

                    treeNode.Nodes.Add(tn);
                    if (xmlNode.HasChildNodes && xmlNode.ChildNodes.Count > 0)
                    {
                        if (node.FirstChild != null && node.FirstChild.NodeType == XmlNodeType.Text)
                            tn.Tag = node.FirstChild.Value;
                        AddNodesToTreeView(node, tn);
                    }
                }
            }
        }
        private void tv_AfterSelect(object sender, TreeViewEventArgs e)
        {
            this.Cursor = Cursors.WaitCursor;
            rtxt.Text = "";
            FillRtxt(tv.SelectedNode, 0);
            this.Cursor = Cursors.Default;
        }
        private void FillRtxt(TreeNode node, int indent)
        {
            string spaces = new string(' ', indent * 2);
            rtxt.Text += spaces + node.Text.ToUpper();
            if (node.Tag != null)
                rtxt.Text += ": " + node.Tag.ToString() + "\n";
            else
                rtxt.Text += "\n";
            indent++;

            foreach (TreeNode childnode in node.Nodes)
                FillRtxt(childnode, indent);
        }
        private void btnSave_Click(object sender, EventArgs e)
        {
            if (sfd.ShowDialog() == DialogResult.OK)
                _configuration.Save(sfd.FileName);
        }
        #endregion
    }
}