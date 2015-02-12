﻿/*
 * Copyright 2012 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using MySql.Data.MySqlClient;
using RandomUtils;
using RandomUtils.Log;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using vApus.Util;

namespace vApus.Results {
    /// <summary>
    /// Creates a new results db. You can connect to an existing one to using ConnectToExistingDatabase(...).
    /// </summary>
    public class ResultsHelper {

        #region Fields
        private readonly object _lock = new object();

        private string _databaseName;

        private DatabaseActions _databaseActions;
        private string _passwordGUID = "{51E6A7AC-06C2-466F-B7E8-4B0A00F6A21F}";

        private readonly byte[] _salt = { 0x49, 0x16, 0x49, 0x2e, 0x11, 0x1e, 0x45, 0x24, 0x86, 0x05, 0x01, 0x03, 0x62 };

        private int _vApusInstanceId, _stresstestId;
        private ulong _stresstestResultId, _concurrencyResultId, _runResultId;

        private FunctionOutputCache _functionOutputCache = new FunctionOutputCache(); //For caching the not so stored procedure data.

        private ConcurrentBag<string> _logs = new ConcurrentBag<string>();
        #endregion

        #region Properties
        /// <summary>
        /// Returns null if not connected
        /// </summary>
        public string DatabaseName { get { return _databaseName; } }

        /// <summary>
        /// You can change this value for when you are distributed testing, so the right dataset is chosen.
        /// This value is not used when filtering results in the procedures eg GetAverageConcurrentUsers.
        /// </summary>
        public int StresstestId {
            get { return _stresstestId; }
            set { lock (_lock) _stresstestId = value; }
        }
        #endregion

        #region Initialize database before stresstest
        /// <summary>
        ///     Builds the schema if needed, if no MySQL target is found or no connection could be made an exception is returned.
        /// </summary>
        /// <returns></returns>
        public Exception BuildSchemaAndConnect() {
            lock (_lock) {
                ClearCache();
                _databaseName = null;
                try {
                    _databaseName = Schema.Build();
                    _databaseActions = Schema.GetDatabaseActionsUsingDatabase(_databaseName);
                } catch (Exception ex) {
                    if (_databaseActions != null) {
                        _databaseActions.ReleaseConnection();
                        _databaseActions = null;
                    }
                    return ex;
                }
                return null;
            }
        }

        /// <summary>
        ///     Only inserts if connected (Call BuildSchemaAndConnect).
        /// </summary>
        /// <param name="description"></param>
        /// <param name="tags"></param>
        public void SetDescriptionAndTags(string description, string[] tags) {
            lock (_lock) {
                if (_databaseActions != null) {
                    _databaseActions.ExecuteSQL("INSERT INTO description(Description) VALUES('" + description + "')");
                    var rowsToInsert = new List<string>(); //Insert multiple values at once.

                    foreach (string tag in tags) {
                        var sb = new StringBuilder("('");
                        sb.Append(tag);
                        sb.Append("')");
                        rowsToInsert.Add(sb.ToString());
                    }
                    _databaseActions.ExecuteSQL(string.Format("INSERT INTO tags(Tag) VALUES {0};", rowsToInsert.Combine(", ")));
                }
            }
        }

        /// <summary>
        /// Do this last for the master, this sets _vApusInstanceId used in this class.
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="version"></param>
        /// <param name="isMaster"></param>
        /// <returns>Id of the stresstest. 0 if BuildSchemaAndConnect was not called.</returns>
        public int SetvApusInstance(string hostName, string ip, int port, string version, string channel, bool isMaster) {
            lock (_lock) {
                if (_databaseActions != null) {
                    _databaseActions.ExecuteSQL(
                        string.Format("INSERT INTO vapusinstances(HostName, IP, Port, Version, Channel, IsMaster) VALUES('{0}', '{1}', '{2}', '{3}', '{4}', '{5}')",
                        hostName, ip, port, version, channel, isMaster ? 1 : 0)
                        );
                    _vApusInstanceId = (int)_databaseActions.GetLastInsertId();
                    return _vApusInstanceId;
                }
                return 0;
            }
        }

        /// <summary>
        /// This sets _stresstestId used in this class.
        /// </summary>
        /// <param name="vApusInstanceId"></param>
        /// <param name="stresstest"></param>
        /// <param name="runSynchronization"></param>
        /// <param name="connection"></param>
        /// <param name="connectionProxy"></param>
        /// <param name="connectionString">Will be encrypted.</param>
        /// <param name="log"></param>
        /// <param name="logRuleSet"></param>
        /// <param name="concurrencies"></param>
        /// <param name="runs"></param>
        /// <param name="minimumDelayInMilliseconds"></param>
        /// <param name="maximumDelayInMilliseconds"></param>
        /// <param name="shuffle"></param>
        /// <param name="distribute"></param>
        /// <param name="monitorBeforeInMinutes"></param>
        /// <param name="monitorAfterInMinutes"></param>
        /// <returns>Id of the stresstest. 0 if BuildSchemaAndConnect was not called.</returns>
        public int SetStresstest(string stresstest, string runSynchronization, string connection, string connectionProxy, string connectionString, string log, string logRuleSet, int[] concurrencies, int runs,
                                         int minimumDelayInMilliseconds, int maximumDelayInMilliseconds, bool shuffle, bool actionDistribution, int maximumNumberOfUserActions, int monitorBeforeInMinutes, int monitorAfterInMinutes) {
            lock (_lock) {
                if (_databaseActions != null) {
                    _databaseActions.ExecuteSQL(
                        string.Format(@"INSERT INTO stresstests(vApusInstanceId, Stresstest, RunSynchronization, Connection, ConnectionProxy, ConnectionString, Logs, LogRuleSet, Concurrencies, Runs,
MinimumDelayInMilliseconds, MaximumDelayInMilliseconds, Shuffle, ActionDistribution, MaximumNumberOfUserActions, MonitorBeforeInMinutes, MonitorAfterInMinutes)
VALUES('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', '{11}', '{12}', '{13}', '{14}', '{15}', '{16}')",
                                      _vApusInstanceId, stresstest, runSynchronization, connection, connectionProxy, connectionString.Encrypt(_passwordGUID, _salt), log, logRuleSet,
                                      concurrencies.Combine(", "), runs, minimumDelayInMilliseconds, maximumDelayInMilliseconds, shuffle ? 1 : 0, actionDistribution ? 1 : 0,
                                      maximumNumberOfUserActions, monitorBeforeInMinutes, monitorAfterInMinutes)
                        );
                    _stresstestId = (int)_databaseActions.GetLastInsertId();
                    return _stresstestId;
                }
                return 0;
            }
        }

        /// <summary>
        /// Use this for single tests.
        /// </summary>
        /// <param name="stresstestId"></param>
        /// <param name="monitor"></param>
        /// <param name="connectionString">Will be encrypted.</param>
        /// <param name="machineConfiguration"></param>
        /// <param name="resultHeaders"></param>
        /// <returns>
        ///     The monitor configuration id in the database, set this in the proper monitor result cache.
        ///     0 if BuildSchemaAndConnect was not called..
        /// </returns>
        public ulong SetMonitor(string monitor, string monitorSource, string connectionString, string machineConfiguration, string[] resultHeaders) {
            return SetMonitor(_stresstestId, monitor, monitorSource, connectionString, machineConfiguration, resultHeaders);
        }
        /// <summary>
        /// Use this for distributed tests.
        /// </summary>
        /// <param name="stresstestId"></param>
        /// <param name="monitor"></param>
        /// <param name="monitorSource"></param>
        /// <param name="connectionString"></param>
        /// <param name="machineConfiguration"></param>
        /// <param name="resultHeaders"></param>
        /// <returns>Id of the monitor. 0 if BuildSchemaAndConnect was not called.</returns>
        public ulong SetMonitor(int stresstestId, string monitor, string monitorSource, string connectionString, string machineConfiguration, string[] resultHeaders) {
            lock (_lock) {
                if (_databaseActions != null) {
                    if (machineConfiguration == null) machineConfiguration = string.Empty;
                    _databaseActions.ExecuteSQL(
                        string.Format(
                            "INSERT INTO monitors(StresstestId, Monitor, MonitorSource, ConnectionString, MachineConfiguration, ResultHeaders) VALUES('{0}', ?Monitor, ?MonitorSource, ?ConnectionString, ?MachineConfiguration, ?ResultHeaders)", stresstestId),
                            CommandType.Text, new MySqlParameter("?Monitor", monitor), new MySqlParameter("?MonitorSource", monitorSource), new MySqlParameter("?ConnectionString", connectionString.Encrypt(_passwordGUID, _salt)),
                                new MySqlParameter("?MachineConfiguration", machineConfiguration), new MySqlParameter("?ResultHeaders", resultHeaders.Combine("; ", string.Empty))
                        );
                    return _databaseActions.GetLastInsertId();
                }
                return 0;
            }
        }
        #endregion

        #region Set Results

        #region Stresstest results
        /// <summary>
        ///     Started at datetime now.
        /// </summary>
        /// <param name="stresstestResult"></param>
        public void SetStresstestStarted(StresstestResult stresstestResult) {
            lock (_lock) {
                if (_databaseActions != null) {
                    _databaseActions.ExecuteSQL(
                        string.Format(
                            "INSERT INTO stresstestresults(StresstestId, StartedAt, StoppedAt, Status, StatusMessage) VALUES('{0}', '{1}', '{2}', 'OK', '')",
                            _stresstestId, Parse(stresstestResult.StartedAt), Parse(DateTime.MinValue))
                        );
                    _stresstestResultId = _databaseActions.GetLastInsertId();
                }
            }
        }

        /// <summary>
        ///     Stopped at datetime now.
        /// </summary>
        /// <param name="stresstestResult"></param>
        /// <param name="status"></param>
        /// <param name="statusMessage"></param>
        public void SetStresstestStopped(StresstestResult stresstestResult, string status = "OK", string statusMessage = "") {
            lock (_lock) {
                stresstestResult.StoppedAt = DateTime.Now;
                if (_databaseActions != null) {
                    _databaseActions.ExecuteSQL(
                        string.Format(
                            "UPDATE stresstestresults SET StoppedAt='{1}', Status='{2}', StatusMessage='{3}' WHERE Id='{0}'",
                            _stresstestResultId, Parse(stresstestResult.StoppedAt), status, statusMessage)
                        );

                    DoAddLogEntries();
                }
            }
        }
        #endregion

        #region Concurrency results

        /// <summary>
        ///     Started at datetime now.
        /// </summary>
        /// <param name="stresstestResultId"></param>
        /// <param name="concurrencyResult"></param>
        public void SetConcurrencyStarted(ConcurrencyResult concurrencyResult) {
            lock (_lock) {
                if (_databaseActions != null) {
                    _databaseActions.ExecuteSQL(
                        string.Format(
                            "INSERT INTO concurrencyresults(StresstestResultId, Concurrency, StartedAt, StoppedAt) VALUES('{0}', '{1}', '{2}', '{3}')",
                            _stresstestResultId, concurrencyResult.Concurrency, Parse(concurrencyResult.StartedAt),
                            Parse(DateTime.MinValue))
                        );
                    _concurrencyResultId = _databaseActions.GetLastInsertId();
                }
            }
        }

        /// <summary>
        ///     Stopped at datetime now.
        /// </summary>
        /// <param name="concurrencyResult"></param>
        public void SetConcurrencyStopped(ConcurrencyResult concurrencyResult) {
            lock (_lock) {
                concurrencyResult.StoppedAt = DateTime.Now;
                if (_databaseActions != null)
                    _databaseActions.ExecuteSQL(
                        string.Format("UPDATE concurrencyresults SET StoppedAt='{1}' WHERE Id='{0}'", _concurrencyResultId,
                                      Parse(concurrencyResult.StoppedAt))
                        );
            }
        }

        #endregion

        #region Run results

        /// <summary>
        /// </summary>
        /// <param name="concurrencyResultId"></param>
        /// <param name="reRunCount"></param>
        /// <param name="startedAt"></param>
        /// <param name="stoppedAt"></param>
        /// <returns>Id of the run result.</returns>
        public void SetRunStarted(RunResult runResult) {
            lock (_lock) {
                if (_databaseActions != null) {
                    _databaseActions.ExecuteSQL(
                        string.Format(
                            "INSERT INTO runresults(ConcurrencyResultId, Run, TotalLogEntryCount, ReRunCount, StartedAt, StoppedAt) VALUES('{0}', '{1}', '0', '0', '{2}', '{3}')",
                            _concurrencyResultId, runResult.Run, Parse(runResult.StartedAt), Parse(DateTime.MinValue))
                        );
                    _runResultId = _databaseActions.GetLastInsertId();
                }
            }
        }

        /// <summary>
        ///     Increase the rerun count value for the result using fx PrepareForRerun() before calling this fx.
        /// </summary>
        /// <param name="runResult"></param>
        public void SetRerun(RunResult runResult) {
            lock (_lock) {
                if (_databaseActions != null)
                    _databaseActions.ExecuteSQL(
                        string.Format("UPDATE runresults SET ReRunCount='{1}' WHERE Id='{0}'", _runResultId,
                                      runResult.RerunCount)
                        );
            }
        }

        /// <summary>
        ///     All the log entry results are save to the database doing this, only do this for the curent run.
        /// </summary>
        /// <param name="runResult"></param>
        public void SetRunStopped(RunResult runResult) {
            lock (_lock) {
                runResult.StoppedAt = DateTime.Now;
                if (_databaseActions != null) {
                    ulong totalLogEntryCount = 0;

                    var sb = new StringBuilder();
                    foreach (VirtualUserResult virtualUserResult in runResult.VirtualUserResults) {
                        totalLogEntryCount += (ulong)virtualUserResult.LogEntryResults.LongLength;

                        if (virtualUserResult != null) {
                            var rowsToInsert = new List<string>(); //Insert multiple values at once.
                            foreach (var logEntryResult in virtualUserResult.LogEntryResults)
                                if (logEntryResult != null && logEntryResult.VirtualUser != null) {
                                    sb.Append("('");
                                    sb.Append(_runResultId);
                                    sb.Append("', '");
                                    sb.Append(virtualUserResult.VirtualUser);
                                    sb.Append("', '");
                                    sb.Append(MySQLEscapeString(logEntryResult.UserAction));
                                    sb.Append("', '");
                                    sb.Append(logEntryResult.LogEntryIndex);
                                    sb.Append("', '");
                                    sb.Append(logEntryResult.SameAsLogEntryIndex);
                                    sb.Append("', '");
                                    sb.Append(MySQLEscapeString(logEntryResult.LogEntry));
                                    sb.Append("', '");
                                    sb.Append(Parse(logEntryResult.SentAt));
                                    sb.Append("', '");
                                    sb.Append(logEntryResult.TimeToLastByteInTicks);
                                    sb.Append("', '");
                                    sb.Append(logEntryResult.DelayInMilliseconds);
                                    sb.Append("', '");
                                    sb.Append(MySQLEscapeString(logEntryResult.Error));
                                    sb.Append("', '");
                                    sb.Append(logEntryResult.Rerun);
                                    sb.Append("')");
                                    rowsToInsert.Add(sb.ToString());
                                    sb.Clear();

                                    if (rowsToInsert.Count == 100) {
                                        _databaseActions.ExecuteSQL(string.Format("INSERT INTO logentryresults(RunResultId, VirtualUser, UserAction, LogEntryIndex, SameAsLogEntryIndex, LogEntry, SentAt, TimeToLastByteInTicks, DelayInMilliseconds, Error, Rerun) VALUES {0};",
                                            rowsToInsert.Combine(", ")));
                                        rowsToInsert.Clear();
                                    }
                                }

                            if (rowsToInsert.Count != 0)
                                _databaseActions.ExecuteSQL(string.Format("INSERT INTO logentryresults(RunResultId, VirtualUser, UserAction, LogEntryIndex, SameAsLogEntryIndex, LogEntry, SentAt, TimeToLastByteInTicks, DelayInMilliseconds, Error, Rerun) VALUES {0};",
                                rowsToInsert.Combine(", ")));
                        }
                    }
                    _databaseActions.ExecuteSQL(string.Format("UPDATE runresults SET TotalLogEntryCount='{1}', StoppedAt='{2}' WHERE Id='{0}'", _runResultId, totalLogEntryCount, Parse(runResult.StoppedAt)));

                    DoAddLogEntries();
                }
            }
        }
        #endregion

        #region Monitor results

        /// <summary>
        ///     Do this at the end of the test.
        /// </summary>
        /// <param name="monitorResultCache">Should have a filled in monitor configuration id.</param>
        public void SetMonitorResults(MonitorResult monitorResultCache) {
            lock (_lock) {
                //Store monitor values with a ',' for decimal seperator.
                CultureInfo prevCulture = Thread.CurrentThread.CurrentCulture;
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("nl-BE");
                if (_databaseActions != null && monitorResultCache.Rows.Count != 0) {
                    ulong monitorConfigurationId = monitorResultCache.MonitorConfigurationId;
                    var rowsToInsert = new List<string>(); //Insert multiple values at once.
                    foreach (var row in monitorResultCache.Rows) {
                        var value = new List<string>();
                        for (int i = 1; i < row.Length; i++) {
                            object cell = row[i];
                            if (cell is double)
                                value.Add(StringUtil.DoubleToLongString((double)cell));
                            else
                                value.Add(cell.ToString());
                        }

                        var sb = new StringBuilder("('");
                        sb.Append(monitorConfigurationId);
                        sb.Append("', '");
                        sb.Append(Parse((DateTime)row[0]));
                        sb.Append("', '");
                        sb.Append(MySQLEscapeString(MySQLEscapeString(value.Combine("; "))));
                        sb.Append("')");
                        rowsToInsert.Add(sb.ToString());
                    }
                    _databaseActions.ExecuteSQL(string.Format("INSERT INTO monitorresults(MonitorId, TimeStamp, Value) VALUES {0};", rowsToInsert.Combine(", ")));
                }
                Thread.CurrentThread.CurrentCulture = prevCulture;

                DoAddLogEntries();
            }
        }

        #endregion

        #endregion

        /// <summary>
        /// Kept in memory and added every run and when the test ends.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="entry"></param>
        public void AddLogEntry(int level, string entry) {
            if (_vApusInstanceId > 0 && _databaseActions != null)
                _logs.Add(string.Format("('{0}', '{1}', '{2}', '{3}')", _vApusInstanceId, Parse(DateTime.Now), level, entry));
        }
        private void DoAddLogEntries() {
            string[] logs = _logs.ToArray();
            if (logs.Length != 0) {
                if (_vApusInstanceId > 0 && _databaseActions != null) 
                    _databaseActions.ExecuteSQL(string.Format("INSERT INTO logs(vApusInstanceId, Timestamp, Level, Entry) VALUES {0};", logs.Combine(", ")));
                _logs = new ConcurrentBag<string>();
            }
            logs = null;
        }

        //For getting stuff fom the database ReaderAndCombiner is used: You can execute a many-to-one distributed test (a tests workload divided over multiple slaves);
        //Results for such test must be combined before processing: it must look like a single test.
        #region Get Configuration and Formatted Results

        #region Configuration
        public string GetDescription() {
            lock (_lock) {
                if (_databaseActions != null) {
                    var dt = ReaderAndCombiner.GetDescription(_databaseActions); ;
                    foreach (DataRow row in dt.Rows) return row.ItemArray[0] as string;
                }
                return string.Empty;
            }
        }
        public List<string> GetTags() {
            lock (_lock) {
                var l = new List<string>();
                if (_databaseActions != null) {
                    var dt = ReaderAndCombiner.GetTags(_databaseActions);
                    foreach (DataRow row in dt.Rows) l.Add(row.ItemArray[0] as string);
                }
                return l;
            }
        }
        public List<int> GetvApusInstanceIds() {
            lock (_lock) {
                var l = new List<int>();
                if (_databaseActions != null) {
                    var dt = ReaderAndCombiner.GetvApusInstances(_databaseActions);
                    foreach (DataRow row in dt.Rows) l.Add((int)row.ItemArray[0]);
                }
                return l;
            }
        }
        public List<KeyValuePair<string, string>> GetvApusInstances() {
            lock (_lock) {
                var l = new List<KeyValuePair<string, string>>();
                if (_databaseActions != null) {
                    var dt = ReaderAndCombiner.GetvApusInstances(_databaseActions);
                    foreach (DataRow row in dt.Rows) {
                        l.Add(new KeyValuePair<string, string>(row.ItemArray[1] as string, string.Empty));
                        l.Add(new KeyValuePair<string, string>(row.ItemArray[2] + ":" + row[3], string.Empty));
                        l.Add(new KeyValuePair<string, string>("Version", row.ItemArray[4] as string));
                        l.Add(new KeyValuePair<string, string>("Channel", row.ItemArray[5] as string));
                        l.Add(new KeyValuePair<string, string>("Is Master", ((bool)row.ItemArray[6]) ? "Yes" : "No"));
                    }
                }
                return l;
            }
        }

        public List<KeyValuePair<string, string>> GetStresstestConfigurations(params int[] stresstestIds) {
            lock (_lock) {
                try {
                    var l = new List<KeyValuePair<string, string>>();
                    if (_databaseActions != null) {
                        var dt = ReaderAndCombiner.GetStresstests(new CancellationToken(), _databaseActions, stresstestIds);
                        foreach (DataRow row in dt.Rows) {
                            l.Add(new KeyValuePair<string, string>(row.ItemArray[2] as string, string.Empty));
                            l.Add(new KeyValuePair<string, string>("Run Synchronization", row.ItemArray[3] as string));
                            l.Add(new KeyValuePair<string, string>(row.ItemArray[4] as string, string.Empty));
                            l.Add(new KeyValuePair<string, string>(row.ItemArray[5] as string, string.Empty));
                            l.Add(new KeyValuePair<string, string>(row.ItemArray[7] as string, string.Empty));
                            l.Add(new KeyValuePair<string, string>(row.ItemArray[8] as string, string.Empty));
                            l.Add(new KeyValuePair<string, string>("Concurrencies", row.ItemArray[9] as string));
                            l.Add(new KeyValuePair<string, string>("Runs", row.ItemArray[10].ToString()));
                            int minDelay = (int)row.ItemArray[11];
                            int maxDelay = (int)row.ItemArray[12];
                            l.Add(new KeyValuePair<string, string>("Delay", minDelay == maxDelay ? minDelay + " ms" : minDelay + " - " + maxDelay + " ms"));
                            l.Add(new KeyValuePair<string, string>("Shuffle", ((bool)row.ItemArray[13]) ? "Yes" : "No"));
                            l.Add(new KeyValuePair<string, string>("Action Distribution", ((bool)row.ItemArray[14]) ? "Yes" : "No"));
                            l.Add(new KeyValuePair<string, string>("Maximum Number of User Actions", ((int)row.ItemArray[15]) == 0 ? "N/A" : ((int)row.ItemArray[15]).ToString()));
                            l.Add(new KeyValuePair<string, string>("Monitor Before", row.ItemArray[16] + " minutes"));
                            l.Add(new KeyValuePair<string, string>("Monitor After", row.ItemArray[17] + " minutes"));
                        }
                    }
                    return l;
                } catch { //backwards compatible
                    var l = new List<KeyValuePair<string, string>>();
                    if (_databaseActions != null) {
                        var dt = ReaderAndCombiner.GetStresstests(new CancellationToken(), _databaseActions, stresstestIds);
                        foreach (DataRow row in dt.Rows) {
                            l.Add(new KeyValuePair<string, string>(row.ItemArray[2] as string, string.Empty));
                            l.Add(new KeyValuePair<string, string>("Run Synchronization", row.ItemArray[3] as string));
                            l.Add(new KeyValuePair<string, string>(row.ItemArray[4] as string, string.Empty));
                            l.Add(new KeyValuePair<string, string>(row.ItemArray[5] as string, string.Empty));
                            l.Add(new KeyValuePair<string, string>(row.ItemArray[7] as string, string.Empty));
                            l.Add(new KeyValuePair<string, string>(row.ItemArray[8] as string, string.Empty));
                            l.Add(new KeyValuePair<string, string>("Concurrencies", row.ItemArray[9] as string));
                            l.Add(new KeyValuePair<string, string>("Runs", row.ItemArray[10].ToString()));
                            int minDelay = (int)row.ItemArray[11];
                            int maxDelay = (int)row.ItemArray[12];
                            l.Add(new KeyValuePair<string, string>("Delay", minDelay == maxDelay ? minDelay + " ms" : minDelay + " - " + maxDelay + " ms"));
                            l.Add(new KeyValuePair<string, string>("Shuffle", ((bool)row.ItemArray[13]) ? "Yes" : "No"));
                            l.Add(new KeyValuePair<string, string>("Distribute", row.ItemArray[14] as string));
                            l.Add(new KeyValuePair<string, string>("Monitor Before", row.ItemArray[15] + " minutes"));
                            l.Add(new KeyValuePair<string, string>("Monitor After", row.ItemArray[16] + " minutes"));
                        }
                    }
                    return l;
                }
            }
        }
        public List<string> GetMonitors() {
            lock (_lock) {
                var l = new List<string>();
                if (_databaseActions != null)
                    foreach (DataRow row in ReaderAndCombiner.GetMonitors(new CancellationToken(), _databaseActions, null, "Monitor").Rows)
                        l.Add(row.ItemArray[0] as string);

                return l;
            }
        }
        public List<string> GetMonitorResultHeaders(string monitor) {
            lock (_lock) {
                var l = new List<string>();
                if (_databaseActions != null)
                    foreach (DataRow row in ReaderAndCombiner.GetMonitors(new CancellationToken(), _databaseActions, "Monitor='" + monitor + "'", "ResultHeaders").Rows)
                        l.AddRange((row.ItemArray[0] as string).Split(new string[] { "; " }, StringSplitOptions.None));

                return l;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="stresstestIds"></param>
        /// <returns>key = monitor id, value = monitor name</returns>
        public Dictionary<int, string> GetMonitors(int[] stresstestIds) {
            lock (_lock) {
                var d = new Dictionary<int, string>();
                if (_databaseActions != null)
                    foreach (DataRow row in ReaderAndCombiner.GetMonitors(new CancellationToken(), _databaseActions, null, stresstestIds, "Id", "Monitor").Rows)
                        d.Add((int)row[0], row[1] as string);

                return d;
            }
        }

        #endregion

        #region Formatted Results
        /// <summary>
        /// Only works for the first stresstest. This is a known issue and it will not be fixed: 1 datatable per stressstest only, otherwise the overview is worth nothing. Use a loop to enumerate multiple stresstest ids.
        /// </summary>
        /// <param name="cancellationToken">Used in await Task.Run...</param>
        /// <param name="stresstestIds">If none, results for the first test will be returned.</param>
        /// <returns></returns>
        public DataTable GetOverview(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions == null) return null;

                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                if (cacheEntryDt == null) {
                    if (stresstestIds.Length == 0) {
                        var stresstests = ReaderAndCombiner.GetStresstests(cancellationToken, _databaseActions, "Id");
                        if (stresstests == null || stresstests.Rows.Count == 0) return null;

                        stresstestIds = new int[] { (int)stresstests.Rows[0].ItemArray[0] };
                    }

                    DataTable overview = GetResultSet("Overview", stresstestIds);
                    if (overview != null) {
                        cacheEntry.ReturnValue = overview;
                        return overview;
                    }

                    cacheEntry.ReturnValue = GetOverview(cancellationToken, "Avg. Response Time (ms)", stresstestIds);
                }
                return cacheEntry.ReturnValue as DataTable;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="stresstestIds"></param>
        /// <returns></returns>
        public DataTable GetOverview95thPercentile(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions == null) return null;

                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                if (cacheEntryDt == null) {
                    if (stresstestIds.Length == 0) {
                        var stresstests = ReaderAndCombiner.GetStresstests(cancellationToken, _databaseActions, "Id");
                        if (stresstests == null || stresstests.Rows.Count == 0) return null;

                        stresstestIds = new int[] { (int)stresstests.Rows[0].ItemArray[0] };
                    }

                    DataTable overview = GetResultSet("Overview95thPercentile", stresstestIds);
                    if (overview != null) {
                        cacheEntry.ReturnValue = overview;
                        return overview;
                    }

                    cacheEntry.ReturnValue = GetOverview(cancellationToken, "95th Percentile of the Response Times (ms)", stresstestIds);
                }
                return cacheEntry.ReturnValue as DataTable;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="stresstestIds"></param>
        /// <returns></returns>
        public DataTable GetOverview99thPercentile(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions == null) return null;

                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                if (cacheEntryDt == null) {
                    if (stresstestIds.Length == 0) {
                        var stresstests = ReaderAndCombiner.GetStresstests(cancellationToken, _databaseActions, "Id");
                        if (stresstests == null || stresstests.Rows.Count == 0) return null;

                        stresstestIds = new int[] { (int)stresstests.Rows[0].ItemArray[0] };
                    }

                    DataTable overview = GetResultSet("Overview99thPercentile", stresstestIds);
                    if (overview != null) {
                        cacheEntry.ReturnValue = overview;
                        return overview;
                    }

                    cacheEntry.ReturnValue = GetOverview(cancellationToken, "99th Percentile of the Response Times (ms)", stresstestIds);
                }
                return cacheEntry.ReturnValue as DataTable;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="stresstestIds"></param>
        /// <returns></returns>
        public DataTable GetOverviewAverageTop5(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions == null) return null;

                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                if (cacheEntryDt == null) {
                    if (stresstestIds.Length == 0) {
                        var stresstests = ReaderAndCombiner.GetStresstests(cancellationToken, _databaseActions, "Id");
                        if (stresstests == null || stresstests.Rows.Count == 0) return null;

                        stresstestIds = new int[] { (int)stresstests.Rows[0].ItemArray[0] };
                    }

                    DataTable overview = GetResultSet("OverviewAverageTop5", stresstestIds);
                    if (overview != null) {
                        cacheEntry.ReturnValue = overview;
                        return overview;
                    }

                    cacheEntry.ReturnValue = GetOverview(cancellationToken, "Avg. Top 5 Response Times (ms)", stresstestIds);
                }
                return cacheEntry.ReturnValue as DataTable;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="responseTimeColumn">"Avg. Response Time (ms)", "95th Percentile of the Response Times (ms)", 
        /// "99th Percentile of the Response Times (ms)", "Avg. Top 5 Response Times (ms)"</param>
        /// <param name="stresstestIds"></param>
        /// <returns></returns>
        private DataTable GetOverview(CancellationToken cancellationToken, string responseTimeColumn, params int[] stresstestIds) {
            if (_databaseActions == null) return null;

            if (stresstestIds.Length == 0) {
                var stresstests = ReaderAndCombiner.GetStresstests(cancellationToken, _databaseActions, "Id");
                if (stresstests == null || stresstests.Rows.Count == 0) return null;

                stresstestIds = new int[] { (int)stresstests.Rows[0].ItemArray[0] };
            }


            DataTable averageUserActions = GetAverageUserActionResults(cancellationToken, true, stresstestIds);
            if (averageUserActions == null) return null;

            DataTable averageConcurrentUsers = GetAverageConcurrencyResults(cancellationToken, stresstestIds);
            if (averageConcurrentUsers == null) return null;

            DataTable overview = CreateEmptyDataTable("Overview", "Stresstest", "Concurrency");
            int range = 0; //The range of values (avg response times) to place under the right user action
            char colon = ':';
            string sColon = ":";
            int userActionIndex = 1;
            int currentConcurrencyResultId = -1;
            var objectType = typeof(object);

            foreach (DataRow uaRow in averageUserActions.Rows) {
                if (cancellationToken.IsCancellationRequested) return null;

                int concurrencyResultId = (int)uaRow.ItemArray[1];
                if (currentConcurrencyResultId != concurrencyResultId) {
                    userActionIndex = 1; //Do not forget to reset this, otherwise we will only get one row.
                    currentConcurrencyResultId = concurrencyResultId;
                }

                string userAction = uaRow.ItemArray[3] as string;
                string[] splittedUserAction = userAction.Split(colon);
                userAction = string.Join(sColon, userActionIndex++, splittedUserAction[splittedUserAction.Length - 1]);
                if (!overview.Columns.Contains(userAction)) {
                    overview.Columns.Add(userAction, objectType);
                    range++;
                }
            }
            overview.Columns.Add("Throughput", objectType);
            overview.Columns.Add("User Actions / s", objectType);
            overview.Columns.Add("Errors", objectType);

            for (int offset = 0; offset < averageUserActions.Rows.Count; offset += range) {
                if (cancellationToken.IsCancellationRequested) return null;

                var row = new List<object>(range + 3);
                row.Add(averageUserActions.Rows[offset].ItemArray[0]); //Add stresstest
                row.Add(averageUserActions.Rows[offset].ItemArray[2]); //Add concurrency
                for (int i = offset; i != offset + range; i++) { //Add the response times
                    if (cancellationToken.IsCancellationRequested) return null;

                    row.Add(i < averageUserActions.Rows.Count ? Convert.ToDouble(averageUserActions.Rows[i][responseTimeColumn]) : 0d);
                }
                row.Add(averageConcurrentUsers.Rows[overview.Rows.Count]["Throughput (responses / s)"]); //And the throughput
                row.Add(averageConcurrentUsers.Rows[overview.Rows.Count]["User Actions / s"]); //And the throughput
                row.Add(averageConcurrentUsers.Rows[overview.Rows.Count]["Errors"]); //And the errors: Bonus
                overview.Rows.Add(row.ToArray());
            }

            return overview;
        }

        public DataTable GetOverviewErrors(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions == null) return null;

                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                if (cacheEntryDt == null) {
                    cacheEntryDt = GetResultSet("OverviewErrors", stresstestIds);
                    if (cacheEntryDt != null) {
                        cacheEntry.ReturnValue = cacheEntryDt;
                        return cacheEntryDt;
                    }

                    DataTable overview = GetOverview(cancellationToken, stresstestIds);
                    cacheEntryDt = CreateEmptyDataTable("OverviewErrors", "Stresstest", "Concurrency", "Errors", "Throughput");

                    foreach (DataRow row in overview.Rows)
                        cacheEntryDt.Rows.Add(row["Stresstest"], row["Concurrency"], row["Errors"], row["Throughput"]);

                    cacheEntry.ReturnValue = cacheEntryDt;
                }
                return cacheEntryDt;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="stresstestIds">Multiple stresstest ids do not make sense, I think</param>
        /// <returns></returns>
        public DataTable GetTop5HeaviestUserActions(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions == null) return null;

                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                if (cacheEntryDt == null) {
                    cacheEntryDt = GetResultSet("Top5HeaviestUserActions", stresstestIds);
                    if (cacheEntryDt != null) {
                        cacheEntry.ReturnValue = cacheEntryDt;
                        return cacheEntryDt;
                    }

                    cacheEntry.ReturnValue = GetTop5HeaviestUserActions(cancellationToken, "Avg. Response Time (ms)", stresstestIds);
                }
                return cacheEntry.ReturnValue as DataTable;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="stresstestIds"></param>
        /// <returns></returns>
        public DataTable GetTop5HeaviestUserActions95thPercentile(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions == null) return null;

                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                if (cacheEntryDt == null) {
                    cacheEntryDt = GetResultSet("Top5HeaviestUserActions95thPercentile", stresstestIds);
                    if (cacheEntryDt != null) {
                        cacheEntry.ReturnValue = cacheEntryDt;
                        return cacheEntryDt;
                    }

                    cacheEntry.ReturnValue = GetTop5HeaviestUserActions(cancellationToken, "95th Percentile of the Response Times (ms)", stresstestIds);
                }
                return cacheEntry.ReturnValue as DataTable;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="stresstestIds"></param>
        /// <returns></returns>
        public DataTable GetTop5HeaviestUserActions99thPercentile(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions == null) return null;

                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                if (cacheEntryDt == null) {
                    cacheEntryDt = GetResultSet("Top5HeaviestUserActions99thPercentile", stresstestIds);
                    if (cacheEntryDt != null) {
                        cacheEntry.ReturnValue = cacheEntryDt;
                        return cacheEntryDt;
                    }

                    cacheEntry.ReturnValue = GetTop5HeaviestUserActions(cancellationToken, "99th Percentile of the Response Times (ms)", stresstestIds);
                }
                return cacheEntry.ReturnValue as DataTable;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="stresstestIds"></param>
        /// <returns></returns>
        public DataTable GetTop5HeaviestUserActionsAverageTop5(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions == null) return null;

                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                if (cacheEntryDt == null) {
                    cacheEntryDt = GetResultSet("Top5HeaviestUserActionsAverageTop5", stresstestIds);
                    if (cacheEntryDt != null) {
                        cacheEntry.ReturnValue = cacheEntryDt;
                        return cacheEntryDt;
                    }

                    cacheEntry.ReturnValue = GetTop5HeaviestUserActions(cancellationToken, "Avg. Top 5 Response Times (ms)", stresstestIds);
                }
                return cacheEntry.ReturnValue as DataTable;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="throughputColumn">Throughput (responses / s), 95th Percentile of the Response Times (ms), 
        /// 99th Percentile of the Response Times (ms), Avg. Top 5 Response Times (ms)</param>
        /// <param name="stresstestIds"></param>
        /// <returns></returns>
        private DataTable GetTop5HeaviestUserActions(CancellationToken cancellationToken, string throughputColumn, params int[] stresstestIds) {
            if (_databaseActions == null) return null;

            DataTable overview = GetOverview(cancellationToken, throughputColumn, stresstestIds);

            //Find the row with the highest concurrency.
            DataRow heaviestRow = null;
            foreach (DataRow row in overview.Rows)
                if (heaviestRow == null || (int)heaviestRow["Concurrency"] < (int)row["Concurrency"])
                    heaviestRow = row;

            //Get the response times in descending order.
            var heaviestResponseTimes = new Dictionary<string, double>();
            for (int i = 2; i != heaviestRow.ItemArray.Length - 2; i++)  //We do not want the first two and the last two columns.
                heaviestResponseTimes.Add(overview.Columns[i].ColumnName, double.Parse(heaviestRow[i].ToString()));

            var uselessDataStructureMakingStuffHard = heaviestResponseTimes.OrderByDescending(kvp => kvp.Value);
            heaviestResponseTimes = new Dictionary<string, double>();
            foreach (var kvp in uselessDataStructureMakingStuffHard)
                heaviestResponseTimes.Add(kvp.Key, kvp.Value);

            //Add the top 5 column names to a new datatable.
            DataTable top5 = CreateEmptyDataTable("Top5HeaviestUserActions", "Stresstest", "Concurrency");
            var objectType = typeof(object);

            int top = 5;
            if (top > heaviestResponseTimes.Count)
                top = heaviestResponseTimes.Count;

            for (int i = 0; i != top; i++)
                top5.Columns.Add(heaviestResponseTimes.GetKeyAt(i), objectType);

            //Add all top 5 response times.
            foreach (DataRow row in overview.Rows) {
                var newRow = new List<object>();
                newRow.Add(row["Stresstest"]);
                newRow.Add(row["Concurrency"]);

                for (int i = 0; i != top; i++)
                    newRow.Add(row[heaviestResponseTimes.GetKeyAt(i)]);

                top5.Rows.Add(newRow.ToArray());
            }

            return top5;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken">Used in await Task.Run...</param>
        /// <param name="stresstestIds">If none, all the results for all tests will be returned.</param>
        /// <returns></returns>
        public DataTable GetAverageConcurrencyResults(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions != null) {
                    var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                    var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                    if (cacheEntryDt == null) {
                        cacheEntryDt = GetResultSet("AverageConcurrencyResults", stresstestIds);
                        if (cacheEntryDt != null) {
                            cacheEntry.ReturnValue = cacheEntryDt;
                            return cacheEntryDt;
                        }

                        cacheEntry.ReturnValue = AverageConcurrencyResultsCalculator.GetInstance().Get(_databaseActions, cancellationToken, stresstestIds);

                        GC.Collect();
                    }
                    return cacheEntry.ReturnValue as DataTable;
                }
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken">Used in await Task.Run...</param>
        /// <param name="stresstestIds">If none, all the results for all tests will be returned.</param>
        /// <returns></returns>
        public DataTable GetAverageUserActionResults(CancellationToken cancellationToken, params int[] stresstestIds) {
            return GetAverageUserActionResults(cancellationToken, false, stresstestIds);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="withConcurrencyResultId">Needed for the overview datatable.</param>
        /// <param name="stresstestIds"></param>
        /// <returns></returns>
        private DataTable GetAverageUserActionResults(CancellationToken cancellationToken, bool withConcurrencyResultId, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions != null) {
                    //Slightly different way of working, the result for withConcurrencyResultId true and false are calculated here, the right result is given back. This is way faster.
                    var methodBase = MethodInfo.GetCurrentMethod();
                    var cacheEntry = _functionOutputCache.GetOrAdd(methodBase, true, stresstestIds);
                    var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                    if (cacheEntryDt == null) {
                        cacheEntryDt = GetResultSet("AverageUserActionResults", stresstestIds);
                        if (cacheEntryDt != null) {
                            cacheEntry.ReturnValue = cacheEntryDt;
                            return cacheEntryDt;
                        }

                        DataTable averageUserActions = AverageUserActionResultsCalculator.GetInstance().Get(_databaseActions, cancellationToken, stresstestIds);

                        cacheEntry.ReturnValue = averageUserActions;

                        //Add the data table without the concurrency result id column.
                        cacheEntry = _functionOutputCache.GetOrAdd(methodBase, false, stresstestIds);

                        //format the output --> remove the column. Done this way because the calculation only needs to happen once.
                        var newAverageUserActionResults = CreateEmptyDataTable("AverageUserActionResults", "Stresstest", "Concurrency", "User Action", "Avg. Response Time (ms)",
                            "Max. Response Time (ms)", "95th Percentile of the Response Times (ms)", "99th Percentile of the Response Times (ms)", "Avg. Top 5 Response Times (ms)", "Avg. Delay (ms)", "Errors");
                        foreach (DataRow row in averageUserActions.Rows) newAverageUserActionResults.Rows.Add(row[0], row[2], row[3], row[4],
                            row[5], row[6], row[7], row[8], row[9], row[10]);
                        cacheEntry.ReturnValue = newAverageUserActionResults;

                        GC.Collect();
                    }

                    cacheEntry = _functionOutputCache.GetOrAdd(methodBase, withConcurrencyResultId, stresstestIds);
                    return cacheEntry.ReturnValue as DataTable;
                }
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken">Used in await Task.Run...</param>
        /// <param name="stresstestIds">If none, all the results for all tests will be returned.</param>
        /// <returns></returns>
        public DataTable GetAverageLogEntryResults(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions != null) {
                    var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                    var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                    if (cacheEntryDt == null) {
                        cacheEntryDt = GetResultSet("AverageLogEntryResults", stresstestIds);
                        if (cacheEntryDt != null) {
                            cacheEntry.ReturnValue = cacheEntryDt;
                            return cacheEntryDt;
                        }

                        cacheEntry.ReturnValue = AverageLogEntryResultsCalculator.GetInstance().Get(_databaseActions, cancellationToken, stresstestIds);

                        GC.Collect();
                    }
                    return cacheEntry.ReturnValue as DataTable;
                }
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken">Used in await Task.Run...</param>
        /// <param name="stresstestIds">If none, all the results for all tests will be returned.</param>
        /// <returns></returns>
        public DataTable GetErrors(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions != null) {
                    var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                    var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                    if (cacheEntryDt == null) {
                        cacheEntryDt = GetResultSet("Errors", stresstestIds);
                        if (cacheEntryDt != null) {
                            cacheEntry.ReturnValue = cacheEntryDt;
                            return cacheEntryDt;
                        }

                        DataTable stresstests = ReaderAndCombiner.GetStresstests(cancellationToken, _databaseActions, stresstestIds, "Id", "Stresstest", "Connection");
                        if (stresstests == null || stresstests.Rows.Count == 0) return null;

                        var errors = CreateEmptyDataTable("Errors", "Stresstest", "At", "Concurrency", "Run", "Virtual User", "User Action", "Log Entry", "Error");

                        foreach (DataRow stresstestsRow in stresstests.Rows) {
                            if (cancellationToken.IsCancellationRequested) return null;

                            int stresstestId = (int)stresstestsRow.ItemArray[0];

                            DataTable stresstestResults = ReaderAndCombiner.GetStresstestResults(cancellationToken, _databaseActions, stresstestId, "Id");
                            if (stresstestResults == null || stresstestResults.Rows.Count == 0) continue;
                            int stresstestResultId = (int)stresstestResults.Rows[0].ItemArray[0];

                            string stresstest = string.Format("{0} {1}", stresstestsRow.ItemArray[1], stresstestsRow.ItemArray[2]);

                            DataTable concurrencyResults = ReaderAndCombiner.GetConcurrencyResults(cancellationToken, _databaseActions, stresstestResultId, "Id", "Concurrency");
                            if (concurrencyResults == null || concurrencyResults.Rows.Count == 0) continue;

                            foreach (DataRow crRow in concurrencyResults.Rows) {
                                if (cancellationToken.IsCancellationRequested) return null;

                                int concurrencyResultId = (int)crRow.ItemArray[0];
                                object concurrency = crRow.ItemArray[1];

                                DataTable runResults = ReaderAndCombiner.GetRunResults(cancellationToken, _databaseActions, concurrencyResultId, "Id", "Run");
                                if (runResults == null || runResults.Rows.Count == 0) continue;

                                foreach (DataRow rrRow in runResults.Rows) {
                                    if (cancellationToken.IsCancellationRequested) return null;

                                    int runResultId = (int)rrRow.ItemArray[0];
                                    object run = rrRow.ItemArray[1];

                                    DataTable logEntryResults = ReaderAndCombiner.GetLogEntryResults(cancellationToken, _databaseActions, "CHAR_LENGTH(Error)!=0", runResultId, "VirtualUser", "UserAction", "LogEntry", "SentAt", "Error");
                                    if (logEntryResults == null || logEntryResults.Rows.Count == 0) continue;

                                    foreach (DataRow ldr in logEntryResults.Rows) {
                                        if (cancellationToken.IsCancellationRequested) return null;
                                        errors.Rows.Add(stresstest, ldr["SentAt"], concurrency, run, ldr["VirtualUser"], ldr["UserAction"], ldr["LogEntry"], ldr["Error"]);
                                    }
                                }
                            }
                        }
                        cacheEntry.ReturnValue = errors;
                    }
                    return cacheEntry.ReturnValue as DataTable;
                }
                return null;
            }
        }

        /// <summary>
        /// Get the user actions and the log entries within, these are asked for the first user of the first run, so if you cancel a test it will not be correct.
        /// However, this is the fastest way to do this and there are no problems with a finished test.
        /// </summary>
        /// <param name="cancellationToken">Used in await Task.Run...</param>
        /// <param name="stresstestIds">If none, all the results for all tests will be returned.</param>
        /// <returns></returns>
        public DataTable GetUserActionComposition(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions == null) return null;

                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                if (cacheEntryDt == null) {
                    cacheEntryDt = GetResultSet("UserActionComposition", stresstestIds);
                    if (cacheEntryDt != null) {
                        cacheEntry.ReturnValue = cacheEntryDt;
                        return cacheEntryDt;
                    }

                    DataTable stresstests = ReaderAndCombiner.GetStresstests(cancellationToken, _databaseActions, stresstestIds, "Id", "Stresstest", "Connection");
                    if (stresstests == null || stresstests.Rows.Count == 0) return null;

                    var userActionComposition = CreateEmptyDataTable("UserActionComposition", "Stresstest", "User Action", "Log Entry");
                    foreach (DataRow stresstestsRow in stresstests.Rows) {
                        if (cancellationToken.IsCancellationRequested) return null;

                        int stresstestId = (int)stresstestsRow.ItemArray[0];

                        DataTable stresstestResults = ReaderAndCombiner.GetStresstestResults(cancellationToken, _databaseActions, stresstestId, "Id");
                        if (stresstestResults == null || stresstestResults.Rows.Count == 0) continue;

                        int stresstestResultId = (int)stresstestResults.Rows[0].ItemArray[0];

                        string stresstest = string.Format("{0} {1}", stresstestsRow.ItemArray[1], stresstestsRow.ItemArray[2]);

                        DataTable concurrencyResults = ReaderAndCombiner.GetConcurrencyResults(cancellationToken, _databaseActions, stresstestResultId, "Id");
                        if (concurrencyResults == null || concurrencyResults.Rows.Count == 0) continue;

                        foreach (DataRow crRow in concurrencyResults.Rows) {
                            if (cancellationToken.IsCancellationRequested) return null;

                            int concurrencyResultId = (int)crRow.ItemArray[0];

                            DataTable runResults = ReaderAndCombiner.GetRunResults(cancellationToken, _databaseActions, concurrencyResultId, "Id");
                            if (runResults == null || runResults.Rows.Count == 0) continue;

                            foreach (DataRow rrRow in runResults.Rows) {
                                if (cancellationToken.IsCancellationRequested) return null;

                                int runResultId = (int)rrRow.ItemArray[0];

                                //We don't want duplicates
                                DataTable logEntryResults = ReaderAndCombiner.GetLogEntryResults(cancellationToken, _databaseActions, "VirtualUser='vApus Thread Pool Thread #1' AND CHAR_LENGTH(SameAsLogEntryIndex)=0", runResultId,
                                        "UserAction", "LogEntry");
                                if (logEntryResults == null || logEntryResults.Rows.Count == 0) continue;

                                var userActions = new Dictionary<string, List<string>>();
                                foreach (DataRow lerRow in logEntryResults.Rows) {
                                    if (cancellationToken.IsCancellationRequested) return null;

                                    string userAction = lerRow["UserAction"] as string;
                                    string logEntry = lerRow["LogEntry"] as string;
                                    if (!userActions.ContainsKey(userAction)) userActions.Add(userAction, new List<string>());
                                    if (!userActions[userAction].Contains(logEntry)) userActions[userAction].Add(logEntry);
                                }

                                //Sort the user actions
                                List<string> sortedUserActions = userActions.Keys.ToList();
                                sortedUserActions.Sort(UserActionComparer.GetInstance());

                                foreach (string userAction in sortedUserActions) {
                                    if (cancellationToken.IsCancellationRequested) return null;

                                    foreach (string logEntry in userActions[userAction]) {
                                        if (cancellationToken.IsCancellationRequested) return null;

                                        userActionComposition.Rows.Add(stresstest, userAction, logEntry);
                                    }
                                }
                                break;
                            }
                            break;
                        }
                    }
                    cacheEntry.ReturnValue = userActionComposition;
                }
                return cacheEntry.ReturnValue as DataTable;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken">Used in await Task.Run...</param>
        /// <param name="stresstestIds">If none, all the results for all tests will be returned.</param>
        /// <returns></returns>
        public DataTable GetMachineConfigurations(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions != null) {
                    var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                    var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                    if (cacheEntryDt == null) {
                        cacheEntryDt = GetResultSet("MachineConfigurations", stresstestIds);
                        if (cacheEntryDt != null) {
                            cacheEntry.ReturnValue = cacheEntryDt;
                            return cacheEntryDt;
                        }

                        DataTable stresstests = ReaderAndCombiner.GetStresstests(cancellationToken, _databaseActions, stresstestIds, "Id", "Stresstest", "Connection");
                        if (stresstests == null || stresstests.Rows.Count == 0) return null;

                        var machineConfigurations = CreateEmptyDataTable("MachineConfigurations", "Stresstest", "Monitor", "Monitor Source", "Machine Configuration");
                        foreach (DataRow stresstestsRow in stresstests.Rows) {
                            if (cancellationToken.IsCancellationRequested) return null;

                            int stresstestId = (int)stresstestsRow.ItemArray[0];
                            string stresstest = string.Format("{0} {1}", stresstestsRow.ItemArray[1], stresstestsRow.ItemArray[2]);

                            DataTable monitors = ReaderAndCombiner.GetMonitors(cancellationToken, _databaseActions, stresstestId, "Monitor", "MonitorSource", "MachineConfiguration");
                            if (monitors == null || monitors.Rows.Count == 0) continue;

                            foreach (DataRow monitorRow in monitors.Rows) {
                                if (cancellationToken.IsCancellationRequested) return null;

                                machineConfigurations.Rows.Add(stresstest, monitorRow.ItemArray[0], monitorRow.ItemArray[1], monitorRow.ItemArray[2]);
                            }
                        }

                        cacheEntry.ReturnValue = machineConfigurations;
                    }
                    return cacheEntry.ReturnValue as DataTable;
                }
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken">Used in await Task.Run...</param>
        /// <param name="stresstestIds">If none, all the results for all tests will be returned.</param>
        /// <returns></returns>
        public DataTable GetAverageMonitorResults(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions == null) return null;

                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                if (cacheEntryDt == null) {
                    cacheEntryDt = GetResultSet("AverageMonitorResults", stresstestIds);
                    if (cacheEntryDt != null) {
                        cacheEntry.ReturnValue = cacheEntryDt;
                        return cacheEntryDt;
                    }

                    cacheEntry.ReturnValue = GetAverageMonitorResults(cancellationToken, -1, stresstestIds);
                }
                return cacheEntry.ReturnValue as DataTable;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken">Used in await Task.Run...</param>
        /// <param name="stresstestIds">If none, all the results for all tests will be returned.</param>
        /// <returns></returns>
        private DataTable GetAverageMonitorResults(CancellationToken cancellationToken, int monitorId, params int[] stresstestIds) {
            if (_databaseActions == null) return null;

            DataTable stresstests = ReaderAndCombiner.GetStresstests(cancellationToken, _databaseActions, stresstestIds, "Id", "Stresstest", "Connection", "MonitorBeforeInMinutes", "MonitorAfterInMinutes");
            if (stresstests == null || stresstests.Rows.Count == 0) return null;

            //Get the monitors + values
            DataTable monitors = null;
            if (monitorId == -1)
                monitors = ReaderAndCombiner.GetMonitors(cancellationToken, _databaseActions, null, stresstestIds, "Id", "StresstestId", "Monitor", "ResultHeaders");
            else
                monitors = ReaderAndCombiner.GetMonitor(cancellationToken, _databaseActions, monitorId, "Id", "StresstestId", "Monitor", "ResultHeaders");

            if (monitors == null || monitors.Rows.Count == 0) return CreateEmptyDataTable("AverageMonitorResults", "Stresstest", "Result Headers");

            //Sort the monitors based on the resultheaders to be able to group different monitor values under the same monitor headers.
            monitors.DefaultView.Sort = "ResultHeaders ASC";
            monitors = monitors.DefaultView.ToTable();

            var columnNames = new List<string>(new string[] { "Stresstest", "Monitor", "Started At", "Measured Time", "Measured Time (ms)", "Concurrency" });
            var resultHeaderStrings = new List<string>();
            var resultHeaders = new List<string>();
            int prevResultHeadersCount = 0;
            var monitorColumnOffsets = new Dictionary<int, int>(); //key monitorID, value offset

            //If there are monitors with the same headers we want to reuse those headers if possible for those monitors.
            foreach (DataRow monitorRow in monitors.Rows) {
                int tempMonitorId = (int)monitorRow.ItemArray[0];

                monitorColumnOffsets.Add(tempMonitorId, resultHeaders.Count);

                string rhs = monitorRow[3] as string;
                if (resultHeaderStrings.Contains(rhs)) {
                    monitorColumnOffsets[tempMonitorId] = prevResultHeadersCount;
                } else {
                    prevResultHeadersCount = resultHeaders.Count;
                    resultHeaderStrings.Add(rhs);
                    var rh = rhs.Split(new string[] { "; " }, StringSplitOptions.None);
                    resultHeaders.AddRange(rh);
                }
            }

            //We cannot have duplicate columnnames.
            foreach (string header in resultHeaders) {
                string formattedHeader = header;
                while (columnNames.Contains(formattedHeader)) formattedHeader += "_";
                columnNames.Add(formattedHeader);
            }
            var averageMonitorResults = CreateEmptyDataTable("AverageMonitorResults", columnNames.ToArray());

            foreach (DataRow stresstestsRow in stresstests.Rows) {
                if (cancellationToken.IsCancellationRequested) return null;

                int stresstestId = (int)stresstestsRow.ItemArray[0];
                string stresstest = string.Format("{0} {1}", stresstestsRow.ItemArray[1], stresstestsRow.ItemArray[2]);
                int monitorBeforeInMinutes = (int)stresstestsRow.ItemArray[3];
                int monitorAfterInMinutes = (int)stresstestsRow.ItemArray[4];

                DataTable stresstestResults = ReaderAndCombiner.GetStresstestResults(cancellationToken, _databaseActions, stresstestId, "Id");
                if (stresstestResults == null || stresstestResults.Rows.Count == 0) continue;
                int stresstestResultId = (int)stresstestResults.Rows[0].ItemArray[0];

                //Get the timestamps to calculate the averages
                DataTable concurrencyResults = ReaderAndCombiner.GetConcurrencyResults(cancellationToken, _databaseActions, stresstestResultId, "Id", "Concurrency", "StartedAt", "StoppedAt");
                if (concurrencyResults == null || concurrencyResults.Rows.Count == 0) continue;

                var concurrencyDelimiters = new Dictionary<int, KeyValuePair<DateTime, DateTime>>(concurrencyResults.Rows.Count);
                var runDelimiters = new Dictionary<int, Dictionary<DateTime, DateTime>>(concurrencyResults.Rows.Count);
                var concurrencies = new Dictionary<int, int>();
                foreach (DataRow crRow in concurrencyResults.Rows) {
                    if (cancellationToken.IsCancellationRequested) return null;

                    int concurrencyResultId = (int)crRow.ItemArray[0];
                    int concurrency = (int)crRow.ItemArray[1];
                    concurrencyDelimiters.Add(concurrencyResultId, new KeyValuePair<DateTime, DateTime>((DateTime)crRow.ItemArray[2], (DateTime)crRow.ItemArray[3]));

                    DataTable runResults = ReaderAndCombiner.GetRunResults(cancellationToken, _databaseActions, concurrencyResultId, "StartedAt", "StoppedAt");
                    if (runResults == null || runResults.Rows.Count == 0) continue;

                    var d = new Dictionary<DateTime, DateTime>(runResults.Rows.Count);
                    foreach (DataRow rrRow in runResults.Rows) {
                        if (cancellationToken.IsCancellationRequested) return null;

                        var start = (DateTime)rrRow.ItemArray[0];
                        if (!d.ContainsKey(start)) d.Add(start, (DateTime)rrRow.ItemArray[1]);
                    }
                    runDelimiters.Add(concurrencyResultId, d);
                    concurrencies.Add(concurrencyResultId, concurrency);
                }

                //Make a bogus run and concurrency to be able to calcullate averages for monitor before and after.
                if (monitorAfterInMinutes != 0 && runDelimiters.Count != 0) {
                    int lastConcurrencyID = 0;
                    foreach (int concId in runDelimiters.Keys)
                        lastConcurrencyID = concId;

                    var lastConcurrency = runDelimiters[lastConcurrencyID];

                    int i = 0;
                    foreach (var lastStop in lastConcurrency.Values)
                        if (i++ == lastConcurrency.Count - 1) {
                            var bogusStart = lastStop.AddMilliseconds(1);
                            var bogusStop = bogusStart.AddMinutes(monitorAfterInMinutes);
                            var monitorAfterBogusRuns = new Dictionary<DateTime, DateTime>(0);
                            monitorAfterBogusRuns.Add(bogusStart, bogusStop);
                            int concurrencyId = lastConcurrencyID + 1;
                            runDelimiters.Add(concurrencyId, monitorAfterBogusRuns);
                            concurrencyDelimiters.Add(concurrencyId, new KeyValuePair<DateTime, DateTime>(bogusStart, bogusStop));
                            concurrencies.Add(concurrencyId, 0);
                        }
                }
                if (monitorBeforeInMinutes != 0 && runDelimiters.Count != 0) {
                    var newRunDelimiters = new Dictionary<int, Dictionary<DateTime, DateTime>>(concurrencyResults.Rows.Count);
                    var newConcurrencyDelimiters = new Dictionary<int, KeyValuePair<DateTime, DateTime>>(concurrencyResults.Rows.Count);
                    var newConcurrencies = new Dictionary<int, int>();

                    var firstConcurrency = runDelimiters.First().Value;
                    foreach (var firstStart in firstConcurrency.Keys) {
                        var bogusStop = firstStart.Subtract(new TimeSpan(TimeSpan.TicksPerMillisecond));
                        var bogusStart = bogusStop.Subtract(new TimeSpan(monitorAfterInMinutes * TimeSpan.TicksPerMinute));
                        var monitorBeforeBogusRuns = new Dictionary<DateTime, DateTime>(0);
                        monitorBeforeBogusRuns.Add(bogusStart, bogusStop);
                        newRunDelimiters.Add(0, monitorBeforeBogusRuns);
                        newConcurrencyDelimiters.Add(0, new KeyValuePair<DateTime, DateTime>(bogusStart, bogusStop));
                        newConcurrencies.Add(0, 0);
                        break;
                    }
                    foreach (var concurrency in runDelimiters)
                        newRunDelimiters.Add(concurrency.Key, concurrency.Value);
                    foreach (var concurrency in concurrencyDelimiters)
                        newConcurrencyDelimiters.Add(concurrency.Key, concurrency.Value);
                    foreach (var concurrency in concurrencies)
                        newConcurrencies.Add(concurrency.Key, concurrency.Value);

                    runDelimiters = newRunDelimiters;
                    concurrencyDelimiters = newConcurrencyDelimiters;
                    concurrencies = newConcurrencies;
                }

                //Calcullate the averages
                foreach (int concurrencyId in runDelimiters.Keys) {
                    if (cancellationToken.IsCancellationRequested) return null;

                    var delimiterValues = runDelimiters[concurrencyId];
                    foreach (DataRow monitorRow in monitors.Rows) {
                        if (cancellationToken.IsCancellationRequested) return null;

                        int monitorStresstestId = (int)monitorRow.ItemArray[1];
                        if (monitorStresstestId != stresstestId) continue;

                        int tempMonitorId = (int)monitorRow.ItemArray[0];
                        object monitor = monitorRow.ItemArray[2];

                        DataTable monitorResults = ReaderAndCombiner.GetMonitorResults(_databaseActions, tempMonitorId, "TimeStamp", "Value");
                        if (monitorResults == null || monitorResults.Rows.Count == 0) continue;

                        var monitorValues = new List<KeyValuePair<DateTime, double[]>>(monitorResults.Rows.Count);
                        foreach (DataRow monitorResultsRow in monitorResults.Rows) {
                            if (cancellationToken.IsCancellationRequested) return null;

                            var timeStamp = (DateTime)monitorResultsRow[0];

                            bool canAdd = false;
                            foreach (var start in delimiterValues.Keys) {
                                if (cancellationToken.IsCancellationRequested) return null;

                                if (timeStamp >= start && timeStamp <= delimiterValues[start]) {
                                    canAdd = true;
                                    break;
                                }
                            }

                            if (canAdd) {
                                //Monitor values stored with a ',' for decimal seperator.
                                CultureInfo prevCulture = Thread.CurrentThread.CurrentCulture;
                                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("nl-BE");

                                string stringValueBlob = monitorResultsRow[1] as string;
                                //Workaround
                                if (stringValueBlob.Contains(".")) Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

                                string[] splittedValue = stringValueBlob.Split(new string[] { "; " }, StringSplitOptions.None);
                                double[] values = new double[splittedValue.Length];

                                for (long l = 0; l != splittedValue.LongLength; l++) {
                                    if (cancellationToken.IsCancellationRequested) return null;

                                    double dou;
                                    values[l] = double.TryParse(splittedValue[l].Trim(), out dou) ? dou : -1d;
                                }
                                monitorValues.Add(new KeyValuePair<DateTime, double[]>(timeStamp, values));

                                Thread.CurrentThread.CurrentCulture = prevCulture;
                            }
                        }

                        double[] averages = GetAverageMonitorResults(cancellationToken, monitorValues);
                        if (cancellationToken.IsCancellationRequested) return null;

                        var startedAt = concurrencyDelimiters[concurrencyId].Key;
                        TimeSpan measuredRunTime = (concurrencyDelimiters[concurrencyId].Value - startedAt);

                        string concurrency = concurrencies[concurrencyId] == 0 ? "--" : concurrencies[concurrencyId].ToString();

                        var newRow = new List<object>(new object[] { stresstest, monitor, startedAt, measuredRunTime.ToString("hh':'mm':'ss'.'fff"), Math.Round(measuredRunTime.TotalMilliseconds, MidpointRounding.AwayFromZero), concurrency });

                        var fragmentedAverages = new object[resultHeaders.Count];
                        for (long p = 0; p != fragmentedAverages.Length; p++)
                            fragmentedAverages[p] = "--";

                        int offset = monitorColumnOffsets[tempMonitorId];

                        //Correct the formatting here.
                        string[] stringAverages = new string[averages.LongLength];
                        for (long l = 0L; l != averages.LongLength; l++) {
                            double dou = averages[l];
                            stringAverages[l] = dou == -1d ? "--" : StringUtil.DoubleToLongString(dou);
                        }

                        stringAverages.CopyTo(fragmentedAverages, offset);

                        newRow.AddRange(fragmentedAverages);
                        averageMonitorResults.Rows.Add(newRow.ToArray());
                    }
                }
            }
            return averageMonitorResults;
        }
        /// <summary>
        /// From a 2 dimensional collection to an array of doubles.
        /// </summary>
        /// <param name="cancellationToken">Used in await Task.Run...</param>
        /// <param name="monitorValues"></param>        /// <returns></returns>
        private double[] GetAverageMonitorResults(CancellationToken cancellationToken, List<KeyValuePair<DateTime, double[]>> monitorValues) {
            var averageMonitorResults = new double[0];
            if (monitorValues.Count != 0) {
                //Average divider
                int valueCount = monitorValues.Count;
                averageMonitorResults = new double[valueCount];

                foreach (var kvp in monitorValues) {
                    if (cancellationToken.IsCancellationRequested) return null;

                    var timeStamp = kvp.Key;
                    var doubles = kvp.Value;

                    // The averages length must be the same as the doubles length.
                    if (averageMonitorResults.Length != doubles.Length) averageMonitorResults = new double[doubles.Length];

                    for (long l = 0; l != doubles.LongLength; l++) {
                        if (cancellationToken.IsCancellationRequested) return null;

                        double value = doubles[l], average = averageMonitorResults[l];

                        if (value == -1) //Detect invalid values.
                            averageMonitorResults[l] = -1;
                        else if (average != -1) //Add the value to the averages at the same index (i), divide it first (no overflow).
                            averageMonitorResults[l] = average + (value / valueCount);
                    }
                }
            }
            return averageMonitorResults;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken">Used in await Task.Run...</param>
        /// <param name="stresstestIds">If none, all the results for all tests will be returned.</param>
        /// <returns></returns>
        public List<DataTable> GetMonitorResults(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions == null) return null;

                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                var cacheEntryDts = cacheEntry.ReturnValue as List<DataTable>;
                if (cacheEntryDts == null || cacheEntryDts.Count == 0) {
                    var dts = new List<DataTable>();

                    Dictionary<int, string> monitorIdsAndNames = GetMonitors(stresstestIds);

                    foreach (int monitorId in monitorIdsAndNames.Keys)
                        dts.Add(GetMonitorResultsByMonitorId(cancellationToken, monitorId));

                    cacheEntry.ReturnValue = dts;
                }
                return cacheEntry.ReturnValue as List<DataTable>;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken">Used in await Task.Run...</param>
        /// <param name="stresstestIds">If none, all the results for all tests will be returned.</param>
        /// <returns></returns>
        public DataTable GetMonitorResultsByMonitorId(CancellationToken cancellationToken, int monitorId) {
            lock (_lock) {
                if (_databaseActions == null) return null;

                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), monitorId);
                var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                if (cacheEntryDt == null) {
                    DataTable monitorResults = GetResultSet("MonitorResults" + monitorId, -1);
                    if (monitorResults == null) {
                        //Get the monitors + values
                        DataTable monitors = ReaderAndCombiner.GetMonitor(cancellationToken, _databaseActions, monitorId, "Id", "StresstestId", "Monitor", "ResultHeaders");
                        if (monitors == null || monitors.Rows.Count == 0) return null;

                        DataRow monitorRow = monitors.Rows[0];
                        int stresstestId = (int)monitorRow["StresstestId"];

                        DataTable stresstests = ReaderAndCombiner.GetStresstests(cancellationToken, _databaseActions, stresstestId, "Id", "Stresstest", "Connection");
                        if (stresstests == null || stresstests.Rows.Count == 0) return null;

                        DataRow stresstestsRow = stresstests.Rows[0];

                        if (cancellationToken.IsCancellationRequested) return null;

                        string stresstest = string.Format("{0} {1}", stresstestsRow["Stresstest"], stresstestsRow["Connection"]);

                        object monitor = monitorRow["Monitor"];
                        string[] headers = (monitorRow["ResultHeaders"] as string).Split(new string[] { "; " }, StringSplitOptions.None);

                        var columns = new List<string>();
                        columns.Add("Stresstest");
                        columns.Add("Monitor");
                        columns.Add("Timestamp");

                        int headerIndex = 1;
                        foreach (string header in headers)
                            columns.Add((headerIndex++) + ") " + header);

                        monitorResults = CreateEmptyDataTable("MonitorResults" + monitorId, columns.ToArray());

                        DataTable mrs = ReaderAndCombiner.GetMonitorResults(_databaseActions, monitorId, "TimeStamp", "Value");

                        //Store monitor values with a ',' for decimal seperator.
                        CultureInfo prevCulture = Thread.CurrentThread.CurrentCulture;
                        Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("nl-BE");

                        var monitorValues = new Dictionary<DateTime, double[]>(mrs.Rows.Count);
                        foreach (DataRow monitorResultsRow in mrs.Rows) {
                            if (cancellationToken.IsCancellationRequested) return null;

                            string stringValueBlob = monitorResultsRow.ItemArray[1] as string;
                            //Workaround
                            if (stringValueBlob.Contains(".")) Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

                            string[] stringValues = stringValueBlob.Split(new string[] { "; " }, StringSplitOptions.None);
                            object[] values = new object[stringValues.LongLength];
                            for (long l = 0L; l != stringValues.LongLength; l++) {
                                string stringValue = stringValues[l];
                                double dou;
                                if (double.TryParse(stringValue, out dou))
                                    values[l] = dou;
                                else
                                    values[l] = stringValue;
                            }

                            var row = new List<object>();
                            row.Add(stresstest);
                            row.Add(monitor);
                            row.Add(monitorResultsRow.ItemArray[0]);
                            row.AddRange(values);

                            monitorResults.Rows.Add(row.ToArray());
                        }

                        Thread.CurrentThread.CurrentCulture = prevCulture;
                    }



                    cacheEntry.ReturnValue = monitorResults;
                }
                return cacheEntry.ReturnValue as DataTable;
            }
        }

        public DataTable GetAverageMonitorResultsByMonitorId(CancellationToken cancellationToken, int monitorId) {
            lock (_lock) {
                if (_databaseActions != null) {
                    var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), monitorId);
                    var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                    if (cacheEntryDt == null) {
                        cacheEntryDt = GetResultSet("AverageMonitorResults" + monitorId);
                        if (cacheEntryDt != null) {
                            cacheEntry.ReturnValue = cacheEntryDt;
                            return cacheEntryDt;
                        }
                        DataTable monitor = ReaderAndCombiner.GetMonitor(cancellationToken, _databaseActions, monitorId, "StresstestId");
                        if (monitor == null || monitor.Rows.Count == 0)
                            return null;
                        DataRow monitorRow = monitor.Rows[0];
                        int stresstestId = (int)monitorRow["StresstestId"];

                        cacheEntry.ReturnValue = GetAverageMonitorResults(cancellationToken, monitorId, stresstestId);
                    }
                    return cacheEntry.ReturnValue as DataTable;
                }
                return null;
            }
        }

        //Specialized stuff
        //----------------

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="concurrencyAndRunsDic">To link the run index to the correct run. stresstest as key, List<concurrency.run> as value</param>
        /// <param name="stresstestIds"></param>
        /// <returns></returns>
        public DataTable GetRunsOverTime(CancellationToken cancellationToken, out Dictionary<string, List<string>> concurrencyAndRunsDic, params int[] stresstestIds) {
            lock (_lock) {
                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                if (cacheEntryDt == null) {
                    concurrencyAndRunsDic = new Dictionary<string, List<string>>();

                    if (_databaseActions == null) return null;

                    DataTable stresstests = ReaderAndCombiner.GetStresstests(cancellationToken, _databaseActions, stresstestIds, "Id", "Stresstest", "Connection");
                    if (stresstests == null || stresstests.Rows.Count == 0) return null;

                    DataTable runsOverTime = CreateEmptyDataTable("RunsOverTime", "Stresstest");

                    var rows = new List<List<object>>();
                    int longestRowCount = 0;
                    foreach (DataRow stresstestsRow in stresstests.Rows) {
                        if (cancellationToken.IsCancellationRequested) return null;

                        int stresstestId = (int)stresstestsRow.ItemArray[0];
                        string stresstest = string.Format("{0} {1}", stresstestsRow.ItemArray[1], stresstestsRow.ItemArray[2]);

                        //To link the run index to the correct run
                        concurrencyAndRunsDic.Add(stresstest, new List<string>());

                        var row = new List<object>();
                        row.Add(stresstest);
                        var stoppedAts = new List<DateTime>();

                        DataTable stresstestResults = ReaderAndCombiner.GetStresstestResults(cancellationToken, _databaseActions, stresstestId, "Id");

                        if (stresstestResults.Rows.Count == 0) continue;
                        int stresstestResultId = (int)stresstestResults.Rows[0].ItemArray[0];

                        DataTable concurrencyResults = ReaderAndCombiner.GetConcurrencyResults(cancellationToken, _databaseActions, stresstestResultId, new string[] { "Id", "Concurrency" });
                        if (concurrencyResults == null || concurrencyResults.Rows.Count == 0) continue;

                        foreach (DataRow crRow in concurrencyResults.Rows) {
                            if (cancellationToken.IsCancellationRequested) return null;
                            int concurrencyResultId = (int)crRow.ItemArray[0];
                            int concurrency = (int)crRow.ItemArray[1];

                            DataTable runResults = ReaderAndCombiner.GetRunResults(cancellationToken, _databaseActions, concurrencyResultId, "Run", "StartedAt", "StoppedAt");
                            if (runResults == null || runResults.Rows.Count == 0) continue;

                            foreach (DataRow rrRow in runResults.Rows) {
                                if (cancellationToken.IsCancellationRequested) return null;
                                int run = (int)rrRow.ItemArray[0];
                                var startedAt = (DateTime)rrRow.ItemArray[1];
                                var stoppedAt = (DateTime)rrRow.ItemArray[2];

                                if (stoppedAts.Count != 0) row.Add(startedAt - stoppedAts[stoppedAts.Count - 1]); //Add gap (test init time and write db results time)
                                stoppedAts.Add(stoppedAt);

                                row.Add(stoppedAt - startedAt); //run time

                                concurrencyAndRunsDic[stresstest].Add(concurrency + "." + run);
                            }
                        }

                        if (row.Count > longestRowCount) longestRowCount = row.Count;
                        rows.Add(row);
                    }

                    //Add run time and gap columns.
                    double longestRowCountMod = ((double)longestRowCount / 2) + 0.5d;
                    var objectType = typeof(object);

                    for (double dou = 1d; dou != longestRowCountMod; dou += 0.5d) {
                        if (cancellationToken.IsCancellationRequested) return null;
                        int i = (int)dou;
                        runsOverTime.Columns.Add((dou - i == 0.5) ? "Init time " + i : i.ToString(), objectType);
                    }

                    //Add the rows.
                    foreach (var row in rows) {
                        if (cancellationToken.IsCancellationRequested) return null;
                        var newRow = new object[longestRowCount];
                        row.ToArray().CopyTo(newRow, 0);
                        runsOverTime.Rows.Add(newRow);
                    }

                    cacheEntry.OutputArguments = new object[] { concurrencyAndRunsDic };
                    cacheEntry.ReturnValue = runsOverTime;
                }
                concurrencyAndRunsDic = cacheEntry.OutputArguments[0] as Dictionary<string, List<string>>;
                return cacheEntry.ReturnValue as DataTable;
            }
        }

        /// <summary>
        /// All given strings are case-sensitive.
        /// This done for all tests in a database (for the whole distributed test). Throughput per watt is a geomean.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="powerMonitorName"></param>
        /// <param name="wattCounter"></param>
        /// <param name="secondaryMonitorName">can be null</param>
        /// <param name="secondaryCounter">can be null</param>
        /// <returns></returns>
        public DataTable GetGeomeanThroughputPerWattOverTime(CancellationToken cancellationToken, string powerMonitorName, string wattCounter, string secondaryMonitorName, string secondaryCounter) {
            lock (_lock) {
                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), powerMonitorName, wattCounter, secondaryMonitorName, secondaryCounter);
                var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                if (cacheEntryDt == null) {
                    //Get all the needed datatables and check if they have rows.
                    DataTable stresstests = ReaderAndCombiner.GetStresstests(cancellationToken, _databaseActions, "Id");
                    if (stresstests == null || stresstests.Rows.Count == 0) return null;

                    int stresstestsRowCount = stresstests.Rows.Count;
                    var tpsPerWatt = new List<List<double>>();
                    var secondaryCounterValues = new List<double>();

                    bool hasSecondaryCounterValues = secondaryMonitorName != null && secondaryCounter != null;

                    //Get all the tp per watts and the performance counter values.
                    for (int stresstestsRowIndex = 0; stresstestsRowIndex != stresstests.Rows.Count; stresstestsRowIndex++) {
                        DataRow row = stresstests.Rows[stresstestsRowIndex];
                        var dt = GetThroughputPerWattOverTime(cancellationToken, (int)row.ItemArray[0], powerMonitorName, wattCounter, secondaryMonitorName, secondaryCounter);

                        for (int dtRowIndex = 0; dtRowIndex != dt.Rows.Count; dtRowIndex++) {
                            if (dtRowIndex >= tpsPerWatt.Count) tpsPerWatt.Add(new List<double>());
                            var l = tpsPerWatt[dtRowIndex];

                            DataRow dtRow = dt.Rows[dtRowIndex];
                            double tpPerWatt = (double)dtRow.ItemArray[1];
                            if (tpPerWatt != 0d) l.Add(tpPerWatt);

                            if (hasSecondaryCounterValues && dtRowIndex >= secondaryCounterValues.Count) secondaryCounterValues.Add((double)dtRow.ItemArray[2]);
                        }
                    }

                    var throughputPerWattOverTime = hasSecondaryCounterValues ?
                        CreateEmptyDataTable("ThroughputPerWatt", "Minute", "Geomean Throughput per Watt", "Average " + secondaryCounter) :
                        CreateEmptyDataTable("ThroughputPerWatt", "Minute", "Geomean Throughput per Watt");

                    for (int i = 0; i != tpsPerWatt.Count; i++) {
                        var entry = tpsPerWatt[i];

                        double geomeanTpPerWatt;
                        if (entry.Count == 1) { //Branching goes a bit faster I think, result would be the same regardlesly.
                            geomeanTpPerWatt = entry[0];
                        } else {
                            geomeanTpPerWatt = 1;
                            foreach (double tpPerWatt in entry) geomeanTpPerWatt *= tpPerWatt;
                            geomeanTpPerWatt = Math.Pow(geomeanTpPerWatt, (1d / entry.Count));
                        }
                        if (hasSecondaryCounterValues)
                            throughputPerWattOverTime.Rows.Add(new object[] { (i + 1), geomeanTpPerWatt, secondaryCounterValues[i] });
                        else
                            throughputPerWattOverTime.Rows.Add(new object[] { (i + 1), geomeanTpPerWatt });
                    }

                    cacheEntry.ReturnValue = throughputPerWattOverTime;
                }
                return cacheEntry.ReturnValue as DataTable;
            }
        }
        /// <summary>
        /// All given strings are case-sensitive
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="stresstestId"></param>
        /// <param name="powerMonitorName"></param>
        /// <param name="wattCounter"></param>
        /// <param name="secondaryMonitorName">can be null</param>
        /// <param name="secondaryCounter">can be null</param>
        /// <returns></returns>
        private DataTable GetThroughputPerWattOverTime(CancellationToken cancellationToken, int stresstestId, string powerMonitorName, string wattCounter, string secondaryMonitorName, string secondaryCounter) {
            //Get all the needed datatables and check if they have rows.
            var stresstests = ReaderAndCombiner.GetStresstests(cancellationToken, _databaseActions, stresstestId, "Id", "MonitorBeforeInMinutes", "MonitorAfterInMinutes");
            if (stresstests == null || stresstests.Rows.Count == 0) return null;

            int powerMonitorStresstestId;
            var wattCounterValues = GetMonitorCounterValues(cancellationToken, powerMonitorName, wattCounter, out powerMonitorStresstestId);
            if (wattCounterValues == null) return null;

            int powerMonitorBefore = 0, powerMonitorAfter = 0;
            foreach (DataRow row in stresstests.Rows)
                if ((int)row.ItemArray[0] == powerMonitorStresstestId) {
                    powerMonitorBefore = (int)row.ItemArray[1];
                    powerMonitorAfter = (int)row.ItemArray[2];
                    break;
                }

            bool hasSecondaryCounterValues = secondaryMonitorName != null && secondaryCounter != null;

            int secondaryMonitorBefore = 0, secondaryMonitorAfter = 0;
            Dictionary<DateTime, double> secondaryCounterValues = null;

            if (hasSecondaryCounterValues) {
                int secondaryMonitorStresstestId = 0;
                secondaryCounterValues = GetMonitorCounterValues(cancellationToken, secondaryMonitorName, secondaryCounter, out secondaryMonitorStresstestId);
                if (secondaryCounterValues == null) return null;

                foreach (DataRow row in stresstests.Rows)
                    if ((int)row.ItemArray[0] == secondaryMonitorStresstestId) {
                        secondaryMonitorBefore = (int)row.ItemArray[1];
                        secondaryMonitorAfter = (int)row.ItemArray[2];
                        break;
                    }
            }

            //Get all the log entries.
            var stresstestResults = ReaderAndCombiner.GetStresstestResults(cancellationToken, _databaseActions, stresstestId, "Id");
            if (stresstestResults == null || stresstestResults.Rows.Count == 0) return null;
            int stresstestResultId = (int)stresstestResults.Rows[0].ItemArray[0];

            var concurrencyResults = ReaderAndCombiner.GetConcurrencyResults(cancellationToken, _databaseActions, stresstestResultId, "Id");
            if (concurrencyResults == null || concurrencyResults.Rows.Count == 0) return null;


            var concurrencyResultIds = new List<int>(concurrencyResults.Rows.Count);
            foreach (DataRow crRow in concurrencyResults.Rows) {
                if (cancellationToken.IsCancellationRequested) return null;

                concurrencyResultIds.Add((int)crRow.ItemArray[0]);
            }


            var runResults = ReaderAndCombiner.GetRunResults(cancellationToken, _databaseActions, concurrencyResultIds.ToArray(), "Id");
            if (runResults == null || runResults.Rows.Count == 0) return null;

            var runResultIds = new List<int>(runResults.Rows.Count);
            foreach (DataRow rrRow in runResults.Rows) {
                if (cancellationToken.IsCancellationRequested) return null;

                runResultIds.Add((int)rrRow.ItemArray[0]);
            }
            var logEntryResults = ReaderAndCombiner.GetLogEntryResults(cancellationToken, _databaseActions, runResultIds.ToArray(), "SentAt", "TimeToLastByteInTicks", "DelayInMilliseconds");
            if (logEntryResults == null || logEntryResults.Rows.Count == 0) return null;

            //Get the throughput per minute.

            //Following two variables serve at bordering monitor before and after.
            DateTime firstSentAt = DateTime.MaxValue;
            DateTime lastSentAt = DateTime.MinValue;

            var logEntries = new List<LogEntry>();
            foreach (DataRow lerRow in logEntryResults.Rows) {
                if (cancellationToken.IsCancellationRequested) return null;

                DateTime sentAt = (DateTime)lerRow["SentAt"];
                if (sentAt < firstSentAt) firstSentAt = sentAt;
                if (sentAt > lastSentAt) lastSentAt = sentAt;
                logEntries.Add(new LogEntry() { SentAt = sentAt, TimeToLastByteInTicks = (long)lerRow["TimeToLastByteInTicks"], DelayInMilliseconds = (int)lerRow["DelayInMilliseconds"] });
            }

            //Determine all the minutes.The key is the upper border.
            var minutes = new Dictionary<DateTime, List<LogEntry>>();
            DateTime nextMinute = firstSentAt;
            while (nextMinute < lastSentAt) {
                nextMinute = nextMinute.AddMinutes(1);
                minutes.Add(nextMinute, new List<LogEntry>());
            }

            //---
            var throughputPerMinute = new Dictionary<int, double>(); //Key == the minute, value == the throughput for that minute
            //---

            //Put log entries in the right "minute".
            foreach (var logEntry in logEntries) {
                if (cancellationToken.IsCancellationRequested) return null;

                foreach (DateTime key in minutes.Keys) {
                    if (cancellationToken.IsCancellationRequested) return null;

                    if (logEntry.SentAt < key) {
                        minutes[key].Add(logEntry);
                        break;
                    }
                }
            }

            TimeSpan totalTimeToLastByte, totalDelay;
            double div;
            foreach (List<LogEntry> currentMinute in minutes.Values) {
                double minuteTp = 0d;
                if (currentMinute.Count != 0) {
                    totalTimeToLastByte = new TimeSpan();
                    totalDelay = new TimeSpan();
                    foreach (var currentMinuteLogEntry in currentMinute) {
                        if (cancellationToken.IsCancellationRequested) return null;

                        totalTimeToLastByte = totalTimeToLastByte.Add(new TimeSpan(currentMinuteLogEntry.TimeToLastByteInTicks));
                        totalDelay = totalDelay.Add(new TimeSpan(currentMinuteLogEntry.DelayInMilliseconds * TimeSpan.TicksPerMillisecond));
                    }

                    div = ((double)(totalTimeToLastByte.Ticks + totalDelay.Ticks) / TimeSpan.TicksPerSecond);
                    minuteTp = ((double)currentMinute.Count) / div;
                }
                throughputPerMinute.Add(throughputPerMinute.Count + 1, minuteTp);
            }

            //Now we need to get the watt and performance counter for each minute of the test. Monitor before and after must be taken into account.

            var wattPerMinute = GetAveragePerMinute(cancellationToken, wattCounterValues);
            Dictionary<int, double> secondaryCounterPerMinute = null;
            if (hasSecondaryCounterValues)
                secondaryCounterPerMinute = GetAveragePerMinute(cancellationToken, secondaryCounterValues);

            //Gather everything and return the data table.
            var throughputPerWattOverTime = hasSecondaryCounterValues ?
                CreateEmptyDataTable("ThroughputPerWatt", "Minute", "Throughput per Watt", "Average " + secondaryCounter) :
                 CreateEmptyDataTable("ThroughputPerWatt", "Minute", "Throughput per Watt");

            if (hasSecondaryCounterValues)
                for (int minute = 1; minute <= secondaryMonitorBefore; minute++) {
                    var row = new object[3];

                    row[0] = minute;
                    row[1] = 0d;
                    row[2] = secondaryCounterPerMinute[minute];

                    throughputPerWattOverTime.Rows.Add(row);
                }

            foreach (int minute in throughputPerMinute.Keys) {
                var row = new object[hasSecondaryCounterValues ? 3 : 2];

                int newMinute = minute + powerMonitorBefore;
                row[0] = minute + secondaryMonitorBefore;

                double tpm = throughputPerMinute[minute];
                if (tpm == 0d) {
                    row[1] = 0d;
                } else {
                    double wpm = wattPerMinute[newMinute];
                    row[1] = wpm == 0d ? 0d : throughputPerMinute[minute] / wpm;
                }

                if (hasSecondaryCounterValues)
                    row[2] = secondaryCounterPerMinute[newMinute];

                throughputPerWattOverTime.Rows.Add(row);
            }

            if (hasSecondaryCounterValues) {
                secondaryMonitorAfter += throughputPerWattOverTime.Rows.Count + 1;
                for (int minute = throughputPerWattOverTime.Rows.Count + 1; minute != secondaryMonitorAfter; minute++) {
                    var row = new object[3];

                    row[0] = minute;
                    row[1] = 0d;
                    row[2] = secondaryCounterPerMinute[minute];

                    throughputPerWattOverTime.Rows.Add(row);
                }
            }
            return throughputPerWattOverTime;
        }
        /// <summary>
        /// Returns an array of the values for a given counter.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="monitorName"></param>
        /// <param name="counter"></param>
        /// <returns></returns>
        private Dictionary<DateTime, double> GetMonitorCounterValues(CancellationToken cancellationToken, string monitorName, string counter, out int stresstestId) {
            stresstestId = 0;
            var monitor = ReaderAndCombiner.GetMonitors(cancellationToken, _databaseActions, "Monitor='" + monitorName + "'", new int[0], new string[] { "Id", "StresstestId", "ResultHeaders" });
            if (monitor == null || monitor.Rows.Count == 0) return null;

            DataRow row = monitor.Rows[0];
            int id = (int)row.ItemArray[0];
            stresstestId = (int)row.ItemArray[1];
            string[] resultHeaders = (row.ItemArray[2] as string).Split(new string[] { "; " }, StringSplitOptions.None);
            int resultHeaderIndex = resultHeaders.IndexOf(counter);

            var monitorResults = ReaderAndCombiner.GetMonitorResults(_databaseActions, id, "TimeStamp", "Value");
            if (monitorResults == null || monitorResults.Rows.Count == 0) return null;

            var dict = new Dictionary<DateTime, double>(monitorResults.Rows.Count);

            for (int i = 0; i != monitorResults.Rows.Count; i++) {
                if (cancellationToken.IsCancellationRequested) return null;

                row = monitorResults.Rows[i];
                string[] value = (row.ItemArray[1] as string).Split(new string[] { "; " }, StringSplitOptions.None);

                double dou;
                dict.Add((DateTime)row.ItemArray[0], double.TryParse(value[resultHeaderIndex], out dou) ? dou : -1d);
            }

            return dict;
        }
        private Dictionary<int, double> GetAveragePerMinute(CancellationToken cancellationToken, Dictionary<DateTime, double> dict) {
            var averagePerMinute = new Dictionary<int, double>();

            DateTime nextMinute = dict.GetKeyAt(0).AddMinutes(1);

            var currentMinute = new List<double>();
            double average;
            int count;
            foreach (var key in dict.Keys) {
                if (cancellationToken.IsCancellationRequested) return null;

                if (key > nextMinute) {
                    average = 0;
                    count = currentMinute.Count;
                    foreach (var currentValue in currentMinute) {
                        if (cancellationToken.IsCancellationRequested) return null;

                        average += (currentValue / count);
                    }

                    averagePerMinute.Add(averagePerMinute.Count + 1, average);

                    nextMinute = nextMinute.AddMinutes(1);
                    currentMinute = new List<double>();
                }
                currentMinute.Add(dict[key]);
            }

            if (currentMinute.Count != 0) {
                average = 0;
                count = currentMinute.Count;
                foreach (var currentValue in currentMinute) {
                    if (cancellationToken.IsCancellationRequested) return null;

                    average += (currentValue / count);
                }
                averagePerMinute.Add(averagePerMinute.Count + 1, average);
            }

            return averagePerMinute;
        }

        public DataTable GetGeomeanThroughputPerWatt(CancellationToken cancellationToken, string powerMonitorName, string wattCounter) {
            lock (_lock) {
                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), powerMonitorName, wattCounter);
                var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                if (cacheEntryDt == null) {
                    //Get all the needed datatables and check if they have rows.
                    var stresstests = ReaderAndCombiner.GetStresstests(cancellationToken, _databaseActions, "Id");
                    if (stresstests == null || stresstests.Rows.Count == 0) return null;

                    int stresstestsRowCount = stresstests.Rows.Count;
                    var tpsPerWatt = new List<double[]>();

                    //Get all the tp per watts and the performance counter values.
                    for (int stresstestsRowIndex = 0; stresstestsRowIndex != stresstests.Rows.Count; stresstestsRowIndex++) {
                        DataRow row = stresstests.Rows[stresstestsRowIndex];
                        var dt = GetThroughputPerWatt(cancellationToken, (int)row.ItemArray[0], powerMonitorName, wattCounter);

                        for (int dtRowIndex = 0; dtRowIndex != dt.Rows.Count; dtRowIndex++) {
                            if (dtRowIndex >= tpsPerWatt.Count) tpsPerWatt.Add(new double[stresstestsRowCount]);
                            var arr = tpsPerWatt[dtRowIndex];

                            DataRow dtRow = dt.Rows[dtRowIndex];
                            double tpPerWatt = (double)dtRow.ItemArray[0];
                            arr[stresstestsRowIndex] = tpPerWatt;
                        }
                    }

                    var throughputPerWatt = CreateEmptyDataTable("ThroughputPerWatt", new string[] { "Geomean Throughput per Watt" }, new Type[] { typeof(double) });

                    for (int i = 0; i != tpsPerWatt.Count; i++) {
                        var entry = tpsPerWatt[i];

                        double geomeanTpPerWatt;
                        if (entry.Length == 1) { //Branching goes a bit faster I think, result would be the same regardlesly.
                            geomeanTpPerWatt = entry[0];
                        } else {
                            geomeanTpPerWatt = 1;
                            foreach (double tpPerWatt in entry) geomeanTpPerWatt *= tpPerWatt;
                            geomeanTpPerWatt = Math.Pow(geomeanTpPerWatt, (1d / entry.Length));
                        }

                        throughputPerWatt.Rows.Add(geomeanTpPerWatt);
                    }

                    cacheEntry.ReturnValue = throughputPerWatt;
                }
                return cacheEntry.ReturnValue as DataTable;
            }
        }
        private DataTable GetThroughputPerWatt(CancellationToken cancellationToken, int stresstestId, string powerMonitorName, string wattCounter) {
            int powerMonitorStresstestId;
            var wattCounterValues = GetMonitorCounterValues(cancellationToken, powerMonitorName, wattCounter, out powerMonitorStresstestId);
            if (wattCounterValues == null) return null;

            //Get all the log entries.
            var stresstestResults = ReaderAndCombiner.GetStresstestResults(cancellationToken, _databaseActions, stresstestId, "Id");
            if (stresstestResults == null || stresstestResults.Rows.Count == 0) return null;
            int stresstestResultId = (int)stresstestResults.Rows[0].ItemArray[0];

            var concurrencyResults = ReaderAndCombiner.GetConcurrencyResults(cancellationToken, _databaseActions, stresstestResultId, "Id");
            if (concurrencyResults == null || concurrencyResults.Rows.Count == 0) return null;


            var concurrencyResultIds = new List<int>(concurrencyResults.Rows.Count);
            foreach (DataRow crRow in concurrencyResults.Rows) {
                if (cancellationToken.IsCancellationRequested) return null;

                concurrencyResultIds.Add((int)crRow.ItemArray[0]);
            }

            var runResults = ReaderAndCombiner.GetRunResults(cancellationToken, _databaseActions, concurrencyResultIds.ToArray(), "Id");
            if (runResults == null || runResults.Rows.Count == 0) return null;

            var runResultIds = new List<int>(runResults.Rows.Count);
            foreach (DataRow rrRow in runResults.Rows) {
                if (cancellationToken.IsCancellationRequested) return null;

                runResultIds.Add((int)rrRow.ItemArray[0]);
            }
            var logEntryResults = ReaderAndCombiner.GetLogEntryResults(cancellationToken, _databaseActions, runResultIds.ToArray(), "SentAt", "TimeToLastByteInTicks", "DelayInMilliseconds");
            if (logEntryResults == null || logEntryResults.Rows.Count == 0) return null;

            //Following two variables serve at bordering monitor before and after.
            DateTime firstSentAt = DateTime.MaxValue;
            DateTime lastSentAt = DateTime.MinValue;

            var logEntries = new List<LogEntry>();
            foreach (DataRow lerRow in logEntryResults.Rows) {
                if (cancellationToken.IsCancellationRequested) return null;

                DateTime sentAt = (DateTime)lerRow["SentAt"];
                if (sentAt < firstSentAt) firstSentAt = sentAt;
                if (sentAt > lastSentAt) lastSentAt = sentAt;
                logEntries.Add(new LogEntry() { SentAt = sentAt, TimeToLastByteInTicks = (long)lerRow["TimeToLastByteInTicks"], DelayInMilliseconds = (int)lerRow["DelayInMilliseconds"] });
            }

            //Get the Throughput
            //---
            double throughput = 0d;
            //---

            TimeSpan totalTimeToLastByte = new TimeSpan();
            TimeSpan totalDelay = new TimeSpan();

            foreach (var logEntry in logEntries) {
                if (cancellationToken.IsCancellationRequested) return null;

                totalTimeToLastByte = totalTimeToLastByte.Add(new TimeSpan(logEntry.TimeToLastByteInTicks));
                totalDelay = totalDelay.Add(new TimeSpan(logEntry.DelayInMilliseconds * TimeSpan.TicksPerMillisecond));
            }

            double div = ((double)(totalTimeToLastByte.Ticks + totalDelay.Ticks) / TimeSpan.TicksPerSecond);
            throughput = ((double)logEntries.Count) / div;

            //Now we need to get the watt and performance counter for each minute of the test. Monitor before and after must be taken into account.

            double averageWatt = GetAverage(cancellationToken, wattCounterValues, firstSentAt, lastSentAt);
            if (averageWatt == -1d) return null;

            //Gather everything and return the data table.
            var throughputPerWatt = CreateEmptyDataTable("ThroughputPerWatt", new string[] { "Throughput per Watt" }, new Type[] { typeof(double) });
            throughputPerWatt.Rows.Add(new object[] { (double)(throughput / averageWatt) });

            return throughputPerWatt;
        }
        private double GetAverage(CancellationToken cancellationToken, Dictionary<DateTime, double> dict, DateTime from, DateTime to) {
            double average = 0d;
            int count = 0;
            foreach (var key in dict.Keys) {
                if (cancellationToken.IsCancellationRequested) return -1f;
                if (key < from) continue;
                if (key > to) break;
                ++count;
            }
            foreach (var key in dict.Keys) {
                if (cancellationToken.IsCancellationRequested) return -1f;
                if (key < from) continue;
                if (key > to) break;
                average += (dict[key] / count);
            }

            return average;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="stresstestIds"></param>
        /// <returns></returns>
        public DataTable GetResponseTimeDistributionForLogEntriesPerConcurrency(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions != null) {
                    var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                    var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                    if (cacheEntryDt == null) {
                        cacheEntryDt = GetResultSet("ResponseTimeDistributionForLogEntriesPerConcurrency", stresstestIds);
                        if (cacheEntryDt != null) {
                            cacheEntry.ReturnValue = cacheEntryDt;
                            return cacheEntryDt;
                        }

                        cacheEntry.ReturnValue = ResponseTimeDistributionForLogEntriesPerConcurrencyCalculator.GetInstance().Get(_databaseActions, cancellationToken, stresstestIds);

                        GC.Collect();
                    }
                    return cacheEntry.ReturnValue as DataTable;
                }
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="stresstestIds"></param>
        /// <returns></returns>
        public DataTable GetResponseTimeDistributionForUserActionsPerConcurrency(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions != null) {
                    var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                    var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                    if (cacheEntryDt == null) {
                        cacheEntryDt = GetResultSet("ResponseTimeDistributionForUserActionsPerConcurrency", stresstestIds);
                        if (cacheEntryDt != null) {
                            cacheEntry.ReturnValue = cacheEntryDt;
                            return cacheEntryDt;
                        }

                        cacheEntry.ReturnValue = ResponseTimeDistributionForUserActionsPerConcurrencyCalculator.GetInstance().Get(_databaseActions, cancellationToken, stresstestIds);

                        GC.Collect();
                    }
                    return cacheEntry.ReturnValue as DataTable;
                }
                return null;
            }
        }

        private bool HasResultSetsTable() {
            DataTable tables = _databaseActions.GetDataTable("show tables;");
            if (tables.Columns.Count != 0)
                foreach (DataRow row in tables.Rows)
                    if (row[0].ToString().Equals("ResultSets", StringComparison.InvariantCultureIgnoreCase)) {
                        return true;
                    }
            return false;
        }
        private DataTable GetResultSet(string name, params int[] stresstestIds) {
            if (HasResultSetsTable()) {
                DataRow row = null;
                if (stresstestIds.Length == 1)
                    row = _databaseActions.GetDataTable(string.Format("Select ResultSet from ResultSets where StresstestId={0} and Name='{1}';", stresstestIds[0], name)).Rows[0];
                else
                    row = _databaseActions.GetDataTable(string.Format("Select ResultSet from ResultSets where Name='{0}';", name)).Rows[0];

                DataTable resultSet = (row["ResultSet"] as string).ToDataTable();
                resultSet.TableName = name;
                return resultSet;
            }
            return null;
        }

        #endregion

        public DataTable GetLogs(CancellationToken cancellationToken, params int[] stresstestIds) {
            lock (_lock) {
                if (_databaseActions == null) return null;

                var cacheEntry = _functionOutputCache.GetOrAdd(MethodInfo.GetCurrentMethod(), stresstestIds);
                var cacheEntryDt = cacheEntry.ReturnValue as DataTable;
                if (cacheEntryDt == null) {
                    if (stresstestIds.Length == 0) {
                        DataTable stresstests = ReaderAndCombiner.GetStresstests(cancellationToken, _databaseActions, "Id");
                        if (stresstests == null || stresstests.Rows.Count == 0) return null;

                        stresstestIds = new int[] { (int)stresstests.Rows[0].ItemArray[0] };
                    }

                    DataTable vApusInstances = ReaderAndCombiner.GetStresstests(cancellationToken, _databaseActions, stresstestIds, "vApusInstanceId");
                    if (vApusInstances == null || vApusInstances.Rows.Count == 0) return null;

                    var vApusInstanceIds = new int[] { (int)vApusInstances.Rows[0].ItemArray[0] };

                    DataTable logs = _databaseActions.GetDataTable(string.Format("Select Timestamp, Level, Entry from logs where vApusInstanceId IN({0});", vApusInstanceIds.Combine(",")));

                    if (logs == null) return null;

                    cacheEntry.ReturnValue = logs;
                    return logs;
                }
                return cacheEntry.ReturnValue as DataTable;
            }
        }

        #endregion

        #region Other
        /// <summary>
        /// Connect to an existing database to execute the procedures on or add slave side data to it.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="databaseName"></param>
        /// <param name="user"></param>
        /// <param name="password"></param>
        public void ConnectToExistingDatabase(string host, int port, string databaseName, string user, string password) {
            lock (_lock) {
                ClearCache();
                try {
                    _databaseActions = new DatabaseActions() { ConnectionString = string.Format("Server={0};Port={1};Database={2};Uid={3};Pwd={4};table cache = true;", host, port, databaseName, user, password) };
                    if (_databaseActions.GetDataTable("Show databases").Rows.Count == 0) throw new Exception("A connection to the results server could not be made!");
                    _databaseName = databaseName;
                } catch {
                    KillConnection();
                    throw;
                }
            }
        }
        /// <summary>
        /// Connect to an existing database to execute the procedures on or add slave side data to it.
        /// </summary>
        /// <param name="databaseActions"></param>
        /// <param name="databaseName"></param>
        public void ConnectToExistingDatabase(DatabaseActions databaseActions, string databaseName) {
            lock (_lock) {
                ClearCache();
                try {
                    _databaseActions = databaseActions;
                    if (_databaseActions.GetDataTable("Show databases").Rows.Count == 0) throw new Exception("A connection to the results server could not be made!");
                    _databaseName = databaseName;
                } catch {
                    KillConnection();
                    throw;
                }
            }
        }
        /// <summary>
        /// Last resort.
        /// </summary>
        public void KillConnection() {
            ClearCache();
            if (_databaseActions != null) {
                try { _databaseActions.ReleaseConnection(); } catch { }
                _databaseActions = null;
            }
        }

        public void ClearCache() {
            if (_functionOutputCache != null) _functionOutputCache.Dispose();
            _functionOutputCache = new FunctionOutputCache();
        }

        /// <summary>
        /// Get all the stresstests: ID, Stresstest, Connection
        /// If the workload was divided over multiple slaves the datatable entries will be combined, in that case the first Id will be put in a row.
        /// </summary>
        /// <returns></returns>
        public DataTable GetStresstests() {
            lock (_lock) {
                if (_databaseActions != null)
                    return ReaderAndCombiner.GetStresstests(new CancellationToken(), _databaseActions, "Id", "Stresstest", "Connection");

                return null;
            }
        }
        public List<int> GetStresstestIds() {
            lock (_lock) {
                var l = new List<int>();
                if (_databaseActions != null) {
                    var dt = ReaderAndCombiner.GetStresstests(new CancellationToken(), _databaseActions, "Id");
                    foreach (DataRow row in dt.Rows) l.Add((int)row.ItemArray[0]);
                }
                return l;
            }
        }
        /// <summary>
        /// stresstest: ID, Stresstest, Connection
        /// If the workload was divided over multiple slaves the datatable entries will be combined, in that case the first Id will be put in a row.
        /// </summary>
        /// <returns></returns>
        public DataTable GetStresstests(int stresstestId) {
            lock (_lock) {
                if (_databaseActions != null)
                    return ReaderAndCombiner.GetStresstests(new CancellationToken(), _databaseActions, stresstestId, "Id", "Stresstest", "Connection");

                return null;
            }
        }
        /// <summary>
        /// Execute a query on the connected database.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public DataTable ExecuteQuery(string query) {
            lock (_lock) {
                if (_databaseActions == null) return null;
                return _databaseActions.GetDataTable(query);
            }
        }

        /// <summary>
        /// Deletes the current database.
        /// </summary>
        public void DeleteResults() { DeleteResults(_databaseName); }
        public void DeleteResults(string databaseName) {
            lock (_lock)
                try {
                    Schema.Drop(databaseName, _databaseActions);
                    _databaseName = null;
                } catch (Exception ex) {
                    Loggers.Log(Level.Error, "Failed deleting the results database.", ex);
                }
        }

        private DataTable CreateEmptyDataTable(string name, params string[] columnNames) {
            var objectType = typeof(object);
            var dataTable = new DataTable(name);
            foreach (string columnName in columnNames) dataTable.Columns.Add(columnName, objectType);
            return dataTable;
        }

        private DataTable CreateEmptyDataTable(string name, string[] columnNames, Type[] columnDataTypes) {
            var dataTable = new DataTable(name);
            for (int i = 0; i != columnNames.Length; i++) dataTable.Columns.Add(columnNames[i], columnDataTypes[i]);
            return dataTable;
        }

        /// <summary>
        /// Parse a date to a valid string for in a MySQL db.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        private string Parse(DateTime dateTime) { return dateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff"); }
        /// <summary>
        ///Mimics PHP's mysql_real_escape_string();
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private static string MySQLEscapeString(string s) { return System.Text.RegularExpressions.Regex.Replace(s, @"[\r\n\x00\x1a\\'""]", @"\$0"); }
        #endregion

        private class UserActionComparer : IComparer<string> {
            private static readonly UserActionComparer _userActionComparer = new UserActionComparer();
            public static UserActionComparer GetInstance() { return _userActionComparer; }

            private const string LOG = "Log ";
            private const string UA = "User Action ";
            private const char COLON = ':';

            private UserActionComparer() { }

            public int Compare(string x, string y) {
                if (x.StartsWith(LOG)) { //Backwards compatible.

                    int xColonUa = x.IndexOf(COLON);
                    if (xColonUa == -1) xColonUa = x.IndexOf(UA) - 1;

                    int yColonUa = y.IndexOf(COLON);
                    if (yColonUa == -1) yColonUa = y.IndexOf(UA) - 1;

                    int logX, logY;
                    if (!int.TryParse(x.Substring(LOG.Length, xColonUa - LOG.Length), out logX)) {
                        xColonUa = x.IndexOf(UA) - 1;
                        int.TryParse(x.Substring(LOG.Length, xColonUa - LOG.Length), out logX);
                    }
                    if (!int.TryParse(x.Substring(LOG.Length, yColonUa - LOG.Length), out logY)) {
                        yColonUa = y.IndexOf(UA) - 1;
                        int.TryParse(x.Substring(LOG.Length, yColonUa - LOG.Length), out logY);
                    }

                    if (logX > logY) return 1;
                    if (logY < logX) return -1;

                    int xUA = x.IndexOf(UA);
                    int yUA = y.IndexOf(UA);

                    x = x.Substring(xUA);
                    y = y.Substring(yUA);
                }

                return UserActionCompare(x, y);
            }
            private int UserActionCompare(string x, string y) {
                x = x.Substring(UA.Length);
                y = y.Substring(UA.Length);

                x = x.Split(COLON)[0];
                y = y.Split(COLON)[0];

                int i = int.Parse(x);
                int j = int.Parse(y);

                return i.CompareTo(j);
            }
        }

        private struct LogEntry {
            public DateTime SentAt;
            public long TimeToLastByteInTicks;
            public int DelayInMilliseconds;
        }
    }
}