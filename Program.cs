using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NNanomsg.Protocols;
using System.Diagnostics.Metrics;
using System.Text;

namespace MiniMcs_ks
{

    internal class Program
    {
        private static ReplySocket nano_s = new ReplySocket();
        private static List<StationMd> stations = new List<StationMd>();
        private static readonly LoggerHelp _logger = LoggerHelp.GetLogger();
        //tcp://127.0.0.1:5555
        private static string url = "tcp://*:8025";
        private static int count = 0;
        static void Main(string[] args)
        {
            //任务开始
            _logger.LogInfo("任务开始");

            stations.Add(new StationMd { Station = "RSTB-05", Empty = true });
            stations.Add(new StationMd { Station = "RSTB-08", Empty = true });
            stations.Add(new StationMd { Station = "RSTB-10", Empty = false });
            stations.Add(new StationMd { Station = "RSTB-13", Empty = true });

            Task[] tasks = new Task[1];
            tasks[0] = Task.Run(async () =>
            {
                await oso_main_message_loopAsync();
            });

            SendCmdToOso("");
            Task.WaitAll(tasks);
        }
        private static object obj = new object();
        private static async Task oso_main_message_loopAsync()
        {
            nano_s.Options.SendTimeout = new TimeSpan(0, 0, 5);
            nano_s.Options.ReceiveTimeout = new TimeSpan(0, 0, 5);
            nano_s.Bind(url);
            await Task.Run(() =>
             {
                 while (true)
                 {
                     try
                     {
                         var receive = nano_s.Receive();
                         if (receive != null)
                         {
                             nano_s.Send(Encoding.UTF8.GetBytes(""));
                             string oso_msg = Encoding.UTF8.GetString(receive);
                             dynamic json_msg = JObject.Parse(oso_msg);
                             lock (obj)
                             {
                                 Handle(json_msg);
                             }
                         }
                     }
                     catch (Exception ae)
                     {
                         _logger.LogError(ae.Message, ae);
                     }
                 }
             });
        }
        //移动任务 -》搬运任务-》移动任务-》搬运任务
        static void Handle(dynamic msg)
        {
            string opname = msg.opname;

            string oht_id = msg.oht_id;
            string cmd_id = msg.cmd_id;
            switch (opname)
            {
                case "OHT_ACQ_DONE"://抓取完成
                    {
                        string stationName = msg.port;
                        var _station = stations.Find(x => x.Station == stationName);
                        if (_station != null)
                        {
                            _station.Empty = true;
                            _logger.LogInfo($"站点：{_station.Station} 取货完成！，cmd:{cmd_id}");
                        }
                    }
                    break;
                case "OHT_DEP_DONE":
                    {
                        string stationName = msg.port;
                        var _station = stations.Find(x => x.Station == stationName);
                        if (_station != null)
                        {
                            _station.Empty = false;
                            _logger.LogInfo($"站点：{_station.Station} 卸货完成！，cmd:{cmd_id}");
                        }
                    }
                    //发送移动任务指定天车

                    break;
                case "OHT_MOVE_DONE":
                    //----------------------------------------开始新的搬运任务指定天车
                    _logger.LogInfo($"移动任务执行完成，cmd:{cmd_id}");
                    SendCmdToOso(oht_id);
                    // SendMoveCmdToOso(oht_id);
                    break;
                case "OHT_CMD_DONE":
                    //----------------------------------------开始新的移动任务指定天车
                    _logger.LogInfo($"搬运任务执行完成，cmd:{cmd_id}");
                    SendMoveCmdToOso(oht_id);
                    break;
            }
        }

        static async void SendMoveCmdToOso(string oht_id)
        {
            await Task.Delay(1000);
            count++;
            string dest = "";
            if (count % 2 == 0)
            {
                dest = "75";
            }
            else
            {
                dest = "99";
            }
            var message = new
            {
                opname = "WEB_MOVE_OHT",
                opparas = new
                {
                    oht_id = oht_id,
                    dest = dest
                }
            };

            using (RequestSocket? s = new RequestSocket())
            {
                var url = "tcp://127.0.0.1:8024";
                s.Connect(url);
                var str = JsonConvert.SerializeObject(message);
                s.Send(Encoding.UTF8.GetBytes(str));
                var AA = s.Receive();
            }
            Console.WriteLine($"send move cmd to oso,OHT:{oht_id}");
        }

        static async void SendCmdToOso(string oht_id)
        {
            await Task.Delay(1000);
            lock (obj)
            {
                {
                    //选择有货的station
                    var notEmpty = stations.Where(x => x.Empty == false).OrderBy(x => x.count).FirstOrDefault();
                    //选择没有货的tation
                    var emptyStation = stations.Where(x => x.Empty == true).OrderBy(x => x.count).LastOrDefault();

                    //组成cmd 发给oso
                    var source = notEmpty?.Station;
                    var dest = emptyStation?.Station;
                    if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(dest))
                    {
                        if (!string.IsNullOrEmpty(oht_id))
                        {
                            SendMoveCmdToOso(oht_id);
                        }

                        Console.WriteLine($"No station selected");
                        return;
                    }
                    notEmpty.Empty = true;
                    var cmd_id = $"OHTC-TEST-" + source + "-" + dest + "-" + DateTime.Now.Ticks;
                    notEmpty.count = notEmpty.count + 1;
                    emptyStation.count = emptyStation.count + 1;

                    var message = new
                    {
                        opname = "WEB_TRANSFER",
                        opparas = new
                        {
                            query_id = "",//待解析TODO
                            priority = 1,
                            user = "OHTC_USER",//待解析TODO
                            transferinfos = new[] { new
                                                        {
                                                            cmd_id=cmd_id,
                                                            carrier_id="AF0001",
                                                            source=source,
                                                            dest=dest,
                                                        }}
                        }
                    };

                    using (RequestSocket? s = new RequestSocket())
                    {
                        var url = "tcp://127.0.0.1:8024";
                        s.Connect(url);
                        var str = JsonConvert.SerializeObject(message);
                        s.Send(Encoding.UTF8.GetBytes(str));
                        var AA = s.Receive();
                    }
                    Console.WriteLine($"send  cmd to oso,cmd_id:{cmd_id}");
                    _logger.LogInfo($"发送指令：{cmd_id} 到OSO");
                }
            }

        }

        class StationMd
        {
            public string Station { get; set; }
            public bool Empty { get; set; }
            public int count { get; set; }
        }
    }
}
