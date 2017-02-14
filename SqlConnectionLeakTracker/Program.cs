using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SqlConnectionLeakTracker
{
    public static class SqlConnectionWrapper
    {
        /// <summary>Keeps track of instantiated SqlConnection objects that haven't been closed/disposed</summary>
        private static ConcurrentDictionary<SqlConnection, string> _instantiatedConnections = new ConcurrentDictionary<SqlConnection, string>();
        /// <summary>Keeps track of all SqlConnection objects that have had open called on them that haven't been closed/disposed</summary>
        private static ConcurrentDictionary<SqlConnection, string> _openedConnections = new ConcurrentDictionary<SqlConnection, string>();
        /// <summary>Keeps track of all SqlConnection objects that have had open called on them (regardless of whether they've been closed/disposed</summary>
        private static ConcurrentDictionary<SqlConnection, string> _allOpenedConnections = new ConcurrentDictionary<SqlConnection, string>();

        private static IReadOnlyList<KeyValuePair<SqlConnection, string>> GetInstantiatedOpenConnections()
            => _instantiatedConnections.Where(c => c.Key.State != ConnectionState.Closed).ToList();

        public static void PrintInstantiatedOpenConnections(string filename, bool append = false)
        {
            var instantiatedOpenConnections = GetInstantiatedOpenConnections();

            if (instantiatedOpenConnections.Count > 0)
                using (var f = File.Open(filename, append ? FileMode.OpenOrCreate : FileMode.Create))
                    PrintInstantiatedOpenConnections(f, instantiatedOpenConnections);
        }

        public static void PrintInstantiatedOpenConnections(Stream stream, IReadOnlyList<KeyValuePair<SqlConnection, string>> instantiatedOpenConnections = null)
        {
            instantiatedOpenConnections = instantiatedOpenConnections ?? GetInstantiatedOpenConnections();
            PrintConnections(stream, instantiatedOpenConnections.Count, instantiatedOpenConnections.Select(kvp => kvp.Value), "instantiated and unclosed connections");
        }

        public static void PrintOpenConnections(string filename, bool append = false)
        {
            if (_openedConnections.Count > 0)
                using (var f = File.Open(filename, append ? FileMode.OpenOrCreate : FileMode.Create))
                    PrintOpenConnections(f);
        }

        public static void PrintOpenConnections(Stream stream)
        {
            PrintConnections(stream, _openedConnections.Count, _openedConnections.Values, "open connections");
        }

        private static IReadOnlyList<IGrouping<string, string>> GetAllOpenedConnectionStrings()
            => _allOpenedConnections.GroupBy(kvp => kvp.Key.ConnectionString, kvp => kvp.Value).ToList();

        public static void PrintAllOpenedConnectionStrings(string filename, bool append = false)
        {
            var groups = GetAllOpenedConnectionStrings();
            if (groups.Count > 0)
                using (var f = File.Open(filename, append ? FileMode.OpenOrCreate : FileMode.Create))
                    PrintAllOpenedConnectionStrings(f, groups);
        }

        public static void PrintAllOpenedConnectionStrings(Stream stream, IReadOnlyList<IGrouping<string, string>> groups)
        {
            groups = groups ?? GetAllOpenedConnectionStrings();

            using (var sw = new StreamWriter(stream, Encoding.UTF8, 1024, true))
            {
                sw.WriteLine($"{groups.Count} unique opened connection strings");
                foreach (var gr in groups)
                {
                    sw.WriteLine($"{gr.Count()} places opened {gr.Key}");
                    sw.WriteLine(string.Join("\n-----\n", gr));
                }
            }
        }

        private static void PrintConnections(Stream stream, int stackTraceCount, IEnumerable<string> stackTraces, string countMessage)
        {
            if (stackTraceCount > 0)
                using (var sw = new StreamWriter(stream, Encoding.UTF8, 1024, true))
                {
                    sw.WriteLine($"{stackTraceCount} {countMessage}");
                    sw.WriteLine(string.Join("\n-----\n", stackTraces));
                }
        }


        public static SqlConnection SqlConnectionCtor()
        {
            var result = new SqlConnection();
            _instantiatedConnections[result] = Environment.StackTrace;
            return result;
        }
        
        public static SqlConnection SqlConnectionCtor(string connectionString)
        {
            var result = new SqlConnection(connectionString);
            _instantiatedConnections[result] = Environment.StackTrace;
            return result;
        }
        
        public static SqlConnection SqlConnectionCtor(string connectionString, SqlCredential credential)
        {
            var result = new SqlConnection(connectionString, credential);
            _instantiatedConnections[result] = Environment.StackTrace;
            return result;
        }


        public static void SqlConnectionOpen(SqlConnection conn)
        {
            RegisterOpened(conn);
            conn.Open();
        }

        public static void DbConnectionOpen(DbConnection conn)
        {
            RegisterOpened(conn as SqlConnection);
            conn.Open();
        }

        public static void IDbConnectionOpen(IDbConnection conn)
        {
            RegisterOpened(conn as SqlConnection);
            conn.Open();
        }

        
        public static Task SqlConnectionOpenAsync(SqlConnection conn)
        {
            RegisterOpened(conn);
            return conn.OpenAsync();
        }

        public static Task DbConnectionOpenAsync(DbConnection conn)
        {
            RegisterOpened(conn as SqlConnection);
            return conn.OpenAsync();
        }
        

        public static Task SqlConnectionOpenAsync(SqlConnection conn, CancellationToken token)
        {
            RegisterOpened(conn);
            return conn.OpenAsync(token);
        }

        public static Task DbConnectionOpenAsync(DbConnection conn, CancellationToken token)
        {
            RegisterOpened(conn as SqlConnection);
            return conn.OpenAsync(token);
        }
        

        private static void RegisterOpened(SqlConnection conn)
        {
            if (conn != null)
            {
                var st = Environment.StackTrace;
                _openedConnections[conn] = st;
                _allOpenedConnections[conn] = st;
            }
        }

        
        public static void SqlConnectionClose(SqlConnection conn)
        {
            Unregister(conn);
            conn.Close();
        }

        public static void DbConnectionClose(DbConnection conn)
        {
            Unregister(conn as SqlConnection);
            conn.Close();
        }
        
        public static void IDbConnectionClose(IDbConnection conn)
        {
            Unregister(conn as SqlConnection);
            conn.Close();
        }

        
        public static void SqlConnectionDispose(SqlConnection conn)
        {
            Unregister(conn);
            conn.Dispose();
        }

        public static void DbConnectionDispose(DbConnection conn)
        {
            Unregister(conn as SqlConnection);
            conn.Dispose();
        }
        
        public static void IDbConnectionDispose(IDbConnection conn)
        {
            Unregister(conn as SqlConnection);
            conn.Dispose();
        }
        
        public static void ComponentDispose(Component conn)
        {
            Unregister(conn as SqlConnection);
            conn.Dispose();
        }

        public static void IComponentDispose(IComponent conn)
        {
            Unregister(conn as SqlConnection);
            conn.Dispose();
        }

        public static void IDisposableDispose(IDisposable disposable)
        {
            Unregister(disposable as SqlConnection);
            disposable.Dispose();
        }

        private static void Unregister(SqlConnection conn)
        {
            string dummy;
            if (conn != null)
            {
                _instantiatedConnections.TryRemove(conn, out dummy);
                _openedConnections.TryRemove(conn, out dummy);
            }
        }

        
        public static IReadOnlyDictionary<string, MethodReference> GetConstructorReplacements(ModuleDefinition moduleToRewrite)
        {
            var sqlConnectionType = CecilHelper.GetType(typeof(SqlConnection));
            var thisType = CecilHelper.GetType(typeof(SqlConnectionWrapper));

            return new[] { 0, 1, 2 }.Select(numParams => Tuple.Create(
                    sqlConnectionType.Methods.Where(m => m.Name == ".ctor" && m.Parameters.Count == numParams).First(),
                    thisType.Methods.Where(m => m.Name == "SqlConnectionCtor" && m.Parameters.Count == numParams).First()))
                .ToDictionary(t => t.Item1.FullName, t => moduleToRewrite.Import(t.Item2));
        }

        public static IReadOnlyDictionary<string, MethodReference> GetMethodReplacements(ModuleDefinition moduleToRewrite)
        {
            // the commented out methods don't actually exist, but there's no guarantee that that
            // won't change in a future version of the .NET framework
            return new[] {
                Tuple.Create(
                    CecilHelper.GetType(typeof(SqlConnection)).Methods.Where(m => m.Name == nameof(SqlConnection.Open)).First(),
                    CecilHelper.GetType(typeof(SqlConnectionWrapper)).Methods.Where(m => m.Name == nameof(SqlConnectionWrapper.SqlConnectionOpen)).First()),
                Tuple.Create(
                    CecilHelper.GetType(typeof(DbConnection)).Methods.Where(m => m.Name == nameof(DbConnection.Open)).First(),
                    CecilHelper.GetType(typeof(SqlConnectionWrapper)).Methods.Where(m => m.Name == nameof(SqlConnectionWrapper.DbConnectionOpen)).First()),
                Tuple.Create(
                    CecilHelper.GetType(typeof(IDbConnection)).Methods.Where(m => m.Name == nameof(IDbConnection.Open)).First(),
                    CecilHelper.GetType(typeof(SqlConnectionWrapper)).Methods.Where(m => m.Name == nameof(SqlConnectionWrapper.IDbConnectionOpen)).First()),
                
                //Tuple.Create(
                //    CecilHelper.GetType(typeof(SqlConnection)).Methods.Where(m => m.Name == nameof(SqlConnection.OpenAsync) && m.Parameters.Count == 0).First(),
                //    CecilHelper.GetType(typeof(SqlConnectionWrapper)).Methods.Where(m => m.Name == nameof(SqlConnectionWrapper.SqlConnectionOpenAsync) && m.Parameters.Count == 1).First()),
                Tuple.Create(
                    CecilHelper.GetType(typeof(DbConnection)).Methods.Where(m => m.Name == nameof(DbConnection.OpenAsync) && m.Parameters.Count == 0).First(),
                    CecilHelper.GetType(typeof(SqlConnectionWrapper)).Methods.Where(m => m.Name == nameof(SqlConnectionWrapper.DbConnectionOpenAsync) && m.Parameters.Count == 1).First()),
                
                Tuple.Create(
                    CecilHelper.GetType(typeof(SqlConnection)).Methods.Where(m => m.Name == nameof(SqlConnection.OpenAsync) && m.Parameters.Count == 1).First(),
                    CecilHelper.GetType(typeof(SqlConnectionWrapper)).Methods.Where(m => m.Name == nameof(SqlConnectionWrapper.SqlConnectionOpenAsync) && m.Parameters.Count == 2).First()),
                Tuple.Create(
                    CecilHelper.GetType(typeof(DbConnection)).Methods.Where(m => m.Name == nameof(DbConnection.OpenAsync) && m.Parameters.Count == 1).First(),
                    CecilHelper.GetType(typeof(SqlConnectionWrapper)).Methods.Where(m => m.Name == nameof(SqlConnectionWrapper.DbConnectionOpenAsync) && m.Parameters.Count == 2).First()),
                
                Tuple.Create(
                    CecilHelper.GetType(typeof(SqlConnection)).Methods.Where(m => m.Name == nameof(SqlConnection.Dispose)).First(),
                    CecilHelper.GetType(typeof(SqlConnectionWrapper)).Methods.Where(m => m.Name == nameof(SqlConnectionWrapper.SqlConnectionDispose)).First()),
                //Tuple.Create(
                //    CecilHelper.GetType(typeof(DbConnection)).Methods.Where(m => m.Name == nameof(DbConnection.Dispose)).First(),
                //    CecilHelper.GetType(typeof(SqlConnectionWrapper)).Methods.Where(m => m.Name == nameof(SqlConnectionWrapper.DbConnectionDispose)).First()),
                //Tuple.Create(
                //    CecilHelper.GetType(typeof(IDbConnection)).Methods.Where(m => m.Name == nameof(IDbConnection.Dispose)).First(),
                //    CecilHelper.GetType(typeof(SqlConnectionWrapper)).Methods.Where(m => m.Name == nameof(SqlConnectionWrapper.IDbConnectionDispose)).First()),
                Tuple.Create(
                    CecilHelper.GetType(typeof(Component)).Methods.Where(m => m.Name == nameof(Component.Dispose)).First(),
                    CecilHelper.GetType(typeof(SqlConnectionWrapper)).Methods.Where(m => m.Name == nameof(SqlConnectionWrapper.ComponentDispose)).First()),
                //Tuple.Create(
                //    CecilHelper.GetType(typeof(IComponent)).Methods.Where(m => m.Name == nameof(IComponent.Dispose)).First(),
                //    CecilHelper.GetType(typeof(SqlConnectionWrapper)).Methods.Where(m => m.Name == nameof(SqlConnectionWrapper.IComponentDispose)).First()),
                Tuple.Create(
                    CecilHelper.GetType(typeof(IDisposable)).Methods.Where(m => m.Name == nameof(IDisposable.Dispose)).First(),
                    CecilHelper.GetType(typeof(SqlConnectionWrapper)).Methods.Where(m => m.Name == nameof(SqlConnectionWrapper.IDisposableDispose)).First()),

                Tuple.Create(
                    CecilHelper.GetType(typeof(SqlConnection)).Methods.Where(m => m.Name == nameof(SqlConnection.Close)).First(),
                    CecilHelper.GetType(typeof(SqlConnectionWrapper)).Methods.Where(m => m.Name == nameof(SqlConnectionWrapper.SqlConnectionClose)).First()),
                Tuple.Create(
                    CecilHelper.GetType(typeof(DbConnection)).Methods.Where(m => m.Name == nameof(DbConnection.Close)).First(),
                    CecilHelper.GetType(typeof(SqlConnectionWrapper)).Methods.Where(m => m.Name == nameof(SqlConnectionWrapper.DbConnectionClose)).First()),
                Tuple.Create(
                    CecilHelper.GetType(typeof(IDbConnection)).Methods.Where(m => m.Name == nameof(IDbConnection.Close)).First(),
                    CecilHelper.GetType(typeof(SqlConnectionWrapper)).Methods.Where(m => m.Name == nameof(SqlConnectionWrapper.IDbConnectionClose)).First()),
            }.ToDictionary(t => t.Item1.FullName, t => moduleToRewrite.Import(t.Item2));
        }
    }

    internal static class CecilHelper
    {
        private static readonly Dictionary<string, ModuleDefinition> _moduleCache = new Dictionary<string, ModuleDefinition>();

        public static ModuleDefinition GetContainingModule(Type type)
        {
            var codebase = type.Assembly.CodeBase;
            ModuleDefinition result;
            if (!_moduleCache.TryGetValue(codebase, out result))
            {
                result = ModuleDefinition.ReadModule(GetFilename(codebase));
                _moduleCache[codebase] = result;
            }
            return result;
        }

        public static AssemblyDefinition GetContainingAssembly(Type type) => AssemblyDefinition.ReadAssembly(GetFilename(type.Assembly.CodeBase));
        
        public static TypeDefinition GetType(Type type) => GetContainingModule(type).GetType(type.FullName);


        private static string GetFilename(string uri)
        {
            if (!uri.StartsWith("file:///", StringComparison.InvariantCultureIgnoreCase))
                throw new ArgumentException($"Expected a file URI but got {uri}");
            return uri.Substring(8);
        }
    }

    internal class Program
    {
        private static void TryRewriteModule(string moduleToRewritePath)
        {
            try
            {
                var numModified = RewriteModule(moduleToRewritePath);
                Console.WriteLine($"{moduleToRewritePath}: {numModified} instructions modified");
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine($"{moduleToRewritePath}: ERROR {e.Message}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"{moduleToRewritePath}: UNKNOWN ERROR");
                Console.WriteLine(e);
            }
        }

        private static int RewriteModule(string moduleToRewritePath)
        {
            var assemblyToRewrite = AssemblyDefinition.ReadAssembly(moduleToRewritePath);
            if (assemblyToRewrite.Name.PublicKeyToken.Length > 0)
                throw new InvalidOperationException("Cannot rewrite a signed assembly");
            var moduleToRewrite = ModuleDefinition.ReadModule(moduleToRewritePath);

            var thisAssembly = CecilHelper.GetContainingAssembly(typeof(SqlConnectionWrapper));
            if (assemblyToRewrite.FullName == thisAssembly.FullName)
                throw new InvalidOperationException("Cannot rewrite myself");
            var thisModule = CecilHelper.GetContainingModule(typeof(SqlConnectionWrapper));

            moduleToRewrite.AssemblyReferences.Add(thisAssembly.Name);

            // we should probably have a better way to compare equality than the name... this will
            // have problems if there are multiple functions with the same name (which is a pretty
            // small corner case)
            var constructorReplacements = SqlConnectionWrapper.GetConstructorReplacements(moduleToRewrite);
            var methodReplacements = SqlConnectionWrapper.GetMethodReplacements(moduleToRewrite);


            var numInstructionsModified = 0;

            foreach (var method in moduleToRewrite.Types.Where(t => t.HasMethods).SelectMany(t => t.Methods).Where(m => m.HasBody))
                numInstructionsModified += RewriteMethod(method, constructorReplacements, methodReplacements, moduleToRewrite);

            if (numInstructionsModified > 0)
                moduleToRewrite.Write(moduleToRewritePath, new WriterParameters { WriteSymbols = true });

            return numInstructionsModified;
        }

        /// <summary>
        /// This doesn't work with Calli instructions, which should be okay because those are not
        /// generated by the C# compiler, at least according to
        /// http://stackoverflow.com/questions/7110666/il-instructions-not-exposed-by-c-sharp
        /// </summary>
        private static int RewriteMethod(MethodDefinition method, IReadOnlyDictionary<string, MethodReference> constructorReplacements, IReadOnlyDictionary<string, MethodReference> methodReplacements, ModuleDefinition moduleToRewrite)
        {
            var replacements = new List<Tuple<Instruction, Instruction>>();
            var il = method.Body.GetILProcessor();

            foreach (var instruction in method.Body.Instructions)
            {
                MethodReference toReplace;
                if (instruction.OpCode == OpCodes.Newobj)
                {
                    var currMethod = instruction.Operand as MethodReference;
                    if (currMethod != null && constructorReplacements.TryGetValue(currMethod.FullName, out toReplace))
                        replacements.Add(Tuple.Create(instruction, il.Create(OpCodes.Call, toReplace)));
                }
                else if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
                {
                    var currMethod = instruction.Operand as MethodReference;
                    if (currMethod != null && methodReplacements.TryGetValue(currMethod.FullName, out toReplace))
                    {
                        // this is a hack. If there is a Constrained instruction before a call to a
                        // virtual method, it means that it is a member function being called on a
                        // value type. When we pass it as a parameter, we need to do box instead,
                        // and the instruction before that needs ot be a ldloc instead of a ldloca

                        // so we should have some code here that looks something like:
                        // > replacements.Add(Tuple.Create(prevInstruction.Previous, il.Create(OpCodes.ldloc, (VariableReference)prevInstruction.Previous.Operand))
                        // > replacements.Add(Tuple.Create(prevInstruction, il.Create(OpCodes.Box, (TypeReference)prevInstruction.Operand));
                        // but with the correct version of ldloca in the first instruction

                        // instead of all of this, we just ignore all of this, because we're only
                        // interested in SqlConnection objects which are never value types
                        var prevInstruction = instruction.Previous;
                        if (prevInstruction != null && prevInstruction.OpCode == OpCodes.Constrained)
                            break; 

                        replacements.Add(Tuple.Create(instruction, il.Create(OpCodes.Call, toReplace)));
                    }
                }
            }

            foreach (var r in replacements)
                il.Replace(r.Item1, r.Item2);

            return replacements.Count;
        }

        public static void Main(string[] args)
        {
            if (args.Length == 0)
                args = new[] { "." };

            foreach (var arg in args)
            {
                if (File.Exists(arg))
                    TryRewriteModule(arg);
                else if (Directory.Exists(arg))
                    foreach (var module in Directory.EnumerateFiles(arg)
                        .Where(f => f.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase)
                        || f.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase)))
                        TryRewriteModule(module);
                else
                    Console.WriteLine($"ERROR: {arg} does not exist");
            }
        }
    }
}
