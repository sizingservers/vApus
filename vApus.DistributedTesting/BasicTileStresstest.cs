﻿/*
 * Copyright 2012 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using vApus.SolutionTree;
using vApus.Stresstest;
using vApus.Util;

namespace vApus.DistributedTesting {
    public class BasicTileStresstest : BaseItem {

        #region Fields
        protected internal Connection _connection;
        private Monitor.MonitorProject _monitorProject;
        private int[] _monitorIndices = { };
        private Monitor.Monitor[] _monitors = { };

        private int[] _slaveIndices = { };
        private Slave[] _slaves = { };
        private int[] _WorkDistribution = { };

        //This is only set when this is null or a solutioncomponent changed event is invoked.
        private List<Slave> _cachedSlavesParent = new List<Slave>();

        #endregion

        #region Properties
        [Description("The connection to the application to test.")]
        [PropertyControl(0), SavableCloneable]
        public Connection Connection {
            get {
                if (_connection != null) {
                    if (_connection.IsEmpty)
                        Connection = SolutionComponent.GetNextOrEmptyChild(typeof(Stresstest.Connection), Solution.ActiveSolution.GetSolutionComponent(typeof(Stresstest.Connections))) as Stresstest.Connection;

                    _connection.SetDescription("The connection to the application to test. [" + ConnectionProxy + "]");
                }
                return _connection;
            }
            set {
                value.ParentIsNull -= _connection_ParentIsNull;
                _connection = value;
                _connection.ParentIsNull += _connection_ParentIsNull;
            }
        }
        [ReadOnly(true)]
        [DisplayName("Connection Proxy")]
        public string ConnectionProxy {
            get {
                if (_connection == null || _connection.IsEmpty || _connection.ConnectionProxy.IsEmpty)
                    return "Connection Proxy: <none>";
                return _connection.ConnectionProxy.ToString();
            }
        }
        [SavableCloneable]
        public int[] MonitorIndices {
            get { return _monitorIndices; }
            set {
                if (value == null)
                    throw new ArgumentNullException("Can be empty but not null.");

                _monitorIndices = value;
            }
        }
        [PropertyControl(1)]
        public Monitor.Monitor[] Monitors {
            get {
                if (_monitors.Length != _monitorIndices.Length && _monitorProject != null) {
                    List<Monitor.Monitor> l = new List<Monitor.Monitor>(_monitorIndices.Length);
                    foreach (int index in _monitorIndices)
                        if (index < _monitorProject.Count) {
                            var monitor = _monitorProject[index] as Monitor.Monitor;
                            if (!l.Contains(monitor))
                                l.Add(monitor);
                        }

                    _monitors = l.ToArray();
                }
                _monitors.SetParent(_monitorProject);

                return _monitors;
            }
            set {
                if (value == null)
                    throw new ArgumentNullException("Can be empty but not null.");
                if (value.Length > 5)
                    throw new ArgumentOutOfRangeException("Maximum 5 allowed.");

                _monitors = value;
                if (_monitorProject != null) {
                    _monitors.SetParent(_monitorProject);

                    List<int> l = new List<int>(_monitors.Length);
                    for (int index = 0; index != _monitorProject.Count; index++)
                        if (_monitors.Contains(_monitorProject[index]) && !l.Contains(index))
                            l.Add(index);

                    _monitorIndices = l.ToArray();
                }
            }
        }
        [SavableCloneable]
        public int[] SlaveIndices {
            get { return _slaveIndices; }
            set {
                if (value == null)
                    throw new ArgumentNullException("Can be empty but not null.");

                _slaveIndices = value;
            }
        }
        [PropertyControl(2)]
        [Description("Currently limited to one (only the first one counts). More than one slave will be handy in the future for many-to-one testing.")]
        public Slave[] Slaves {
            get {
                if (_cachedSlavesParent.Count == 0) FillAndGetSlavesParent();
                if (_cachedSlavesParent.Count == 0) {
                    var emptySlaves = new Slave[0];
                    emptySlaves.SetParent(_cachedSlavesParent);
                    return emptySlaves;
                }
                if (_slaves.Length != _slaveIndices.Length) {
                    var l = new List<Slave>(_slaveIndices.Length);
                    foreach (int index in _slaveIndices)
                        if (index < _cachedSlavesParent.Count) {
                            var slave = _cachedSlavesParent[index] as Slave;
                            if (!l.Contains(slave)) l.Add(slave);
                        }

                    Slaves = l.ToArray();
                }
                var currentParent = _slaves.GetParent();
                if (currentParent == null || !currentParent.Equals(_cachedSlavesParent)) _slaves.SetParent(_cachedSlavesParent);
                return _slaves;
            }
            set {
                if (value == null) throw new ArgumentNullException("Can be empty but not null.");

                _slaves = value;

                if (_cachedSlavesParent.Count == 0) FillAndGetSlavesParent();
                if (_cachedSlavesParent.Count != 0) {
                    _slaves.SetParent(_cachedSlavesParent);

                    var l = new List<int>(_slaves.Length);
                    for (int index = 0; index != _cachedSlavesParent.Count; index++)
                        if (_slaves.Contains(_cachedSlavesParent[index]) && !l.Contains(index))
                            l.Add(index);

                    _slaveIndices = l.ToArray();
                }
            }
        }
        internal List<Slave> FillAndGetSlavesParent() {
            try {
                if (this.Parent != null &&
                    this.Parent.GetParent() != null &&
                    this.Parent.GetParent().GetParent() != null &&
                    this.Parent.GetParent().GetParent().GetParent() != null) {
                    _cachedSlavesParent.Clear();
                    Clients clientsAndSlaves = (this.Parent.GetParent().GetParent().GetParent() as DistributedTest).Clients;

                    foreach (Client client in clientsAndSlaves)
                        foreach (Slave slave in client)
                            _cachedSlavesParent.Add(slave);


                }
            } catch {
                _cachedSlavesParent.Clear();
            }
            return _cachedSlavesParent;
        }
        [SavableCloneable]
        public int[] WorkDistribution {
            get { return _WorkDistribution; }
            set { _WorkDistribution = value; }
        }
        #endregion

        #region Constructor
        public BasicTileStresstest() {
            ShowInGui = false;
            if (Solution.ActiveSolution != null)
                Init();
            else
                Solution.ActiveSolutionChanged += new EventHandler<ActiveSolutionChangedEventArgs>(Solution_ActiveSolutionChanged);
        }
        #endregion

        #region Functions
        private void Solution_ActiveSolutionChanged(object sender, ActiveSolutionChangedEventArgs e) {
            Solution.ActiveSolutionChanged -= Solution_ActiveSolutionChanged;
            Init();
        }
        private void Init() {
            Connection = SolutionComponent.GetNextOrEmptyChild(typeof(Stresstest.Connection), Solution.ActiveSolution.GetSolutionComponent(typeof(Stresstest.Connections))) as Stresstest.Connection;
            _monitorProject = Solution.ActiveSolution.GetSolutionComponent(typeof(Monitor.MonitorProject)) as Monitor.MonitorProject;

            _monitors = new Monitor.Monitor[0];
            _slaves = new Slave[0];

            SolutionComponentChanged += new EventHandler<SolutionComponentChangedEventArgs>(SolutionComponentChanged_SolutionComponentChanged);
        }
        private void _connection_ParentIsNull(object sender, EventArgs e) {
            if (_connection == sender)
                Connection = SolutionComponent.GetNextOrEmptyChild(typeof(Stresstest.Connection), Solution.ActiveSolution.GetSolutionComponent(typeof(Stresstest.Connections))) as Stresstest.Connection;
        }
        private void SolutionComponentChanged_SolutionComponentChanged(object sender, SolutionComponentChangedEventArgs e) {
            //Cleanup _monitors if _monitorProject Changed
            if (sender == _monitorProject || sender is Monitor.Monitor) {
                List<Monitor.Monitor> l = new List<Monitor.Monitor>(_monitorProject.Count);
                foreach (Monitor.Monitor monitor in _monitors)
                    if (!l.Contains(monitor) && _monitorProject.Contains(monitor))
                        l.Add(monitor);

                Monitors = l.ToArray();
            } else if (sender is Clients || sender is Client || sender is Slave || sender is BasicTileStresstest || sender is Tile || sender is DistributedTest) //Cleanup slaves
            {
                FillAndGetSlavesParent();
                if (_cachedSlavesParent.Count != 0 && (sender == _cachedSlavesParent || sender is Client || sender is Slave)) {
                    List<Slave> l = new List<Slave>(_cachedSlavesParent.Count);
                    foreach (Slave slave in _slaves)
                        if (!l.Contains(slave) && _cachedSlavesParent.Contains(slave))
                            l.Add(slave);

                    Slaves = l.ToArray();
                }
            }
        }

        /// <summary>
        /// Create clone of this.f
        /// </summary>
        /// <returns></returns>
        public BasicTileStresstest Clone() {
            var clone = new BasicTileStresstest();
            clone.Connection = _connection;
            clone.MonitorIndices = new int[_monitorIndices.Length];
            _monitorIndices.CopyTo(clone.MonitorIndices, 0);
            clone.SlaveIndices = new int[_slaveIndices.Length];
            _slaveIndices.CopyTo(clone.SlaveIndices, 0);
            return clone;
        }
        #endregion
    }
}