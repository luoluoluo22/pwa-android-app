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
    private static string _webAppUrl = "https://luoluoluo22.github.io/pwa-android-app/"; // Web 端托管地址
    private static string _historyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "灵动传Pro", "chat_history.v1.txt");

    public class ChatMessage {
        public string Content { get; set; }
        public string Time { get; set; }
        public HorizontalAlignment Alignment { get; set; }
        public SolidColorBrush Background { get; set; }
        public Visibility ImageVisibility { get; set; } = Visibility.Collapsed;
        public BitmapImage ImageSource { get; set; }
        public string FilePath { get; set; }
        public bool IsMe { get; set; }
    }

    public static void Exec(IStepContext context) {
        Application.Current.Dispatcher.Invoke(() => {
            try {
                if (_window != null) { try { _window.Close(); } catch { } }
                StopServer();
                LoadHistory(); // 启动时加载历史

                _window = new Window {
                    Title = "灵动传 Pro - 电脑工作台 (v1.1)",
                    Width = 550, Height = 800, Topmost = false,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = new SolidColorBrush(Color.FromRgb(25, 25, 30)),
                    AllowDrop = true
                };

                var mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // --- Header ---
                _currentIp = GetSmartIPAddress();
                var topBorder = new Border { Background = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), Padding = new Thickness(15), BorderThickness = new Thickness(0,0,0,1), BorderBrush = Brushes.DimGray };
                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var qrImg = new Image { Width = 80, Height = 80, Source = GetQRImage(_currentIp), Cursor = System.Windows.Input.Cursors.Hand };

                var infoStack = new StackPanel { Margin = new Thickness(15, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                infoStack.Children.Add(new TextBlock { Text = "📱 扫描二维码或点击下方按钮复制链接", Foreground = Brushes.Gray, FontSize = 11, Margin = new Thickness(0,0,0,5) });
                
                var btnCopyLink = new Button { 
                    Content = $"🔗 复制配对链接 ({_currentIp})", 
                    FontSize = 14, 
                    Foreground = Brushes.Cyan, 
                    FontWeight = FontWeights.Bold,
                    Background = new SolidColorBrush(Color.FromArgb(30, 0, 255, 255)),
                    BorderThickness = new Thickness(1), 
                    BorderBrush = Brushes.Cyan,
                    Padding = new Thickness(10,5,10,5),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                
                Action copyAction = () => {
                     string fullUrl = $"{_webAppUrl.TrimEnd('/')}/?ip={_currentIp}";
                     Clipboard.SetText(fullUrl); 
                     MessageBox.Show($"配对链接已复制！\n\n【注意】微信扫码可能无法直接打开。请在手机浏览器（如 Chrome, Safari, 自带浏览器）中粘贴并访问：\n{fullUrl}", "提示");
                 };

                btnCopyLink.Click += (s, e) => copyAction();
                qrImg.MouseDown += (s, e) => copyAction();
                
                infoStack.Children.Add(btnCopyLink);

                var btnClear = new Button { 
                    Content = "🗑️ 清空记录", Width = 90, Height = 30, Margin = new Thickness(10,0,0,0),
                    Background = Brushes.Transparent, Foreground = Brushes.Gray, BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray
                };
                btnClear.Click += (s, e) => { 
                    if(MessageBox.Show("确定要清空所有记录吗？", "提示", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                        _messages.Clear();
                        if (File.Exists(_historyPath)) File.Delete(_historyPath);
                    }
                };

                Grid.SetColumn(qrImg, 0); headerGrid.Children.Add(qrImg);
                Grid.SetColumn(infoStack, 1); headerGrid.Children.Add(infoStack);
                Grid.SetColumn(btnClear, 2); headerGrid.Children.Add(btnClear);
                topBorder.Child = headerGrid;
                Grid.SetRow(topBorder, 0); mainGrid.Children.Add(topBorder);

                // --- Chat List ---
                var scrollViewer = new ScrollViewer { Padding = new Thickness(10), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                _chatList = new ItemsControl {
                    ItemsSource = _messages,
                    ItemTemplate = CreateMessageTemplate()
                };
                scrollViewer.Content = _chatList;
                Grid.SetRow(scrollViewer, 1); mainGrid.Children.Add(scrollViewer);

                // --- Input ---
                var inputArea = new Grid { Margin = new Thickness(15), Height = 50 };
                inputArea.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                inputArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                inputArea.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var btnAdd = new Button { 
                    Content = "➕", Width = 45, Height = 45, FontSize = 22, Margin = new Thickness(0,0,10,0),
                    Background = new SolidColorBrush(Color.FromRgb(45, 55, 72)), Foreground = Brushes.White, BorderThickness = new Thickness(0)
                };
                btnAdd.Click += (s, e) => {
                    var dialog = new Microsoft.Win32.OpenFileDialog();
                    if (dialog.ShowDialog() == true) SendFile(dialog.FileName);
                };

                var txtBox = new TextBox { 
                    FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(12,0,12,0),
                    Background = new SolidColorBrush(Color.FromRgb(40, 40, 45)), Foreground = Brushes.White, BorderThickness = new Thickness(1), BorderBrush = Brushes.DimGray
                };
                var btnSend = new Button { 
                    Content = "发送", Width = 90, Margin = new Thickness(10,0,0,0),
                    Background = new SolidColorBrush(Color.FromRgb(99, 102, 241)), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold
                };
                Action doSend = () => { if (!string.IsNullOrEmpty(txtBox.Text)) { SendText(txtBox.Text); txtBox.Clear(); } };
                btnSend.Click += (s, e) => doSend();
                txtBox.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) doSend(); };
                
                Grid.SetColumn(btnAdd, 0); inputArea.Children.Add(btnAdd);
                Grid.SetColumn(txtBox, 1); inputArea.Children.Add(txtBox);
                Grid.SetColumn(btnSend, 2); inputArea.Children.Add(btnSend);
                Grid.SetRow(inputArea, 2); mainGrid.Children.Add(inputArea);

                _window.Drop += (s, e) => {
                    if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                        foreach (var f in files) SendFile(f);
                    }
                };

                _window.Content = mainGrid;
                _window.Closed += (s, e) => StopServer();
                _window.Show();

                StartServer(3001);
                // 自动滚动
                Application.Current.Dispatcher.BeginInvoke(new Action(()=> {
                   var scroll = FindVisualChild<ScrollViewer>(scrollViewer);
                   if (scroll != null) scroll.ScrollToEnd();
                }), System.Windows.Threading.DispatcherPriority.Background);

            } catch (Exception ex) { MessageBox.Show(ex.Message); }
        });
    }

    private static void LogMessage(string content, bool isMe, string filePath = null, string time = null) {
        Application.Current.Dispatcher.Invoke(() => {
            string useTime = time ?? DateTime.Now.ToString("HH:mm");
            var msg = new ChatMessage {
                Content = content,
                Time = useTime,
                IsMe = isMe,
                Alignment = isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Background = isMe ? new SolidColorBrush(Color.FromRgb(99, 102, 241)) : new SolidColorBrush(Color.FromRgb(45, 55, 72)),
                FilePath = filePath
            };
            if (!string.IsNullOrEmpty(filePath) && (filePath.ToLower().EndsWith(".jpg") || filePath.ToLower().EndsWith(".png") || filePath.ToLower().EndsWith(".jpeg"))) {
                try {
                    var bi = new BitmapImage(); bi.BeginInit(); bi.UriSource = new Uri(filePath); bi.DecodePixelWidth = 200; bi.EndInit();
                    msg.ImageSource = bi; msg.ImageVisibility = Visibility.Visible;
                } catch { }
            }
            _messages.Add(msg);
            
            // 实时持久化 (仅在非加载模式下)
            if (time == null) PersistentMessage(content, isMe, filePath, useTime);

            var scroll = FindVisualChild<ScrollViewer>(_chatList);
            if (scroll != null) scroll.ScrollToEnd();
        });
    }

    private static void PersistentMessage(string content, bool isMe, string path, string time) {
        try {
            string dir = Path.GetDirectoryName(_historyPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            // 格式: Time|IsMe|Path|Content (Base64 处理 Content 防换行干扰)
            string encodedContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(content ?? ""));
            string line = $"{time}|{isMe}|{path}|{encodedContent}\n";
            File.AppendAllText(_historyPath, line);
        } catch { }
    }

    private static void LoadHistory() {
        _messages.Clear();
        if (!File.Exists(_historyPath)) return;
        try {
            string[] lines = File.ReadAllLines(_historyPath);
            foreach (var line in lines) {
                var parts = line.Split('|');
                if (parts.Length < 4) continue;
                string time = parts[0];
                bool isMe = bool.Parse(parts[1]);
                string path = parts[2] == "" ? null : parts[2];
                string content = Encoding.UTF8.GetString(Convert.FromBase64String(parts[3]));
                LogMessage(content, isMe, path, time);
            }
        } catch { }
    }

    private static DataTemplate CreateMessageTemplate() {
        var template = new DataTemplate(typeof(ChatMessage));
        var grid = new FrameworkElementFactory(typeof(Grid));
        grid.SetValue(Grid.MarginProperty, new Thickness(0, 5, 0, 5));
        grid.SetBinding(Grid.HorizontalAlignmentProperty, new Binding("Alignment"));

        var menu = new ContextMenu();
        var miOpen = new MenuItem { Header = "📂 打开所在文件夹" };
        miOpen.Click += (s, e) => { var m = (s as MenuItem).DataContext as ChatMessage; if (!string.IsNullOrEmpty(m.FilePath)) System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{m.FilePath}\""); };
        var miCopy = new MenuItem { Header = "📋 复制文字" };
        miCopy.Click += (s, e) => { var m = (s as MenuItem).DataContext as ChatMessage; Clipboard.SetText(m.Content); };
        menu.Items.Add(miOpen); menu.Items.Add(miCopy);
        grid.SetValue(Grid.ContextMenuProperty, menu);

        var stack = new FrameworkElementFactory(typeof(StackPanel));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        border.SetValue(Border.PaddingProperty, new Thickness(12, 8, 12, 8));
        border.SetBinding(Border.BackgroundProperty, new Binding("Background"));
        border.SetValue(Border.MaxWidthProperty, 320.0);
        border.SetValue(Border.CursorProperty, System.Windows.Input.Cursors.Hand);
        border.AddHandler(Border.MouseLeftButtonDownEvent, new System.Windows.Input.MouseButtonEventHandler((s, e) => {
            var m = (s as Border).DataContext as ChatMessage;
            if (m.ImageVisibility == Visibility.Visible && !string.IsNullOrEmpty(m.FilePath)) {
                var win = new Window { Title = "预览", Width = 800, Height = 600, WindowStartupLocation = WindowStartupLocation.CenterScreen };
                win.Content = new Image { Source = new BitmapImage(new Uri(m.FilePath)), Stretch = Stretch.Uniform }; win.Show();
            }
        }));

        var innerStack = new FrameworkElementFactory(typeof(StackPanel));
        var img = new FrameworkElementFactory(typeof(Image));
        img.SetBinding(Image.SourceProperty, new Binding("ImageSource"));
        img.SetBinding(Image.VisibilityProperty, new Binding("ImageVisibility"));
        img.SetValue(Image.MaxWidthProperty, 200.0);
        img.SetValue(Image.MarginProperty, new Thickness(0, 0, 0, 5));
        var txt = new FrameworkElementFactory(typeof(TextBlock));
        txt.SetBinding(TextBlock.TextProperty, new Binding("Content"));
        txt.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        txt.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        txt.SetValue(TextBlock.FontSizeProperty, 13.0);

        innerStack.AppendChild(img); innerStack.AppendChild(txt);
        border.AppendChild(innerStack);
        var timeTxt = new FrameworkElementFactory(typeof(TextBlock));
        timeTxt.SetBinding(TextBlock.TextProperty, new Binding("Time"));
        timeTxt.SetValue(TextBlock.FontSizeProperty, 9.0);
        timeTxt.SetValue(TextBlock.ForegroundProperty, Brushes.Gray);
        timeTxt.SetBinding(TextBlock.HorizontalAlignmentProperty, new Binding("Alignment"));
        timeTxt.SetValue(TextBlock.MarginProperty, new Thickness(2, 2, 2, 0));

        stack.AppendChild(border); stack.AppendChild(timeTxt);
        grid.AppendChild(stack); template.VisualTree = grid;
        return template;
    }

    private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject {
        if (obj == null) return null;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++) {
            var child = VisualTreeHelper.GetChild(obj, i);
            if (child is T t) return t;
            var res = FindVisualChild<T>(child); if (res != null) return res;
        }
        return null;
    }

    private static BitmapImage GetQRImage(string ip) {
        string url = $"{_webAppUrl.TrimEnd('/')}/?ip={ip}";
        return new BitmapImage(new Uri($"https://api.qrserver.com/v1/create-qr-code/?size=150x150&data={WebUtility.UrlEncode(url)}"));
    }

    private static void SendText(string text) {
        string escaped = text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        string json = "{\"hasFile\": true, \"type\": \"text\", \"content\": \"" + escaped + "\"}";
        lock (_outgoingQueue) { _outgoingQueue.Add(json); }
        LogMessage(text, true);
    }

    private static void SendFile(string path) {
        try {
            byte[] bytes = File.ReadAllBytes(path);
            string base64 = Convert.ToBase64String(bytes);
            string ext = Path.GetExtension(path).ToLower();
            bool isImg = (ext == ".jpg" || ext == ".png" || ext == ".jpeg");
            string fileName = Path.GetFileName(path);
            string mime = isImg ? "image/jpeg" : "application/octet-stream";
            string json = "{\"hasFile\": true, \"type\": \"file\", \"fileName\": \"" + fileName + "\", \"fileData\": \"data:" + mime + ";base64," + base64 + "\"}";
            lock (_outgoingQueue) { _outgoingQueue.Add(json); }
            LogMessage(isImg ? $"[图片] {fileName}" : $"[文件] {fileName}", true, path);
        } catch (Exception ex) { MessageBox.Show("失败: " + ex.Message); }
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
            while (!_cts.Token.IsCancellationRequested) { try { var ctx = await _listener.GetContextAsync(); ProcessRequest(ctx); } catch { break; } }
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
                    using (var r = new StreamReader(req.InputStream, Encoding.UTF8)) {
                        string t = await r.ReadToEndAsync(); LogMessage(t, false);
                        Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(t));
                    }
                } else {
                    string fn = Encoding.UTF8.GetString(Convert.FromBase64String(req.Headers["File-Name"]));
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "手机传来");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    string path = Path.Combine(folder, fn);
                    using (var fs = new FileStream(path, FileMode.Create)) { await req.InputStream.CopyToAsync(fs); }
                    LogMessage(fn, false, path);
                    if (fn.ToLower().EndsWith(".jpg") || fn.ToLower().EndsWith(".png")) {
                        Application.Current.Dispatcher.Invoke(() => { try { Clipboard.SetImage(new BitmapImage(new Uri(path))); } catch { } });
                    }
                }
            } else if (req.Url.AbsolutePath == "/poll") {
                string json = "{\"hasFile\": false}";
                lock (_outgoingQueue) { if (_outgoingQueue.Count > 0) { json = _outgoingQueue[0]; _outgoingQueue.RemoveAt(0); } }
                byte[] buffer = Encoding.UTF8.GetBytes(json); res.OutputStream.Write(buffer, 0, buffer.Length);
            }
        } catch { } finally { try { res.Close(); } catch { } }
    }

    private static void StopServer() {
        if (_cts != null) { _cts.Cancel(); _cts = null; }
        if (_listener != null) { try { _listener.Abort(); } catch { } _listener = null; }
    }
}
