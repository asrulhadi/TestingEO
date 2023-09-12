using DotNetty.Extensions;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Threading.Tasks;

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
    public class MainWindowViewModel : ViewModelBase
    {
        public TcpSocketClient client { get; set; } = null;
        public List<byte> Cmd { get; } = new List<byte>();
        private List<DataChecked> ListInitC1 = new List<DataChecked>();
        private List<DataChecked> ListInitC2 = new List<DataChecked>();
        public MainWindowViewModel()
        {
            InitReply();
            this.WhenAnyValue(x => x.StatusChanged)
                .Throttle(TimeSpan.FromMilliseconds(500))
                .Subscribe(_ => ReceivedAll = ListInitC1.TrueForAll(d => d.Received) && ListInitC2.TrueForAll(d => d.Received));
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
            await client.ShutdownAsync();
            Connected = false;
            InitReply();
            client = null;
        }
        private void ProcessData(byte[] bytes)
        {
            // make a copy of incming data - in case shared buffer
            int len = bytes.Length;
            byte[] data = new byte[len];
            bytes.CopyTo(data, 0);
            Log.Debug("Data Received: {0}", BitConverter.ToString(data).Replace("-",String.Empty));
            for (int i = 0; i < data.Length; i++)
            {
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
        }

        public void CheckedBoxed(bool isChecked)
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
        [Reactive] public int StatusChanged { get; set; }
        [Reactive] public bool ReceivedAll { get; set; } = false;
        [Reactive] public bool Connected { get; set; } = false;
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
