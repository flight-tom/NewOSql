using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace oSQL {

    internal static class Program
    {
        private const int MaxCount = 10;
        private static readonly SemaphoreSlim Semaphore = new(MaxCount, MaxCount);
        private static StreamWriter? Sw { get; set; }
        private static StreamWriter? ExportFileSw { get; set; }

        private static async Task Main(string[] args) {
            if (args.Length > 0) {
                var option = new Option();
                option.Setup(args);
                PrepareLogAndExportFile(option);

                var sqlConnectionString = PrepareConnectionString(option);
                if (option.RenewDB) await DropAndCreateNewDb(sqlConnectionString, option);
                if (!string.IsNullOrEmpty(option.SqlFolder)) {
                    var dir = new DirectoryInfo(option.SqlFolder);
                    if (dir.Exists)
                        await RunAllSqlScripts(dir, option, sqlConnectionString);
                } else if (string.IsNullOrEmpty(option.SqlPath)) {
                    var file = new FileInfo(option.SqlPath);
                    if (file.Exists) {
                        if (string.IsNullOrEmpty(option.ExportPath))
                            await ExecuteSqlFile(file, sqlConnectionString, option);
                        else
                            ExportData(file, sqlConnectionString, option);
                    }
                }

                Dispose();
                //if (has_error)
                //    throw new ApplicationException("Some scripts were running with error, please check out!");
            } else
                ShowHelp();
        }

        private static void ExportData(FileInfo sqlFile, string sqlConnectionString, Option option)
        {
            // if (ExportFileSw is null)
            //     throw new ArgumentNullException(nameof(sqlFile), "You didn't specify a file path for exporting!");
            // var sql = // $"USE [{option.DestDatabase}]\n" + 
            //           ReadSql(sqlFile);
            // CodeScan(sql);
            // using var conn = new SqlConnection(sqlConnectionString);
            // var cmd = conn.CreateCommand();
            // cmd.CommandType = CommandType.Text;
            // cmd.CommandText = sql;
            // cmd.CommandTimeout = 0;
            // OutputResultToExportFile(cmd);
        }

        private static void Dispose() {
            if (Sw is not null) {
                Sw.Close();
                Sw.Dispose();
            }

            if (ExportFileSw is null) return;
            ExportFileSw.Close();
            ExportFileSw.Dispose();
        }

        private static async Task ExecuteSqlFile(FileInfo sqlFile, string sqlConnectionString, Option option) {
            // var sqlScriptContent = $"USE [{option.DestDatabase}]\n" + ReadSql(sqlFile);

            await Semaphore.WaitAsync();
            try
            {
                var sr = ReadSql(sqlFile);
                var hasError = false;
                var sql = (await sr.ReadLineAsync())?.Trim();
                var sb = new StringBuilder();
                while (!sr.EndOfStream)
                {
                    if (string.Compare(sql, "GO", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        var sqlScript = sb.ToString();
                        try
                        {
                            await ExecuteSql(sqlConnectionString, sqlScript);
                        }
                        catch (Exception ex)
                        {
                            LogMessage("ERROR : " + sqlFile.FullName + " : " + ex.Message);
                            hasError = true;
                            throw;
                        }
                        finally
                        {
                            sb.Clear();
                        }
                    }
                    else
                    {
                        sb.AppendLine(sql);
                    }

                    sql = (await sr.ReadLineAsync())?.Trim();
                }
            }
            finally
            {
                Semaphore.Release();
                Console.WriteLine(
                    $"The sql file[{sqlFile.Name}] has been done, there're {MaxCount - Semaphore.CurrentCount} tasks still running...");
            }
        }

        private static async Task RunAllSqlScripts(DirectoryInfo dir, Option option, string connectionString) {
			var sqlFiles = dir.GetFiles ("*.sql").OrderBy (d => d.Name);
            var tasks = sqlFiles.Select(file => ExecuteSqlFile(file, connectionString, option)).ToArray();
            Task.WaitAll(tasks);

            var subDirs = dir.GetDirectories ().OrderBy (d => d.Name).ToList();
            if (!subDirs.Any()) return;
            foreach (var sub in subDirs)
                await RunAllSqlScripts(sub, option, connectionString);
        }

        private static async Task DropAndCreateNewDb(string sqlConnectionString, Option option) {
            await ExecuteSql(sqlConnectionString, $"IF DB_ID('{option.DestDatabase}') IS NOT NULL\nDROP DATABASE [{option.DestDatabase}]");
            await ExecuteSql(sqlConnectionString, $"CREATE DATABASE [{option.DestDatabase}]");
        }

        private static async Task ExecuteSql(string sqlConnectionString, string sql) {
            await using var conn = new SqlConnection(sqlConnectionString);
            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = sql;
            cmd.CommandTimeout = 0;
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            await conn.CloseAsync();
        }

        private static string PrepareConnectionString(Option option) {
            return option.EnableIntegratedSecurity
                ? $"Data Source={option.ServerIp};Initial Catalog={option.DestDatabase};Persist Security Info=True;Integrated Security=True"
                : $"Data Source={option.ServerIp};Initial Catalog={option.DestDatabase};Persist Security Info=True;User ID={option.DbAccount};Password={option.DbPassword}";
        }

        private static void OutputResultToExportFile(SqlCommand cmd) {
            var dt = new DataTable();
            using (var da = new SqlDataAdapter()) {
                da.SelectCommand = cmd;
                da.Fill(dt);
            }

            WriteTheColumnLine(dt);
            WriteResultToFile(dt);
        }

        private static void WriteResultToFile(DataTable dt) {
            foreach (DataRow dr in dt.Rows) {
                List<string> ss = new();
                for (var i = 0; i < dt.Columns.Count; i++)
                {
                    var obj = dr[dt.Columns[i].ColumnName];
                    {
                        var t = obj.GetType();
                        if (t == typeof(DateTime))
                            ss.Add($"\"{((DateTime)obj).ToShortDateString()}\"");
                        else if (double.TryParse(obj.ToString(), out _))
                            ss.Add(obj.ToString() ?? string.Empty);
                        else
                            ss.Add($"\"{(obj.ToString() ?? string.Empty).Replace("\"", "\"\"")}\"");
                    }
                }
                var s = string.Join<string>(",", ss.ToArray());
                ExportFileSw?.WriteLine(s);
            }
        }

        private static void WriteTheColumnLine(DataTable dt) {
            for (var i = 0; i < dt.Columns.Count; i++) {
                var c = dt.Columns[i];
                if (0 == i)
                    ExportFileSw?.Write("\"" + c.ColumnName + "\"");
                else
                    ExportFileSw?.Write(",\"" + c.ColumnName + "\"");
            }
            ExportFileSw?.WriteLine();
        }

        /// <summary>
        /// For security issue check
        /// </summary>
        /// <param name="sql">sql want to be scanned</param>
        private static void CodeScan(string sql) {
            #region security check

            var tmp = sql.ToUpper();
            if (tmp.Contains("INSERT") || tmp.Contains("DELETE") || tmp.Contains("UPDATE") || tmp.Contains("DROP") || tmp.Contains("CREATE"))
                throw new ArgumentException("You are using export argument to query result. This sql can't have any other kind statement beside SELECT\r\n. INSERT, UPDATE, DROP, CREATE or DELETE are all Inappropriated.");
            if (!tmp.StartsWith("SELECT"))
                throw new ArgumentException("You have start with \"SELECT\" when using the export feature.");

            #endregion security check
        }

        private static void PrepareLogAndExportFile(Option option) {
            if (!string.IsNullOrEmpty(option.LogPath)) {
                var logFile = new FileInfo(option.LogPath);
                if (logFile.Exists) logFile.Delete();
                Sw = logFile.CreateText();
            }

            if (!string.IsNullOrEmpty(option.ExportPath)) {
                var exportFile = new FileInfo(option.ExportPath);
                if (exportFile.Exists) exportFile.Delete();
                ExportFileSw = new StreamWriter(exportFile.Create(), Encoding.Default);
            }
        }

        private static StreamReader ReadSql(FileInfo sqlFile) {
            Console.WriteLine($"Processing object for {sqlFile.FullName} ......");
            return sqlFile.OpenText();
        }

        private static void LogMessage(string message) {
            Sw?.WriteLine(message);
            Console.Error.WriteLine(message);
        }

        private static void ShowHelp () {
            Console.WriteLine ("**oSQL.exe**");
            Console.WriteLine (" License: Apache 2.0");
            Console.WriteLine (" Author: Tom Tang <tomtang0406@gmail.com>");
            Console.WriteLine (" Runtime: dotnet 6.0");
            Console.WriteLine (" Version: 2.0.0.3");
            Console.WriteLine ("==========================================");
            Console.WriteLine ("Usage:");
            Console.WriteLine ("oSQL.exe -s [Server IP] [-is:use integrated security| -u <account> -p <password>] -o [log file path] [-i <sql script file path> | -dir <folder path contains sql files>] [-renew: drop destination database and re-create] -d [destination database] -e [export file path]");
            Console.WriteLine ("Sample:");
            Console.WriteLine ("OSQL.exe -s .\\SQLEXPRESS -u sa -p p@ssw0rd  -o .\\CPBU_SQLDEPLOY.LOG -i \"database\\10_tables\\00.table_create.sql\" -d SampleDB");
            Console.WriteLine ("OSQL.exe -s .\\SQLEXPRESS -is  -o .\\log.log -i \"database\\10_tables\\00.table_create.sql\" -d SampleDB");
            Console.WriteLine ("OSQL.exe -s .\\SQLEXPRESS -is  -o .\\log.log -dir \"database\" -renew -d SampleDB");
        }
    }
}