using System;
using System.Net;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Quicker.Public;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows.Data;

public class PCFileServer {
    private static HttpListener _listener;
    private static CancellationTokenSource _cts;
    private static List<string> _outgoingQueue = new List<string>();
    private static Window _window;
    private static ItemsControl _chatList;
    private static ObservableCollection<ChatMessage> _messages = new ObservableCollection<ChatMessage>();
    private static string _currentIp;

    public class ChatMessage {
        public string Content { get; set; }
        public string Time { get; set; }
        public HorizontalAlignment Alignment { get; set; }
        public SolidColorBrush Background { get; set; }
        public string Icon { get; set; }
    }

    public static void Exec(IStepContext context) {
        Application.Current.Dispatcher.Invoke(() => {
            try {
                if (_window != null) { try { _window.Close(); } catch { } }
                StopServer();
                _messages.Clear();

                _window = new Window {
                    Title = "灵动传 Pro - 电脑端",
                    Width = 500, Height = 750, Topmost = true,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = new SolidColorBrush(Color.FromRgb(25, 25, 30)),
                    AllowDrop = true
                };

                var mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header/QR
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Chat
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Input

                // --- 1. Header (QR) ---
                _currentIp = GetSmartIPAddress();
                var headerBorder = new Border { 
                    Background = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)), 
                    Padding = new Thickness(15) 
                };
                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var qrImg = new Image { Width = 100, Height = 100, Source = GetQRImage(_currentIp) };
                var infoStack = new StackPanel { Margin = new Thickness(15, 0, 0, 0) };
                infoStack.Children.Add(new TextBlock { Text = "📱 扫码连接手机", Foreground = Brushes.Gray, FontSize = 12 });
                var ipEdit = new TextBox { 
                    Text = _currentIp, FontSize = 16, Foreground = Brushes.Cyan, 
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0,0,0,1), BorderBrush = Brushes.Cyan,
                    Margin = new Thickness(0,5,0,5)
                };
                ipEdit.TextChanged += (s, e) => {
                    _currentIp = ipEdit.Text;
                    qrImg.Source = GetQRImage(_currentIp);
                };
                infoStack.Children.Add(ipEdit);
                infoStack.Children.Add(new TextBlock { Text = "🟢 支持文字发送 & 拖拽文件入窗", Foreground = Brushes.LimeGreen, FontSize = 11 });

                Grid.SetColumn(qrImg, 0); headerGrid.Children.Add(qrImg);
                Grid.SetColumn(infoStack, 1); headerGrid.Children.Add(infoStack);
                headerBorder.Child = headerGrid;
                Grid.SetRow(headerBorder, 0); mainGrid.Children.Add(headerBorder);

                // --- 2. Chat List (Bubbles) ---
                var scrollViewer = new ScrollViewer { Padding = new Thickness(10), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                _chatList = new ItemsControl {
                    ItemsSource = _messages,
                    ItemTemplate = CreateMessageTemplate()
                };
                scrollViewer.Content = _chatList;
                Grid.SetRow(scrollViewer, 1); mainGrid.Children.Add(scrollViewer);

                // --- 3. Input & Actions ---
                var inputArea = new Grid { Margin = new Thickness(15), Height = 50 };
                inputArea.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // + Button
                inputArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // TextBox
                inputArea.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Send Button

                var btnAdd = new Button { 
                    Content = "➕", Width = 40, Height = 40, FontSize = 20, Margin = new Thickness(0,0,10,0),
                    Background = new SolidColorBrush(Color.FromRgb(45, 55, 72)), Foreground = Brushes.White, BorderThickness = new Thickness(0)
                };
                btnAdd.Click += (s, e) => {
                    var dialog = new Microsoft.Win32.OpenFileDialog();
                    if (dialog.ShowDialog() == true) {
                        SendFile(dialog.FileName);
                    }
                };

                var txtBox = new TextBox { 
                    FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(10,0,10,0),
                    Background = new SolidColorBrush(Color.FromRgb(40, 40, 45)), Foreground = Brushes.White, BorderThickness = new Thickness(1)
                };
                var btnSend = new Button { 
                    Content = "发送", Width = 80, Margin = new Thickness(10,0,0,0),
                    Background = new SolidColorBrush(Color.FromRgb(99, 102, 241)), Foreground = Brushes.White, BorderThickness = new Thickness(0)
                };
                
                btnSend.Click += (s, e) => {
                    if (!string.IsNullOrEmpty(txtBox.Text)) {
                        SendText(txtBox.Text);
                        txtBox.Clear();
                    }
                };
                txtBox.KeyDown += (s, e) => {
                    if (e.Key == System.Windows.Input.Key.Enter && !string.IsNullOrEmpty(txtBox.Text)) {
                        SendText(txtBox.Text);
                        txtBox.Clear();
                    }
                };
                
                Grid.SetColumn(btnAdd, 0); inputArea.Children.Add(btnAdd);
                Grid.SetColumn(txtBox, 1); inputArea.Children.Add(txtBox);
                Grid.SetColumn(btnSend, 2); inputArea.Children.Add(btnSend);
                Grid.SetRow(inputArea, 2); mainGrid.Children.Add(inputArea);

                // --- Drag and Drop ---
                _window.Drop += (s, e) => {
                    if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                        foreach (var file in files) SendFile(file);
                    }
                };

                _window.Content = mainGrid;
                _window.Closed += (s, e) => StopServer();
                _window.Show();

                StartServer(3001);
                LogMessage("服务端已就绪，等待手机连接...", false);
            } catch (Exception ex) { MessageBox.Show(ex.Message); }
        }) ;
    }

    private static void LogMessage(string content, bool isMe) {
        Application.Current.Dispatcher.Invoke(() => {
            _messages.Add(new ChatMessage {
                Content = content,
                Time = DateTime.Now.ToString("HH:mm"),
                Alignment = isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Background = isMe ? new SolidColorBrush(Color.FromRgb(99, 102, 241)) : new SolidColorBrush(Color.FromRgb(45, 55, 72)),
                Icon = isMe ? "👨" : "📱"
            });
            // 自动滚动到底部
            if (VisualTreeHelper.GetChildrenCount(_chatList.Parent as ScrollViewer) > 0) {
                var scrollViewer = _chatList.Parent as ScrollViewer;
                scrollViewer.ScrollToEnd();
            }
        });
    }

    private static DataTemplate CreateMessageTemplate() {
        var xaml = @"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
            <Grid Margin='0,5,0,5' HorizontalAlignment='{Binding Alignment}'>
                <StackPanel Orientation='Vertical'>
                    <Border CornerRadius='10' Padding='12,8,12,8' Background='{Binding Background}' MaxWidth='350'>
                        <TextBlock Text='{Binding Content}' TextWrapping='Wrap' Foreground='White' FontSize='13'/>
                    </Border>
                    <TextBlock Text='{Binding Time}' FontSize='9' Foreground='Gray' HorizontalAlignment='{Binding Alignment}' Margin='2,2,2,0'/>
                </StackPanel>
            </Grid>
        </DataTemplate>";
        return (DataTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
    }

    private static BitmapImage GetQRImage(string ip) {
        string url = $"https://luoluoluo22.github.io/pwa-android-app/?ip={ip}";
        return new BitmapImage(new Uri($"https://api.qrserver.com/v1/create-qr-code/?size=150x150&data={WebUtility.UrlEncode(url)}"));
    }

    private static void SendText(string text) {
        string escapedContent = text.Replace("\\", "\\\\")
                                     .Replace("\"", "\\\"")
                                     .Replace("\n", "\\n")
                                     .Replace("\r", "\\r")
                                     .Replace("\t", "\\t");
        string json = "{\"hasFile\": true, \"type\": \"text\", \"content\": \"" + escapedContent + "\"}";
        lock (_outgoingQueue) { _outgoingQueue.Add(json); }
        LogMessage(text, true);
    }

    private static void SendFile(string path) {
        try {
            byte[] bytes = File.ReadAllBytes(path);
            string base64 = Convert.ToBase64String(bytes);
            string ext = Path.GetExtension(path).ToLower();
            string mime = (ext == ".jpg" || ext == ".png" || ext == ".jpeg") ? "image/jpeg" : "application/octet-stream";
            string fileName = Path.GetFileName(path);
            
            string json = "{\"hasFile\": true, \"type\": \"file\", \"fileName\": \"" + fileName + "\", \"fileData\": \"data:" + mime + ";base64," + base64 + "\"}";
            lock (_outgoingQueue) { _outgoingQueue.Add(json); }
            LogMessage($"[发送文件] {fileName}", true);
        } catch (Exception ex) { MessageBox.Show("发送失败: " + ex.Message); }
    }

    private static string GetSmartIPAddress() {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        return host.AddressList.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && (ip.ToString().StartsWith("192.168.") || ip.ToString().StartsWith("10.")))?.ToString() ?? "127.0.0.1";
    }

    private static void StartServer(int port) {
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://*:{port}/");
        _listener.Start();
        Task.Run(async () => {
            while (!_cts.Token.IsCancellationRequested) {
                try {
                    var ctx = await _listener.GetContextAsync();
                    ProcessRequest(ctx);
                } catch { break; }
            }
        });
    }

    private static async void ProcessRequest(HttpListenerContext ctx) {
        var req = ctx.Request; var res = ctx.Response;
        res.Headers.Add("Access-Control-Allow-Origin", "*");
        res.Headers.Add("Access-Control-Allow-Headers", "Content-Type, File-Name, Msg-Type");
        res.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
        if (req.HttpMethod == "OPTIONS") { res.Close(); return; }

        try {
            if (req.Url.AbsolutePath == "/upload") {
                string msgType = req.Headers["Msg-Type"] ?? "file";
                if (msgType == "text") {
                    using (var reader = new StreamReader(req.InputStream, Encoding.UTF8)) {
                        string text = await reader.ReadToEndAsync();
                        LogMessage(text, false);
                        Application.Current.Dispatcher.Invoke(() => { Clipboard.SetText(text); });
                    }
                } else {
                    string fileName = Encoding.UTF8.GetString(Convert.FromBase64String(req.Headers["File-Name"]));
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "手机传来");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    string savePath = Path.Combine(folder, fileName);
                    using (var fs = new FileStream(savePath, FileMode.Create)) { await req.InputStream.CopyToAsync(fs); }
                    LogMessage($"[收到文件] {fileName}", false);
                    if (fileName.EndsWith(".jpg") || fileName.EndsWith(".png")) {
                        Application.Current.Dispatcher.Invoke(() => { Clipboard.SetImage(new BitmapImage(new Uri(savePath))); });
                    }
                }
            } else if (req.Url.AbsolutePath == "/poll") {
                string json = "{\"hasFile\": false}";
                lock (_outgoingQueue) {
                    if (_outgoingQueue.Count > 0) {
                        json = _outgoingQueue[0];
                        _outgoingQueue.RemoveAt(0);
                    }
                }
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                res.OutputStream.Write(buffer, 0, buffer.Length);
            }
        } catch { } finally { res.Close(); }
    }

    private static void StopServer() {
        if (_cts != null) { _cts.Cancel(); _cts = null; }
        if (_listener != null) { try { _listener.Abort(); } catch { } _listener = null; }
    }
}
