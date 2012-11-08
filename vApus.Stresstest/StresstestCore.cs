﻿/*
 * Copyright 2009 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading;
using vApus.Results.Model;
using vApus.Util;

namespace vApus.Stresstest
{
    public class StresstestCore : IDisposable
    {
        #region Fields
        /// <summary>
        /// For handling fatal errors of a thread.
        /// </summary>
        private object _lock = new object();

        /// <summary>
        /// To be able to execute log entries parallel.
        /// </summary>
        [ThreadStatic]
        private static SyncAndAsyncWorkItem _syncAndAsyncWorkItem;
        /// <summary>Measures intit actions and such to be able to output to the gui.</summary>
        private Stopwatch _sw = new Stopwatch();

        private Stresstest _stresstest;
        private ConnectionProxyPool _connectionProxyPool;
        private StresstestThreadPool _threadPool;

        private TestPatternsAndDelaysGenerator _testPatternsAndDelaysGenerator;
        private Log _log;
        private LogEntry[] _logEntries;
        private TestableLogEntry[][] _testableLogEntries;
        private int[][] _delays;

        private AutoResetEvent _sleepWaitHandle = new AutoResetEvent(false);
        private AutoResetEvent _runSynchronizationContinueWaitHandle = new AutoResetEvent(false);
        private RunSynchronization _runSynchronization;

        /// <summary> The number of all parallel connections, they will be disposed along the road. </summary>
        private int _parallelConnectionsModifier;

        /// <summary> Store the current run for run synchronization. </summary>
        private int _continueCounter;

        private volatile bool _failed, _cancel, _completed, _break, _isDisposed;
        /// <summary> Needed for break on last. </summary>
        private volatile bool _runDoneOnce;

        private StresstestResult _stresstestResult;
        private ConcurrencyResult _concurrentUsersResult;
        private RunResult _runResult;
        #endregion

        #region Properties
        public RunSynchronization RunSynchronization
        {
            get { return _runSynchronization; }
            set { _runSynchronization = value; }
        }
        public StresstestResult StresstestResult
        {
            get { return _stresstestResult; }
        }
        public int BusyThreadCount
        {
            get { return _threadPool == null ? 0 : _threadPool.BusyThreadCount; }
        }
        public int UsedThreadCount
        {
            get { return _threadPool == null ? 0 : _threadPool.UsedThreadCount; }
        }
        public bool IsDisposed
        {
            get { return _isDisposed; }
        }
        #endregion

        #region Con-/Destructor
        /// <summary>
        /// 
        /// </summary>
        /// <param name="stresstest"></param>
        /// <param name="limitSimultaniousRunningToOne">Allow only one stresstest to run at a time.</param>
        public StresstestCore(Stresstest stresstest, bool limitSimultaniousRunningToOne = true)
        {
            StaticObjectServiceWrapper.ObjectService.MaxSuscribers = limitSimultaniousRunningToOne ? 1 : int.MaxValue;
            StaticObjectServiceWrapper.ObjectService.Suscribe(this);

            _stresstest = stresstest;
        }
        ~StresstestCore()
        {
            Dispose();
        }
        #endregion

        #region Functions
        /// <summary>
        /// Do this before everything else.
        /// </summary>
        public void InitializeTest()
        {
            try
            {
                InvokeMessage("Initializing the Test.");
                InitializeLog();
                InitializeConnectionProxyPool();
            }
            catch
            {
                _failed = true;
                throw;
            }
        }
        /// <summary>
        /// Will apply the log rule set.
        /// Parallel connections will be determined here also.
        /// </summary> 
        private void InitializeLog()
        {
            if (_cancel)
                return;

            InvokeMessage("Initializing Log...");
            _sw.Start();
            if (_stresstest.Log.LogRuleSet.IsEmpty)
            {
                Exception ex = new Exception("No rule set has been assigned to the selected log.");
                LogWrapper.LogByLevel(ex.ToString(), LogLevel.Error);
                throw ex;
            }
            if (_stresstest.Log.Count == 0)
            {
                Exception ex = new Exception("There are no entries in the selected log.");
                LogWrapper.LogByLevel(ex.ToString(), LogLevel.Error);
                throw ex;
            }

            _log = LogTimesOccurancies(_stresstest.Log, _stresstest.Distribute);
            _log.ApplyLogRuleSet();

            //Parallel connections, check per user aciotn
            _parallelConnectionsModifier = 0;
            List<LogEntry> logEntries = new List<LogEntry>();
            foreach (var item in _log)
                if (item is UserAction)
                    foreach (LogEntry logEntry in item)
                    {
                        logEntries.Add(logEntry);
#warning Occurances for parallel executions?

                        if (logEntry.ExecuteInParallelWithPrevious)
                            ++_parallelConnectionsModifier;
                    }
                else if (item is LogEntry)
                    logEntries.Add(item as LogEntry);

            _logEntries = logEntries.ToArray();
            int actionCount = _stresstest.Distribute == ActionAndLogEntryDistribution.Fast ? _stresstest.Log.Count : _log.Count; //Needed for fast log entry distribution

            _testPatternsAndDelaysGenerator = new TestPatternsAndDelaysGenerator
            (
                _logEntries,
                actionCount,
                _stresstest.Shuffle,
                _stresstest.Distribute,
                _stresstest.MinimumDelay,
                _stresstest.MaximumDelay
            );
            _sw.Stop();
            InvokeMessage(string.Format(" ...Log Initialized in {0}.", _sw.Elapsed.ToLongFormattedString()));
            _sw.Reset();
        }
        /// <summary>
        /// Expands the log (into a new one) times the occurance, this is only done if the Action Distribution is not equal to none.
        /// Otherwise the original on is returned.
        /// </summary>
        /// <returns></returns>
        private Log LogTimesOccurancies(Log log, ActionAndLogEntryDistribution distribute)
        {
            if (distribute == ActionAndLogEntryDistribution.None)
            {
                return log;
            }
            else
            {
                Log newLog = log.Clone(false);
                foreach (var item in log)
                    if (item is UserAction)
                    {
                        UserAction action = item as UserAction;
                        for (int i = 0; i != action.Occurance; i++)
                        {
                            var actionClone = new UserAction(action.Label);
                            actionClone.Occurance = 1; //Must be one now, this value doesn't matter anymore.
                            actionClone.Pinned = action.Pinned;

                            foreach (LogEntry child in action)
                                for (int j = 0; j != child.Occurance; j++)
                                {
                                    var childClone = child.Clone(false);
                                    childClone.Occurance = 1;
                                    actionClone.AddWithoutInvokingEvent(childClone, false);
                                }

                            newLog.AddWithoutInvokingEvent(actionClone, false);
                        }
                    }
                    else
                    {
                        LogEntry entry = item as LogEntry;
                        for (int i = 0; i != entry.Occurance; i++)
                        {
                            var entryClone = entry.Clone(false);
                            entryClone.Occurance = 1;
                            newLog.AddWithoutInvokingEvent(entryClone, false);
                        }
                    }

                return newLog;
            }
        }
        private void InitializeConnectionProxyPool()
        {
            if (_cancel)
                return;

            InvokeMessage("Initialize Connection Proxy Pool...");
            _sw.Start();
            _connectionProxyPool = new ConnectionProxyPool(_stresstest.Connection);
            CompilerResults compilerResults = _connectionProxyPool.CompileConnectionProxyClass(false);
            if (compilerResults.Errors.HasErrors)
            {
                StringBuilder sb = new StringBuilder("Failed at compiling the connection proxy class:");
                sb.AppendLine();
                foreach (CompilerError compileError in compilerResults.Errors)
                {
                    sb.AppendFormat("Error number {0}, Line {1}, Column {2}: {3}", compileError.ErrorNumber, compileError.Line, compileError.Column, compileError.ErrorText);
                    sb.AppendLine();
                }
                _connectionProxyPool.Dispose();
                _connectionProxyPool = null;
                Exception ex = new Exception(sb.ToString());
                LogWrapper.LogByLevel(ex.ToString(), LogLevel.Error);
                // This handles notification to the user.
                throw ex;
            }
            string error;
            _connectionProxyPool.TestConnection(out error);
            if (error != null)
            {
                _connectionProxyPool.Dispose();
                _connectionProxyPool = null;
                Exception ex = new Exception(error);
                LogWrapper.LogByLevel(ex.ToString(), LogLevel.Error);
                throw ex;
            }
            _sw.Stop();
            InvokeMessage(string.Format(" ...Connection Proxy Pool Inititialized in {0}.", _sw.Elapsed.ToLongFormattedString()));
            _sw.Reset();
        }
        public StresstestStatus ExecuteStresstest()
        {
            //No run started yet.
            _continueCounter = -1;
            SetStresstestStarted();
            //Loop all concurrent users
            for (int concurrentUsersIndex = 0; concurrentUsersIndex != _stresstest.Concurrency.Length; concurrentUsersIndex++)
            {
                int concurrentUsers = _stresstest.Concurrency[concurrentUsersIndex];
                if (_cancel) break;

                SetConcurrencyStarted(concurrentUsersIndex);
                //Loop 'run' times for the concurrent users.
                for (int run = 0; run != _stresstest.Runs; run++)
                {
                    if (_cancel) break;
                    _runDoneOnce = false;

                Rerun:
                    //Initialize all for a new test.
                    if (_cancel) break;
                    //Initialize the log entries and delays (every time so the tests for different runs are not the same)
                    DetermineTestableLogEntriessAndDelays(concurrentUsers);
                    if (_cancel) break;
                    SetThreadPool(concurrentUsers);
                    if (_cancel) break;
                    //Initialize the connection proxy pool (every time, so dead or locked connections can't be used anymore)
                    SetConnectionProxyPool(concurrentUsers);
                    if (_cancel) break;

                    if (_runDoneOnce)
                    {
                        InvokeMessage(string.Format("|----> | Rerunning Run {0}...", run + 1), Color.White);
                    }
                    else
                    {
                        ++_continueCounter;

                        SetRunInitializedFirstTime(concurrentUsersIndex, run + 1);
                        //Wait here untill the master sends continue when using run sync.
                        if (_runSynchronization != RunSynchronization.None)
                        {
                            InvokeMessage("Waiting for Continue Message from Master...");
                            _runSynchronizationContinueWaitHandle.WaitOne();
                            InvokeMessage("Continuing...");
                        }
                    }

                    if (_cancel) break;

                    //Set this run started, used for for instance monitoring --> the range for this run can be determined this way.
                    SetRunStarted();

                    //Do the actual work and invoke when the run is finished.
                    try
                    {
                        _threadPool.DoWorkAndWaitForIdle();
                    }
                    catch (Exception ex)
                    {
                        if (!_isDisposed)
                            InvokeMessage("|----> |Run Not Finished Succesfully!\n|Thread Pool Exception:\n" + ex, Color.Red, LogLevel.Error);
                    }
                    finally
                    {
                        SetRunStopped();
                    }

                    //Wait here when the run is broken untill the master sends continue when using run sync.
                    if (_runSynchronization == RunSynchronization.BreakOnFirstFinished)
                    {
                        ++_continueCounter;
                        SetRunDoneOnce();

                        InvokeMessage("Waiting for Continue Message from Master...");
                        _runSynchronizationContinueWaitHandle.WaitOne();
                        InvokeMessage("Continuing...");
                    }
                    //Rerun untill the master sends a break. This is better than recurions --> no stack overflows.
                    else if (_runSynchronization == RunSynchronization.BreakOnLastFinished && !_break)
                    {
                        SetRunDoneOnce();

                        InvokeMessage("Initializing Rerun...");
                        //Increase resultset
                        _runResult.PrepareForRerun();
                        goto Rerun;
                    }
                }
                _concurrentUsersResult.StoppedAt = DateTime.Now;
            }

            return Completed();
        }

        private void DetermineTestableLogEntriessAndDelays(int concurrentUsers)
        {
            try
            {
                InvokeMessage(string.Format("       |Determining Test Patterns and Delays for {0} Concurrent Users...", concurrentUsers));
                _sw.Start();
                var testableLogEntries = new List<TestableLogEntry[]>();
                var delays = new List<int[]>();

                Random delaysRandom = new Random(DateTime.Now.Millisecond);
                for (int t = 0; t != concurrentUsers; t++)
                {
                    if (_cancel)
                        return;

                    int[] testPatternIndices, delayPattern;
                    _testPatternsAndDelaysGenerator.GetPatterns(out testPatternIndices, out delayPattern);

                    var tle = new TestableLogEntry[testPatternIndices.Length];
                    int index = 0;

                    var parameterizedStructure = _log.GetParameterizedStructure();

                    for (int i = 0; i != testPatternIndices.Length; i++)
                    {
                        if (_cancel)
                            return;

                        int j = testPatternIndices[i];

                        LogEntry logEntry = _logEntries[j];

                        string logEntryIndex = string.Empty;
                        StringTree parameterizedLogEntry = parameterizedStructure[j];
                        string userAction = string.Empty;
                        int userActionIndex = 0;

                        UserAction parent = logEntry.Parent as UserAction;
                        if (parent == null)
                        {
                            logEntryIndex = logEntry.Index.ToString();
                            userActionIndex = logEntry.Index;
                            userAction = "None defined " + userActionIndex;
                        }
                        else
                        {
                            logEntryIndex = parent.Index + "." + logEntry.Index;
                            userActionIndex = parent.Index;
                            userAction = parent.ToString();
                        }

                        tle[index++] = new TestableLogEntry(logEntryIndex, parameterizedLogEntry, userActionIndex, userAction,
                            logEntry.ExecuteInParallelWithPrevious, logEntry.ParallelOffsetInMs);
                    }

                    testableLogEntries.Add(tle);
                    delays.Add(delayPattern);
                }


                _testableLogEntries = testableLogEntries.ToArray();
                _delays = delays.ToArray();

                _sw.Stop();
                InvokeMessage(string.Format("       | ...Test Patterns and Delays Determined in {0}.", _sw.Elapsed.ToLongFormattedString()));
                _sw.Reset();
            }
            catch
            {
                if (!_cancel)
                    throw;
            }
        }
        private void SetThreadPool(int concurrentUsers)
        {
            if (_cancel)
                return;

            InvokeMessage(string.Format("       |Setting Thread Pool for {0} Concurrent Users...", concurrentUsers));
            _sw.Start();
            if (_threadPool == null)
            {
                _threadPool = new StresstestThreadPool(Work);
                _threadPool.ThreadWorkException += new EventHandler<MessageEventArgs>(_threadPool_ThreadWorkException);
            }
            _threadPool.SetThreads(concurrentUsers);
            _sw.Stop();
            InvokeMessage(string.Format("       | ...Thread Pool Set in {0}.", _sw.Elapsed.ToLongFormattedString()));
            _sw.Reset();
        }
        private void SetConnectionProxyPool(int concurrentUsers)
        {
            if (_cancel)
                return;

            InvokeMessage(string.Format("       |Setting Connections for {0} Concurrent Users...", concurrentUsers));
            _sw.Start();
            try
            {
                _connectionProxyPool.SetAndConnectConnectionProxies(concurrentUsers, _parallelConnectionsModifier);
                InvokeMessage(string.Format("       | ...Connections Set in {0}.", _sw.Elapsed.ToLongFormattedString()));
            }
            catch
            {
                throw;
            }
            finally
            {
                _sw.Stop();
                _sw.Reset();
            }
        }

        /// <summary>
        /// </summary>
        public void Break()
        {
            if (!_break && !(_completed | _cancel | _failed))
            {
                _break = true;
                InvokeMessage("Breaking the execution for the current run...");

                _sleepWaitHandle.Set();
            }
        }
        /// <summary>
        /// </summary>
        /// <param name="continueCounter">Every time the execution is paused the continue counter is incremented by one.</param>
        public void Continue(int continueCounter)
        {
            if (_continueCounter == continueCounter && !(_completed | _cancel | _failed))
            {
                _break = false;
                _runSynchronizationContinueWaitHandle.Set();
            }
        }

        #region Work
        public void Work(int threadIndex)
        {
            try
            {
                IConnectionProxy connectionProxy = _connectionProxyPool[threadIndex];
                if (!connectionProxy.IsConnectionOpen)
                    connectionProxy.OpenConnection();

                TestableLogEntry[] testableLogEntries = _testableLogEntries[threadIndex];
                int[] delays = _delays[threadIndex];

                int testableLogEntryIndex = 0;
                while (testableLogEntryIndex != testableLogEntries.Length)
                {
                    if (_cancel || _break)
                    {
                        _sleepWaitHandle.Set();
                        return;
                    }

                    int incrementIndex;
                    ExecuteLogEntry(threadIndex, testableLogEntryIndex, connectionProxy, testableLogEntries, delays, out incrementIndex);
                    testableLogEntryIndex += incrementIndex;
                }
            }
            catch (Exception e)
            {
                if (!_cancel && !_break)
                    InvokeMessage("Work failed for " + Thread.CurrentThread.Name + ".\n" + e.ToString(), Color.Red, LogLevel.Error);
            }
        }

        /// <summary>
        /// Decides if the log entry must be executed in parallel with others or not.
        /// </summary>
        /// <param name="threadIndex"></param>
        /// <param name="testableLogEntryIndex"></param>
        /// <param name="incrementIndex">Can be greater than 1 if some are parallel executed.</param>
        private void ExecuteLogEntry
            (
                int threadIndex,
                int testableLogEntryIndex,
                IConnectionProxy connectionProxy,
                TestableLogEntry[] testableLogEntries,
                int[] delays,
                out int incrementIndex
            )
        {
            incrementIndex = 1;

            if (_cancel || _break)
            {
                _sleepWaitHandle.Set();
                return;
            }

            TestableLogEntry testableLogEntry = testableLogEntries[testableLogEntryIndex];

            //Check if this log entry could be the first of a parallel executed range of entries
            //If so the range will be executed multi threaded
            if (!testableLogEntry.ExecuteInParallelWithPrevious || !_stresstest.UseParallelExecutionOfLogEntries)
            {
                int testableLogEntriesLength = 0, exclusiveEnd = 0;
                List<TestableLogEntry> parallelTestableLogEntries = null;

                // Make the range if following is true.
                if (_stresstest.UseParallelExecutionOfLogEntries)
                {
                    testableLogEntriesLength = testableLogEntries.Length;
                    exclusiveEnd = testableLogEntryIndex + 1;
                    parallelTestableLogEntries = new List<TestableLogEntry>();
                    //Do not go out of bounds
                    if (exclusiveEnd != testableLogEntriesLength)
                        while (testableLogEntries[exclusiveEnd].ExecuteInParallelWithPrevious)
                        {
                            parallelTestableLogEntries.Add(testableLogEntries[exclusiveEnd]);
                            ++exclusiveEnd;
                            ++incrementIndex;
                            if (exclusiveEnd == testableLogEntriesLength)
                                break;
                        }
                }

                //Do work parallel if needed.
                if (incrementIndex == 1)
                {
                    if (_syncAndAsyncWorkItem == null)
                        _syncAndAsyncWorkItem = new SyncAndAsyncWorkItem();

                    _syncAndAsyncWorkItem.ExecuteLogEntry(this, _sleepWaitHandle, _runResult, threadIndex, testableLogEntryIndex, testableLogEntry, connectionProxy, delays[testableLogEntryIndex]);
                }
                else
                {
                    //Get the connection proxies, this first one is not a parallel one but the connection proxy for the specific user, this way data (cookies for example) kan be saved for other log entries.
                    var pcps = _connectionProxyPool.ParallelConnectionProxies[threadIndex];
                    //Only use the ones that are needed, the first one is actually not parallel.
                    var parallelConnectionProxies = new ParallelConnectionProxy[exclusiveEnd - testableLogEntryIndex];
                    parallelConnectionProxies[0] = new ParallelConnectionProxy(connectionProxy);

                    //Check if already used, if not then choose and flag it "used"
                    int setAt = 1;
                    for (int pi = 0; pi != pcps.Length; pi++)
                    {
                        var pcp = pcps[pi];
                        if (!pcp.Used)
                        {
                            pcp.Used = true;
                            parallelConnectionProxies[setAt] = pcp;
                            if (++setAt == parallelConnectionProxies.Length)
                                break;
                        }
                    }

#warning Making threads on the fly is maybe not a very good idea, maybe this must reside in the thread pool (like parallel connection proxies are in the connection proxy pool)

                    //Make a mini thread pool (Thread pools in thread pools, what it this madness?! :p)
                    Thread[] pThreads = new Thread[parallelConnectionProxies.Length];
                    //if(pcpIndex == parallelConnectionProxies.length) it is finished.
                    ManualResetEvent pThreadsSignalStart = new ManualResetEvent(false);
                    AutoResetEvent pThreadsSignalFinished = new AutoResetEvent(false);

                    int pcpIndex = -1, pThreadIndex = 0;
                    int finished = 0;
                    string threadName = Thread.CurrentThread.Name + " [Parallel #";
                    for (int pleIndex = testableLogEntryIndex; pleIndex != exclusiveEnd; pleIndex++)
                    {
                        //Anonymous delegate for the sake of simplicity, pleIndex in the state --> otherwise the wrong value can and will be picked
                        Thread pThread = new Thread(delegate(object state)
                        {
                            int index = (int)state;
                            if (_syncAndAsyncWorkItem == null)
                                _syncAndAsyncWorkItem = new SyncAndAsyncWorkItem();

                            pThreadsSignalStart.WaitOne();

                            try
                            {
                                _syncAndAsyncWorkItem.ExecuteLogEntry(this, _sleepWaitHandle, _runResult, threadIndex, index, testableLogEntries[index],
                                    parallelConnectionProxies[Interlocked.Increment(ref pcpIndex)].ConnectionProxy, delays[index]);
                            }
                            catch
                            {
                                //when stopping a test...
                            }

                            if (Interlocked.Increment(ref finished) == parallelConnectionProxies.Length)
                                pThreadsSignalFinished.Set();

                        });

                        //Add it to the pool, just for making sure they are kept in memory 
                        pThreads[pThreadIndex] = pThread;

                        pThread.Name = threadName + (++pThreadIndex) + "]";
                        pThread.IsBackground = true;
                        pThread.Start(pleIndex);
                    }

                    //Start them all at the same time
                    pThreadsSignalStart.Set();
                    pThreadsSignalFinished.WaitOne();

                    pThreadsSignalFinished.Dispose();
                    pThreadsSignalStart.Dispose();
                    pThreads = null;
                }
            }
        }
        private class SyncAndAsyncWorkItem
        {
            public void ExecuteLogEntry
                (
                    StresstestCore stresstestCore,
                    AutoResetEvent sleepWaitHandle,
                    RunResult runResult,
                    int threadIndex,
                    int testableLogEntryIndex,
                    TestableLogEntry testableLogEntry,
                    IConnectionProxy connectionProxy,
                    int delayInMilliseconds
                )
            {
                DateTime sentAt = DateTime.Now;
                TimeSpan timeToLastByte = new TimeSpan();
                Exception exception = null;

                if (stresstestCore._cancel || stresstestCore._break)
                {
                    sleepWaitHandle.Set();
                    return;
                }

                //Sleep the offset of parallel log entries.
                if (testableLogEntry.ExecuteInParallelWithPrevious)
                    Thread.Sleep(testableLogEntry.ParallelOffsetInMs);

                bool retried = false;
            RetryOnce:
                try
                {
                    if (connectionProxy == null || connectionProxy.IsDisposed)
                    {
                        exception = new Exception("Connectionproxy is disposed. Metrics for this log entry (" + testableLogEntry.ParameterizedLogEntryString + ") are not correct.");
                    }
                    else
                    {
                        StringTree parameterizedLogEntry = testableLogEntry.ParameterizedLogEntry;
                        connectionProxy.SendAndReceive(parameterizedLogEntry, out sentAt, out timeToLastByte, out exception);
                    }
                }
                catch (Exception ex)
                {
                    if (!retried && connectionProxy != null && !connectionProxy.IsDisposed)
                    {
                        try
                        {
                            retried = true;
                            connectionProxy.OpenConnection();
                            goto RetryOnce;
                        }
                        catch (Exception e)
                        {
                            exception = new Exception("An error in the connection proxy has occured and is now disposed due to for instance a time out or a bug in the connection proxy code; I tried to reopen the connection so testing could continue for this simulated user including a retry (once) for the current log entry, but it failed; Metrics for this log entry (" + testableLogEntry.ParameterizedLogEntryString + ") are not correct:\n" + ex + "\n\nReconnect failure:\n" + e);
                        }
                    }
                    else
                    {
                        try
                        {
                            if (connectionProxy != null)
                                connectionProxy.Dispose();
                        }
                        catch { }
                        connectionProxy = null;
                        exception = new Exception("An error in the connection proxy has occured, after the second try, (and is now disposed) due to for instance a time out or a bug in the connection proxy code; Metrics for this log entry (" + testableLogEntry.ParameterizedLogEntryString + ") are not correct:\n" + ex);
                    }
                    throw (exception);
                }
                finally
                {
                    var result = runResult.VirtualUserResults[threadIndex];
                    result.VirtualUser = Thread.CurrentThread.Name;

                    var logEntryResult = new LogEntryResult()
                    {
                        LogEntryIndex = testableLogEntry.LogEntryIndex,
                        LogEntryString = testableLogEntry.ParameterizedLogEntryString,
                        UserActionIndex = testableLogEntry.UserActionIndex,
                        UserAction = testableLogEntry.UserAction,
                        SentAt = sentAt,
                        TimeToLastByte = timeToLastByte,
                        DelayInMilliseconds = delayInMilliseconds,
                        Exception = exception
                    };
                    result.SetLogEntryResultAt(testableLogEntryIndex, logEntryResult);


                    if (delayInMilliseconds != 0 && !(stresstestCore._cancel || stresstestCore._break))
                        sleepWaitHandle.WaitOne(delayInMilliseconds);
                }
            }
        }
        #endregion

        private StresstestStatus Completed()
        {
            _completed = true;
            _connectionProxyPool.ShutDown();
            DisposeThreadPool();
            _connectionProxyPool.Dispose();

            _stresstestResult.StoppedAt = DateTime.Now;
            if (_cancel)
            {
                _stresstestResult.Status = "Cancelled";
                return StresstestStatus.Cancelled;
            }
            if (_failed)
            {
                _stresstestResult.Status = "Failed";
                return StresstestStatus.Error;
            }
            return StresstestStatus.Ok;
        }

        public void Cancel()
        {
            if (!_cancel)
            {
                _cancel = true;
                _sleepWaitHandle.Set();
                _runSynchronizationContinueWaitHandle.Set();
                if (_threadPool != null)
                    DisposeThreadPool();
                if (_connectionProxyPool != null)
                    _connectionProxyPool.ShutDown();
                if (_stresstestResult != null)
                    _stresstestResult.StoppedAt = DateTime.Now;
            }
        }

        private void DisposeThreadPool()
        {
            if (_threadPool != null)
                try
                {
                    _threadPool.Dispose(100);
                }
                catch { }
            _threadPool = null;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                try
                {
                    _isDisposed = true;
                    Cancel();
                    _connectionProxyPool.Dispose();
                    _connectionProxyPool = null;

                    _sleepWaitHandle.Close();
                    _sleepWaitHandle.Dispose();
                    _sleepWaitHandle = null;

                    _runSynchronizationContinueWaitHandle.Close();
                    _runSynchronizationContinueWaitHandle.Dispose();
                    _runSynchronizationContinueWaitHandle = null;

                    _logEntries = null;
                    _testableLogEntries = null;
                    _testPatternsAndDelaysGenerator.Dispose();
                    _testPatternsAndDelaysGenerator = null;
                    _stresstestResult = null;
                    _sw = null;
                }
                catch { }

                StaticObjectServiceWrapper.ObjectService.Unsuscribe(this);
            }
        }

        #region Events
        public event EventHandler<StresstestResultEventArgs> StresstestStarted;
        public event EventHandler<ConcurrencyResultEventArgs> ConcurrentUsersStarted;
        public event EventHandler<RunResultEventArgs> RunStarted;
        public event EventHandler<RunResultEventArgs> RunStopped;
        public event EventHandler<RunResultEventArgs> RunInitializedFirstTime;
        public event EventHandler RunDoneOnce;
        public event EventHandler<MessageEventArgs> Message;

        private void SetStresstestStarted()
        {
            var stresstestConfiguration = new StresstestConfiguration()
            {
                Stresstest = _stresstest.ToString(),
                RunSynchronization = _runSynchronization.ToString(),
                Connection = _stresstest.Connection.ToString(),
                ConnectionProxy = _stresstest.ConnectionProxy.ToString(),
                ConnectionString = _stresstest.Connection.ConnectionString,
                Log = _stresstest.Log.ToString(),
                LogRuleSet = _stresstest.LogRuleSet.ToString(),
                //Monitors
                Concurrency = _stresstest.Concurrency.Combine(", "),
                Runs = _stresstest.Runs,
                MinimumDelay = _stresstest.MinimumDelay,
                MaximumDelay = _stresstest.MaximumDelay,
                Shuffle = _stresstest.Shuffle,
                Distribute = _stresstest.Distribute.ToString(),
                MonitorBefore = _stresstest.MonitorBefore,
                MonitorAfter = _stresstest.MonitorAfter
            };
            _stresstestResult = new StresstestResult() { StartedAt = DateTime.Now, StresstestConfiguration = stresstestConfiguration };

            InvokeMessage("Starting the stresstest...");

            if (!_cancel && StresstestStarted != null)
                SynchronizationContextWrapper.SynchronizationContext.Send(delegate { StresstestStarted(this, new StresstestResultEventArgs(_stresstestResult)); }, null);
        }
        private void SetConcurrencyStarted(int concurrentUsersIndex)
        {
            int concurrentUsers = _stresstest.Concurrency[concurrentUsersIndex];
            _concurrentUsersResult = new ConcurrencyResult() { ConcurrentUsers = concurrentUsers, RunCount = _stresstest.Runs, StartedAt = DateTime.Now };
            _stresstestResult.ConcurrencyResults.Add(_concurrentUsersResult);
            InvokeMessage(string.Format("|-> {0} Concurrent Users... (Initializing the first run, be patient)", concurrentUsers), Color.MediumPurple);

            if (!_cancel && ConcurrentUsersStarted != null)
                SynchronizationContextWrapper.SynchronizationContext.Send(delegate { ConcurrentUsersStarted(this, new ConcurrencyResultEventArgs(_concurrentUsersResult)); }, null);
        }
        /// <summary>
        /// For monitoring --> to know the time offset of the counters so a range can be linked to a run.
        /// The current run result (_runResult) is also given with in the event's event args.
        /// </summary>
        private void SetRunStarted()
        {
            _runResult.StartedAt = DateTime.Now;
            if (_cancel && RunStarted != null)
                SynchronizationContextWrapper.SynchronizationContext.Send(delegate { RunStarted(this, new RunResultEventArgs(_runResult)); }, null);
        }
        /// <summary>
        /// For monitoring --> to know the time offset of the counters so a range can be linked to a run.
        /// </summary>
        private void SetRunStopped()
        {
            _runResult.StoppedAt = DateTime.Now;
            vApus.Results.Metrics metrics = vApus.Results.MetricsHelper.GetMetrics(_runResult);
            InvokeMessage("|----> |Run Finished in " + metrics.MeasuredRunTime + "!", Color.MediumPurple);

            if (_cancel && RunStopped != null)
                SynchronizationContextWrapper.SynchronizationContext.Send(delegate { RunStopped(this, new RunResultEventArgs(_runResult)); }, null);
        }
        /// <summary>
        /// For run sync
        /// </summary>
        /// <param name="concurrentUsersIndex"></param>
        /// <param name="run"></param>
        private void SetRunInitializedFirstTime(int concurrentUsersIndex, int run)
        {
            InvokeMessage(string.Format("|----> |Run {0}...", run), Color.MediumPurple);

            int singleUserLogEntryCount = _testPatternsAndDelaysGenerator.PatternLength;
            int concurrentUsers = _stresstest.Concurrency[concurrentUsersIndex];

            _runResult = new RunResult() { Run = run, VirtualUserResults = new VirtualUserResult[concurrentUsers] };
            for (int i = 0; i < concurrentUsers; i++)
                _runResult.VirtualUserResults[i] = new VirtualUserResult() { UserActionCount = _testPatternsAndDelaysGenerator.UserActionsInPattern, LogEntryResults = new LogEntryResult[singleUserLogEntryCount] };

            _concurrentUsersResult.RunResults.Add(_runResult);

            if (!_cancel && RunInitializedFirstTime != null)
                SynchronizationContextWrapper.SynchronizationContext.Send(delegate { RunInitializedFirstTime(this, new RunResultEventArgs(_runResult)); }, null);
        }
        /// <summary>
        /// For run sync (break on last finished)
        /// </summary>
        private void SetRunDoneOnce()
        {
            if (!_runDoneOnce)
            {
                _runDoneOnce = true;
                if (!_cancel && RunDoneOnce != null)
                    SynchronizationContextWrapper.SynchronizationContext.Send(delegate { RunDoneOnce(this, null); }, null);
            }
        }
        private void _threadPool_ThreadWorkException(object sender, MessageEventArgs e)
        {
            InvokeMessage(e.Message, e.Color, e.LogLevel);
        }
        private void InvokeMessage(string message, LogLevel logLevel = LogLevel.Info)
        {
            InvokeMessage(message, Color.Empty, logLevel);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="color">can be Color.Empty</param>
        /// <param name="logLevel"></param>
        private void InvokeMessage(string message, Color color, LogLevel logLevel = LogLevel.Info)
        {
            try
            {
                LogWrapper.LogByLevel(message, logLevel);
                if (Message != null)
                    SynchronizationContextWrapper.SynchronizationContext.Send(delegate { Message(this, new MessageEventArgs(message, color, logLevel)); }, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed invoking message: " + message + " at log level: " + logLevel + ".\n" + ex.ToString());
            }
        }
        #endregion

        #endregion

        /// <summary>
        /// Log entry with metadata.
        /// </summary>
        private struct TestableLogEntry
        {
            public StringTree ParameterizedLogEntry;

            public int UserActionIndex;
            /// <summary>
            /// Should be log.IndexOf(LogEntry) or log.IndexOf(UserAction) + "." + UserAction.IndexOf(LogEntry), this must be unique
            /// </summary>
            public string LogEntryIndex, ParameterizedLogEntryString, UserAction;
            /// <summary>
            /// Execute parallel with immediate previous sibling;
            /// </summary>
            public bool ExecuteInParallelWithPrevious;
            /// <summary>
            /// The offset in ms before this 'parallel log entry' is executed (this simulates what browsers do).
            /// </summary>
            public int ParallelOffsetInMs;

            /// <summary>
            /// Log entry with metadata.
            /// </summary>
            /// <param name="logEntryIndex">Should be log.IndexOf(LogEntry) or log.IndexOf(UserAction) + '.'. + UserAction.IndexOf(LogEntry), this must be unique</param>
            /// <param name="parameterizedLogEntry"></param>
            /// <param name="userAction">Can not be null, string.empty is allowed</param>
            /// <param name="userAction">Can not be null, string.empty is allowed</param>
            /// <param name="generateWhileTestingParameterTokens">The needed ones will be filtered.</param>
            public TestableLogEntry(string logEntryIndex, StringTree parameterizedLogEntry, int userActionIndex, string userAction,
                bool executeInParallelWithPrevious, int parallelOffsetInMs)
            {
                if (userAction == null)
                    throw new ArgumentNullException("userAction");

                LogEntryIndex = logEntryIndex;
                UserActionIndex = userActionIndex;

                ParameterizedLogEntry = parameterizedLogEntry;
                ParameterizedLogEntryString = ParameterizedLogEntry.CombineValues();
                UserAction = userAction;
                ExecuteInParallelWithPrevious = executeInParallelWithPrevious;
                ParallelOffsetInMs = parallelOffsetInMs;
            }
        }
    }
}