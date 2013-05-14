using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Net;
using ARSoft.Tools.Net.Dns;

namespace YADnsServer
{
    public partial class DNSServer : ServiceBase
    {
        DnsServer server;
        DnsClient serverOfCCSU;
        DnsClient serverOfGlobal;

        private static readonly List<IPAddress> serverOfCCSUIP;

        private static readonly string HostListPath;

        static DNSServer()
        {
            HostListPath = YADnsServer.Properties.Settings.Default["HostListPath"] as string;

            serverOfCCSUIP = new List<IPAddress>();
            serverOfCCSUIP.Add(IPAddress.Parse("218.196.40.8"));
            serverOfCCSUIP.Add(IPAddress.Parse("218.196.40.18"));
            serverOfCCSUIP.Add(IPAddress.Parse("218.196.40.9"));

        }

        private readonly System.Collections.Generic.Dictionary<string, string> ListOfHosts;
        private readonly System.Collections.Generic.Dictionary<string, string> ListOfHostsEx;
        private readonly System.Collections.Generic.List<IPAddress> GlobalResolveList;

        public DNSServer()
        {
            InitializeComponent();

            try
            {
                server = new DnsServer(IPAddress.Any, 10, 10, new DnsServer.ProcessQuery(ProcessQuery));

                serverOfCCSU = new DnsClient(serverOfCCSUIP, 1);


                //读取host-list
                ListOfHosts = new Dictionary<string, string>();
                ListOfHostsEx = new Dictionary<string, string>();
                GlobalResolveList = new List<IPAddress>();
                parseEHostFile();
                if (GlobalResolveList.Count() == 0)
                {
                    EventLog.WriteEntry("DNS", "GlobalResolveList is null. means upload resolve is disabled in this case", EventLogEntryType.Warning);
                }
                serverOfGlobal = new DnsClient(GlobalResolveList, 5);
            }
            catch (Exception e)
            {
                EventLog.WriteEntry("DNS", e.ToString());
                System.Environment.Exit(0);
            }

        }


        private void parseEHostFile()
        {
            if (!System.IO.File.Exists(HostListPath))
            {
                EventLog.WriteEntry("DNS", "NonExistedConfigFile:" + HostListPath);
                return;
            }
            // 效率较低的一个做法
            foreach (var m in System.IO.File.ReadAllLines(HostListPath))
            {
                //对于每一行
                //干掉开头结尾的空格
                string r = m.Trim();
                //判断有没有#
                int whereofsharp = r.IndexOf('#');
                if (whereofsharp != -1)
                {
                    r = r.Remove(whereofsharp);
                }
                r = r.Trim();
                if (string.IsNullOrWhiteSpace(r))
                    continue;
                var parts = r.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                
                //扩展正则表达式
                switch (parts[0])
                {
                    case "+":
                        if (parts.Count() == 3)
                        {
                            // 泛域名解析
                            addToDirectoryIfNotContains(ListOfHostsEx, parts[2], parts[1]);
                        }
                        else
                        {
                            EventLog.WriteEntry("DNS", "improper Line begin with +, Can't Parse：" + m);
                        }
                        break;

                    case "*":
                        if (parts.Count() == 2)
                        {
                            // 全局解析
                            addToListOfGlobalResolve(GlobalResolveList, parts[1]);
                        }
                        else
                        {
                            EventLog.WriteEntry("DNS", "improper Line begin with *, Can't Parse：" + m);
                        }
                        break;

                    default:
                        if (parts.Count() == 2)
                        {
                            // 兼容hosts文件
                            addToDirectoryIfNotContains(ListOfHostsEx, parts[1], parts[0]);
                        }
                        else
                        {
                            EventLog.WriteEntry("DNS", "Can't Parse：" + m);
                        }
                        continue;
                }                  
            }
        }

        private void addToListOfGlobalResolve(List<IPAddress> GlobalResolveList, string address)
        {
            GlobalResolveList.Add(IPAddress.Parse(address));
        }

        private static void addToDirectoryIfNotContains(System.Collections.Generic.Dictionary<string, string> target, string key, string value)
        {
            if (!target.ContainsKey(key))
            {
                target.Add(key, value);
            }
            else
            {
                EventLog.WriteEntry("DNS", "warning duplicate line for key：" + key);
            }
        }



        protected override void OnStart(string[] args)
        {
            server.Start();
        }

        public void testOnly_DoStart(string[] args)
        {
            OnStart(args);
            while (true)
                System.Threading.Thread.Sleep(10000);
        }


        DnsMessageBase ProcessQuery(DnsMessageBase message, IPAddress clientAddress, System.Net.Sockets.ProtocolType protocol)
        {
            message.IsQuery = false;
            DnsMessage query = message as DnsMessage;
            //处理在hosts里头的东西

            DnsRecordBase ld;
            if (!queryInHosts(query.Questions[0].Name, out ld))
            {
                if (query.Questions[0].Name.EndsWith("ccsu.cn") || query.Questions[0].Name.EndsWith("ccsu.cn."))
                {
                    //处理长沙学院的东西
                    query.AnswerRecords.AddRange(queryRemoteDns(query.Questions[0].Name, serverOfCCSU));
                }
                else
                {
                    query.AnswerRecords.AddRange(queryRemoteDns(query.Questions[0].Name, serverOfGlobal));
                }
            }
            else
            {
                query.AnswerRecords.Add(ld);
            }

            return message;
        }

        private bool queryInHosts(string name, out DnsRecordBase ld)
        {
            string nohisname;
            if (name.EndsWith("."))
            {
                nohisname = name.Remove(name.Length - 1);
            }
            else
            {
                nohisname = name;
            }
            // 基本方法
            if (ListOfHosts.ContainsKey(nohisname))
            {
                ld = new ARecord(name, 3600, IPAddress.Parse(ListOfHosts[nohisname]));
                return true;
            }
            // 允许扩展的做法，允许使用正则表达式
            foreach (var m in ListOfHostsEx)
            {
                if (System.Text.RegularExpressions.Regex.Match(nohisname, m.Key, System.Text.RegularExpressions.RegexOptions.Singleline).Length == nohisname.Length)
                {
                    ld = new ARecord(name, 3600, IPAddress.Parse(m.Value));
                    return true;
                }
            }

            ld = null;
            return false;
        }

        private static List<DnsRecordBase> queryRemoteDns(string domain, DnsClient dnsServer)
        {

            DnsMessage dnsMessage = dnsServer.Resolve(domain, RecordType.A);
            return dnsMessage.AnswerRecords;

        }

        protected override void OnStop()
        {
            server.Stop();
        }


    }

}
