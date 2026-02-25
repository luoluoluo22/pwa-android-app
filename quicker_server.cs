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
    private static List<BroadcastMessage> _broadcastBuffer = new List<BroadcastMessage>();
    private static long _msgIdCounter = 0;
    private static Window _window;

    public class BroadcastMessage {
        public long Id { get; set; }
        public string Json { get; set; }
    }
    private static ItemsControl _chatList;
    private static ScrollViewer _mainScrollViewer;
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
        public Visibility VideoVisibility { get; set; } = Visibility.Collapsed;
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
                    Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)), // 对应 var(--bg-dark) #0f172a
                    AllowDrop = true,
                    FontFamily = new FontFamily("Outfit, Noto Sans SC, Microsoft YaHei")
                };

                var mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // --- Header ---
                _currentIp = GetSmartIPAddress();
                var topBorder = new Border { 
                    Background = new SolidColorBrush(Color.FromArgb(230, 15, 23, 42)), 
                    Padding = new Thickness(15, 12, 15, 12), 
                    BorderThickness = new Thickness(0,0,0,1), 
                    BorderBrush = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)) 
                };
                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // QR Column
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Copy Button Column
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Clear Button Column

                // QR Section (Left)
                var qrStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var qrImg = new Image { 
                    Width = 65, Height = 65, 
                    Source = GetQRImage(_currentIp), 
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = "点击放大二维码"
                };
                qrImg.MouseDown += (s, e) => {
                    var zoomWin = new Window {
                        Title = "扫码配对", Width = 350, Height = 450,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                        ResizeMode = ResizeMode.NoResize,
                        Topmost = true
                    };
                    var zoomStack = new StackPanel { Margin = new Thickness(25) };
                    var bigQr = new Image { Width = 250, Height = 250, Source = GetQRImage(_currentIp), Margin = new Thickness(0,0,0,20) };
                    var hintText = new TextBlock { 
                        Text = "⚠️ 微信扫码暂不支持直接打开\n请使用手机自带相机、支付宝或QQ扫码", 
                        Foreground = Brushes.Gold, FontSize = 13, FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap 
                    };
                    var linkText = new TextBlock { 
                        Text = $"{_webAppUrl.TrimEnd('/')}/?ip={_currentIp}", 
                        Foreground = Brushes.Gray, FontSize = 10, Margin = new Thickness(0,15,0,0), 
                        TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap 
                    };
                    zoomStack.Children.Add(bigQr);
                    zoomStack.Children.Add(hintText);
                    zoomStack.Children.Add(linkText);
                    zoomWin.Content = zoomStack;
                    zoomWin.ShowDialog();
                };
                var qrLabel = new TextBlock { 
                    Text = "手机扫码", Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), 
                    FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,5,0,0) 
                };
                qrStack.Children.Add(qrImg);
                qrStack.Children.Add(qrLabel);

                // Copy Button Section (Center)
                var btnCopyLink = new Button { 
                    Content = "扫码失败？复制链接给手机", 
                    FontSize = 12, 
                    Foreground = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    FontWeight = FontWeights.SemiBold,
                    Background = new SolidColorBrush(Color.FromArgb(51, 99, 102, 241)),
                    BorderThickness = new Thickness(1), 
                    BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    Padding = new Thickness(12,8,12,8),
                    Margin = new Thickness(15, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Template = CreateFlatButtonTemplate(new CornerRadius(8))
                };
                btnCopyLink.Click += (s, e) => {
                    string fullUrl = $"{_webAppUrl.TrimEnd('/')}/?ip={_currentIp}";
                    Clipboard.SetText(fullUrl); 
                    MessageBox.Show($"配对链接已复制！\n\n请在手机浏览器中访问：\n{fullUrl}", "提示");
                };

                var btnClear = new Button { 
                    Content = "🗑️", Width = 35, Height = 35, Margin = new Thickness(5,0,0,0),
                    Background = Brushes.Transparent, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), 
                    BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = "清空记录", VerticalAlignment = VerticalAlignment.Center
                };
                btnClear.Click += (s, e) => { 
                    if(MessageBox.Show("确定要清空所有记录吗？", "提示", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                        _messages.Clear();
                        if (File.Exists(_historyPath)) File.Delete(_historyPath);
                    }
                };

                Grid.SetColumn(qrStack, 0); headerGrid.Children.Add(qrStack);
                Grid.SetColumn(btnCopyLink, 1); headerGrid.Children.Add(btnCopyLink);
                Grid.SetColumn(btnClear, 2); headerGrid.Children.Add(btnClear);
                topBorder.Child = headerGrid;
                Grid.SetRow(topBorder, 0); mainGrid.Children.Add(topBorder);

                // --- Chat List ---
                _mainScrollViewer = new ScrollViewer { 
                    Padding = new Thickness(10), 
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto 
                };
                _chatList = new ItemsControl {
                    ItemsSource = _messages,
                    ItemTemplate = CreateMessageTemplate()
                };
                _mainScrollViewer.Content = _chatList;
                Grid.SetRow(_mainScrollViewer, 1); mainGrid.Children.Add(_mainScrollViewer);

                // --- Input ---
                var inputArea = new Border {
                    Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)), // var(--chat-bg) #1e293b
                    Padding = new Thickness(15),
                    BorderThickness = new Thickness(0,1,0,0),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255))
                };
                var inputGrid = new Grid { Height = 45 };
                inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var btnAdd = new Button { 
                    Content = "➕", Width = 40, Height = 40, FontSize = 20, Margin = new Thickness(0,0,10,0),
                    Background = Brushes.Transparent, Foreground = new SolidColorBrush(Color.FromRgb(99, 102, 241)), 
                    BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                    Template = CreateFlatButtonTemplate(new CornerRadius(20))
                };
                btnAdd.Click += (s, e) => {
                    var dialog = new Microsoft.Win32.OpenFileDialog();
                    if (dialog.ShowDialog() == true) SendFile(dialog.FileName);
                };

                var txtBox = new TextBox { 
                    FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(15,0,15,0),
                    Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)), Foreground = Brushes.White, 
                    BorderThickness = new Thickness(0), CaretBrush = Brushes.White,
                    Template = CreateTextBoxTemplate(new CornerRadius(22))
                };
                var btnSend = new Button { 
                    Content = "发送", Width = 70, Height = 38, Margin = new Thickness(10,0,0,0),
                    Background = new SolidColorBrush(Color.FromRgb(99, 102, 241)), Foreground = Brushes.White, 
                    BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Template = CreateFlatButtonTemplate(new CornerRadius(19))
                };
                Action doSend = () => { if (!string.IsNullOrEmpty(txtBox.Text)) { SendText(txtBox.Text); txtBox.Clear(); } };
                btnSend.Click += (s, e) => doSend();
                txtBox.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) doSend(); };
                
                Grid.SetColumn(btnAdd, 0); inputGrid.Children.Add(btnAdd);
                Grid.SetColumn(txtBox, 1); inputGrid.Children.Add(txtBox);
                Grid.SetColumn(btnSend, 2); inputGrid.Children.Add(btnSend);
                inputArea.Child = inputGrid;
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
                   if (_mainScrollViewer != null) _mainScrollViewer.ScrollToEnd();
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
                Background = isMe ? new SolidColorBrush(Color.FromRgb(99, 102, 241)) : new SolidColorBrush(Color.FromRgb(30, 41, 59)), // sent: var(--primary), received: var(--chat-bg)
                FilePath = filePath
            };
            if (!string.IsNullOrEmpty(filePath)) {
                string ext = filePath.ToLower();
                if (ext.EndsWith(".jpg") || ext.EndsWith(".png") || ext.EndsWith(".jpeg")) {
                    try {
                        var bi = new BitmapImage(); bi.BeginInit(); bi.UriSource = new Uri(filePath); bi.DecodePixelWidth = 200; bi.EndInit();
                        msg.ImageSource = bi; msg.ImageVisibility = Visibility.Visible;
                    } catch { }
                } else if (ext.EndsWith(".mp4") || ext.EndsWith(".mov") || ext.EndsWith(".avi")) {
                    msg.VideoVisibility = Visibility.Visible;
                }
            }
            _messages.Add(msg);
            
            // 实时持久化 (仅在非加载模式下)
            if (time == null) PersistentMessage(content, isMe, filePath, useTime);

            // 自动滚动
            if (_mainScrollViewer != null) {
                _mainScrollViewer.UpdateLayout();
                _mainScrollViewer.ScrollToEnd();
            }
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
        grid.SetValue(Grid.MarginProperty, new Thickness(0, 8, 0, 8));
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
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(16)); // 对齐手机端圆角
        border.SetValue(Border.PaddingProperty, new Thickness(14, 10, 14, 10));
        border.SetBinding(Border.BackgroundProperty, new Binding("Background"));
        border.SetValue(Border.MaxWidthProperty, 320.0);
        border.SetValue(Border.CursorProperty, System.Windows.Input.Cursors.Hand);
        border.AddHandler(Border.MouseLeftButtonDownEvent, new System.Windows.Input.MouseButtonEventHandler((s, e) => {
            var m = (s as Border).DataContext as ChatMessage;
            if (!string.IsNullOrEmpty(m.FilePath)) {
                if (m.ImageVisibility == Visibility.Visible) {
                    var win = new Window { Title = "预览", Width = 800, Height = 600, WindowStartupLocation = WindowStartupLocation.CenterScreen };
                    win.Content = new Image { Source = new BitmapImage(new Uri(m.FilePath)), Stretch = Stretch.Uniform }; win.Show();
                } else if (m.VideoVisibility == Visibility.Visible) {
                    System.Diagnostics.Process.Start(m.FilePath);
                }
            }
        }));

        var innerStack = new FrameworkElementFactory(typeof(StackPanel));
        
        // 图片预览
        var img = new FrameworkElementFactory(typeof(Image));
        img.SetBinding(Image.SourceProperty, new Binding("ImageSource"));
        img.SetBinding(Image.VisibilityProperty, new Binding("ImageVisibility"));
        img.SetValue(Image.MaxWidthProperty, 200.0);
        img.SetValue(Image.MarginProperty, new Thickness(0, 0, 0, 5));

        // 视频预览 (使用图标代替，点击打开)
        var videoGrid = new FrameworkElementFactory(typeof(Grid));
        videoGrid.SetBinding(Grid.VisibilityProperty, new Binding("VideoVisibility"));
        videoGrid.SetValue(Grid.MarginProperty, new Thickness(0, 0, 0, 5));
        
        var videoBorder = new FrameworkElementFactory(typeof(Border));
        videoBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)));
        videoBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        videoBorder.SetValue(Border.PaddingProperty, new Thickness(10));
        
        var videoText = new FrameworkElementFactory(typeof(TextBlock));
        videoText.SetValue(TextBlock.TextProperty, "🎬 点击播放视频");
        videoText.SetValue(TextBlock.ForegroundProperty, Brushes.Gold);
        videoText.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        
        videoBorder.AppendChild(videoText);
        videoGrid.AppendChild(videoBorder);

        var txt = new FrameworkElementFactory(typeof(TextBlock));
        txt.SetBinding(TextBlock.TextProperty, new Binding("Content"));
        txt.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        txt.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        txt.SetValue(TextBlock.FontSizeProperty, 13.0);

        innerStack.AppendChild(img); 
        innerStack.AppendChild(videoGrid);
        innerStack.AppendChild(txt);
        border.AppendChild(innerStack);
        var timeTxt = new FrameworkElementFactory(typeof(TextBlock));
        timeTxt.SetBinding(TextBlock.TextProperty, new Binding("Time"));
        timeTxt.SetValue(TextBlock.FontSizeProperty, 10.0);
        timeTxt.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(148, 163, 184))); // var(--text-dim)
        timeTxt.SetBinding(TextBlock.HorizontalAlignmentProperty, new Binding("Alignment"));
        timeTxt.SetValue(TextBlock.MarginProperty, new Thickness(5, 4, 5, 0));

        stack.AppendChild(border); stack.AppendChild(timeTxt);
        grid.AppendChild(stack); template.VisualTree = grid;
        return template;
    }

    private static ControlTemplate CreateFlatButtonTemplate(CornerRadius cornerRadius) {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "border";
        border.SetValue(Border.CornerRadiusProperty, cornerRadius);
        border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        
        var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(contentPresenter);
        template.VisualTree = border;
        
        var trigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(Border.OpacityProperty, 0.8, "border"));
        template.Triggers.Add(trigger);
        
        return template;
    }

    private static ControlTemplate CreateTextBoxTemplate(CornerRadius cornerRadius) {
        var template = new ControlTemplate(typeof(TextBox));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, cornerRadius);
        border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        
        var scrollViewer = new FrameworkElementFactory(typeof(ScrollViewer));
        scrollViewer.Name = "PART_ContentHost";
        scrollViewer.SetValue(ScrollViewer.MarginProperty, new Thickness(0));
        scrollViewer.SetValue(ScrollViewer.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(scrollViewer);
        template.VisualTree = border;
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
        long id = Interlocked.Increment(ref _msgIdCounter);
        string json = "{\"hasFile\": true, \"id\": " + id + ", \"type\": \"text\", \"content\": \"" + escaped + "\"}";
        lock (_broadcastBuffer) { 
            _broadcastBuffer.Add(new BroadcastMessage { Id = id, Json = json }); 
            if (_broadcastBuffer.Count > 100) _broadcastBuffer.RemoveAt(0); // 保持缓冲区在合理大小
        }
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
            long id = Interlocked.Increment(ref _msgIdCounter);
            string json = "{\"hasFile\": true, \"id\": " + id + ", \"type\": \"file\", \"fileName\": \"" + fileName + "\", \"fileData\": \"data:" + mime + ";base64," + base64 + "\"}";
            lock (_broadcastBuffer) { 
                _broadcastBuffer.Add(new BroadcastMessage { Id = id, Json = json }); 
                if (_broadcastBuffer.Count > 100) _broadcastBuffer.RemoveAt(0);
            }
            LogMessage(isImg ? $"[图片] {fileName}" : $"[文件] {fileName}", true, path);
        } catch (Exception ex) { MessageBox.Show("失败: " + ex.Message); }
    }

    private static string GetSmartIPAddress() {
        try {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            // 优先找 192.168 或 10. 开头的局域网地址
            var preferredIp = host.AddressList.FirstOrDefault(ip => 
                ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && 
                (ip.ToString().StartsWith("192.168.") || ip.ToString().StartsWith("10."))
            );
            if (preferredIp != null) return preferredIp.ToString();

            // 如果没找到，找任何非回环的 IPv4 地址
            var anyIp = host.AddressList.FirstOrDefault(ip => 
                ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && 
                !IPAddress.IsLoopback(ip)
            );
            return anyIp?.ToString() ?? "127.0.0.1";
        } catch { return "127.0.0.1"; }
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
                string lastIdStr = req.QueryString["lastId"];
                long lastId = 0;
                long.TryParse(lastIdStr, out lastId);
                
                string json = "{\"hasFile\": false}";
                lock (_broadcastBuffer) {
                    if (lastIdStr == "-1") {
                        // 首次连接，告知当前最大 ID，不获取历史消息
                        json = "{\"hasFile\": false, \"nextId\": " + _msgIdCounter + "}";
                    } else {
                        var newMsgs = _broadcastBuffer.Where(m => m.Id > lastId).ToList();
                        if (newMsgs.Count > 0) {
                            json = newMsgs[0].Json;
                        }
                    }
                }
                byte[] buffer = Encoding.UTF8.GetBytes(json); res.OutputStream.Write(buffer, 0, buffer.Length);
            }
        } catch { } finally { try { res.Close(); } catch { } }
    }

    private static void StopServer() {
        if (_cts != null) { _cts.Cancel(); _cts = null; }
        if (_listener != null) { try { _listener.Abort(); } catch { } _listener = null; }
    }
}
