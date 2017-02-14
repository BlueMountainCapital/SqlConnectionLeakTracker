using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TestApp
{
    internal class Program
    {
        private const string ExpectedOpenConnections = @"1 open connections
   at System.Environment.GetStackTrace(Exception e, Boolean needFileInfo)
   at System.Environment.get_StackTrace()
   at SqlConnectionLeakTracker.SqlConnectionWrapper.RegisterOpened(SqlConnection conn)
   at SqlConnectionLeakTracker.SqlConnectionWrapper.DbConnectionOpen(DbConnection conn)
   at TestApp.Program.Main(String[] args)";

        private const string ExpectedInstantiatedConnections = @"2 instantiated connections
   at System.Environment.GetStackTrace(Exception e, Boolean needFileInfo)
   at System.Environment.get_StackTrace()
   at SqlConnectionLeakTracker.SqlConnectionWrapper.SqlConnectionCtor()
   at TestApp.Program.Main(String[] args)
-----
   at System.Environment.GetStackTrace(Exception e, Boolean needFileInfo)
   at System.Environment.get_StackTrace()
   at SqlConnectionLeakTracker.SqlConnectionWrapper.SqlConnectionCtor(String connectionString)
   at TestApp.Program.Main(String[] args)";

        private static void Main(string[] args)
        {
            // these are all closed
            using (new SqlConnection()) { }
            new SqlConnection().Close();
            new SqlConnection().Dispose();

            // instantiated but never disposed/closed
            new SqlConnection();

            // these are opened and closed properly
            var s = new SqlConnection(@"Data Source=(LocalDB)\MSSQLLocalDB");
            s.Open(); // this requires SQL Server Express to be installed
            s.Close();

            s = new SqlConnection(@"Data Source=(LocalDB)\MSSQLLocalDB");
            s.Open(); // this requires SQL Server Express to be installed
            s.Dispose();

            // opened and never disposed/closed
            s = new SqlConnection(@"Data Source=(LocalDB)\MSSQLLocalDB");
            s.Open(); // this requires SQL Server Express to be installed


            // make sure value-type IDisposables work correctly
            using (var e = new List<double>().GetEnumerator()) { }
            
            var openConnections = GetString(SqlConnectionLeakTracker.SqlConnectionWrapper.PrintOpenConnections);
            if (!CompareStackTraces(ExpectedOpenConnections, openConnections))
            {
                Console.WriteLine("ERROR does not match expected");
                Console.WriteLine(openConnections);
            }

            var instantiatedConnections = GetString(stream => SqlConnectionLeakTracker.SqlConnectionWrapper.PrintInstantiatedOpenConnections(stream));
            if (!CompareStackTraces(ExpectedInstantiatedConnections, instantiatedConnections))
            {
                Console.WriteLine("ERROR does not match expected");
                Console.WriteLine(instantiatedConnections);
            }
        }

        private static string GetString(Action<Stream> streamWriter)
        {
            using (var ms = new MemoryStream())
            {
                streamWriter(ms);
                ms.Seek(0, SeekOrigin.Begin);
                using (var r = new StreamReader(ms))
                    return r.ReadToEnd();
            }
        }

        private static bool CompareStackTraces(string expected, string actual)
        {
            using (var expectedEtor = ((IReadOnlyList<string>)expected.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)).GetEnumerator())
            using (var actualEtor = ((IReadOnlyList<string>)actual.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)).GetEnumerator())
            {
                bool expectedHasNext, actualHasNext;
                
                do
                {
                    expectedHasNext = expectedEtor.MoveNext();
                    actualHasNext = actualEtor.MoveNext();
                    if (expectedHasNext != actualHasNext)
                    {
                        Console.WriteLine("differetn number of lines");
                        return false; // different number of elements
                    }
                    if (!expectedHasNext)
                        break;
                    
                    if (!actualEtor.Current.StartsWith(expectedEtor.Current))
                    {
                        Console.WriteLine($"Line problem: #{actualEtor.Current}# #{expectedEtor.Current}#");
                        return false; // line does not match up
                    }
                } while (expectedHasNext);
            }

            return true;
        }
    }
}
