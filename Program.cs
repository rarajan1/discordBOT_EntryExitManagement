using Discord;
using Discord.WebSocket;
using System;
using System.IO;
using System.Linq;
using System.Text;
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
            }
            );


        class DiscordBotService : BackgroundService
        {

            private DiscordSocketClient _client;
            private readonly IConfiguration _configuration;
            private string Token = ""; //トークン
            private TimeOutExe UpdateMember = new TimeOutExe(10);
            private List<MemberDatas> memberDatasList = new List<MemberDatas>();

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
                //タイムアウトするとメンバーを更新する
                UpdateMember.TimeoutedFunc += UpdateMemberDisplay;
                //タブレットから信号を受けとった時のメソッドを登録
                signalReceived += SignalRecived;

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

            private async Task<string> SignalRecived(string data)
            {
                var s = data.Split(',');
                if (s[1] == "icon")
                    return await SendAvatar(ulong.Parse(s[0])); //アバターのデータを返す
                else
                {
                    UpdateMembers(data);//メンバーの情報を追加する
                    return "ok";
                }
            }
            private async Task<string> SendAvatar(ulong sendedGuildId)
            {
                string sentData = "";
                //BOTが属しているサーバーを取得
                var guilds = _client.Guilds;    

                foreach(var guild in guilds)
                {
                    //アイコンを要求してきたサーバーIDなら
                    if(guild.Id == sendedGuildId)
                    {
                        var memberDatas = GetMemberDatas(sendedGuildId);
                        var guildsUser = guild.GetUsersAsync();
                        await foreach (var users in guildsUser)
                            //メンバーの情報をmemberDatasに代入
                            foreach (var user in users)
                            {
                                var member = memberDatas.SetUserID(user.Id);
                                member.avatarURL = user.GetDisplayAvatarUrl();
                                member.memberName = user.DisplayName;
                                if (member.date == null)
                                    member.date = DateTime.MinValue;
                                sentData += member.ToString();
                            }
                    }
                }
                sentData = sentData.TrimEnd(',');
                return sentData;
            }

            private async Task UpdateMembers(string data)
            {
                foreach (var memberDatas in memberDatasList)
                {
                    //メンバーの入退出を更新
                    var result = memberDatas.UpdateMemberActive(data);
                    
                    Console.WriteLine(result);

                    //Logに描き込み
                    using (StreamWriter sw = new StreamWriter($"Log_{memberDatas.discordServerID}.txt", true, Encoding.GetEncoding("UTF-8")))
                    {
                        sw.Write(result);
                    }

                    //ステータス欄に表示

                    UpdateMember.SetData(memberDatas.activeMemberCount.ToString());
                }
                return;
            }

            private int oldmemberCount = 0;
            private async Task<string> UpdateMemberDisplay(string data)
            {
                var memberCount = int.Parse(data);
                await Task.Run(() =>
                {
                    _client.SetGameAsync($"{memberCount}人が研究室", null, ActivityType.Competing);
                    var chatchannel = _client.GetChannel(978806146452303896) as SocketTextChannel;
                    if (oldmemberCount == 0 && memberCount >= 1)
                    {
                        _client.SetStatusAsync(UserStatus.Online);
                        chatchannel.SendMessageAsync($"{DateTime.Now.ToString("HH時mm分")} OPEN!!");
                    }
                    if (oldmemberCount >= 1 && memberCount == 0)
                    {
                        _client.SetStatusAsync(UserStatus.DoNotDisturb);
                        chatchannel.SendMessageAsync($"{DateTime.Now.ToString("HH時mm分")} CLOSE!!");
                    }
                    oldmemberCount = memberCount;
                });
                return null;
            }

            private MemberDatas GetMemberDatas(ulong discordServerId)
            {
                if (memberDatasList.Any(x => x.discordServerID == discordServerId))
                    return memberDatasList.Where(x => x.discordServerID == discordServerId).ToList()[0];
                else
                {
                    var memberData = new MemberDatas(discordServerId);
                    memberDatasList.Add(memberData);
                    return memberData;
                }
            }
        };

        class ClientReceiveServer : BackgroundService
        {
            private readonly IConfiguration _configuration;
            private string IPAdress = "192.168.1.3";
            private int port = 12000;

            public ClientReceiveServer(IConfiguration configuration)
            {
                _configuration = configuration;
            }

            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                TcpListener server = null;
                try
                {
                    IPAddress localAddr = IPAddress.Parse(IPAdress);
                    server = new TcpListener(localAddr, port);

                    Console.WriteLine(localAddr);

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

                        string s = await signalReceived(data);
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

class TimeOutExe
{
    private string datas = "";
    private int initTime;
    private int currentTime;
    private bool currentStart = false;

    public Func<string, Task> TimeoutedFunc;

    public TimeOutExe(int initTime)
    {
        this.initTime = initTime;
        currentTime = initTime;
    }

    public void AddData(string data)
    {
        datas += $"{data},";
        UpdateTimeout(initTime);
    }

    public void SetData(string data)
    {
        datas = data;
        UpdateTimeout(initTime);
    }

    public void UpdateTimeout(int newTime = 10)
    {
        if (!currentStart)
        {
            Task.Run(() => ExecuteTimeoutedFunc());
            currentStart = true;
        }
        currentTime = newTime;
    }

    private async Task ExecuteTimeoutedFunc()
    {
        while (currentTime > 0)
        {
            await Task.Delay(1000);
            currentTime--;
        }
        await TimeoutedFunc(datas);
        currentStart = false;
        currentTime = initTime;
        datas = "";
    }
};
