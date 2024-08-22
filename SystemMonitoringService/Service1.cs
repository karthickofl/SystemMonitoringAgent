using System;
using System.Configuration;
using System.IO;
using System.ServiceProcess;
using System.Timers;
using Serilog;

namespace SystemMonitoringAgent
{
    public partial class Service1 : ServiceBase
    {
        private Timer timer;
        private readonly SysMonHelper sysMonHelper;
        private readonly ILogger log;

        public Service1()
        {
           // InitializeComponent();
            string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "system_monitoring_service_.log");

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(logFile, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 2)
                .CreateLogger();

            log = Log.Logger;
            log.Information("Service initialized successfully.");

            sysMonHelper = new SysMonHelper(log);
            sysMonHelper.SystemUtilDataCollector();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                int interval = int.Parse(ConfigurationManager.AppSettings["ServiceRestartTimeInterval"] ?? "60000");

                timer = new Timer(interval);
                timer.Elapsed += ProcessSysMon;
                timer.Start();

                log.Information("Service started successfully with an interval of {Interval} milliseconds.", interval);
            }
            catch (Exception ex)
            {
                log.Fatal(ex, "Critical error during service start: {Message}", ex.Message);
                this.Stop();
            }
        }

        protected override void OnStop()
        {
            timer?.Stop();
            timer?.Dispose();
            log.Information("Service stopped successfully.");
            Log.CloseAndFlush();
        }

        private void ProcessSysMon(object sender, ElapsedEventArgs e)
        {
            try
            {
                sysMonHelper.SystemUtilDataCollector();
                log.Information("System utilization details retrieved.");
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error retrieving system utilization details: {Message}", ex.Message);
            }
        }
    }
}
