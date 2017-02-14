using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SqlConnectionLeakTracker
{
    public class PerfCounters
    {
        private static PerformanceCounter NumberOfActiveConnectionPoolsCounter;
        private static PerformanceCounter NumberOfPooledConnectionsCounter;

        private static SqlConnection connection = new SqlConnection();

        public static int ActiveConnectionPools => (int)NumberOfActiveConnectionPoolsCounter.NextValue();
        public static int PooledConnections => (int)NumberOfPooledConnectionsCounter.NextValue();

        public static void SetUpPerformanceCounters()
        {
            if (NumberOfActiveConnectionPoolsCounter != null)
                return;

            var instanceName = GetInstanceName();
            NumberOfActiveConnectionPoolsCounter = new PerformanceCounter
            {
                CategoryName = ".NET Data Provider for SqlServer",
                CounterName = "NumberOfActiveConnectionPools",
                InstanceName = instanceName
            };

            NumberOfPooledConnectionsCounter = new PerformanceCounter
            {
                CategoryName = ".NET Data Provider for SqlServer",
                CounterName = "NumberOfPooledConnections",
                InstanceName = instanceName
            };
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int GetCurrentProcessId();

        private static string GetInstanceName()
        {
            //This works for Winforms apps.  
            string instanceName = System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Name;

            instanceName = instanceName ??
                AppDomain.CurrentDomain.FriendlyName.ToString()
                .Replace('(', '[').Replace(')', ']').Replace('#', '_')
                .Replace('/', '_').Replace('\\', '_');

            string pid = GetCurrentProcessId().ToString();
            instanceName = instanceName + "[" + pid + "]";
            return instanceName;
        }
    }

}
