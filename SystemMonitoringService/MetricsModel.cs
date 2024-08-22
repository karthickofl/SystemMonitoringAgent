using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SystemMonitoringAgent
{
    public class ParamMetrics
    {
       public string ParamName { get; set; }
        public dynamic ParamValue { get; set; }
        public int ErrorCode { get; set; }
    }

    public class SystemMetricsData
    {
        public string IPV4Address { get; set; }
        public string HostName { get; set; }
        public string OSVersion { get; set; }
        public string RetrievalTimestamp { get; set; }

        public List<ParamMetrics> ParamsMetricsList = new List<ParamMetrics>();
    }

}
