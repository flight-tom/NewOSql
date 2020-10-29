using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;

namespace oSQL
{
    class Program
    {
        public static StreamWriter Sw { get; set; }
        public static StreamWriter ExportFileSw { get; set; }

        [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        static void Main(string[] args)
        {
            Option option = new Option();
            option.Setup(args);
            if (args.Length > 0)
            {
                if (string.IsNullOrEmpty(option.SqlPath)) return;

                PrepareLogAndExportFile(option);

                var sql_connection_string = string.Format("Data Source={0};Initial Catalog={1};Persist Security Info=True;User ID={2};Password={3}",
                    option.ServerIp, option.DestDatabase, option.DbAccount, option.DbPassword);

                var sql_script_content = ReadSql(option);

                bool has_error = false;
                while (true)
                {
                    int encounter_error = 0;
                    try
                    {
                        using (var conn = new SqlConnection(sql_connection_string))
                        {
                            conn.Open();
                            foreach (var sql in sql_script_content.Split('\t'))
                                try
                                {
                                    if (string.IsNullOrEmpty(sql)) continue;

                                    using (var cmd = conn.CreateCommand())
                                    {
                                        cmd.CommandType = CommandType.Text;
                                        cmd.CommandText = sql;
                                        cmd.CommandTimeout = 0;
                                        if (!string.IsNullOrEmpty(option.ExportPath))
                                        {
                                            cmd.ExecuteNonQuery();
                                        }
                                        else
                                        {
                                            if (null == ExportFileSw)
                                                throw new ArgumentNullException("You didn't specify a file path for exporting!", "exportFile");

                                            CodeScan(sql);

                                            OutputResultToExportFile(cmd);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogMessage("ERROR : " + option.SqlPath + " : " + ex.Message);
                                    has_error = true;
                                }
                            conn.Close();
                        }
                        break;
                    }
                    catch (SqlException sqlEx)
                    {
                        LogMessage("ERROR : " + sqlEx.Message);
                        if (encounter_error < 3)
                        {
                            LogMessage("Encounter SQL error, wait 5 seconds and retry....");
                            encounter_error++;
                            Thread.Sleep(5 * 1000);
                        }
                        else
                            throw;
                    }
                }
                if (null != Sw) Sw.Close(); Sw.Dispose();
                if (null != ExportFileSw) ExportFileSw.Close(); ExportFileSw.Dispose();
                //if (has_error)
                //    throw new ApplicationException("Some scripts were running with error, please check out!");
            }
            else
                ShowHelp();
        }

        private static void OutputResultToExportFile(SqlCommand cmd)
        {
            DataTable dt = new DataTable();
            using (var da = new SqlDataAdapter())
            {
                da.SelectCommand = cmd;
                da.Fill(dt);
            }

            WriteTheColumnLine(dt);
            WriteResultToFile(dt);
        }

        private static void WriteResultToFile(DataTable dt)
        {
            foreach (DataRow dr in dt.Rows)
            {
                List<string> ss = new List<string>();
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    var obj = dr[dt.Columns[i].ColumnName];
                    var t = obj.GetType();
                    if (t == typeof(DateTime))
                        ss.Add(string.Format("\"{0}\"", ((DateTime)obj).ToShortDateString()));
                    else if (double.TryParse(obj.ToString(), out _))
                        ss.Add(obj.ToString());
                    else
                        ss.Add(string.Format("\"{0}\"", obj.ToString().Replace("\"", "\"\"")));
                }
                var s = string.Join<string>(",", ss.ToArray());
                ExportFileSw.WriteLine(s);
            }
        }

        private static void WriteTheColumnLine(DataTable dt)
        {
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                DataColumn c = dt.Columns[i];
                if (0 == i)
                    ExportFileSw.Write("\"" + c.ColumnName + "\"");
                else
                    ExportFileSw.Write(",\"" + c.ColumnName + "\"");
            }
            ExportFileSw.WriteLine();
        }

        /// <summary>
        /// For security issue check
        /// </summary>
        /// <param name="sql">sql want to be scanned</param>
        private static void CodeScan(string sql)
        {
            #region security check
            var tmp = sql.ToUpper();
            if (tmp.Contains("INSERT") || tmp.Contains("DELETE") || tmp.Contains("UPDATE") || tmp.Contains("DROP") || tmp.Contains("CREATE"))
                throw new ArgumentException("You are using export argument to query result. This sql can't have any other kind statement beside SELECT\r\n. INSERT, UPDATE, DROP, CREATE or DELETE are all Inappropriated.");
            if (!tmp.StartsWith("SELECT"))
                throw new ArgumentException("You have start with \"SELECT\" when using the export feature.");
            #endregion
        }

        private static void PrepareLogAndExportFile(Option option)
        {
            if (!string.IsNullOrEmpty(option.LogPath))
            {
                var logFile = new FileInfo(option.LogPath);
                if (logFile.Exists) logFile.Delete();
                Sw = logFile.CreateText();
            }

            if (!string.IsNullOrEmpty(option.ExportPath))
            {
                var exportFile = new FileInfo(option.ExportPath);
                if (exportFile.Exists) exportFile.Delete();
                ExportFileSw = new StreamWriter(exportFile.Create(), Encoding.Default);
            }
        }

        private static string ReadSql(Option option)
        {
            string sql_script_content = null;
            LogMessage(string.Format("Processing object for {0} ......", option.SqlPath));
            using (var sr = new StreamReader(option.SqlPath, true))
            {
                sql_script_content = sr.ReadToEnd();
                sr.Close();
            }
            // normalize content
            sql_script_content = sql_script_content.Replace("\t", " ");
            sql_script_content = sql_script_content.Replace("GO\r\n", "\t").Replace("go\r\n", "\t");
            if (sql_script_content.EndsWith("GO"))
                sql_script_content = sql_script_content.Substring(0, sql_script_content.Length - "GO".Length);
            return sql_script_content;
        }

        private static void LogMessage(string message)
        {
            if (null != Sw) Sw.WriteLine(message);
            Console.Error.WriteLine(message);
        }

        private static void ShowHelp()
        {
            Console.WriteLine("**oSQL.exe**");
            Console.WriteLine(" License: Apache 2.0");
            Console.WriteLine(" Author: Tom Tang <tomtang0406@gmail.com>");
            Console.WriteLine("==========================================");
            Console.WriteLine("Usage:");
            Console.WriteLine("oSQL.exe -S [Server IP] -U [db account] -P [db password] -o [log file path] -i [sql script file path] -d [destination database] -e [export file path]");
            Console.WriteLine("Sample:");
            Console.WriteLine("OSQL.EXE -S ./SQLEXPRESS -U sa -P p@ssw0rd  -o .\\CPBU_SQLDEPLOY.LOG -i \"database\\10_tables\\00.table_create.sql\" -d SampleDB");
        }
    }
}
