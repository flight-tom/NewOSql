using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace oSQL {

    internal class Program {
        public static StreamWriter? Sw { get; set; }
        public static StreamWriter? ExportFileSw { get; set; }

        private static void Main(string[] args) {
            var option = new Option();
            option.Setup(args);
            if (args.Length > 0) {
                if (string.IsNullOrEmpty(option.SqlPath)) return;

                PrepareLogAndExportFile(option);

                var sql_connection_string = PrepareConnectionString(option);
                if (option.RenewDB) DropAndCreateNewDB(sql_connection_string, option);
                if (!string.IsNullOrEmpty(option.SqlFolder)) {
                    var dir = new DirectoryInfo(option.SqlFolder);
                    if (dir.Exists)
                        RunAllSqlScripts(dir, option, sql_connection_string);
                } else {
                    var file = new FileInfo(option.SqlPath);
                    if (file.Exists) {
                        if (string.IsNullOrEmpty(option.ExportPath))
                            ExecuteSqlFile(file, sql_connection_string);
                        else
                            ExportData(file, sql_connection_string);
                    }
                }

                Dispose();
                //if (has_error)
                //    throw new ApplicationException("Some scripts were running with error, please check out!");
            } else
                ShowHelp();
        }

        private static void ExportData(FileInfo sqlFile, string sql_connection_string) {
            if (ExportFileSw is null)
                throw new ArgumentNullException("You didn't specify a file path for exporting!", "exportFile");
            var sql = ReadSql(sqlFile);
            CodeScan(sql);
            using var conn = new SqlConnection(sql_connection_string);
            var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = sql;
            cmd.CommandTimeout = 0;
            OutputResultToExportFile(cmd);
        }

        private static void Dispose() {
            if (Sw is not null) {
                Sw.Close();
                Sw.Dispose();
            }
            if (ExportFileSw is not null) {
                ExportFileSw.Close();
                ExportFileSw.Dispose();
            }
        }

        private static void ExecuteSqlFile(FileInfo sqlFile, string sql_connection_string) {
            var sql_script_content = ReadSql(sqlFile);

            bool has_error = false;
            while (true) {
                var encounter_error = 0;
                try {
                    using var conn = new SqlConnection(sql_connection_string);
                    conn.Open();
                    foreach (var sql in sql_script_content.Split('\t'))
                        try {
                            if (string.IsNullOrEmpty(sql)) continue;

                            using var cmd = conn.CreateCommand();
                            cmd.CommandType = CommandType.Text;
                            cmd.CommandText = sql;
                            cmd.CommandTimeout = 0;
                            cmd.ExecuteNonQuery();
                        } catch (Exception ex) {
                            LogMessage("ERROR : " + sqlFile.FullName + " : " + ex.Message);
                            has_error = true;
                        }
                    conn.Close();
                    break;
                } catch (SqlException sqlEx) {
                    LogMessage("ERROR : " + sqlEx.Message);
                    if (encounter_error < 3) {
                        LogMessage("Encounter SQL error, wait 5 seconds and retry....");
                        encounter_error++;
                        Thread.Sleep(5 * 1000);
                    } else
                        throw;
                }
            }
        }

        private static void RunAllSqlScripts(DirectoryInfo dir, Option option, string connectionString) {
            var sub_dirs = dir.GetDirectories();
            if (sub_dirs.Any())
                foreach (var sub in sub_dirs)
                    RunAllSqlScripts(sub, option, connectionString);

            var sql_files = dir.GetFiles("*.sql");
            foreach (var file in sql_files)
                ExecuteSqlFile(file, connectionString);
        }

        private static void DropAndCreateNewDB(string sql_connection_string, Option option) {
            ExecuteSql(sql_connection_string, $"DROP DATABASE [{option.DestDatabase}]");
            ExecuteSql(sql_connection_string, $"CREATE DATABASE [{option.DestDatabase}]");
        }

        private static void ExecuteSql(string sql_connection_string, string sql) {
            using var conn = new SqlConnection(sql_connection_string);
            using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = sql;
            cmd.CommandTimeout = 0;
            conn.Open();
            cmd.ExecuteNonQuery();
            conn.Close();
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
                for (var i = 0; i < dt.Columns.Count; i++) {
                    var obj = dr[dt.Columns[i].ColumnName];
                    if (obj is not null) {
                        var t = obj.GetType();
                        if (t == typeof(DateTime))
                            ss.Add(string.Format("\"{0}\"", ((DateTime)obj).ToShortDateString()));
                        else if (double.TryParse(obj.ToString(), out _))
                            ss.Add(obj.ToString() ?? string.Empty);
                        else
                            ss.Add(string.Format("\"{0}\"", (obj.ToString() ?? string.Empty).Replace("\"", "\"\"")));
                    }
                }
                var s = string.Join<string>(",", ss.ToArray());
                ExportFileSw?.WriteLine(s);
            }
        }

        private static void WriteTheColumnLine(DataTable dt) {
            for (var i = 0; i < dt.Columns.Count; i++) {
                DataColumn c = dt.Columns[i];
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

        private static string ReadSql(FileInfo sqlFile) {
            string? sql_script_content = null;
            LogMessage(string.Format("Processing object for {0} ......", sqlFile.FullName));
            using (var sr = sqlFile.OpenText()) {
                sql_script_content = sr.ReadToEnd();
                sr.Close();
            }
            // normalize content
            sql_script_content = sql_script_content.Replace("\t", " ").Replace("\r", string.Empty);
            sql_script_content = sql_script_content.Replace($"GO\n", "\t").Replace($"go\n", "\t");
            if (sql_script_content.EndsWith("GO"))
                sql_script_content = sql_script_content[..^"GO".Length];
            if (sql_script_content.EndsWith("go"))
                sql_script_content = sql_script_content[..^"go".Length];

            return sql_script_content.Trim();
        }

        private static void LogMessage(string message) {
            Sw?.WriteLine(message);
            Console.Error.WriteLine(message);
        }

        private static void ShowHelp() {
            Console.WriteLine("**oSQL.exe**");
            Console.WriteLine(" License: Apache 2.0");
            Console.WriteLine(" Author: Tom Tang <tomtang0406@gmail.com>");
            Console.WriteLine(" Runtime: dotnet standard 6.0");
            Console.WriteLine("==========================================");
            Console.WriteLine("Usage:");
            Console.WriteLine("oSQL.exe -s [Server IP] [-is:use integrated security| -u <account> -p <password>] -o [log file path] [-i <sql script file path> | -dir <folder path contains sql files>] [-renew: drop destination database and re-create] -d [destination database] -e [export file path]");
            Console.WriteLine("Sample:");
            Console.WriteLine("OSQL.exe -s ./SQLEXPRESS -U sa -P p@ssw0rd  -o .\\CPBU_SQLDEPLOY.LOG -i \"database\\10_tables\\00.table_create.sql\" -d SampleDB");
            Console.WriteLine("OSQL.exe -is  -o .\\log.log -i \"database\\10_tables\\00.table_create.sql\" -d SampleDB");
            Console.WriteLine("OSQL.exe -is  -o .\\log.log -dir \"database\" -renew -d SampleDB");
        }
    }
}