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
        DnsClient serverOfGoogle;

        private static readonly List<IPAddress> serverOfCCSUIP;
        private static readonly List<IPAddress> serverOfGoogleIP;

        private static readonly string HostListPath = @"C:\windows\system32\drivers\etc\e-hosts";

        static DNSServer()
        {
            serverOfCCSUIP = new List<IPAddress>();
            serverOfCCSUIP.Add(IPAddress.Parse("218.196.40.8"));
            serverOfCCSUIP.Add(IPAddress.Parse("218.196.40.18"));
            serverOfCCSUIP.Add(IPAddress.Parse("218.196.40.9"));

            serverOfGoogleIP = new List<IPAddress>();
            serverOfGoogleIP.Add(IPAddress.Parse("8.8.8.8"));
            serverOfGoogleIP.Add(IPAddress.Parse("8.8.4.4"));
        }

        private readonly System.Collections.Generic.Dictionary<string, string> ListOfHosts;
        private readonly System.Collections.Generic.Dictionary<string, string> ListOfHostsEx;

        public DNSServer()
        {
            InitializeComponent();

            try
            {
                server = new DnsServer(IPAddress.Any, 10, 10, new DnsServer.ProcessQuery(ProcessQuery));

                serverOfCCSU = new DnsClient(serverOfCCSUIP, 1);

                serverOfGoogle = new DnsClient(serverOfGoogleIP, 5);

                //读取host-list
                ListOfHosts = new Dictionary<string, string>();
                ListOfHostsEx = new Dictionary<string, string>();
                parseEHostFile();
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
                EventLog.WriteEntry("DNS", "不存在" + HostListPath);
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
                var parts = r.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Count() > 3 || parts.Count() < 2)
                {
                    //假设本行有问题
                    EventLog.WriteEntry("DNS", "无法解析：" + m);
                    continue;
                }
                if (parts.Count() == 3)
                {
                    //扩展正则表达式
                    if (parts[0] != "*")
                    {
                        EventLog.WriteEntry("DNS", "无法解析：" + m);
                        continue;
                    }
                    addToDirectoryIfNotContains(ListOfHostsEx,parts[2],parts[1]);

                }
                else
                {
                    addToDirectoryIfNotContains(ListOfHostsEx, parts[1], parts[0]);
                }
            }
        }

        private static void addToDirectoryIfNotContains(System.Collections.Generic.Dictionary<string, string> target, string key, string value)
        {
            if (!target.ContainsKey(key))
            {
                target.Add(key, value);
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
                    query.AnswerRecords.AddRange(queryRemoteDns(query.Questions[0].Name, serverOfGoogle));
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
