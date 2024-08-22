using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;
using Newtonsoft.Json;
using Serilog;

namespace SystemMonitoringAgent
{
    internal class SysMonHelper
    {


        private readonly ILogger log;
        public SysMonHelper(ILogger ilog)
        {
            log = ilog;
        }

        #region WMIQuery 
        private readonly string proccesorQ = "SELECT LoadPercentage FROM Win32_Processor";
        private readonly string memoryQ = "SELECT FreePhysicalMemory,TotalVisibleMemorySize from Win32_OperatingSystem";
        //private readonly string diskUtilQ = "SELECT Description, DeviceId, FreeSpace, Size from Win32_LogicalDisk";
        //private readonly string availableDiskQ = "SELECT Description, DeviceId, FreeSpace, Size from Win32_LogicalDisk";
        //private readonly string avgDiskWriteQ = "select AvgDiskBytesPerWrite from Win32_PerfFormattedData_PerfDisk_LogicalDisk";
        //private readonly string avgDiskReadQ = "select AvgDiskBytesPerRead from Win32_PerfFormattedData_PerfDisk_LogicalDisk";
        //private readonly string currDiskQueueQ = "select CurrentDiskQueueLength from win32_perfformatteddata_perfdisk_logicaldisk";
        //private readonly string netInBytes = "select BytesReceivedPersec from Win32_PerfFormattedData_Tcpip_NetworkInterface";
        //private readonly string netOutBytes = "select BytesSentPersec from Win32_PerfFormattedData_Tcpip_NetworkInterface";
        //private readonly string netInBytesPI = "select BytesReceivedPersec ,Name from Win32_PerfFormattedData_Tcpip_NetworkInterface";
        //private readonly string netOutBytesPI = "select BytesSentPersec , Name from Win32_PerfFormattedData_Tcpip_NetworkInterface";
        //private readonly string servicelistQ = "Select Name,State from Win32_Service Where Name = '0'";
        private readonly string osVersionQ = "Select Caption from Win32_OperatingSystem";
        #endregion

        #region global parameter names 
        private readonly string _processorUtil = "ProcessorUtil";
        private readonly string _memoryUtil = "MemoryUtil";
        private readonly string _osVersion = "OSVersion";

        //private readonly string _diskUtilPerDrive = "DiskUtilPerDrive";
        //private readonly string _availableDiskUtil = "AvailableDisk";
        //private readonly string _averageLogicalDiskWritesUtil = "AverageLogicalDiskWrites";
        //private readonly string _averageLogicalDiskReadsUtil = "AverageLogicalDiskReads";
        //private readonly string _currentLogicalDiskQueueLengthUtil = "CurrentLogicalDiskQueueLength";
        //private readonly string _networkInterfaceInBytesUtil = "NetworkInterfaceInBytes";
        //private readonly string _networkInterfaceOutBytesUtil = "NetworkInterfaceOutBytes";
        #endregion

        #region GetAppconfig Values
        static readonly string apiUrl = ConfigurationManager.AppSettings["APIURL"];
        static readonly string postApiEndpoint = ConfigurationManager.AppSettings["PostApiEndPoint"];
        #endregion



        public void SystemUtilDataCollector()
        {
            SystemMetricsData sysMetricsData = new SystemMetricsData();
            var paramUtilList = new List<ParamMetrics>();
            string osVersion = null;
            DateTime currentDateTime = DateTime.Now;
            string formattedDateTime = currentDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

            try
            {
                log.Information("SystemUtilDataCollector() entered");

                paramUtilList.Add(GetParameterUtil(_processorUtil, proccesorQ));
                paramUtilList.Add(GetParameterUtil(_memoryUtil, memoryQ));
                paramUtilList.Add(GetParameterUtil(_osVersion, osVersionQ));
                var osVersionItem = paramUtilList.FirstOrDefault(s => s.ParamName == "OSVersion");

                if (osVersionItem != null)
                {
                    osVersion = osVersionItem.ParamValue;
                    paramUtilList.Remove(osVersionItem);
                }

                sysMetricsData.HostName = Dns.GetHostName();
                sysMetricsData.IPV4Address = GetLocalIPv4Address();
                sysMetricsData.RetrievalTimestamp = formattedDateTime;
                sysMetricsData.ParamsMetricsList.AddRange(paramUtilList);
                sysMetricsData.OSVersion = osVersion;

                log.Information("SystemUtilDataCollector completed successfully. Data: {@SysUtilList}", paramUtilList);
                SendMetricsDataToAPI(sysMetricsData);

            }
            catch (Exception ex)
            {
                log.Error(ex, "An error occurred in SystemUtilDataCollector");
            }

        }

