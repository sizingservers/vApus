﻿/*
 * Copyright 2010 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using vApus.SolutionTree;
using vApus.Stresstest;
using vApus.Util;

namespace vApus.DistributedTesting
{
    public class DistributedTestCore : IDisposable
    {
        #region Fields
        private object _lock = new object();

        private Stopwatch _sw = new Stopwatch();

        private DistributedTest _distributedTest;
        private MasterSideCommunicationHandler _masterCommunication = new MasterSideCommunicationHandler();
        private List<TileStresstest> _usedTileStresstests = new List<TileStresstest>();
        private bool _isDisposed;

        private volatile string[] _ok = new string[] { };
        private volatile string[] _cancelled = new string[] { };
        private volatile string[] _failed = new string[] { };

        private volatile string[] _runInitialized = new string[] { };
        private volatile string[] _runDoneOnce = new string[] { };

        //Invoke only once
        private bool _finishedInvoked = false;
        #endregion

        #region Properties
        public IEnumerable<TileStresstest> UsedTileStresstests
        {
            get
            {
                foreach (TileStresstest tileStresstest in _usedTileStresstests)
                    yield return tileStresstest;
            }
        }
        public bool IsDisposed
        {
            get { return _isDisposed; }
        }
        
        public int OK
        {
            get { return _ok.Length; }
        }
        public int Cancelled
        {
            get { return _cancelled.Length; }
        }
        public int Failed
        {
            get { return _failed.Length; }
        }

        public int RunInitializedCount
        {
            get { return _runInitialized.Length; }
        }
        public int RunDoneOnceCount
        {
            get { return _runDoneOnce.Length; }
        }
        /// <summary>
        /// The stresstests that are not finished.
        /// </summary>
        public int Running
        {
            get { return _usedTileStresstests.Count - Finished; }
        }
        public int Finished
        {
            get { return OK + Cancelled + Failed; }
        }
        #endregion

        #region Con-/Destructor
        //Only one test can run at the same time, if this is called and another test (stresstest core or distributed test core) exists (not disposed) an argument out of range exception will be thrown.
        public DistributedTestCore(DistributedTest distributedTest)
        {
            StaticObjectServiceWrapper.ObjectService.MaxSuscribers = 1;
            StaticObjectServiceWrapper.ObjectService.Suscribe(this);
            
            _distributedTest = distributedTest;
            _masterCommunication.ListeningError += new EventHandler<ListeningErrorEventArgs>(_masterCommunication_ListeningError);
            _masterCommunication.OnPushMessageReceived += new EventHandler<PushMessageReceivedEventArgs>(_masterCommunication_OnPushMessageReceived);
        }
        ~DistributedTestCore()
        {
            Dispose();
        }
        #endregion

        #region Functions
        /// <summary>
        /// Connects + sends tests
        /// </summary>
        public void Initialize()
        {
            InvokeMessage("Initializing the Test.");
            Connect();
            SendAndReceiveInitializeTest();
        }
        private void Connect()
        {
            InvokeMessage("Connecting slaves...");
            _sw.Start();
            _usedTileStresstests.Clear();
            foreach (BaseItem item in _distributedTest.Tiles)
            {
                Tile tile = item as Tile;
                if (tile.Use)
                    foreach (BaseItem childItem in tile)
                    {
                        TileStresstest tileStresstest = childItem as TileStresstest;
                        if (tileStresstest.Use)
                        {
                            Exception exception;
#warning Allow multiple slaves to be able to distribute work.
                            int processID;
                            _masterCommunication.ConnectSlave(tileStresstest.BasicTileStresstest.Slaves[0].IP, tileStresstest.BasicTileStresstest.Slaves[0].Port, out processID, out exception);
                            if (exception == null)
                            {
                                _usedTileStresstests.Add(tileStresstest);
                                InvokeMessage(string.Format("|->Connected {0} - {1}", tileStresstest.Parent, tileStresstest));
                            }
                            else
                            {
                                Dispose();
                                Exception ex = new Exception(string.Format("Could not connect to one of the slaves ({0} - {1})!{2}{3}", tileStresstest.Parent, tileStresstest, Environment.NewLine, exception.ToString()));
                                InvokeMessage(ex.ToString(), LogLevel.Error);
                                throw ex;
                            }
                        }
                    }
            }
            if (_usedTileStresstests.Count == 0)
            {
                Exception ex = new Exception("Please use at least one test!");
                InvokeMessage(ex.ToString(), LogLevel.Warning);
                throw ex;
            }
            _sw.Stop();
            InvokeMessage(string.Format(" ...Connected slaves in {0}", _sw.Elapsed.ToLongFormattedString()));
            _sw.Reset();
        }
        private void SendAndReceiveInitializeTest()
        {
            InvokeMessage("Initializing test on slaves...");
            _sw.Start();
            Parallel.ForEach(_usedTileStresstests, delegate(TileStresstest tileStresstest)
            {
                Exception exception;
                _masterCommunication.InitializeTest(tileStresstest, _distributedTest.RunSynchronization, out exception);
                if (exception == null)
                {
                    InvokeMessage(string.Format("|->Initialized {0} - {1}", tileStresstest.Parent, tileStresstest));
                }
                else
                {
                    Dispose();
                    Exception ex = new Exception(string.Format("Could not initialize {0} - {1}.{2}{3}", tileStresstest.Parent, tileStresstest, Environment.NewLine, exception.ToString()));
                    InvokeMessage(ex.ToString(), LogLevel.Error);
                    throw ex;
                }
            });
            _sw.Stop();
            InvokeMessage(string.Format(" ...Test initialized in {0}", _sw.Elapsed.ToLongFormattedString()));
            _sw.Reset();
        }
        public void Start()
        {
            InvokeMessage("Starting the test...");
            Exception exception;
            _masterCommunication.StartTest(out exception);

            if (exception == null)
            {
                InvokeMessage(" ...Started!", Color.LightGreen);
            }
            else
            {
                Dispose();
                InvokeMessage(exception.ToString(), LogLevel.Error);
                throw exception;
            }

            if (exception != null)
            {
                Dispose();
                Exception ex = new Exception(string.Format("Could not start the Distributed Test.{0}{1}", Environment.NewLine, exception.ToString()));
                InvokeMessage(ex.ToString(), LogLevel.Error);
                throw ex;
            }
        }

        private void _masterCommunication_ListeningError(object sender, ListeningErrorEventArgs e)
        {
            lock (_lock)
            {
                if (!_isDisposed && Finished < _usedTileStresstests.Count)
                {
                    try
                    {
#warning allow multiple slaves yadda yadda
                        foreach (TileStresstest tileStresstest in _usedTileStresstests)
                            if (tileStresstest.BasicTileStresstest.Slaves[0].IP == e.SlaveIP && tileStresstest.BasicTileStresstest.Slaves[0].Port == e.SlavePort)
                                _failed = AddUniqueToStringArray(_failed, tileStresstest.TileStresstestIndex);

                        InvokeOnListeningError(e);

                        if (RunInitializedCount == Running)
                        {
                            _runInitialized = new string[] { };
                            _masterCommunication.SendContinue();
                        }
                        else if (RunDoneOnceCount == Running)
                        {
                            _runDoneOnce = new string[] { };
                            _masterCommunication.SendContinue();
                        }
                        else if (_distributedTest.RunSynchronization ==
                            RunSynchronization.BreakOnFirstFinished && RunDoneOnceCount == Finished + 1)
                        {
                            _masterCommunication.SendBreak();
                        }


                        if (Finished == _usedTileStresstests.Count)
                            InvokeOnFinished();
                    }
                    catch { }
                }
            }
        }
        private string[] AddUniqueToStringArray(string[] arr, string item)
        {
            foreach (string s in arr)
                if (s == item)
                    return arr;

            string[] newArr = new string[arr.Length + 1];
            for (int index = 0; index != arr.Length; index++)
                newArr[index] = arr[index];
            newArr[arr.Length] = item;

            return newArr;
        }
        private void _masterCommunication_OnPushMessageReceived(object sender, PushMessageReceivedEventArgs e)
        {
            lock (_lock)
            {
                try
                {
                    var pushMessage = e.PushMessage;

                    bool okCancelError = true;
                    switch (pushMessage.StresstestResult)
                    {
                        case StresstestResult.Busy:
                            okCancelError = false;
                            break;
                        case StresstestResult.Ok:
                            _ok = AddUniqueToStringArray(_ok, pushMessage.TileStresstestIndex);
                            break;
                        case StresstestResult.Cancelled:
                            _cancelled = AddUniqueToStringArray(_cancelled, pushMessage.TileStresstestIndex);
                            break;
                        case StresstestResult.Error:
                            _failed = AddUniqueToStringArray(_failed, pushMessage.TileStresstestIndex);
                            break;
                    }


                    //Check if it is needed to do anything
                    if (Running != 0)
                    {
                        //Thread this as stopped.
                        if (okCancelError)
                            _runDoneOnce = AddUniqueToStringArray(_runDoneOnce, pushMessage.TileStresstestIndex);

                        switch (_distributedTest.RunSynchronization)
                        {
                            case RunSynchronization.None:
                                break;

                            //Send Break, wait for all stopped, send continue, wait for all started, send continue
                            case RunSynchronization.BreakOnFirstFinished:
                                if (pushMessage.RunStateChange == RunStateChange.ToRunInitializedFirstTime)
                                {
                                    _runInitialized = AddUniqueToStringArray(_runInitialized, pushMessage.TileStresstestIndex);
                                    if (RunInitializedCount == Running)
                                    {
                                        _runInitialized = new string[] { };
                                        _masterCommunication.SendContinue();
                                    }
                                }
                                else if (pushMessage.RunStateChange == RunStateChange.ToRunDoneOnce)
                                {
                                    _runDoneOnce = AddUniqueToStringArray(_runDoneOnce, pushMessage.TileStresstestIndex);
                                    if (RunDoneOnceCount == _usedTileStresstests.Count)
                                    {
                                        _runDoneOnce = new string[] { };
                                        //Increment the index here to be able to continue to the next run.
                                        _masterCommunication.SendContinue();
                                    }
                                    else if (RunDoneOnceCount == 1)
                                    {
                                        //Break the tests that are still in the previous run
                                        _masterCommunication.SendBreak();
                                    }
                                }
                                break;

                            //Wait for all stopped, send continue, wait for all started, send continue
                            case RunSynchronization.BreakOnLastFinished:
                                if (pushMessage.RunStateChange == RunStateChange.ToRunInitializedFirstTime)
                                {
                                    _runInitialized = AddUniqueToStringArray(_runInitialized, pushMessage.TileStresstestIndex);
                                    if (RunInitializedCount == Running)
                                    {
                                        _runInitialized = new string[] { };
                                        _masterCommunication.SendContinue();
                                    }
                                }
                                else if (pushMessage.RunStateChange == RunStateChange.ToRunDoneOnce)
                                {
                                    _runDoneOnce = AddUniqueToStringArray(_runDoneOnce, pushMessage.TileStresstestIndex);
                                    if (RunDoneOnceCount == Running)
                                    {
                                        _runDoneOnce = new string[] { };
                                        _masterCommunication.SendBreak();
                                    }
                                }
                                break;
                        }
                    }
                    InvokeOnPushMessageReceived(pushMessage);

                    if (Finished == _usedTileStresstests.Count)
                    {
                        StaticObjectServiceWrapper.ObjectService.Unsuscribe(this);
                        InvokeOnFinished();
                    }
                }
                catch { }
            }
        }
        public void Stop()
        {
            InvokeMessage("Stopping the test...");
#warning Handle exception
            Exception ex;
            _masterCommunication.StopTest(out ex);

            InvokeMessage("Test Cancelled!", LogLevel.Warning);
        }
        /// <summary>
        /// This will reopen the connection if needed, get the results and close the connection again.
        /// </summary>
        /// <returns></returns>
        public List<ResultsMessage> GetResults()
        {
            InvokeMessage("Getting results (it can take a minute or two until transmission begins)...");
#warning Handle Exception
            Exception ex;
            return _masterCommunication.GetResults(out ex);
        }


        /// <summary>
        /// Stops the seeding at the torrent server.
        /// </summary>
        public void StopSeedingResults(TileStresstest tileStresstest, string torrentName)
        {
#warning implement this.
            //lock (this)
            //{
            //    Exception ex;
            //    _masterCommunication.StopSeedingResults(tileStresstest, torrentName, out ex);
            //    _masterCommunication.DisconnectSlave(tileStresstest.SlaveIP, tileStresstest.SlavePort);
            //}
        }
        public void Dispose()
        {
            if (!_isDisposed)
            {
                try
                {
                    _isDisposed = true;
                    _masterCommunication.Dispose();
                    _masterCommunication = null;
                    _usedTileStresstests = null;
                    _sw = null;
                }
                catch { }

                StaticObjectServiceWrapper.ObjectService.Unsuscribe(this);
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// Messages to the user on the message textbox
        /// </summary>
        public event EventHandler<MessageEventArgs> Message;
        public event EventHandler<PushMessageReceivedEventArgs> OnPushMessageReceived;
        public event EventHandler<ListeningErrorEventArgs> OnListeningError;
        public event EventHandler<FinishedEventArgs> OnFinished;

        private void InvokeMessage(string message, LogLevel logLevel = LogLevel.Info)
        {
            InvokeMessage(message, Color.Empty, logLevel);
        }
        private void InvokeMessage(string message, Color color, LogLevel logLevel = LogLevel.Info)
        {
            LogWrapper.LogByLevel(message, logLevel);
            if (Message != null)
            {
                if (logLevel == LogLevel.Error)
                {
                    string[] split = message.Split(new char[] { '\n', '\r' }, StringSplitOptions.None);
                    message = split[0] + "\n See " + Logger.DEFAULT_LOCATION + " for the stack trace.";
                }
                SynchronizationContextWrapper.SynchronizationContext.Send(delegate { Message(this, new MessageEventArgs(message, color, logLevel)); });
            }
        }
        private void InvokeOnPushMessageReceived(PushMessage pushMessage)
        {
            if (OnPushMessageReceived != null)
                SynchronizationContextWrapper.SynchronizationContext.Send(delegate { OnPushMessageReceived(this, new PushMessageReceivedEventArgs(pushMessage)); });
        }
        private void InvokeOnListeningError(ListeningErrorEventArgs listeningErrorEventArgs)
        {
            LogWrapper.LogByLevel(listeningErrorEventArgs, LogLevel.Error);
            if (OnListeningError != null)
                SynchronizationContextWrapper.SynchronizationContext.Send(delegate { OnListeningError(this, listeningErrorEventArgs); });
        }
        private void InvokeOnFinished()
        {
            if (!_finishedInvoked)
            {
                LogWrapper.Log("Test finished!");
                if (OnFinished != null)
                    SynchronizationContextWrapper.SynchronizationContext.Send(delegate { OnFinished(this, new FinishedEventArgs(OK, Cancelled, Failed)); });
            }
        }
        #endregion
    }
}
