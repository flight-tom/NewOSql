namespace oSQL
{
    internal struct Option
    {
        public string ServerIp { get; private set; }
        public string DbAccount { get; private set; }
        public string DbPassword { get; private set; }
        public string LogPath { get; private set; }
        public string SqlPath { get; private set; }
        public string DestDatabase { get; private set; }
        public string ExportPath { get; private set; }

        public void Setup(string[] args)
        {
            if (args.Length > 0)
                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    if (arg.StartsWith("-"))
                        switch (arg.ToLower())
                        {
                            case "-s":
                                ServerIp = args[i + 1];
                                break;
                            case "-u":
                                DbAccount = args[i + 1];
                                break;
                            case "-p":
                                DbPassword = args[i + 1];
                                break;
                            case "-o":
                                LogPath = args[i + 1];
                                break;
                            case "-i":
                                SqlPath = args[i + 1];
                                break;
                            case "-d":
                                DestDatabase = args[i + 1];
                                break;
                            case "-e":
                                ExportPath = args[i + 1];
                                break;
                        }
                }
            else
            {

            }
        }
    }
}
