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

public class PCFileServer {
    private static HttpListener _listener;
    private static CancellationTokenSource _cts;
    private static TextBox _logBox;
    private static Window _window;
    
    // 待发送给手机的消息队列
    private static List<object> _outgoingQueue = new List<object>();

    public static void Exec(IStepContext context) {
        Application.Current.Dispatcher.Invoke(() => {
            try {
                if (_window != null) { try { _window.Close(); } catch { } }
                StopServer();

                _window = new Window {
                    Title = "全能灵动传 - 电脑助手",
                    Width = 600, Height = 650, Topmost = true,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = new SolidColorBrush(Color.FromRgb(25, 25, 30))
                };

                var mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header + QR
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Logs
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Input Area

                // --- 1. Header Area (QR Code) ---
                var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(15) };
                
                string myIp = GetLocalIPAddress();
                string pairingUrl = $"https://luoluoluo22.github.io/pwa-android-app/?ip={myIp}";
                
                // 使用在线接口生成 QR 码显示（零依赖方案）
                var qrImage = new Image {
                    Width = 120, Height = 120, Margin = new Thickness(0,0,20,0),
                    Source = new BitmapImage(new Uri($"https://api.qrserver.com/v1/create-qr-code/?size=150x150&data={WebUtility.UrlEncode(pairingUrl)}"))
                };
                
                var infoStack = new StackPanel();
                infoStack.Children.Add(new TextBlock { Text = "📱 手机扫码或访问配对地址:", Foreground = Brushes.Gray, FontSize = 12 });
                var urlText = new TextBox { 
                    Text = pairingUrl, IsReadOnly = true, Background = Brushes.Transparent, 
                    Foreground = Brushes.Cyan, BorderThickness = new Thickness(0), Margin = new Thickness(0,5,0,0) 
                };
                infoStack.Children.Add(urlText);
                infoStack.Children.Add(new TextBlock { Text = "🟢 支持: 发送文件、同步剪贴板", Foreground = Brushes.LimeGreen, Margin = new Thickness(0,10,0,0) });

                headerStack.Children.Add(qrImage);
                headerStack.Children.Add(infoStack);
                Grid.SetRow(headerStack, 0);
                mainGrid.Children.Add(headerStack);

                // --- 2. Log/Chat Area ---
                _logBox = new TextBox {
                    IsReadOnly = true, Background = Brushes.Black, Foreground = Brushes.LimeGreen,
                    AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    FontFamily = new FontFamily("Consolas"), FontSize = 13, Padding = new Thickness(10), Margin = new Thickness(10)
                };
                Grid.SetRow(_logBox, 1);
                mainGrid.Children.Add(_logBox);

                // --- 3. Input Area ---
                var inputGrid = new Grid { Margin = new Thickness(10, 0, 10, 15) };
                inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var txtInput = new TextBox { 
                    Height = 40, Padding = new Thickness(10, 8, 10, 8), FontSize = 14,
                    Background = new SolidColorBrush(Color.FromRgb(40, 40, 45)), Foreground = Brushes.White,
                    BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray
                };
                
                var btnSend = new Button { 
                    Content = "发送到手机", Width = 100, Margin = new Thickness(10, 0, 0, 0),
                    Background = new SolidColorBrush(Color.FromRgb(60, 100, 240)), Foreground = Brushes.White
                };
                
                btnSend.Click += (s, e) => {
                    if (!string.IsNullOrEmpty(txtInput.Text)) {
                        SendTextToPhone(txtInput.Text);
                        Log($"[发送] {txtInput.Text}");
                        txtInput.Clear();
                    }
                };

                Grid.SetColumn(txtInput, 0); inputGrid.Children.Add(txtInput);
                Grid.SetColumn(btnSend, 1); inputGrid.Children.Add(btnSend);
                Grid.SetRow(inputGrid, 2);
                mainGrid.Children.Add(inputGrid);

                _window.Content = mainGrid;
                _window.Closed += (s, e) => { StopServer(); };
                _window.Show();

                StartServerAsync(3001);
                Log("🚀 灵动传 Pro 已启动...");
            } catch (Exception ex) {
                MessageBox.Show("启动失败: " + ex.Message);
            }
        });
    }

    private static void Log(string msg) {
        if (_window == null || _logBox == null) return;
        _window.Dispatcher.BeginInvoke(new Action(() => {
            _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            _logBox.ScrollToEnd();
        }));
    }

    private static string GetLocalIPAddress() {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList) {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !ip.ToString().StartsWith("127.")) 
                return ip.ToString();
        }
        return "127.0.0.1";
    }

    private static void StartServerAsync(int port) {
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
            if (req.Url.AbsolutePath == "/upload" && req.HttpMethod == "POST") {
                string msgType = req.Headers["Msg-Type"] ?? "file";
                
                if (msgType == "text") {
                    // 接收文字
                    using (var reader = new StreamReader(req.InputStream, Encoding.UTF8)) {
                        string text = await reader.ReadToEndAsync();
                        Log($"💬 收到: {text}");
                        Application.Current.Dispatcher.Invoke(() => {
                            Clipboard.SetText(text);
                            Log("✨ 文字已同步至电脑剪贴板");
                        });
                    }
                } else {
                    // 接收文件
                    string encodedName = req.Headers["File-Name"];
                    string fileName = Encoding.UTF8.GetString(Convert.FromBase64String(encodedName));
                    Log($"📦 接收: {fileName}");
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "手机传来");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    string savePath = Path.Combine(folder, fileName);
                    using (var fs = new FileStream(savePath, FileMode.Create)) { await req.InputStream.CopyToAsync(fs); }
                    Log($"✅ 保存至桌面");
                    if (fileName.ToLower().EndsWith(".jpg") || fileName.ToLower().EndsWith(".png")) {
                        Application.Current.Dispatcher.Invoke(() => {
                            Clipboard.SetImage(new BitmapImage(new Uri(savePath)));
                            Log("✨ 图片已进入剪贴板");
                        });
                    }
                }
                byte[] success = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
                res.OutputStream.Write(success, 0, success.Length);
            } 
            else if (req.Url.AbsolutePath == "/poll") {
                string json = "{\"hasFile\": false}";
                lock (_outgoingQueue) {
                    if (_outgoingQueue.Count > 0) {
                        json = (string)_outgoingQueue[0];
                        _outgoingQueue.RemoveAt(0);
                    }
                }
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                res.OutputStream.Write(buffer, 0, buffer.Length);
            }
        } catch (Exception ex) { Log($"❌ 异常: {ex.Message}"); }
        finally { res.Close(); }
    }

    private static void SendTextToPhone(string content) {
        lock (_outgoingQueue) {
            string json = "{\"hasFile\": true, \"type\": \"text\", \"content\": \"" + content.Replace("\"", "\\\"").Replace("\n","\\n") + "\"}";
            _outgoingQueue.Add(json);
        }
    }

    private static void StopServer() {
        if (_cts != null) { _cts.Cancel(); _cts = null; }
        if (_listener != null) { try { _listener.Abort(); } catch { } _listener = null; }
    }
}
