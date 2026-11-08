using System;
using System.IO;
using System.Net;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace vLLM.Launcher
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public enum EngineMode
    {
        vLLM,
        Ollama
    }

    public class MainForm : Form
    {
        // UI Controls
        private Panel pnlHeader;
        private Label lblAppTitle;
        private Button btnEngineSwitch;
        private Label lblStatusBadge;

        private GroupBox grpControls;
        private Button btnStart;
        private Button btnStop;
        private Button btnRestart;
        private Label lblModel;
        private ComboBox cmbModels;

        private GroupBox grpEndpoints;
        private Label lblLocal;
        private TextBox txtLocalUrl;
        private Button btnCopyLocal;
        private Label lblLan;
        private TextBox txtLanUrl;
        private Button btnCopyLan;

        private GroupBox grpChat;
        private TextBox txtPrompt;
        private Button btnSend;
        private TextBox txtResponse;

        private Panel pnlFooter;
        private Button btnOpenFolder;
        private Button btnViewLogs;
        private Button btnHostSetup;
        private Button btnMinimizeTray;
        private Button btnExitApp;

        // Tray
        private NotifyIcon notifyIcon;
        private ContextMenuStrip trayMenu;
        private Timer healthTimer;

        // State
        private EngineMode currentEngine = EngineMode.vLLM;
        private bool isReady = false;
        private string rootDir;
        private string activeModel = "None";
        private int vllmPort = 8000;
        private int ollamaPort = 11434;
        private string cachedLanIp = "127.0.0.1";

        public MainForm()
        {
            rootDir = AppDomain.CurrentDomain.BaseDirectory;
            LoadConfig();
            cachedLanIp = DetectLanIp();

            InitializeAppIcon();
            InitializeComponent();
            InitializeTray();

            RefreshModelList();
            UpdateEndpointFields();
            UpdateEngineDisplay();

            healthTimer = new Timer();
            healthTimer.Interval = 3000;
            healthTimer.Tick += new EventHandler(OnHealthTimerTick);
            healthTimer.Start();

            CheckHealth();
        }

        private void InitializeAppIcon()
        {
            string iconPath = Path.Combine(rootDir, "icon.ico");
            if (File.Exists(iconPath))
            {
                try
                {
                    this.Icon = new Icon(iconPath);
                }
                catch { }
            }
            if (this.Icon == null)
            {
                try
                {
                    this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
                catch { }
            }
        }

        private void InitializeComponent()
        {
            this.Text = "Private LLM Launcher & Control Panel";
            this.Size = new Size(680, 720);
            this.MinimumSize = new Size(680, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(243, 244, 246);
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // 1. Header Panel
            pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 60;
            pnlHeader.BackColor = Color.FromArgb(31, 41, 55); // Solid Dark Slate
            this.Controls.Add(pnlHeader);

            lblAppTitle = new Label();
            lblAppTitle.Text = "PRIVATE LLM CONTROL PANEL";
            lblAppTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblAppTitle.ForeColor = Color.White;
            lblAppTitle.Location = new Point(16, 18);
            lblAppTitle.AutoSize = true;
            pnlHeader.Controls.Add(lblAppTitle);

            btnEngineSwitch = new Button();
            btnEngineSwitch.Text = "Engine: [ vLLM ] (Switch to Ollama)";
            btnEngineSwitch.Location = new Point(280, 14);
            btnEngineSwitch.Size = new Size(240, 32);
            btnEngineSwitch.FlatStyle = FlatStyle.Flat;
            btnEngineSwitch.BackColor = Color.FromArgb(55, 65, 81);
            btnEngineSwitch.ForeColor = Color.White;
            btnEngineSwitch.Click += new EventHandler(OnEngineSwitchClick);
            pnlHeader.Controls.Add(btnEngineSwitch);

            lblStatusBadge = new Label();
            lblStatusBadge.Text = "[ STOPPED ]";
            lblStatusBadge.Location = new Point(535, 14);
            lblStatusBadge.Size = new Size(110, 32);
            lblStatusBadge.TextAlign = ContentAlignment.MiddleCenter;
            lblStatusBadge.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblStatusBadge.BackColor = Color.FromArgb(107, 114, 128); // Solid Gray
            lblStatusBadge.ForeColor = Color.White;
            pnlHeader.Controls.Add(lblStatusBadge);

            // 2. Controls GroupBox
            grpControls = new GroupBox();
            grpControls.Text = "Engine Lifecycle & Model Selection";
            grpControls.Location = new Point(16, 75);
            grpControls.Size = new Size(632, 105);
            this.Controls.Add(grpControls);

            btnStart = new Button();
            btnStart.Text = "Start Engine";
            btnStart.Location = new Point(16, 26);
            btnStart.Size = new Size(115, 34);
            btnStart.FlatStyle = FlatStyle.System;
            btnStart.Click += new EventHandler(OnStartClick);
            grpControls.Controls.Add(btnStart);

            btnStop = new Button();
            btnStop.Text = "Stop Engine";
            btnStop.Location = new Point(140, 26);
            btnStop.Size = new Size(115, 34);
            btnStop.FlatStyle = FlatStyle.System;
            btnStop.Click += new EventHandler(OnStopClick);
            grpControls.Controls.Add(btnStop);

            btnRestart = new Button();
            btnRestart.Text = "Restart";
            btnRestart.Location = new Point(265, 26);
            btnRestart.Size = new Size(95, 34);
            btnRestart.FlatStyle = FlatStyle.System;
            btnRestart.Click += new EventHandler(OnRestartClick);
            grpControls.Controls.Add(btnRestart);

            lblModel = new Label();
            lblModel.Text = "Active Model:";
            lblModel.Location = new Point(16, 70);
            lblModel.Size = new Size(95, 22);
            grpControls.Controls.Add(lblModel);

            cmbModels = new ComboBox();
            cmbModels.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbModels.Location = new Point(115, 67);
            cmbModels.Size = new Size(500, 25);
            cmbModels.SelectedIndexChanged += new EventHandler(OnModelSelectionChanged);
            grpControls.Controls.Add(cmbModels);

            // 3. Endpoints GroupBox
            grpEndpoints = new GroupBox();
            grpEndpoints.Text = "API Endpoints (OpenAI Compatible /v1)";
            grpEndpoints.Location = new Point(16, 190);
            grpEndpoints.Size = new Size(632, 100);
            this.Controls.Add(grpEndpoints);

            lblLocal = new Label();
            lblLocal.Text = "Local URL:";
            lblLocal.Location = new Point(16, 28);
            lblLocal.Size = new Size(75, 20);
            grpEndpoints.Controls.Add(lblLocal);

            txtLocalUrl = new TextBox();
            txtLocalUrl.ReadOnly = true;
            txtLocalUrl.Location = new Point(95, 25);
            txtLocalUrl.Size = new Size(430, 25);
            txtLocalUrl.BackColor = Color.White;
            grpEndpoints.Controls.Add(txtLocalUrl);

            btnCopyLocal = new Button();
            btnCopyLocal.Text = "Copy";
            btnCopyLocal.Location = new Point(535, 24);
            btnCopyLocal.Size = new Size(80, 27);
            btnCopyLocal.FlatStyle = FlatStyle.System;
            btnCopyLocal.Click += delegate { CopyToClipboard(txtLocalUrl.Text); };
            grpEndpoints.Controls.Add(btnCopyLocal);

            lblLan = new Label();
            lblLan.Text = "LAN URL:";
            lblLan.Location = new Point(16, 62);
            lblLan.Size = new Size(75, 20);
            grpEndpoints.Controls.Add(lblLan);

            txtLanUrl = new TextBox();
            txtLanUrl.ReadOnly = true;
            txtLanUrl.Location = new Point(95, 59);
            txtLanUrl.Size = new Size(430, 25);
            txtLanUrl.BackColor = Color.White;
            grpEndpoints.Controls.Add(txtLanUrl);

            btnCopyLan = new Button();
            btnCopyLan.Text = "Copy";
            btnCopyLan.Location = new Point(535, 58);
            btnCopyLan.Size = new Size(80, 27);
            btnCopyLan.FlatStyle = FlatStyle.System;
            btnCopyLan.Click += delegate { CopyToClipboard(txtLanUrl.Text); };
            grpEndpoints.Controls.Add(btnCopyLan);

            // 4. Quick Test Chat GroupBox
            grpChat = new GroupBox();
            grpChat.Text = "Quick Test Chat (CLI Inferensi)";
            grpChat.Location = new Point(16, 300);
            grpChat.Size = new Size(632, 310);
            this.Controls.Add(grpChat);

            txtPrompt = new TextBox();
            txtPrompt.Location = new Point(16, 26);
            txtPrompt.Size = new Size(505, 26);
            txtPrompt.Text = "Halo, jelaskan secara singkat keunggulan inferensi model privat.";
            grpChat.Controls.Add(txtPrompt);

            btnSend = new Button();
            btnSend.Text = "Send";
            btnSend.Location = new Point(530, 24);
            btnSend.Size = new Size(86, 29);
            btnSend.FlatStyle = FlatStyle.System;
            btnSend.Click += new EventHandler(OnSendChatClick);
            grpChat.Controls.Add(btnSend);

            txtResponse = new TextBox();
            txtResponse.Location = new Point(16, 65);
            txtResponse.Size = new Size(600, 230);
            txtResponse.Multiline = true;
            txtResponse.ScrollBars = ScrollBars.Vertical;
            txtResponse.ReadOnly = true;
            txtResponse.BackColor = Color.White;
            txtResponse.Font = new Font("Consolas", 9.5f, FontStyle.Regular);
            txtResponse.Text = "Klik tombol 'Send' untuk menguji respons server.";
            grpChat.Controls.Add(txtResponse);

            // 5. Footer Panel
            pnlFooter = new Panel();
            pnlFooter.Dock = DockStyle.Bottom;
            pnlFooter.Height = 55;
            pnlFooter.BackColor = Color.FromArgb(229, 231, 235);
            this.Controls.Add(pnlFooter);

            btnOpenFolder = new Button();
            btnOpenFolder.Text = "Open Models";
            btnOpenFolder.Location = new Point(16, 12);
            btnOpenFolder.Size = new Size(110, 32);
            btnOpenFolder.FlatStyle = FlatStyle.System;
            btnOpenFolder.Click += new EventHandler(OnOpenModelsClick);
            pnlFooter.Controls.Add(btnOpenFolder);

            btnViewLogs = new Button();
            btnViewLogs.Text = "View Logs";
            btnViewLogs.Location = new Point(135, 12);
            btnViewLogs.Size = new Size(100, 32);
            btnViewLogs.FlatStyle = FlatStyle.System;
            btnViewLogs.Click += new EventHandler(OnViewLogsClick);
            pnlFooter.Controls.Add(btnViewLogs);

            btnHostSetup = new Button();
            btnHostSetup.Text = "Host & Firewall Setup";
            btnHostSetup.Location = new Point(245, 12);
            btnHostSetup.Size = new Size(150, 32);
            btnHostSetup.FlatStyle = FlatStyle.System;
            btnHostSetup.Click += new EventHandler(OnHostSetupClick);
            pnlFooter.Controls.Add(btnHostSetup);

            btnMinimizeTray = new Button();
            btnMinimizeTray.Text = "Minimize to Tray";
            btnMinimizeTray.Location = new Point(410, 12);
            btnMinimizeTray.Size = new Size(125, 32);
            btnMinimizeTray.FlatStyle = FlatStyle.System;
            btnMinimizeTray.Click += delegate { this.Hide(); notifyIcon.ShowBalloonTip(1500, "Launcher Standby", "Aplikasi berjalan di System Tray.", ToolTipIcon.Info); };
            pnlFooter.Controls.Add(btnMinimizeTray);

            btnExitApp = new Button();
            btnExitApp.Text = "Exit";
            btnExitApp.Location = new Point(545, 12);
            btnExitApp.Size = new Size(85, 32);
            btnExitApp.FlatStyle = FlatStyle.System;
            btnExitApp.Click += new EventHandler(OnExitClick);
            pnlFooter.Controls.Add(btnExitApp);
        }

        private void InitializeTray()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.RenderMode = ToolStripRenderMode.System;

            trayMenu.Items.Add(new ToolStripMenuItem("Open Dashboard", null, delegate { RestoreWindow(); }));
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(new ToolStripMenuItem("Start Engine", null, new EventHandler(OnStartClick)));
            trayMenu.Items.Add(new ToolStripMenuItem("Stop Engine", null, new EventHandler(OnStopClick)));
            trayMenu.Items.Add(new ToolStripMenuItem("Restart Engine", null, new EventHandler(OnRestartClick)));
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(new ToolStripMenuItem("Copy LAN URL", null, delegate { CopyToClipboard(txtLanUrl.Text); }));
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(new ToolStripMenuItem("Exit Application", null, new EventHandler(OnExitClick)));

            notifyIcon = new NotifyIcon();
            notifyIcon.Text = "Private LLM Launcher";
            notifyIcon.Icon = this.Icon;
            notifyIcon.ContextMenuStrip = trayMenu;
            notifyIcon.Visible = true;

            notifyIcon.DoubleClick += delegate { RestoreWindow(); };
            notifyIcon.Click += delegate(object s, EventArgs e)
            {
                MouseEventArgs me = e as MouseEventArgs;
                if (me != null && me.Button == MouseButtons.Left)
                {
                    RestoreWindow();
                }
            };
        }

        private void RestoreWindow()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
            this.Activate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                notifyIcon.ShowBalloonTip(1500, "Launcher Standby", "Aplikasi diminimalkan ke System Tray. Klik icon untuk membuka.", ToolTipIcon.Info);
            }
            base.OnFormClosing(e);
        }

        private void OnEngineSwitchClick(object sender, EventArgs e)
        {
            if (currentEngine == EngineMode.vLLM)
            {
                currentEngine = EngineMode.Ollama;
            }
            else
            {
                currentEngine = EngineMode.vLLM;
            }

            UpdateEngineDisplay();
            UpdateEndpointFields();
            CheckHealth();
        }

        private void UpdateEngineDisplay()
        {
            if (currentEngine == EngineMode.vLLM)
            {
                btnEngineSwitch.Text = "Engine: [ vLLM ] (Switch to Ollama)";
                this.Text = "Private LLM Control Panel - Engine: vLLM (Port " + vllmPort + ")";
                notifyIcon.Text = "Private LLM (vLLM)";
            }
            else
            {
                btnEngineSwitch.Text = "Engine: [ Ollama ] (Switch to vLLM)";
                this.Text = "Private LLM Control Panel - Engine: Ollama (Port " + ollamaPort + ")";
                notifyIcon.Text = "Private LLM (Ollama)";
            }
        }

        private void UpdateEndpointFields()
        {
            int port = (currentEngine == EngineMode.vLLM) ? vllmPort : ollamaPort;
            txtLocalUrl.Text = string.Format("http://localhost:{0}/v1", port);
            txtLanUrl.Text = string.Format("http://{0}:{1}/v1", cachedLanIp, port);
        }

        private void OnStartClick(object sender, EventArgs e)
        {
            SetStatusDisplay("STARTING", Color.FromArgb(245, 158, 11)); // Solid Amber

            if (currentEngine == EngineMode.vLLM)
            {
                string script = Path.Combine(rootDir, "start.bat");
                if (File.Exists(script))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c \"" + script + "\"",
                        WorkingDirectory = rootDir,
                        WindowStyle = ProcessWindowStyle.Normal
                    });
                }
            }
            else
            {
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
                string script = Path.Combine(rootDir, "stop.bat");
                if (File.Exists(script))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c \"" + script + "\"",
                        WorkingDirectory = rootDir,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                }
            }
            else
            {
                foreach (Process p in Process.GetProcessesByName("ollama"))
                {
                    try { p.Kill(); } catch { }
                }
            }

            SetStatusDisplay("STOPPED", Color.FromArgb(107, 114, 128)); // Solid Gray
            isReady = false;
        }

        private void OnRestartClick(object sender, EventArgs e)
        {
            OnStopClick(sender, e);
            System.Threading.Thread.Sleep(2000);
            OnStartClick(sender, e);
        }

        private void OnHealthTimerTick(object sender, EventArgs e)
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
                        if (!isReady)
                        {
                            isReady = true;
                            notifyIcon.ShowBalloonTip(2000, "Engine Ready",
                                string.Format("Server {0} siap melayani request di port {1}", currentEngine, port),
                                ToolTipIcon.Info);
                        }
                        SetStatusDisplay("READY", Color.FromArgb(16, 185, 129)); // Solid Green
                        return;
                    }
                }
            }
            catch { }

            isReady = false;
            SetStatusDisplay("STOPPED", Color.FromArgb(107, 114, 128)); // Solid Gray
        }

        private void SetStatusDisplay(string text, Color color)
        {
            lblStatusBadge.Text = "[ " + text + " ]";
            lblStatusBadge.BackColor = color;
        }

        private void RefreshModelList()
        {
            cmbModels.Items.Clear();
            string modelsDir = Path.Combine(rootDir, "models");

            if (Directory.Exists(modelsDir))
            {
                string[] subDirs = Directory.GetDirectories(modelsDir);
                foreach (string dir in subDirs)
                {
                    string name = Path.GetFileName(dir);
                    cmbModels.Items.Add(name);
                }
            }

            if (cmbModels.Items.Count > 0)
            {
                int idx = cmbModels.FindStringExact(activeModel);
                cmbModels.SelectedIndex = (idx >= 0) ? idx : 0;
            }
            else
            {
                cmbModels.Items.Add("(No models in ./models/)");
                cmbModels.SelectedIndex = 0;
            }
        }

        private void OnModelSelectionChanged(object sender, EventArgs e)
        {
            string selected = cmbModels.SelectedItem as string;
            if (string.IsNullOrEmpty(selected) || selected.StartsWith("(")) return;

            if (!selected.Equals(activeModel, StringComparison.OrdinalIgnoreCase))
            {
                activeModel = selected;
                SaveModelToEnv(selected);
            }
        }

        private void SaveModelToEnv(string modelName)
        {
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
        }

        private void OnSendChatClick(object sender, EventArgs e)
        {
            string prompt = txtPrompt.Text.Trim();
            if (string.IsNullOrEmpty(prompt)) return;

            btnSend.Enabled = false;
            txtResponse.Text = "Sending request to " + txtLocalUrl.Text + "...\r\n";

            int port = (currentEngine == EngineMode.vLLM) ? vllmPort : ollamaPort;
            string url = string.Format("http://127.0.0.1:{0}/v1/chat/completions", port);
            string jsonPayload = "{\"model\":\"" + activeModel + "\",\"messages\":[{\"role\":\"user\",\"content\":\"" + EscapeJson(prompt) + "\"}],\"max_tokens\":256}";

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
                    string content = ExtractJsonContent(json);
                    txtResponse.Text = string.Format("[Latency: {0:F2}s]\r\n\r\n{1}", sw.Elapsed.TotalSeconds, content);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                txtResponse.Text = string.Format("[Error after {0:F2}s]: {1}\r\n\r\nPastikan engine sudah berstatus READY.", sw.Elapsed.TotalSeconds, ex.Message);
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

        private string ExtractJsonContent(string json)
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

        private void CopyToClipboard(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
                notifyIcon.ShowBalloonTip(1200, "Copied", text, ToolTipIcon.Info);
            }
        }

        private void OnOpenModelsClick(object sender, EventArgs e)
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

        private void OnExitClick(object sender, EventArgs e)
        {
            healthTimer.Stop();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            Application.Exit();
        }

        private void LoadConfig()
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

        private string DetectLanIp()
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
    }
}
