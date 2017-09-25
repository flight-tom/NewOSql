using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;

namespace oSQL
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
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
                    string sql_connection_string = string.Format("Data Source={0};Initial Catalog={1};Persist Security Info=True;User ID={2};Password={3}",
                        server_ip, dest_database, db_account, db_password);
                    string sql_script_content = null;
                    using (var sr = new StreamReader(sql_path))
                    {
                        sql_script_content = sr.ReadToEnd();
                        sr.Close();
                    }
                    // normalize content
                    sql_script_content = sql_script_content.Replace("\t", " ").ToLower();
                    sql_script_content = sql_script_content.Replace("go", "\t");
                    bool has_error = false;
                    using (var conn = new SqlConnection(sql_connection_string))
                    {
                        conn.Open();
                        foreach (var sql in sql_script_content.Split('\t'))
                            try
                            {
                                using (var cmd = conn.CreateCommand())
                                {
                                    cmd.CommandText = sql;
                                    cmd.CommandType = CommandType.Text;
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine(ex);
                                has_error = true;
                            } 
                        conn.Close();
                    }
                    if (has_error)
                        throw new ApplicationException("Some scripts were running with error, please check out!");
                }
                else
                    ShowHelp();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                throw;
            }
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
