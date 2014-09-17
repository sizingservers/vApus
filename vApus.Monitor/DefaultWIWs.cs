﻿/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using Newtonsoft.Json;
using vApus.Monitor.Sources.Base;
namespace vApus.Monitor {
    internal static class DefaultWIWs {
        private static string _allAvailable = "[{\"name\":\"*\",\"isAvailable\":true}]";

        private static string _dstat = "[{\"name\":\"FOO\",\"isAvailable\":true,\"subs\":[{\"name\":\"procs\",\"subs\":[{\"name\":\"run\"},{\"name\":\"blk\"},{\"name\":\"new\"}]},{\"name\":\"memory usage\",\"subs\":[{\"name\":\"used\"},{\"name\":\"buff\"},{\"name\":\"cach\"},{\"name\":\"free\"}]},{\"name\":\"paging\",\"subs\":[{\"name\":\"in\"},{\"name\":\"out\"}]},{\"name\":\"dsk/total\",\"subs\":[{\"name\":\"read\"},{\"name\":\"writ\"}]},{\"name\":\"system\",\"subs\":[{\"name\":\"int\"},{\"name\":\"csw\"}]},{\"name\":\"total cpu usage\",\"subs\":[{\"name\":\"usr\"},{\"name\":\"sys\"},{\"name\":\"idl\"},{\"name\":\"wai\"},{\"name\":\"hiq\"},{\"name\":\"siq\"}]},{\"name\":\"net/total\",\"subs\":[{\"name\":\"recv\"},{\"name\":\"send\"}]}]}]";
        private static string _wmi = "[{\"name\":\"FOO\",\"isAvailable\":true,\"subs\":[{\"name\":\"Memory.Available Bytes\",\"counter\":\"System.Collections.Generic.List`1[System.String]\",\"subs\":[{\"name\":\"__Total__\"}]},{\"name\":\"Memory.Cache Bytes\",\"counter\":\"System.Collections.Generic.List`1[System.String]\",\"subs\":[{\"name\":\"__Total__\"}]},{\"name\":\"PhysicalDisk.Avg. Disk Queue Length\",\"counter\":\"System.Collections.Generic.List`1[System.String]\",\"subs\":[{\"name\":\"_Total\"}]},{\"name\":\"Processor Information.% Idle Time\",\"counter\":\"System.Collections.Generic.List`1[System.String]\",\"subs\":[{\"name\":\"_Total\"}]},{\"name\":\"Processor Information.% Interrupt Time\",\"counter\":\"System.Collections.Generic.List`1[System.String]\",\"subs\":[{\"name\":\"_Total\"}]},{\"name\":\"Processor Information.% Privileged Time\",\"counter\":\"System.Collections.Generic.List`1[System.String]\",\"subs\":[{\"name\":\"_Total\"}]},{\"name\":\"Processor Information.% User Time\",\"counter\":\"System.Collections.Generic.List`1[System.String]\",\"subs\":[{\"name\":\"_Total\"}]},{\"name\":\"PhysicalDisk.Disk Read Bytes/sec\",\"subs\":[{\"name\":\"_Total\"}]},{\"name\":\"PhysicalDisk.Disk Write Bytes/sec\",\"subs\":[{\"name\":\"_Total\"}]}]}]";
        private static string _esxi = "[{\"name\":\"FOO\",\"isAvailable\":true,\"subs\":[{\"name\":\"cpu.idle.summation (millisecond)\",\"subs\":[{\"name\":\"*\"}]},{\"name\":\"cpu.usage.average (percent)\",\"subs\":[{\"name\":\"*\"}]},{\"name\":\"cpu.wait.summation (millisecond)\",\"subs\":[{\"name\":\"*\"}]},{\"name\":\"disk.deviceLatency.average (millisecond)\",\"subs\":[{\"name\":\"*\"}]},{\"name\":\"disk.queueLatency.average (millisecond)\",\"subs\":[{\"name\":\"*\"}]},{\"name\":\"mem.consumed.average (kiloBytes)\",\"subs\":[{\"name\":\"0\"}]},{\"name\":\"mem.usage.average (percent)\",\"subs\":[{\"name\":\"0\"}]},{\"name\":\"power.power.average (watt)\",\"subs\":[{\"name\":\"0\"}]},{\"name\":\"mem.active.average (kiloBytes)\",\"subs\":[{\"name\":\"0\"}]}]}]";

