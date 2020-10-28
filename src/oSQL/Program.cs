using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Threading;

namespace oSQL
{
    class Program
    {
        static void Main(string[] args)
        {
            string server_ip = null, db_account = null, db_password = null, log_path = null, sql_path = null, dest_database = null, export_path = null;
            bool is_export = false;
            if (args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    if (arg.StartsWith("-"))
                    {
                        switch (arg.ToLower())
                        {
                            case "-s":
                                server_ip = args[i + 1];
                                break;
                            case "-u":
                                db_account = args[i + 1];
                                break;
                            case "-p":
                                db_password = args[i + 1];
                                break;
                            case "-o":
                                log_path = args[i + 1];
                                break;
                            case "-i":
                                sql_path = args[i + 1];
                                break;
                            case "-d":
                                dest_database = args[i + 1];
                                break;
                            case "-e":
                                is_export = true;
                                export_path = args[i + 1];
                                break;
                        }
                    }
                    else
                        continue;
                }
                StreamWriter sw = null;
                if (!string.IsNullOrEmpty(log_path))
                {
                    var logFile = new FileInfo(log_path);
                    if (logFile.Exists) logFile.Delete();
                    sw = logFile.CreateText();
                }
                FileInfo exportFile = null;
                if (!string.IsNullOrEmpty(export_path))
                {
                    exportFile = new FileInfo(export_path);
                    if (exportFile.Exists) exportFile.Delete();
                }

                if (string.IsNullOrEmpty(sql_path)) return;

                //DateTime now = DateTime.Now;
                //Console.WriteLine(string.Format("[{0}] - Current Directory : {1}", now, Environment.CurrentDirectory));
                //Console.WriteLine(string.Format("[{0}] - SQL CENTRAL Script Directory : ", now));
                //Console.WriteLine(string.Format("[{0}] - Source Database Script Folder : {1}", now, sql_path));
                //Console.WriteLine(string.Format("[{0}] - Target Database IP or FQDN : {1}", now, server_ip));
                //Console.WriteLine(string.Format("[{0}] - Database Access Username : {1}", now, db_account));
                //Console.WriteLine(string.Format("[{0}] - Database Access Password : {1}", now, db_password));
                //Console.WriteLine(string.Format("[{0}] - Database Name : {1}", now, dest_database));
                //Console.WriteLine(string.Format("[{0}] - Deployment Output File Name : {1}", now, log_path));
                //Console.WriteLine(string.Format("[{0}] - SQL Connection Command : OSQL.EXE -S {1} -U {2} -P {3}  -o {4} -i [SQL Input File Name]", now, server_ip, db_account, db_password, log_path));
                string sql_connection_string = string.Format("Data Source={0};Initial Catalog={1};Persist Security Info=True;User ID={2};Password={3}",
                    server_ip, dest_database, db_account, db_password);
                string sql_script_content = null;
                LogMessage(ref sw, string.Format("Processing object for {0} ......", sql_path));
                using (var sr = new StreamReader(sql_path, true))
                {
                    sql_script_content = sr.ReadToEnd();
                    sr.Close();
                }
                // normalize content
                sql_script_content = sql_script_content.Replace("\t", " ");
                sql_script_content = sql_script_content.Replace("GO\r\n", "\t").Replace("go\r\n", "\t");
                if (sql_script_content.EndsWith("GO"))
                    sql_script_content = sql_script_content.Substring(0, sql_script_content.Length - "GO".Length);

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
                                    if (!string.IsNullOrEmpty(sql))
                                        using (var cmd = conn.CreateCommand())
                                        {
                                            cmd.CommandType = CommandType.Text;
                                            cmd.CommandText = sql;
                                            cmd.CommandTimeout = 0;
                                            if (!is_export)
                                            {
                                                cmd.ExecuteNonQuery();
                                            }
                                            else
                                            {
                                                #region security check
                                                var tmp = sql.ToUpper();
                                                if (tmp.Contains("INSERT") || tmp.Contains("DELETE") || tmp.Contains("UPDATE") || tmp.Contains("DROP") || tmp.Contains("CREATE"))
                                                    throw new ArgumentException("You are using export argument to query result. This sql can't have any other kind statement beside SELECT\r\n. INSERT, UPDATE, DROP, CREATE or DELETE are all Inappropriated.");
                                                if (!tmp.StartsWith("SELECT"))
                                                    throw new ArgumentException("You have start with \"SELECT\" when using the export feature.");
                                                #endregion

                                                DataTable dt = new DataTable();
                                                using (var da = new SqlDataAdapter())
                                                {
                                                    da.SelectCommand = cmd;
                                                    da.Fill(dt);
                                                }

                                                if (null != exportFile)
                                                    using (var export_sw = new StreamWriter(exportFile.Create(), Encoding.Default))
                                                    {
                                                        for (int i = 0; i < dt.Columns.Count; i++)
                                                        {
                                                            DataColumn c = dt.Columns[i];
                                                            if (0 == i)
                                                                export_sw.Write("\"" + c.ColumnName + "\"");
                                                            else
                                                                export_sw.Write(",\"" + c.ColumnName + "\"");
                                                        }
                                                        export_sw.WriteLine();

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
                                                            export_sw.WriteLine(s);
                                                        }
                                                        export_sw.Close();
                                                    }
                                                else
                                                    throw new ArgumentNullException("You didn't specify a file path for exporting!", "exportFile");
                                            }
                                        }
                                }
                                catch (Exception ex)
                                {
                                    LogMessage(ref sw, "ERROR : " + sql_path + " : " + ex.Message);
                                    has_error = true;
                                }
                            conn.Close();
                        }
                        break;
                    }
                    catch (SqlException sqlEx)
                    {
                        LogMessage(ref sw, "ERROR : " + sqlEx.Message);
                        if (encounter_error < 3)
                        {
                            LogMessage(ref sw, "Encounter SQL error, wait 5 seconds and retry....");
                            encounter_error++;
                            Thread.Sleep(5 * 1000);
                        }
                        else
                            throw;
                    }
                }
                if (null != sw) sw.Close();
                //if (has_error)
                //    throw new ApplicationException("Some scripts were running with error, please check out!");
            }
            else
                ShowHelp();
        }

        private static void LogMessage(ref StreamWriter sw, string message)
        {
            if (null != sw) sw.WriteLine(message);
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