        private ParamMetrics GetParameterUtil(string paramName, string wql)
        {
            var paramUtilModel = new ParamMetrics();

            try
            {
                paramUtilModel.ParamName = paramName;
                paramUtilModel.ErrorCode = 0;
                var searcher = new ManagementObjectSearcher(new ObjectQuery(wql));
                var searcherResult = searcher.Get();

                foreach (var item in searcherResult)
                {
                    switch (paramName)
                    {
                        case "ProcessorUtil":
                            paramUtilModel.ParamValue = item["LoadPercentage"].ToString();
                            break;
                        case "MemoryUtil":
                            double totalMemorySize = Convert.ToDouble(item["TotalVisibleMemorySize"]);
                            double freePhysicalMemory = Convert.ToDouble(item["FreePhysicalMemory"]);
                            double memoryPct = 100.0 - freePhysicalMemory / totalMemorySize * 100.0;
                            paramUtilModel.ParamValue = memoryPct.ToString("F2");
                            break;
                        case "OSVersion":
                            paramUtilModel.ParamValue = item["Caption"].ToString();
                            break;
                        default:
                            paramUtilModel.ParamValue = $"Unsupported/ not implemented parameter name: {paramName}";
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                paramUtilModel.ParamValue = null;
                paramUtilModel.ErrorCode = -1;
                log.Error(ex, $"Error processing parameter {paramName}: {ex.Message}");
            }

            return paramUtilModel;
        }

        public string GetLocalIPv4Address()
        {
            List<string> ipv4AddressList = new List<string>();
            string csvStrResult = string.Empty;

            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up)
                    {
                        PhysicalAddress address = ni.GetPhysicalAddress();
                        if (address != null)
                        {
                            var ss = string.Join(":", address.GetAddressBytes().Select(b => b.ToString("X2")));
                        }

                        foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip.Address))
                            {
                                ipv4AddressList.Add(ip.Address.ToString());
                            }
                        }
                    }
                }
                csvStrResult = string.Join(", ", ipv4AddressList);
            }
            catch (Exception ex)
            {
                log.Error(ex, "An error occurred in GetLocalIPv4Address()");
            }

            return csvStrResult;
        }

        public void SendMetricsDataToAPI(SystemMetricsData metricsdata)
        {

            //  SystemMetricsData systemMetricsData = new SystemMetricsData();
            string postJson = "";

            try
            {
                string PostApiURL = apiUrl + postApiEndpoint;

                log.Information("SendMetricsDataToAPI() Entered");

                if (metricsdata != null)
                {
                    string webAddr = PostApiURL;
                    var httpWebRequest = (HttpWebRequest)WebRequest.Create(webAddr);
                    httpWebRequest.ServerCertificateValidationCallback = delegate { return true; };
                    httpWebRequest.ContentType = "application/json; charset=utf-8";
                    httpWebRequest.Method = "POST";

                    // log.Information("SendMetricsDataToAPI API Endpoint: " + webAddr, "");

                    postJson = JsonConvert.SerializeObject(metricsdata);
                    log.Information("metricsdata post json: " + postJson);


                    using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                    {
                        streamWriter.Write(postJson);
                        streamWriter.Flush();
                    }

                    try
                    {
                        var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

                        log.Information("SendMetricsDataToAPI | API HIT Status : " + httpResponse.StatusCode.ToString());

                        if (httpResponse.StatusCode == HttpStatusCode.OK)
                        {

                            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                            {
                                var responseText = streamReader.ReadToEnd();
                                log.Information("SendMetricsDataToAPI | API Response: " + responseText);
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        log.Information("SendMetricsDataToAPI | API Connect Exception :" + ex);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Information("SendMetricsDataToAPI | Exception :" + ex);
            }

        }


    }
}
