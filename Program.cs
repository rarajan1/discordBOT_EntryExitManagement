using Discord;
using Discord.WebSocket;
using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace discordBOT_EntryExitManagement
{

    class Program
    {
        public static Func<string, Task<string>> signalReceived;

        //ホストの起動をMain関数とする
        public static void Main(string[] args) => ConfigureHostBuider(args).Build().Run();

        //ホスト設定
        public static IHostBuilder ConfigureHostBuider(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .ConfigureLogging(b =>
            {
                b.AddConsole();
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<ClientReceiveServer>();
                services.AddHostedService<DiscordBotService>();
            });
            

        class DiscordBotService : BackgroundService
        {

            private DiscordSocketClient _client;
            private SocketGuild discordServer;
            private readonly IConfiguration _configuration;
            private string Token = " ";
            private int memberCount = 0;
            private int MemberCount{ get { return memberCount; } set { memberCount = value; if (memberCount < 0) memberCount = 0; } }
            private List<string> CurentMember = new List<string>();
            
            public DiscordBotService(IConfiguration configuration)
            {
                _configuration = configuration;
            }

            //初期設定
            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                _client = new DiscordSocketClient(new DiscordSocketConfig
                {
                    LogLevel = Discord.LogSeverity.Info,
                    AlwaysDownloadUsers = true
                });
                _client.Log += x =>
                {
                    Console.WriteLine($"{x.Message}, {x.Exception}");
                    return Task.CompletedTask;
                };
                
                //タブレットから信号を受けとった時のメソッドを登録
                signalReceived += this.SignalReceived;

                //discordにログイン
                await _client.LoginAsync(Discord.TokenType.Bot, Token);
                await _client.StartAsync();
                await _client.SetStatusAsync(UserStatus.DoNotDisturb);
                await _client.SetGameAsync($"0人が研究室", null, ActivityType.Competing);
            }

            public override async Task StopAsync(CancellationToken cancellationToken)
            {
                await _client.StopAsync();
            }

            private async Task<string> SignalReceived(string data)
            {
                if(data == "icon")
                {
                    //var g = _client.Guilds.GetEnumerator().Current as SocketGuild;
                    //var ds =g;
                    //Console.WriteLine(g);
                    //string sentData = "";
                    //using (var fstream = new FileStream("memberIdList.txt",FileMode.Open, FileAccess.Read))
                    //{
                    //    var reader = new StreamReader(fstream, Encoding.UTF8);
                    //
                    //    while(!reader.EndOfStream)
                    //    {
                    //        var line = reader.ReadLine();
                    //        var userData = await _client.GetUserAsync(ulong.Parse(line));
                    //        sentData += $"{userData.GetAvatarUrl()},";
                    //        sentData += $"{userData.Username},";
                    //    }
                    //}
                    //return $"{sentData}";
                    
                    string sentData = "";
                    int i=0;
                    using (var fstream = new FileStream("memberNameList.txt", FileMode.Open, FileAccess.Read))
                    {
                        var reader = new StreamReader(fstream, Encoding.UTF8);
                    
                        while(!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            sentData += $"{line},";
                            if(i%2 == 1)
                                sentData += $"{CurentMember.Contains(line)},";
                            i++;
                        }
                    }
                    sentData = sentData.TrimEnd(',');
                    return sentData;
                }else
                {
                    var signals = data.Split(',');
                    int oldMemberCount = MemberCount;
                    using (StreamWriter sw = new StreamWriter("Log.txt", true, Encoding.GetEncoding("UTF-8")))
                    {
                        for (int i = 0; i < signals.Length / 3; i++)
                        {
                            
                            if (signals[i * 3 + 2] == "IN")
                            {
                                MemberCount++;
                                CurentMember.Add(signals[i * 3 + 1]);
                            }
                            if (signals[i * 3 + 2] == "OUT")
                            {
                                MemberCount--;
                                CurentMember.Remove(signals[i * 3 + 1]);
                            }
                            sw.Write($"{signals[i * 3]},{signals[i * 3 + 1]},{signals[i * 3 + 2]}");
                            Console.WriteLine($"{ signals[i * 3]} {signals[i * 3 + 1]} {signals[i * 3 + 2]}");
                        }   
                    }

                    await _client.SetGameAsync($"{memberCount}人が研究室", null, ActivityType.Competing);
                        
                    var chatchannel = _client.GetChannel(978806146452303896) as SocketTextChannel;
                    if (oldMemberCount == 0 && MemberCount >= 1)
                    {
                        await _client.SetStatusAsync(UserStatus.Online);
                        await chatchannel.SendMessageAsync($"{DateTime.Now.ToString("HH時mm分")} OPEN!!");
                    }
                    if (oldMemberCount >= 1  && MemberCount == 0)
                    {
                        await _client.SetStatusAsync(UserStatus.DoNotDisturb);
                        await chatchannel.SendMessageAsync($"{DateTime.Now.ToString("HH時mm分")} CLOSE!!");
                    }
                    return "ok";
                }
            }
        };

        class ClientReceiveServer : BackgroundService
        {
            private readonly IConfiguration _configuration;

            public ClientReceiveServer(IConfiguration configuration)
            {
                _configuration = configuration;
            }

            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                TcpListener server = null;
                try
                {
                    int port = 12000;
                    IPAddress localAddr = IPAddress.Parse("172.17.23.238");
                    server = new TcpListener(localAddr, port);

                    server.Start();

                    Byte[] bytes = new Byte[512];
                    
                    String data = "";

                    while (true)
                    {
                        Console.Write("Waiting for a connection... ");
                        TcpClient client = await server.AcceptTcpClientAsync();
                        
                        Console.WriteLine("Connected!");

                        NetworkStream stream = client.GetStream();

                        int i;


                        // Loop to receive all the data sent by the client.
                        i = await stream.ReadAsync(bytes, 0, bytes.Length);

                        data = System.Text.Encoding.UTF8.GetString(bytes, 0, i);

                        
                        // Translate data bytes to a ASCII string.

                        string s = "message from server-discord";
                        s = await signalReceived(data);
                        Byte[] msg = System.Text.Encoding.UTF8.GetBytes(s);
                        stream.Write(msg, 0, msg.Length);

                        client.Close();
                    }
                }
                catch (SocketException e)
                {
                    Console.WriteLine($"SocketException: {e}");
                }
                catch (ObjectDisposedException e)
                {
                    Console.WriteLine($"ObejctDisposedException {e}");
                }
                finally
                {
                    // Stop listening for new clients.
                    server.Stop();
                }
            }
        };
    }
}