        /// <summary>
        /// Returns the default to monitor counters for a given monitor source.
        /// </summary>
        /// <param name="monitorSource"></param>
        /// <returns></returns>
        private static Entities Get(string monitorSource) {
            string defaultWIW = null;

            switch (monitorSource) {
                case "Dstat Agent":
                    defaultWIW = _dstat;
                    break;
                case "Local WMI":
                case "WMI Agent":
                    defaultWIW = _wmi;
                    break;
                case "ESXi":
                    defaultWIW = _esxi;
                    break;
                case "Racktivity":
                case "Hotbox Agent":
                case "HMT Agent":
                case "IPMI":
                    defaultWIW = _allAvailable;
                    break;
            }

            if (defaultWIW == null) return null;

            return JsonConvert.DeserializeObject<Entities>(defaultWIW);
        }

        /// <summary>
        /// Set the default counters to the wiw of the given monito, if available. Only applicable for available entities.
        /// </summary>
        /// <param name="monitor"></param>
        /// <param name="wdyh"></param>
        public static void Set(Monitor monitor, Entities wdyh) {
            Entities defaultWIW = Get(monitor.MonitorSource.ToString());
            if (defaultWIW != null) {
                var wiw = new Entities();

                //Add all available entities from wdyh.
                if (defaultWIW[0].GetName() == "*" && defaultWIW[0].GetSubs().Count == 0) {
                    foreach (Entity wdyhEntity in wdyh)
                        if (wdyhEntity.IsAvailable()) {
                            var entity = new Entity(wdyhEntity.GetName(), true);
                            wiw.Add(entity);

                            foreach (CounterInfo wdyhCounterInfo in wdyhEntity.GetSubs()) {
                                var counterInfo = new CounterInfo(wdyhCounterInfo.GetName());
                                entity.GetSubs().Add(counterInfo);

                                foreach (CounterInfo wdyhInstance in wdyhCounterInfo.GetSubs())
                                    counterInfo.GetSubs().Add(new CounterInfo(wdyhInstance.GetName()));
                            }
                        }
                } else { //Or add specific counter infos.
                    int entitiesLength = defaultWIW[0].GetName() == "*" ? wdyh.Count : 1;
                    Entity defaultWiwEntity = defaultWIW[0];

                    //Set the entities to Wiw...
                    for (int entityIndex = 0; entityIndex != entitiesLength; entityIndex++) {
                        Entity wdyhEntity = wdyh[entityIndex];
                        if (!wdyhEntity.IsAvailable()) continue;

                        var entity = new Entity(wdyhEntity.GetName(), wdyhEntity.IsAvailable());
                        wiw.Add(entity);

                        //And the counter infos...
                        foreach (CounterInfo defaultWiwCounterInfo in defaultWiwEntity.GetSubs()) {
                            CounterInfo wdyhCounterInfo = GetCounterInfo(wdyhEntity, defaultWiwCounterInfo.GetName());
                            if (wdyhCounterInfo != null) {
                                var counterInfo = new CounterInfo(defaultWiwCounterInfo.GetName());
                                entity.GetSubs().Add(counterInfo);

                                //And the instances.
                                if (defaultWiwCounterInfo.GetSubs().Count != 0) {
                                    int instancesLength = defaultWiwCounterInfo.GetSubs()[0].GetName() == "*" ? wdyhCounterInfo.GetSubs().Count : 1;

                                    for (int instanceIndex = 0; instanceIndex != instancesLength; instanceIndex++)
                                        counterInfo.GetSubs().Add(new CounterInfo(wdyhCounterInfo.GetSubs()[instanceIndex].GetName()));
                                }
                            }
                        }
                    }
                }
                monitor.Wiw = wiw;
            }
        }

        private static CounterInfo GetCounterInfo(Entity entity, string counterInfoName) {
            foreach (CounterInfo counterInfo in entity.GetSubs())
                if (counterInfo.GetName() == counterInfoName)
                    return counterInfo;
            return null;
        }
    }
}