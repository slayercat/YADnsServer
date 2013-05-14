using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace YADnsServer
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        static void Main(string[] args)
        {
            if (args.Count() != 0 && args[0] == "/debug")
            {
                var m = new DNSServer();
                m.testOnly_DoStart(args);
                return;
            }

            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] 
            { 
                new DNSServer() 
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
