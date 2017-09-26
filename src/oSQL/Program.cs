using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace oSQL
{
    class Program
    {
        static void Main(string[] args)
        {
            ShowHelp();
            string server_ip = null, db_account = null, db_password = null, log_path = null, sql_path = null, dest_database = null;
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
                        }
                    }
                    else
                        continue;
                }
                DateTime now = DateTime.Now;
                Console.WriteLine(string.Format("[{0}] - Current Directory : {1}", now, Environment.CurrentDirectory));
                Console.WriteLine(string.Format("[{0}] - SQL CENTRAL Script Directory : ", now));
                Console.WriteLine(string.Format("[{0}] - Source Database Script Folder : {1}", now, sql_path));
                Console.WriteLine(string.Format("[{0}] - Target Database IP or FQDN : {1}", now, server_ip));
                Console.WriteLine(string.Format("[{0}] - Database Access Username : {1}", now, db_account));
                Console.WriteLine(string.Format("[{0}] - Database Access Password : {1}", now, db_password));
                Console.WriteLine(string.Format("[{0}] - Database Name : {1}", now, dest_database));
                Console.WriteLine(string.Format("[{0}] - Deployment Output File Name : {1}", now, log_path));
                Console.WriteLine(string.Format("[{0}] - SQL Connection Command : OSQL.EXE -S {1} -U {2} -P {3}  -o {4} -i [SQL Input File Name]", now, server_ip, db_account, db_password, log_path));
                string sql_connection_string = string.Format("Data Source={0};Initial Catalog={1};Persist Security Info=True;User ID={2};Password={3}",
                    server_ip, dest_database, db_account, db_password);
                string sql_script_content = null;
                Console.WriteLine(string.Format("Processing object for {0} ......", sql_path));
                using (var sr = new StreamReader(sql_path, true))
                {
                    sql_script_content = sr.ReadToEnd();
                    sr.Close();
                }
                // normalize content
                sql_script_content = sql_script_content.Replace("\t", " ");
                sql_script_content = sql_script_content.Replace("GO\r\n", "\t").Replace("go\r\n", "\t");
                bool has_error = false;
                using (var sw = new StreamWriter(log_path))
                {
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
                                        sw.WriteLine(sql);
                                        using (var cmd = conn.CreateCommand())
                                        {
                                            cmd.CommandText = sql;
                                            cmd.CommandType = CommandType.Text;
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.Error.WriteLine(ex.Message);
                                        has_error = true;
                                    }
                                conn.Close();
                            }
                            break;
                        }
                        catch (SqlException sqlEx)
                        {
                            Console.Error.WriteLine(sqlEx.Message);
                            if (encounter_error < 3)
                            {
                                Console.WriteLine("Encounter SQL error, wait 5 seconds and retry....");
                                encounter_error++;
                                Thread.Sleep(5 * 1000);
                            }
                            else
                                throw;
                        }
                    }
                }
                //if (has_error)
                //    throw new ApplicationException("Some scripts were running with error, please check out!");
            }
            else
                ShowHelp();
        }

        private static void ShowHelp()
        {
            Console.WriteLine("**oSQL.exe**");
            Console.WriteLine(" License: Apache 2.0");
            Console.WriteLine(" Author: Tom Tang <tomtang0406@gmail.com>");
            Console.WriteLine("==========================================");
            Console.WriteLine("Usage:");
            Console.WriteLine("oSQL.exe -S [Server IP] -U [db account] -P [db password] -o [log file path] -i [sql script file path] -d [destination database]");
            Console.WriteLine("Sample:");
            Console.WriteLine("OSQL.EXE -S 127.0.0.1 -U sa -P p@ssw0rd  -o .\\CPBU_SQLDEPLOY.LOG -i \"database\\10_tables\\00.table_create.sql\" -d SampleDB");
        }
    }
}
