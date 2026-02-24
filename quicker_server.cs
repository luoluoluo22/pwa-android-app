using System;
using System.Net;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Quicker.Public;

public class PCFileServer {
    private static HttpListener _listener;
    private static string OutgoingFileName = "";
    private static string OutgoingFileData = "";
    private static CancellationTokenSource _cts;
    private static TextBox _logBox;
    private static Window _window;

    public static void Exec(IStepContext context) {
        Application.Current.Dispatcher.Invoke(() => {
            try {
                if (_window != null) { try { _window.Close(); } catch { } }
                StopServer();

                _window = new Window {
                    Title = "极简传书 - 后端助手 (3001端口)",
                    Width = 550, Height = 450,
                    Topmost = true,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(25, 25, 30))
                };

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var header = new TextBlock { 
                    Text = "🟢 服务监听中 (端口 3001)... 关闭窗口即自动停止", 
                    Foreground = System.Windows.Media.Brushes.Gray, 
                    Margin = new Thickness(15, 10, 15, 10) 
                };
                
                _logBox = new TextBox {
                    IsReadOnly = true,
                    Background = System.Windows.Media.Brushes.Black,
                    Foreground = System.Windows.Media.Brushes.LimeGreen,
                    AcceptsReturn = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 13, Padding = new Thickness(10), Margin = new Thickness(10)
                };

                var btnSend = new Button { 
                    Content = "📤 发送文件到手机", Height = 40, Margin = new Thickness(10, 0, 10, 15),
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 100, 240)),
                    Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold
                };
                btnSend.Click += (s, e) => PrepareFileForPhone();

                Grid.SetRow(header, 0); grid.Children.Add(header);
                Grid.SetRow(_logBox, 1); grid.Children.Add(_logBox);
                Grid.SetRow(btnSend, 2); grid.Children.Add(btnSend);

                _window.Content = grid;
                _window.Closed += (s, e) => { StopServer(); };
                _window.Show();

                // 使用 3001 端口
                StartServerAsync(3001);
                Log("🚀 服务启动成功！");
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

    private static void StartServerAsync(int port) {
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{port}/");
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
        res.Headers.Add("Access-Control-Allow-Headers", "Content-Type, File-Name");
        res.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
        if (req.HttpMethod == "OPTIONS") { res.Close(); return; }

        try {
            if (req.Url.AbsolutePath == "/upload") {
                byte[] nameData = Convert.FromBase64String(req.Headers["File-Name"]);
                string fileName = Encoding.UTF8.GetString(nameData);
                Log($"📦 接收文件: {fileName}");
                
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "手机传来");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                string savePath = Path.Combine(folder, fileName);
                using (var fs = new FileStream(savePath, FileMode.Create)) {
                    await req.InputStream.CopyToAsync(fs);
                }
                Log($"✅ 已保存至桌面");

                _window.Dispatcher.Invoke(() => {
                    try {
                        var img = new System.Windows.Media.Imaging.BitmapImage(new Uri(savePath));
                        Clipboard.SetImage(img);
                        Log("✨ 图片已自动存入剪贴板");
                    } catch { }
                });
                
                byte[] success = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
                res.OutputStream.Write(success, 0, success.Length);
            } else if (req.Url.AbsolutePath == "/poll") {
                string json = "{\"hasFile\": false}";
                if (!string.IsNullOrEmpty(OutgoingFileName)) {
                    json = "{\"hasFile\": true, \"fileName\": \"" + OutgoingFileName + "\", \"fileData\": \"" + OutgoingFileData + "\"}";
                    Log($"📤 电脑文件已同步到手机");
                    OutgoingFileName = ""; OutgoingFileData = "";
                }
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                res.OutputStream.Write(buffer, 0, buffer.Length);
            }
        } catch (Exception ex) {
            Log($"❌ 异常: {ex.Message}");
        } finally {
            try { res.Close(); } catch { }
        }
    }

    private static void PrepareFileForPhone() {
        var dialog = new Microsoft.Win32.OpenFileDialog();
        if (dialog.ShowDialog() == true) {
            byte[] bytes = File.ReadAllBytes(dialog.FileName);
            string base64 = Convert.ToBase64String(bytes);
            OutgoingFileName = Path.GetFileName(dialog.FileName);
            OutgoingFileData = $"data:application/octet-stream;base64,{base64}";
            Log($"准备就绪: {OutgoingFileName}");
        }
    }

    private static void StopServer() {
        if (_cts != null) { _cts.Cancel(); _cts = null; }
        if (_listener != null) { try { _listener.Abort(); } catch { } _listener = null; }
    }
}
