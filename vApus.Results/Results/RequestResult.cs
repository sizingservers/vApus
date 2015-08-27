﻿/*
 * Copyright 2012 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;

namespace vApus.Results {
    public class RequestResult {
        /// <summary>
        /// Use this to determine that this is a filled in request result.
        /// </summary>
        public string VirtualUser { get; set; }
        public string UserAction { get; set; }

        /// <summary>
        ///     Index in scenario
        /// </summary>
        public string RequestIndex { get; set; }
        public string SameAsRequestIndex { get; set; }

        public string Request { get; set; }
        public bool InParallelWithPrevious { get; set; }
        public DateTime SentAt { get; set; }
        public long TimeToLastByteInTicks { get; set; }
        /// <summary>
        /// Should only apply to the first request. This is the delay before the test starts. Not taken in account for result calculations.
        /// </summary>
        public int InitialDelayInMilliseconds { get; set; }
        /// <summary>
        /// Delay (see it as user think time in web apps) after the last request in a user action. Does not happen after the last request --> useless and influences the results.
        /// </summary>
        public int DelayInMilliseconds { get; set; }
        public string Error { get; set; }

        /// <summary>
        /// 0 for all but break on last runs.
        /// </summary>
        public int Rerun { get; set; }

        public RequestResult() { }
    }
}