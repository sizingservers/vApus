﻿/*
 * Copyright 2010 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using vApus.REST.Convert;
using vApus.SolutionTree;
using vApus.Stresstest;
using vApus.Util;

namespace vApus.DistributedTesting
{
    public class DistributedTestCore : IDisposable
    {
        #region Fields

        private readonly DistributedTest _distributedTest;
        private readonly object _lock = new object();

        /// <summary>
        ///     The full result path where results are saved.
        /// </summary>
        private readonly string _resultPath;

        private volatile string[] _cancelled = new string[] {};
        private object _communicationLock = new object();
        private volatile string[] _failed = new string[] {};

        //Invoke only once
        private bool _finishedHandled;
        private bool _hasResults;
        private bool _isDisposed;

        private volatile string[] _ok = new string[] {};

        /// <summary>
        ///     The paths to the results.
        /// </summary>
        private Dictionary<TileStresstest, string> _results = new Dictionary<TileStresstest, string>();

        private object _resultsLock = new object();
        private int _resultsReceived;
        private volatile string[] _runDoneOnce = new string[] {};
        private volatile string[] _runInitialized = new string[] {};
        private Stopwatch _sw = new Stopwatch();

        /// <summary>
        ///     The messages pusht from the slaves.
        /// </summary>
        private Dictionary<TileStresstest, TestProgressMessage> _testProgressMessages =
            new Dictionary<TileStresstest, TestProgressMessage>();

        private object _testProgressMessagesLock = new object();
        private List<TileStresstest> _usedTileStresstests = new List<TileStresstest>();
        private object _usedTileStresstestsLock = new object();

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

        /// <summary>
        ///     Key= index of the tile stresstest. Value = pusht progress message from slave.
        /// </summary>
        public Dictionary<TileStresstest, TestProgressMessage> TestProgressMessages
        {
            get
            {
                lock (_testProgressMessagesLock)
                    return _testProgressMessages;
            }
        }

        /// <summary>
        ///     The paths to the results.
        /// </summary>
        public Dictionary<TileStresstest, string> Results
        {
            get
            {
                lock (_resultsLock)
                    return _results;
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
        ///     The stresstests that are not finished.
        /// </summary>
        public int Running
        {
            get { return _usedTileStresstests.Count - Finished; }
        }

        public int Finished
        {
            get { return OK + Cancelled + Failed; }
        }

        /// <summary>
        ///     To check wheter you can close the distributed test view without a warning or not.
        /// </summary>
        public bool HasResults
        {
            get { return _hasResults; }
        }

        #endregion

        #region Con-/Destructor

        //Only one test can run at the same time, if this is called and another test (stresstest core or distributed test core) exists (not disposed) an argument out of range exception will be thrown.
        public DistributedTestCore(DistributedTest distributedTest)
        {
            ObjectRegistrar.MaxRegistered = 1;
            ObjectRegistrar.Register(this);

            _distributedTest = distributedTest;
            MasterSideCommunicationHandler.ListeningError += _masterCommunication_ListeningError;
            MasterSideCommunicationHandler.TestInitialized += MasterSideCommunicationHandler_TestInitialized;
            MasterSideCommunicationHandler.OnTestProgressMessageReceived +=
                _masterCommunication_OnTestProgressMessageReceived;

            //The path where results are stored.
            string subResultDir = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            _resultPath = Path.Combine(_distributedTest.ResultPath, subResultDir);

#if EnableBetaFeature
            WriteRestConfig();
#endif
        }

        ~DistributedTestCore()
        {
            Dispose();
            GC.Collect();
        }

        #endregion

        #region Functions

        public void Dispose()
        {
            if (!_isDisposed)
            {
                try
                {
                    _isDisposed = true;

                    MasterSideCommunicationHandler.ListeningError -= _masterCommunication_ListeningError;
                    MasterSideCommunicationHandler.TestInitialized -= MasterSideCommunicationHandler_TestInitialized;
                    MasterSideCommunicationHandler.OnTestProgressMessageReceived -=
                        _masterCommunication_OnTestProgressMessageReceived;

                    _communicationLock = null;
                    _testProgressMessagesLock = null;
                    _usedTileStresstestsLock = null;
                    _resultsLock = null;

                    _hasResults = false;
                    _usedTileStresstests = null;
                    _testProgressMessages = null;
                    _results = null;
                    _sw = null;

                    _ok = null;
                    _cancelled = null;
                    _failed = null;

                    _runInitialized = null;
                    _runDoneOnce = null;
                }
                catch
                {
                }

                ObjectRegistrar.Unregister(this);
            }
        }

        /// <summary>
        ///     Connects + sends tests
        /// </summary>
        public void Initialize()
        {
            InvokeMessage("Initializing the Test.");

            //_ok = new string[] { };
            //_cancelled = new string[] { };
            //_failed = new string[] { };
            //_runInitialized = new string[] { };
            //_runDoneOnce = new string[] { };

            //_finishedHandled = false;
            //_hasResults = false;

            Connect();
            SendAndReceiveInitializeTest();
        }

        private void Connect()
        {
            InvokeMessage("Connecting slaves...");
            _sw.Start();

            MasterSideCommunicationHandler.Init();

            foreach (BaseItem item in _distributedTest.Tiles)
            {
                var tile = item as Tile;
                if (tile.Use)
                    foreach (BaseItem childItem in tile)
                    {
                        var tileStresstest = childItem as TileStresstest;
                        if (tileStresstest.Use)
                        {
                            Exception exception;
#warning Allow multiple slaves to be able to distribute work.
                            int processID;
                            MasterSideCommunicationHandler.ConnectSlave(
                                tileStresstest.BasicTileStresstest.Slaves[0].IP,
                                tileStresstest.BasicTileStresstest.Slaves[0].Port, out processID, out exception);
                            if (exception == null)
                            {
                                _usedTileStresstests.Add(tileStresstest);
                                lock (_testProgressMessagesLock)
                                    _testProgressMessages.Add(tileStresstest, new TestProgressMessage());
                                InvokeMessage(string.Format("|->Connected {0} - {1}", tileStresstest.Parent,
                                                            tileStresstest));
                            }
                            else
                            {
                                Dispose();
                                var ex =
                                    new Exception(
                                        string.Format("Could not connect to one of the slaves ({0} - {1})!{2}{3}",
                                                      tileStresstest.Parent, tileStresstest, Environment.NewLine,
                                                      exception));
                                InvokeMessage(ex.ToString(), LogLevel.Error);
                                throw ex;
                            }
                        }
                    }
            }
            if (_usedTileStresstests.Count == 0)
            {
                var ex = new Exception("Please use at least one test!");
                InvokeMessage(ex.ToString(), LogLevel.Warning);
                throw ex;
            }
            _sw.Stop();
            InvokeMessage(string.Format(" ...Connected slaves in {0}", _sw.Elapsed.ToLongFormattedString()));
            _sw.Reset();
        }

        private void SendAndReceiveInitializeTest()
        {
            InvokeMessage("Initializing tests on slaves [Please, be patient]...");
            _sw.Start();
            Exception[] exceptions = MasterSideCommunicationHandler.InitializeTests(_usedTileStresstests.ToArray(),
                                                                                    _distributedTest.RunSynchronization);
            if (exceptions.Length != 0)
            {
                var ex = new Exception("Could not initialize one or more tests!\n" + exceptions.Combine("\n"));
                InvokeMessage(ex.ToString(), LogLevel.Error);
                throw ex;
            }

            _sw.Stop();
            InvokeMessage(string.Format(" ...Test initialized in {0}", _sw.Elapsed.ToLongFormattedString()));
            _sw.Reset();
        }

        private void MasterSideCommunicationHandler_TestInitialized(object sender, TestInitializedEventArgs e)
        {
            lock (_lock)
                if (e.Exception == null)
                {
                    InvokeMessage(string.Format("|->Initialized {0} - {1}", e.TileStresstest.Parent, e.TileStresstest));
                }
                else
                {
                    var ex =
                        new Exception(string.Format("Could not initialize {0} - {1}.{2}{3}", e.TileStresstest.Parent,
                                                    e.TileStresstest, Environment.NewLine, e.Exception));
                    InvokeMessage(ex.ToString(), LogLevel.Error);
                }
        }

        public void Start()
        {
            InvokeMessage("Starting the test...");
            Exception exception;
            MasterSideCommunicationHandler.StartTest(out exception);

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
                var ex =
                    new Exception(string.Format("Could not start the Distributed Test.{0}{1}", Environment.NewLine,
                                                exception.ToString()));
                InvokeMessage(ex.ToString(), LogLevel.Error);
                throw ex;
            }
        }

        private void _masterCommunication_ListeningError(object sender, ListeningErrorEventArgs e)
        {
            if (_isDisposed)
                return;

            lock (_communicationLock)
            {
                try
                {
                    if (!_isDisposed && Finished < _usedTileStresstests.Count)
                    {
#warning Allow multiple slaves for work distribution
                        foreach (TileStresstest tileStresstest in _usedTileStresstests)
                            if (tileStresstest.BasicTileStresstest.Slaves[0].IP == e.SlaveIP &&
                                tileStresstest.BasicTileStresstest.Slaves[0].Port == e.SlavePort)
                                _failed = AddUniqueToStringArray(_failed, tileStresstest.TileStresstestIndex);

                        InvokeOnListeningError(e);

                        Stop();
                            //The test cannot be valid, therefore stop testing. It is stopped in the gui also, but stop it here explicitely for when the gui fails.

                        if (Finished == _usedTileStresstests.Count)
                            HandleFinished();
                    }
                }
                catch
                {
                }
            }
        }

        private string[] AddUniqueToStringArray(string[] arr, string item)
        {
            foreach (string s in arr)
                if (s == item)
                    return arr;

            var newArr = new string[arr.Length + 1];
            for (int index = 0; index != arr.Length; index++)
                newArr[index] = arr[index];
            newArr[arr.Length] = item;

            return newArr;
        }

        private void _masterCommunication_OnTestProgressMessageReceived(object sender,
                                                                        TestProgressMessageReceivedEventArgs e)
        {
            if (_isDisposed)
                return;

            lock (_communicationLock)
            {
                try
                {
                    _hasResults = true;

                    TestProgressMessage tpm = e.TestProgressMessage;
                    TileStresstest ts = GetTileStresstest(e.TestProgressMessage.TileStresstestIndex);
                    lock (_testProgressMessagesLock)
                        _testProgressMessages[ts] = tpm;

                    bool okCancelError = true;
                    switch (tpm.StresstestResult)
                    {
                        case StresstestStatus.Busy:
                            okCancelError = false;
                            break;
                        case StresstestStatus.Ok:
                            _ok = AddUniqueToStringArray(_ok, tpm.TileStresstestIndex);
                            break;
                        case StresstestStatus.Cancelled:
                            _cancelled = AddUniqueToStringArray(_cancelled, tpm.TileStresstestIndex);
                            break;
                        case StresstestStatus.Error:
                            _failed = AddUniqueToStringArray(_failed, tpm.TileStresstestIndex);
                            break;
                    }

                    //Check if it is needed to do anything
                    if (Running != 0 && Cancelled == 0 && Failed == 0)
                    {
                        //Threat this as stopped.
                        if (okCancelError)
                            _runDoneOnce = AddUniqueToStringArray(_runDoneOnce, tpm.TileStresstestIndex);

                        switch (_distributedTest.RunSynchronization)
                        {
                            case RunSynchronization.None:
                                break;

                                //Send Break, wait for all stopped, send continue, wait for all started, send continue
                            case RunSynchronization.BreakOnFirstFinished:
                                if (tpm.RunStateChange == RunStateChange.ToRunInitializedFirstTime)
                                {
                                    _runInitialized = AddUniqueToStringArray(_runInitialized, tpm.TileStresstestIndex);
                                    if (RunInitializedCount == Running)
                                    {
                                        _runInitialized = new string[] {};
                                        MasterSideCommunicationHandler.SendContinue();
                                    }
                                }
                                else if (tpm.RunStateChange == RunStateChange.ToRunDoneOnce)
                                {
                                    _runDoneOnce = AddUniqueToStringArray(_runDoneOnce, tpm.TileStresstestIndex);
                                    if (RunDoneOnceCount == _usedTileStresstests.Count)
                                    {
                                        _runDoneOnce = new string[] {};
                                        //Increment the index here to be able to continue to the next run.
                                        MasterSideCommunicationHandler.SendContinue();
                                    }
                                    else if (RunDoneOnceCount == 1)
                                    {
                                        //Break the tests that are still in the previous run
                                        MasterSideCommunicationHandler.SendBreak();
                                    }
                                }
                                break;

                                //Wait for all stopped, send continue, wait for all started, send continue
                            case RunSynchronization.BreakOnLastFinished:
                                if (tpm.RunStateChange == RunStateChange.ToRunInitializedFirstTime)
                                {
                                    _runInitialized = AddUniqueToStringArray(_runInitialized, tpm.TileStresstestIndex);
                                    if (RunInitializedCount == Running)
                                    {
                                        _runInitialized = new string[] {};
                                        MasterSideCommunicationHandler.SendContinue();
                                    }
                                }
                                else if (tpm.RunStateChange == RunStateChange.ToRunDoneOnce)
                                {
                                    _runDoneOnce = AddUniqueToStringArray(_runDoneOnce, tpm.TileStresstestIndex);
                                    if (RunDoneOnceCount == Running)
                                    {
                                        _runDoneOnce = new string[] {};
                                        MasterSideCommunicationHandler.SendBreak();
                                    }
                                }
                                break;
                        }
                    }
                    InvokeOnTestProgressMessageReceived(ts, tpm);

#if EnableBetaFeature
                    WriteRestProgress(ts, tpm);
#endif

                    if (Cancelled != 0 || Failed != 0)
                        Stop(); //Test is invalid stop the test.

                    if (Finished == _usedTileStresstests.Count)
                        HandleFinished();
                }
                catch
                {
                }
            }
        }

        private TileStresstest GetTileStresstest(string tileStresstestIndex)
        {
            lock (_usedTileStresstestsLock)
            {
                foreach (TileStresstest ts in _usedTileStresstests)
                    if (ts.TileStresstestIndex == tileStresstestIndex)
                        return ts;
                return null;
            }
        }

        /// <summary>
        ///     Stop the distributed test.
        /// </summary>
        public void Stop()
        {
            MasterSideCommunicationHandler.StopTest();
        }

        private void HandleFinished()
        {
            if (!_finishedHandled)
            {
                _finishedHandled = true;

                ObjectRegistrar.Unregister(this);

                InvokeMessage("Getting results (it can take a minute or two until transmission begins)...");
                Exception exception = null;

                try
                {
                    if (!Directory.Exists(_distributedTest.ResultPath))
                        Directory.CreateDirectory(_distributedTest.ResultPath);
                    if (!Directory.Exists(_resultPath))
                        Directory.CreateDirectory(_resultPath);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                if (exception == null)
                {
                    Exception[] exceptions;
                    foreach (ResultsMessage rm in MasterSideCommunicationHandler.GetResults(out exceptions))
                        try
                        {
                            var torrentClient = new TorrentClient();
                            torrentClient.SetTag(rm.TileStresstestIndex);
                            torrentClient.ProgressUpdated += torrentClient_ProgressUpdated;
                            torrentClient.DownloadCompleted += torrentClient_DownloadCompleted;

                            InvokeResultsDownloadProgressUpdated(rm.TileStresstestIndex, 1);

                            torrentClient.DownloadTorrentFromBytes(rm.TorrentInfo, _resultPath);
                        }
                        catch (Exception ex)
                        {
                            exception = ex;
                        }

                    if (exceptions.Length != 0)
                        exception = new Exception(exceptions.Combine("\n"));
                }

                if (exception != null)
                {
                    Interlocked.Increment(ref _resultsReceived);
                    InvokeMessage("Could not receive one or more resuls!\n" + exception, LogLevel.Error);
                }
                if (_resultsReceived == Finished)
                    InvokeOnFinished();
            }
        }

        private void torrentClient_ProgressUpdated(TorrentClient source, TorrentEventArgs e)
        {
            if (_resultsReceived != Finished && e.PercentCompleted != 100)
            {
                var tileStresstestIndex = source.GetTag() as string;
                InvokeResultsDownloadProgressUpdated(tileStresstestIndex, e.PercentCompleted);
            }
        }

        private void torrentClient_DownloadCompleted(TorrentClient source, TorrentEventArgs e)
        {
            source.ProgressUpdated -= torrentClient_ProgressUpdated;
            source.DownloadCompleted -= torrentClient_DownloadCompleted;
            source.StopTorrent();
            var tileStresstestIndex = source.GetTag() as string;
            source.RemoveTag();

            InvokeResultsDownloadCompleted(tileStresstestIndex, source.Name);

            source = null;

            if (Interlocked.Increment(ref _resultsReceived) == Finished)
                InvokeOnFinished();
        }

        #endregion

        #region REST

        private void WriteRestConfig()
        {
            try
            {
                Converter.ClearWrittenFiles();

                var testConfigCache = new Hashtable(1);
                foreach (Tile tile in _distributedTest.Tiles)
                    foreach (TileStresstest tileStresstest in tile)
                        if (tileStresstest.Use)
                            Converter.SetTestConfig(testConfigCache, _distributedTest.ToString(),
                                                    _distributedTest.RunSynchronization.ToString(),
                                                    "Tile " + (tileStresstest.Parent as Tile).Index + " Stresstest " +
                                                    tileStresstest.Index + " " +
                                                    tileStresstest.BasicTileStresstest.Connection.Label,
                                                    tileStresstest.BasicTileStresstest.Connection,
                                                    tileStresstest.BasicTileStresstest.ConnectionProxy,
                                                    tileStresstest.BasicTileStresstest.Monitors,
                                                    tileStresstest.BasicTileStresstest.Slaves.Length == 0
                                                        ? string.Empty
                                                        : tileStresstest.BasicTileStresstest.Slaves[0].ToString(),
                                                    tileStresstest.AdvancedTileStresstest.Log,
                                                    tileStresstest.AdvancedTileStresstest.LogRuleSet,
                                                    tileStresstest.AdvancedTileStresstest.Concurrency,
                                                    tileStresstest.AdvancedTileStresstest.Runs,
                                                    tileStresstest.AdvancedTileStresstest.MinimumDelay,
                                                    tileStresstest.AdvancedTileStresstest.MaximumDelay,
                                                    tileStresstest.AdvancedTileStresstest.Shuffle,
                                                    tileStresstest.AdvancedTileStresstest.Distribute,
                                                    tileStresstest.AdvancedTileStresstest.MonitorBefore,
                                                    tileStresstest.AdvancedTileStresstest.MonitorAfter);

                Converter.WriteToFile(testConfigCache, "TestConfig");
            }
            catch
            {
            }
        }

        private void WriteRestProgress(TileStresstest tileStresstest, TestProgressMessage testProgressMessage)
        {
            try
            {
                var testProgressCache = new Hashtable(1);
                foreach (
                    TileConcurrencyProgressResult tcu in
                        testProgressMessage.TileStresstestProgressResults.TileConcurrencyProgressResults)
                    foreach (TileRunProgressResult tru in tcu.TileRunProgressResults)
                        Converter.SetTestProgress(testProgressCache, _distributedTest.ToString(),
                                                  "Tile " + (tileStresstest.Parent as Tile).Index + " Stresstest " +
                                                  tileStresstest.Index + " " +
                                                  tileStresstest.BasicTileStresstest.Connection.Label,
                                                  tcu.ConcurrentUsers, tru.Run + 1,
                                                  tru.Metrics,
                                                  tru.EstimatedRuntimeLeft,
                                                  testProgressMessage.RunStateChange,
                                                  testProgressMessage.StresstestResult);

                Converter.WriteToFile(testProgressCache, "TestProgress");
            }
            catch
            {
            }
        }

        #endregion

        #region Events

        /// <summary>
        ///     Messages to the user on the message textbox
        /// </summary>
        public event EventHandler<MessageEventArgs> Message;

        public event EventHandler<TestProgressMessageReceivedEventArgs> OnTestProgressMessageReceived;
        public event EventHandler<ListeningErrorEventArgs> OnListeningError;
        public event EventHandler<ResultsDownloadProgressUpdatedEventArgs> ResultsDownloadProgressUpdated;
        public event EventHandler<ResultsDownloadCompletedEventArgs> ResultsDownloadCompleted;
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
                    string[] split = message.Split(new[] {'\n', '\r'}, StringSplitOptions.None);
                    message = split[0] + "\n See " + Logger.DEFAULT_LOCATION + " for the stack trace.";
                }
                SynchronizationContextWrapper.SynchronizationContext.Send(
                    delegate { Message(this, new MessageEventArgs(message, color, logLevel)); }, null);
            }
        }

        private void InvokeOnTestProgressMessageReceived(TileStresstest tileStresstest,
                                                         TestProgressMessage testProgressMessage)
        {
            if (OnTestProgressMessageReceived != null)
                SynchronizationContextWrapper.SynchronizationContext.Send(
                    delegate
                        {
                            OnTestProgressMessageReceived(this,
                                                          new TestProgressMessageReceivedEventArgs(tileStresstest,
                                                                                                   testProgressMessage));
                        }, null);
        }

        private void InvokeOnListeningError(ListeningErrorEventArgs listeningErrorEventArgs)
        {
            LogWrapper.LogByLevel(listeningErrorEventArgs, LogLevel.Error);
            if (OnListeningError != null)
                SynchronizationContextWrapper.SynchronizationContext.Send(
                    delegate { OnListeningError(this, listeningErrorEventArgs); }, null);
        }

        private void InvokeResultsDownloadProgressUpdated(string tileStresstestIndex, double percentCompleted)
        {
            lock (this)
            {
                TileStresstest ts = GetTileStresstest(tileStresstestIndex);

                if (ResultsDownloadProgressUpdated != null)
                    foreach (
                        EventHandler<ResultsDownloadProgressUpdatedEventArgs> del in
                            ResultsDownloadProgressUpdated.GetInvocationList())
                        del.BeginInvoke(this, new ResultsDownloadProgressUpdatedEventArgs(ts, (int) percentCompleted),
                                        null, null);
            }
        }

        private void InvokeResultsDownloadCompleted(string tileStresstestIndex, string subResultDir)
        {
            lock (this)
            {
                TileStresstest ts = GetTileStresstest(tileStresstestIndex);
                lock (_resultsLock)
                    if (_results.ContainsKey(ts))
                        return;

                string resultPath = Path.Combine(_resultPath, subResultDir);

                StopSeedingResults(ts);

                lock (_resultsLock)
                    _results.Add(ts, resultPath);
                if (ResultsDownloadCompleted != null)
                    foreach (
                        EventHandler<ResultsDownloadCompletedEventArgs> del in
                            ResultsDownloadCompleted.GetInvocationList())
                        del.BeginInvoke(this, new ResultsDownloadCompletedEventArgs(ts, resultPath), null, null);
            }
        }

        /// <summary>
        ///     Stops the seeding at the torrent server. (slave side)
        /// </summary>
        private void StopSeedingResults(TileStresstest tileStresstest)
        {
#warning Allow multiple slaves for work distribution
            Slave slave = tileStresstest.BasicTileStresstest.Slaves[0];

            Exception ex;
            MasterSideCommunicationHandler.StopSeedingResults(tileStresstest, out ex);
            MasterSideCommunicationHandler.DisconnectSlave(slave.IP, slave.Port);
        }

        private void InvokeOnFinished()
        {
            InvokeMessage("Test finished!");
            if (OnFinished != null)
                SynchronizationContextWrapper.SynchronizationContext.Send(
                    delegate { OnFinished(this, new FinishedEventArgs(OK, Cancelled, Failed)); }, null);
        }

        #endregion
    }
}