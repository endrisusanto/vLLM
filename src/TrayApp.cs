using System;
using System.IO;
using System.Net;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace vLLM.Tray
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayContext());
        }
    }

    public enum EngineMode
    {
        vLLM,
        Ollama
    }

    public class TrayContext : ApplicationContext
    {
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;
        private ToolStripMenuItem statusMenuItem;
        private ToolStripMenuItem engineSwitchMenuItem;
        private ToolStripMenuItem startMenuItem;
        private ToolStripMenuItem stopMenuItem;
        private ToolStripMenuItem restartMenuItem;
        private ToolStripMenuItem modelsMenu;
        private Timer healthTimer;

        private EngineMode currentEngine = EngineMode.vLLM;
        private bool isRunning = false;
        private string activeModel = "None";
        private int vllmPort = 8000;
        private int ollamaPort = 11434;
        private string rootDir;
        private Icon baseAppIcon = null;

        public TrayContext()
        {
            rootDir = AppDomain.CurrentDomain.BaseDirectory;
            LoadConfiguration();
            LoadBaseIcon();

            InitializeComponents();
            UpdateIcon(Color.FromArgb(107, 114, 128));

            healthTimer = new Timer();
            healthTimer.Interval = 3000;
            healthTimer.Tick += new EventHandler(OnHealthCheckTick);
            healthTimer.Start();

            CheckHealth();
        }

        private void LoadBaseIcon()
        {
            string iconPath = Path.Combine(rootDir, "icon.ico");
            if (File.Exists(iconPath))
            {
                try
                {
                    baseAppIcon = new Icon(iconPath, 16, 16);
                }
                catch { }
            }

            if (baseAppIcon == null)
            {
                try
                {
                    baseAppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
                catch { }
            }
        }

        private void LoadConfiguration()
        {
            string envPath = Path.Combine(rootDir, ".env");
            if (File.Exists(envPath))
            {
                string[] lines = File.ReadAllLines(envPath);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("MODEL_NAME="))
                    {
                        activeModel = trimmed.Substring("MODEL_NAME=".Length).Trim();
                    }
                    else if (trimmed.StartsWith("HOST_PORT="))
                    {
                        int p;
                        if (int.TryParse(trimmed.Substring("HOST_PORT=".Length).Trim(), out p))
                        {
                            vllmPort = p;
                        }
                    }
                }
            }
        }

        private void InitializeComponents()
        {
            contextMenu = new ContextMenuStrip();
            contextMenu.RenderMode = ToolStripRenderMode.System;

            // 1. Status Info Header
            statusMenuItem = new ToolStripMenuItem();
            statusMenuItem.Text = "[Status: Checking...] Engine: vLLM";
            statusMenuItem.Enabled = false;
            contextMenu.Items.Add(statusMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // 2. Engine Mode Switch Button
            engineSwitchMenuItem = new ToolStripMenuItem();
            engineSwitchMenuItem.Text = "Switch Engine: Mode [vLLM] -> [Ollama]";
            engineSwitchMenuItem.Click += new EventHandler(OnEngineSwitchClick);
            contextMenu.Items.Add(engineSwitchMenuItem);

            // 3. Lifecycle Controls
            startMenuItem = new ToolStripMenuItem("Start Engine", null, new EventHandler(OnStartClick));
            stopMenuItem = new ToolStripMenuItem("Stop Engine", null, new EventHandler(OnStopClick));
            restartMenuItem = new ToolStripMenuItem("Restart Engine", null, new EventHandler(OnRestartClick));

            contextMenu.Items.Add(startMenuItem);
            contextMenu.Items.Add(stopMenuItem);
            contextMenu.Items.Add(restartMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // 4. Models Submenu
            modelsMenu = new ToolStripMenuItem("Select Model");
            modelsMenu.DropDownOpening += new EventHandler(OnModelsMenuOpening);
            contextMenu.Items.Add(modelsMenu);

            // 5. Utility Actions
            contextMenu.Items.Add(new ToolStripMenuItem("Copy LAN Endpoint", null, new EventHandler(OnCopyLanUrlClick)));
            contextMenu.Items.Add(new ToolStripMenuItem("Copy Local Endpoint", null, new EventHandler(OnCopyLocalUrlClick)));
            contextMenu.Items.Add(new ToolStripMenuItem("Quick Test Chat", null, new EventHandler(OnQuickChatClick)));
            contextMenu.Items.Add(new ToolStripMenuItem("Open Models Folder", null, new EventHandler(OnOpenModelsFolderClick)));
            contextMenu.Items.Add(new ToolStripMenuItem("View Live Logs", null, new EventHandler(OnViewLogsClick)));
            contextMenu.Items.Add(new ToolStripMenuItem("Host Setup (Firewall & WSL)", null, new EventHandler(OnHostSetupClick)));

            contextMenu.Items.Add(new ToolStripSeparator());

            // 6. Exit
            contextMenu.Items.Add(new ToolStripMenuItem("Exit Tray App", null, new EventHandler(OnExitClick)));

            notifyIcon = new NotifyIcon();
            notifyIcon.Text = "Private LLM Launcher (vLLM)";
            notifyIcon.ContextMenuStrip = contextMenu;
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += new EventHandler(OnQuickChatClick);
        }

        private void OnEngineSwitchClick(object sender, EventArgs e)
        {
            if (currentEngine == EngineMode.vLLM)
            {
                currentEngine = EngineMode.Ollama;
                engineSwitchMenuItem.Text = "Switch Engine: Mode [Ollama] -> [vLLM]";
                notifyIcon.Text = "Private LLM Launcher (Ollama)";
            }
            else
            {
                currentEngine = EngineMode.vLLM;
                engineSwitchMenuItem.Text = "Switch Engine: Mode [vLLM] -> [Ollama]";
                notifyIcon.Text = "Private LLM Launcher (vLLM)";
            }

            CheckHealth();
        }

        private void OnStartClick(object sender, EventArgs e)
        {
            UpdateIcon(Color.FromArgb(245, 158, 11)); // Solid Amber / Yellow
            statusMenuItem.Text = string.Format("[Status: Starting...] Engine: {0}", currentEngine);

            if (currentEngine == EngineMode.vLLM)
            {
                string startScript = Path.Combine(rootDir, "start.bat");
                if (File.Exists(startScript))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c \"" + startScript + "\"",
                        WorkingDirectory = rootDir,
                        WindowStyle = ProcessWindowStyle.Normal
                    });
                }
            }
            else
            {
                // Ollama Start
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c ollama serve",
                    WindowStyle = ProcessWindowStyle.Minimized
                });
            }
        }

        private void OnStopClick(object sender, EventArgs e)
        {
            if (currentEngine == EngineMode.vLLM)
            {
                string stopScript = Path.Combine(rootDir, "stop.bat");
                if (File.Exists(stopScript))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c \"" + stopScript + "\"",
                        WorkingDirectory = rootDir,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                }
            }
            else
            {
                // Stop Ollama process
                foreach (Process proc in Process.GetProcessesByName("ollama"))
                {
                    try { proc.Kill(); } catch { }
                }
            }

            UpdateIcon(Color.FromArgb(107, 114, 128)); // Solid Gray
            statusMenuItem.Text = string.Format("[Status: Stopped] Engine: {0}", currentEngine);
            isRunning = false;
        }

        private void OnRestartClick(object sender, EventArgs e)
        {
            OnStopClick(sender, e);
            System.Threading.Thread.Sleep(2000);
            OnStartClick(sender, e);
        }

        private void OnHealthCheckTick(object sender, EventArgs e)
        {
            CheckHealth();
        }

        private void CheckHealth()
        {
            int port = (currentEngine == EngineMode.vLLM) ? vllmPort : ollamaPort;
            string url = (currentEngine == EngineMode.vLLM)
                ? string.Format("http://127.0.0.1:{0}/health", port)
                : string.Format("http://127.0.0.1:{0}/api/version", port);

            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 1500;
                req.Method = "GET";

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    if (resp.StatusCode == HttpStatusCode.OK)
                    {
                        if (!isRunning)
                        {
                            isRunning = true;
                            notifyIcon.ShowBalloonTip(2000, "LLM Engine Ready",
                                string.Format("Engine {0} is online on port {1}", currentEngine, port),
                                ToolTipIcon.Info);
                        }
                        UpdateIcon(Color.FromArgb(16, 185, 129)); // Solid Green
                        statusMenuItem.Text = string.Format("[Status: Ready] Engine: {0} | Port: {1}", currentEngine, port);
                        return;
                    }
                }
            }
            catch
            {
                // Engine unreachable
            }

            if (isRunning)
            {
                isRunning = false;
            }
            UpdateIcon(Color.FromArgb(107, 114, 128)); // Solid Gray
            statusMenuItem.Text = string.Format("[Status: Stopped] Engine: {0} | Port: {1}", currentEngine, port);
        }

        private void OnModelsMenuOpening(object sender, EventArgs e)
        {
            modelsMenu.DropDownItems.Clear();
            string modelsDir = Path.Combine(rootDir, "models");

            if (Directory.Exists(modelsDir))
            {
                string[] subDirs = Directory.GetDirectories(modelsDir);
                if (subDirs.Length > 0)
                {
                    foreach (string dir in subDirs)
                    {
                        string dirName = Path.GetFileName(dir);
                        ToolStripMenuItem item = new ToolStripMenuItem(dirName);
                        if (dirName.Equals(activeModel, StringComparison.OrdinalIgnoreCase))
                        {
                            item.Checked = true;
                        }
                        item.Click += delegate
                        {
                            SetActiveModel(dirName);
                        };
                        modelsMenu.DropDownItems.Add(item);
                    }
                }
                else
                {
                    ToolStripMenuItem emptyItem = new ToolStripMenuItem("(No models in ./models/)");
                    emptyItem.Enabled = false;
                    modelsMenu.DropDownItems.Add(emptyItem);
                }
            }
        }

        private void SetActiveModel(string modelName)
        {
            activeModel = modelName;
            string envPath = Path.Combine(rootDir, ".env");
            if (File.Exists(envPath))
            {
                string[] lines = File.ReadAllLines(envPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Trim().StartsWith("MODEL_NAME="))
                    {
                        lines[i] = "MODEL_NAME=" + modelName;
                        break;
                    }
                }
                File.WriteAllLines(envPath, lines, Encoding.UTF8);
            }

            MessageBox.Show(string.Format("Model set to: {0}\nRestart engine to apply.", modelName),
                "Model Switched", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string GetLocalLanIp()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                     ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
                {
                    string name = ni.Name.ToLower();
                    if (name.Contains("vethernet") || name.Contains("wsl") || name.Contains("docker"))
                        continue;

                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(ip.Address))
                        {
                            return ip.Address.ToString();
                        }
                    }
                }
            }
            return "127.0.0.1";
        }

        private void OnCopyLanUrlClick(object sender, EventArgs e)
        {
            int port = (currentEngine == EngineMode.vLLM) ? vllmPort : ollamaPort;
            string lanIp = GetLocalLanIp();
            string url = string.Format("http://{0}:{1}/v1", lanIp, port);
            Clipboard.SetText(url);
            notifyIcon.ShowBalloonTip(1500, "URL Copied", url, ToolTipIcon.Info);
        }

        private void OnCopyLocalUrlClick(object sender, EventArgs e)
        {
            int port = (currentEngine == EngineMode.vLLM) ? vllmPort : ollamaPort;
            string url = string.Format("http://127.0.0.1:{0}/v1", port);
            Clipboard.SetText(url);
            notifyIcon.ShowBalloonTip(1500, "URL Copied", url, ToolTipIcon.Info);
        }

        private void OnOpenModelsFolderClick(object sender, EventArgs e)
        {
            string path = Path.Combine(rootDir, "models");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start("explorer.exe", path);
        }

        private void OnViewLogsClick(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/k docker compose logs -f vllm",
                WorkingDirectory = rootDir,
                WindowStyle = ProcessWindowStyle.Normal
            });
        }

        private void OnHostSetupClick(object sender, EventArgs e)
        {
            string script = Path.Combine(rootDir, "setup.bat");
            if (File.Exists(script))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = script,
                    WorkingDirectory = rootDir,
                    Verb = "runas"
                });
            }
        }

        private void OnQuickChatClick(object sender, EventArgs e)
        {
            int port = (currentEngine == EngineMode.vLLM) ? vllmPort : ollamaPort;
            using (ChatForm form = new ChatForm(port, currentEngine.ToString(), activeModel))
            {
                form.ShowDialog();
            }
        }

        private void OnExitClick(object sender, EventArgs e)
        {
            healthTimer.Stop();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            Application.Exit();
        }

        private void UpdateIcon(Color statusColor)
        {
            using (Bitmap bmp = new Bitmap(16, 16))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                if (baseAppIcon != null)
                {
                    g.DrawIcon(baseAppIcon, new Rectangle(0, 0, 16, 16));

                    // Solid status badge on bottom right (no gradients)
                    using (SolidBrush dotBrush = new SolidBrush(statusColor))
                    {
                        g.FillEllipse(dotBrush, 10, 10, 6, 6);
                    }
                    using (Pen borderPen = new Pen(Color.FromArgb(20, 20, 20), 1))
                    {
                        g.DrawEllipse(borderPen, 10, 10, 5, 5);
                    }
                }
                else
                {
                    using (SolidBrush brush = new SolidBrush(statusColor))
                    {
                        g.FillEllipse(brush, 1, 1, 14, 14);
                    }
                    using (Pen borderPen = new Pen(Color.FromArgb(40, 40, 40), 1))
                    {
                        g.DrawEllipse(borderPen, 1, 1, 13, 13);
                    }
                }

                IntPtr hIcon = bmp.GetHicon();
                if (notifyIcon.Icon != null)
                {
                    DestroyIcon(notifyIcon.Icon.Handle);
                    notifyIcon.Icon.Dispose();
                }
                notifyIcon.Icon = Icon.FromHandle(hIcon);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);
    }

    // Flat UI Quick Chat Form - Zero Gradients, Zero Emojis
    public class ChatForm : Form
    {
        private TextBox txtPrompt;
        private TextBox txtOutput;
        private Button btnSend;
        private Label lblHeader;
        private int targetPort;
        private string engineName;
        private string modelName;

        public ChatForm(int port, string engine, string model)
        {
            targetPort = port;
            engineName = engine;
            modelName = model;

            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Text = "Quick Test Chat - " + engineName;
            this.Size = new Size(580, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(245, 245, 245);
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            if (File.Exists(iconPath))
            {
                try { this.Icon = new Icon(iconPath); } catch { }
            }

            lblHeader = new Label();
            lblHeader.Text = string.Format("Target: http://127.0.0.1:{0}/v1 | Engine: {1} | Model: {2}", targetPort, engineName, modelName);
            lblHeader.Location = new Point(16, 12);
            lblHeader.Size = new Size(540, 24);
            lblHeader.ForeColor = Color.FromArgb(50, 50, 50);
            this.Controls.Add(lblHeader);

            txtPrompt = new TextBox();
            txtPrompt.Location = new Point(16, 40);
            txtPrompt.Size = new Size(430, 26);
            txtPrompt.Text = "Halo, jelaskan secara singkat keunggulan inferensi model privat.";
            this.Controls.Add(txtPrompt);

            btnSend = new Button();
            btnSend.Text = "Send";
            btnSend.Location = new Point(454, 38);
            btnSend.Size = new Size(95, 30);
            btnSend.FlatStyle = FlatStyle.System;
            btnSend.Click += new EventHandler(OnSendClick);
            this.Controls.Add(btnSend);

            txtOutput = new TextBox();
            txtOutput.Location = new Point(16, 80);
            txtOutput.Size = new Size(532, 340);
            txtOutput.Multiline = true;
            txtOutput.ScrollBars = ScrollBars.Vertical;
            txtOutput.ReadOnly = true;
            txtOutput.BackColor = Color.FromArgb(255, 255, 255);
            txtOutput.Font = new Font("Consolas", 9.5f, FontStyle.Regular);
            txtOutput.ForeColor = Color.FromArgb(20, 20, 20);
            this.Controls.Add(txtOutput);
        }

        private void OnSendClick(object sender, EventArgs e)
        {
            string prompt = txtPrompt.Text.Trim();
            if (string.IsNullOrEmpty(prompt)) return;

            btnSend.Enabled = false;
            txtOutput.Text = "Connecting to endpoint and generating response...\r\n";

            string url = string.Format("http://127.0.0.1:{0}/v1/chat/completions", targetPort);
            string jsonPayload = "{\"model\":\"" + modelName + "\",\"messages\":[{\"role\":\"user\",\"content\":\"" + EscapeJson(prompt) + "\"}],\"max_tokens\":256}";

            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Timeout = 60000;

                byte[] data = Encoding.UTF8.GetBytes(jsonPayload);
                req.ContentLength = data.Length;

                using (Stream stream = req.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    sw.Stop();
                    string json = reader.ReadToEnd();
                    string answer = ExtractContent(json);

                    txtOutput.Text = string.Format("[Latency: {0:F2}s]\r\n\r\n{1}", sw.Elapsed.TotalSeconds, answer);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                txtOutput.Text = string.Format("[Error after {0:F2}s]: {1}\r\n\r\nPastikan engine sudah berstatus Ready.", sw.Elapsed.TotalSeconds, ex.Message);
            }
            finally
            {
                btnSend.Enabled = true;
            }
        }

        private string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }

        private string ExtractContent(string json)
        {
            int idx = json.IndexOf("\"content\":");
            if (idx == -1) return json;

            int start = json.IndexOf("\"", idx + 10);
            if (start == -1) return json;

            int end = start + 1;
            while (end < json.Length)
            {
                if (json[end] == '"' && json[end - 1] != '\\')
                    break;
                end++;
            }

            if (end > start)
            {
                string raw = json.Substring(start + 1, end - start - 1);
                return raw.Replace("\\n", "\r\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
            }
            return json;
        }
    }
}
