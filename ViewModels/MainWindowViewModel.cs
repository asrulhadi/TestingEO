using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DotNetty.Extensions;

using DynamicData;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Serilog;

using TestingEO.Models;

#pragma warning disable CS8618

namespace TestingEO.ViewModels
{
    public class DataChecked : ReactiveObject
    {
        public string Data { get; set; }
        public string Comment { get; set; }
        [Reactive] public bool Received { get; set; }

        public DataChecked(string data, bool check = false, string comment = "")
        {
            Data = data;
            Received = check;
            Comment = comment;
        }
        public bool Sama(string cmd)
        {
            bool sama = cmd.StartsWith(Data.Substring(0, 8));
            if (sama) Log.Verbose("Cmd dijumpai: {0}", Data);
            return sama;
        }
    }
    public partial class MainWindowViewModel : ViewModelBase
    {
        public TcpSocketClient? client { get; set; } = null;
        public Pelco pelco = new();
        public List<byte> Cmd { get; } = new List<byte>();
        public ObservableCollection<string> Replied { get; set; } = new();
        private List<DataChecked> ListInitC1 = new List<DataChecked>();
        private List<DataChecked> ListInitC2 = new List<DataChecked>();

        IDisposable d;
        public MainWindowViewModel()
        {
            InitReply();
            this.WhenAnyValue(x => x.StatusChanged)
                .Throttle(TimeSpan.FromMilliseconds(500))
                .Subscribe(_ => ReceivedAll = ListInitC1.TrueForAll(d => d.Received) && ListInitC2.TrueForAll(d => d.Received));

            //this.WhenAnyValue(x => x.ZoomLevel)
            //    //.Throttle(TimeSpan.FromMilliseconds(500))
            //    .Subscribe(x => Debug.WriteLine("ZoomLevel = {0}", x));

            pelco.FunctionListReady = InitCommandList;
            pelco.StartPopulate();
        }
        public void InitReply()
        {
            //InitReplyC1 = new ObservableCollection<DataChecked>()
            //InitReplyC2 = new ObservableCollection<DataChecked>()
            // masukkan semula semua dalam list
            if (ListInitC1.Count == 0)
            {
                {
                    c1FF010059 = new DataChecked("FF01005903B714", false, "Pan Position");      // Response Pan Position
                    c1FF01005B = new DataChecked("FF01005B0656B8", false, "Tilt Position");     // Response Tilt Position
                    c1FF01005D = new DataChecked("FF01005D00005E", false, "Zoom Position");     // Response Zoom Position
                    c1FF010063 = new DataChecked("FF010063151891", false, "Focus Position");    // Response Focus Position
                    c1FF01008D = new DataChecked("FF01008D1E02AE", false);    // Styris Command - 008D
                    c1FF01008F = new DataChecked("FF01008F000090", false);    // Styris Command - 008F
                    c1FF0100B5 = new DataChecked("FF0100B50001B7", false);    // Styris Command - 00B5
                    c1FF0100BF = new DataChecked("FF0100BF0003C3", false, "Wiper Status??");    // Styris Command - 00BF - Wiper?
                    c1FF0100C1 = new DataChecked("FF0100C10002C4", false);    // Styris Command - 00C1
                    c1FF0100C5 = new DataChecked("FF0100C50002C8", false);    // Styris Command - 00C5
                    c1FF0100C7 = new DataChecked("FF0100C70002CA", false);    // Styris Command - 00C7
                    c1FF0100C9 = new DataChecked("FF0100C90003CD", false);    // Styris Command - 00C9
                }
                ListInitC1.Add(c1FF010059);
                ListInitC1.Add(c1FF01005B);
                ListInitC1.Add(c1FF01005D);
                ListInitC1.Add(c1FF010063);
                ListInitC1.Add(c1FF01008D);
                ListInitC1.Add(c1FF01008F);
                ListInitC1.Add(c1FF0100B5);
                ListInitC1.Add(c1FF0100BF);
                ListInitC1.Add(c1FF0100C1);
                ListInitC1.Add(c1FF0100C5);
                ListInitC1.Add(c1FF0100C7);
                ListInitC1.Add(c1FF0100C9);
            }
            if (ListInitC2.Count == 0)
            {
                {
                    c2FF02005D = new DataChecked("FF02005DE662A7", false, "Zoom Position");     // Response Zoom Position
                    c2FF020063 = new DataChecked("FF0200630384EC", false, "Focus Position");    // Response Focus Position
                    c2FF02008D = new DataChecked("FF02008DF5D458", false);    // Styris Command - 008D
                    c2FF0200B5 = new DataChecked("FF0200B50001B8", false);    // Styris Command - 00B5
                    c2FF0200B7 = new DataChecked("FF0200B7007F38", false);    // Styris Command - 00B7
                    c2FF0200B9 = new DataChecked("FF0200B90000BB", false);    // Styris Command - 00B9
                    c2FF0200BB = new DataChecked("FF0200BB0001BE", false);    // Styris Command - 00BB
                    c2FF0200C3 = new DataChecked("FF0200C30001C6", false);    // Styris Command - 00C3
                }
                ListInitC2.Add(c2FF02005D);
                ListInitC2.Add(c2FF020063);
                ListInitC2.Add(c2FF02008D);
                ListInitC2.Add(c2FF0200B5);
                ListInitC2.Add(c2FF0200B7);
                ListInitC2.Add(c2FF0200B9);
                ListInitC2.Add(c2FF0200BB);
                ListInitC2.Add(c2FF0200C3);
            }
            // dan falsekan semua
            ListInitC1.ForEach(d => d.Received = false);
            ListInitC2.ForEach(d => d.Received = false);
            StatusChanged = 0;
        }
        public void InitCommandList(Task task)
        {
            // wait for the task???
            // init the list of command
            var infos = new (ObservableCollection<string>, Dictionary<string, MethodInfo>)[]
            {
                (ProcA, pelco.str2procA),
                (ProcB, pelco.str2procB),
                (ProcC, pelco.str2procC)
            };
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var (d,s) in infos)
                {
                    var t = s.Keys.ToList(); t.Sort();
                    d.AddRange(t);
                }
            });
        }
        [RelayCommand] private void SendPelco(string command)
        {
            Console.WriteLine("Command to Send {0}", command);
        }

        [RelayCommand] private void PelcoSpecific(string command)
        {
            var cmds = command.Split("@");
            byte camId = Byte.Parse(cmds[1].Split("=")[^1]);
            var c = cmds.Length switch
            {
                3 when bool.TryParse(cmds[2], out bool cond) => pelco.get(cmds[0], camId, cond),
                3 when int.TryParse(cmds[2], out int data) => pelco.get(cmds[0], camId, data),
                2 => pelco.get(cmds[0], camId),
                _ => (new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, (_,_) => new byte[] { 0xDE, 0xAD, 0xBE, 0xEF })
            };
            string cmd = BitConverter.ToString(c.Item1).Replace("-", String.Empty);
            Console.WriteLine("Command Len={3} with CamId={1} {0} => {2}", command, camId, cmd, cmds.Length );
            client?.SendAsync(c.Item1);
        }
        [RelayCommand] private void PelcoPTZ(string cmd)
        {
            if (cmd is null) return;
            var cmds = cmd.Split('@');
            var pelcoCmd = cmds[0];
            var camId = int.Parse(cmds[1].Split("=")[^1]);
            var value = cmds.Length == 3 && int.TryParse(cmds[2], out int val) ? val : -1;
            Debug.WriteLine($"{cmd} => {value}");
            if (pelcoCmd.StartsWith("Set Tilt"))
            {
                // tilt is inverse
                if (value < 0) value = -value;
                else if (value > 0) value = 36000 - value;
                PelcoSpecific($"{cmds[0]}@{cmds[1]}@{value}");
            }
            else
            {
                if (pelcoCmd.StartsWith("Zoom")) PelcoSpecific(cmd);
                else PelcoSpecific($"{cmd}@50");
                // wait for a while - before send reset
                Task.Delay(500).Wait();
                PelcoSpecific($"Reset@CamId={camId}");
            }
        }
        public void ConnectToEO()
        {
            int port;
            try
            {
                port = Convert.ToInt32(Port);
                Log.Debug("Connecting to {0}:{1}", Host, Port);
                // setup connection
                client = new TcpSocketClient(Host, Convert.ToInt32(Port));
                client.OnConnect(() => Connected = true);
                client.OnReceive(bytes => ProcessData(bytes));
                // do the connection
                client.ConnectAsync();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error {e}", e.Message);
                throw;
            }
        }
        public async void CloseConnect()
        {
            if (!Connected) return;
            await client!.ShutdownAsync();
            Connected = false;
            InitReply();
            client = null;
        }
        private void ProcessData(byte[] bytes)
        {
            // make a copy of incoming data - in case shared buffer
            int len = bytes.Length;
            byte[] data = new byte[len];
            bytes.CopyTo(data, 0);
            Log.Debug("Data Received: {0}", BitConverter.ToString(data).Replace("-",String.Empty));
            for (int i = 0; i < data.Length; i++)
            {
                if ((Cmd.Count == 0) && (data[i] != 0xFF)) continue;
                try
                {
                    Cmd.Add(data[i]);
                    if (Cmd.Count == 7) SearchAndMark();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Exception kat ProcessData");
                    throw;
                }
            }
        }
        private void SearchAndMark(List<DataChecked> arr, string cmd)
        {
            List<DataChecked> tukarkan = new List<DataChecked>();
            arr.ForEach(d => { if (d.Sama(cmd)) { tukarkan.Add(d); StatusChanged++; } });
            Observable.Start(() => tukarkan.ForEach(d => d.Received = true), RxApp.MainThreadScheduler);
        }
        private void SearchAndMark()
        {
            byte[] cmdB = new byte[7];
            Cmd.CopyTo(cmdB, 0);
            Cmd.Clear();

            string cmdS = BitConverter.ToString(cmdB).Replace("-", String.Empty);
            Log.Verbose("Searching for: {0}", cmdS);
            Task.Run(() => SearchAndMark(ListInitC1, cmdS));
            Task.Run(() => SearchAndMark(ListInitC2, cmdS));
            // add to Replied List
            Dispatcher.UIThread.Post(() =>
            {
                while (Replied.Count > 100) Replied.RemoveAt(0);
                Replied.Add(BitConverter.ToString(cmdB).Replace("-", String.Empty));
            });
        }

        [RelayCommand] private void CheckedBoxed(bool isChecked)
        {
            if (!isChecked)
            {
                InitReply();
                return;
            }
            Log.Debug("Sending init data: {0}", isChecked);
            client.SendAsync(new byte[] { 0xFF, 0x01, 0x00, 0x09, 0x00, 0x0f, 0x19 });
        }

        public void Closing(object? sender, CancelEventArgs e)
        {
            Task.Run(()=>CloseConnect()).Wait();
            Log.Verbose("Closing Windows");
            Log.CloseAndFlush();
        }

        public string Host { get; set; }
        public string Port { get; set; }
        public ObservableCollection<string> ProcA { get; set; } = new();
        public ObservableCollection<string> ProcB { get; set; } = new();
        public ObservableCollection<string> ProcC { get; set; } = new();
        [ObservableProperty] private int statusChanged;
        [ObservableProperty] private bool receivedAll = false;
        [ObservableProperty] private bool connected = false;

        #region InitC1
        [Reactive] public DataChecked c1FF010059 { get; set; }   // Response Pan Position
        [Reactive] public DataChecked c1FF01005B { get; set; }   // Response Tilt Position
        [Reactive] public DataChecked c1FF01005D { get; set; }   // Response Zoom Position
        [Reactive] public DataChecked c1FF010063 { get; set; }   // Response Focus Position
        [Reactive] public DataChecked c1FF01008D { get; set; }   // Styris Command - 008D
        [Reactive] public DataChecked c1FF01008F { get; set; }   // Styris Command - 008F
        [Reactive] public DataChecked c1FF0100B5 { get; set; }   // Styris Command - 00B5
        [Reactive] public DataChecked c1FF0100BF { get; set; }   // Styris Command - 00BF - Wiper?
        [Reactive] public DataChecked c1FF0100C1 { get; set; }   // Styris Command - 00C1
        [Reactive] public DataChecked c1FF0100C5 { get; set; }   // Styris Command - 00C5
        [Reactive] public DataChecked c1FF0100C7 { get; set; }   // Styris Command - 00C7
        [Reactive] public DataChecked c1FF0100C9 { get; set; }   // Styris Command - 00C9
        #endregion
        #region InitC2
        [Reactive] public DataChecked c2FF02005D { get; set; }   // Response Zoom Position
        [Reactive] public DataChecked c2FF020063 { get; set; }   // Response Focus Position
        [Reactive] public DataChecked c2FF02008D { get; set; }   // Styris Command - 008D
        [Reactive] public DataChecked c2FF0200B5 { get; set; }   // Styris Command - 00B5
        [Reactive] public DataChecked c2FF0200B7 { get; set; }   // Styris Command - 00B7
        [Reactive] public DataChecked c2FF0200B9 { get; set; }   // Styris Command - 00B9
        [Reactive] public DataChecked c2FF0200BB { get; set; }   // Styris Command - 00BB
        [Reactive] public DataChecked c2FF0200C3 { get; set; }   // Styris Command - 00C3
        #endregion
    }
}
