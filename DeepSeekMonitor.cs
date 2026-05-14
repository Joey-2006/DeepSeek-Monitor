using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Timers;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace DeepSeekMonitor
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApp());
        }
    }

    class TrayApp : Form
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private System.Timers.Timer refreshTimer;
        private string apiKey = "";
        private decimal balance = 0;
        private string currency = "CNY";
        private long totalTokens = 0;
        private string configPath;

        public TrayApp()
        {
            configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeepSeekMonitor", "config.txt"
            );
            LoadConfig();

            trayIcon = new NotifyIcon();
            trayIcon.Text = "DeepSeek";
            trayIcon.Icon = CreateIcon();
            trayIcon.Visible = true;

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Refresh Now", null, OnRefresh);
            trayMenu.Items.Add("Set API Key", null, OnSettings);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Exit", null, OnExit);
            trayIcon.ContextMenuStrip = trayMenu;

            trayIcon.Click += OnTrayClick;

            refreshTimer = new System.Timers.Timer(60000);
            refreshTimer.Elapsed += OnTimer;
            refreshTimer.AutoReset = true;
            refreshTimer.Start();

            if (!string.IsNullOrEmpty(apiKey))
                RefreshData();

            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    foreach (var line in File.ReadAllLines(configPath))
                    {
                        if (line.StartsWith("api_key="))
                            apiKey = line.Substring(8).Trim();
                    }
                }
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                File.WriteAllText(configPath, "api_key=" + apiKey + "\n");
            }
            catch { }
        }

        private void OnTrayClick(object sender, EventArgs e)
        {
            if (e is MouseEventArgs)
            {
                MouseEventArgs me = (MouseEventArgs)e;
                if (me.Button == MouseButtons.Left)
                    ShowDetailPopup();
            }
        }

        private void OnRefresh(object sender, EventArgs e) { RefreshData(); }
        private void OnSettings(object sender, EventArgs e) { ShowSettings(); }
        private void OnExit(object sender, EventArgs e)
        {
            refreshTimer.Stop();
            trayIcon.Visible = false;
            Application.Exit();
        }
        private void OnTimer(object sender, ElapsedEventArgs e) { RefreshData(); }

        private void ShowSettings()
        {
            string input = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter your DeepSeek API Key:\n(saved locally, never uploaded)",
                "DeepSeek Monitor Setup",
                apiKey
            );
            if (!string.IsNullOrEmpty(input) && input != apiKey)
            {
                apiKey = input.Trim();
                SaveConfig();
                RefreshData();
            }
        }

        private void RefreshData()
        {
            try
            {
                if (string.IsNullOrEmpty(apiKey)) return;
                FetchBalance();
                UpdateTray();
            }
            catch (Exception ex)
            {
                trayIcon.Text = "Err";
            }
        }

        private void FetchBalance()
        {
            try { WebRequest.DefaultWebProxy = new WebProxy("127.0.0.1", 7897); } catch {}
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://api.deepseek.com/user/balance");
                req.Method = "GET";
                req.Headers["Authorization"] = "Bearer " + apiKey;
                req.Timeout = 10000;
                req.UserAgent = "DeepSeekMonitor/1.0";

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                {
                    string json = reader.ReadToEnd();
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    Dictionary<string, object> data = js.Deserialize<Dictionary<string, object>>(json);
                    if (data.ContainsKey("balance_infos"))
                    {
                        var arr = data["balance_infos"] as System.Collections.ArrayList;
                        if (arr != null && arr.Count > 0)
                        {
                            var info = arr[0] as Dictionary<string, object>;
                            if (info != null)
                            {
                                if (info.ContainsKey("total_balance"))
                                    balance = Convert.ToDecimal(info["total_balance"]);
                                if (info.ContainsKey("currency"))
                                    currency = info["currency"].ToString();
                            }
                        }
                    }
                }

                try
                {
                    string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                    HttpWebRequest usageReq = (HttpWebRequest)WebRequest.Create(
                        "https://api.deepseek.com/dashboard/billing/usage?start_date=" + today + "&end_date=" + today);
                    usageReq.Method = "GET";
                    usageReq.Headers["Authorization"] = "Bearer " + apiKey;
                    usageReq.Timeout = 10000;
                    usageReq.UserAgent = "DeepSeekMonitor/1.0";

                    using (HttpWebResponse usageResp = (HttpWebResponse)usageReq.GetResponse())
                    using (StreamReader usageReader = new StreamReader(usageResp.GetResponseStream()))
                    {
                        string usageJson = usageReader.ReadToEnd();
                        JavaScriptSerializer js = new JavaScriptSerializer();
                        Dictionary<string, object> usageData = js.Deserialize<Dictionary<string, object>>(usageJson);
                        if (usageData.ContainsKey("total_usage"))
                            totalTokens = Convert.ToInt64(Convert.ToDouble(usageData["total_usage"]));
                    }
                }
                catch { }
            }
            catch (WebException)
            {
                HttpWebRequest altReq = (HttpWebRequest)WebRequest.Create("https://api.deepseek.com/dashboard/balance");
                altReq.Method = "GET";
                altReq.Headers["Authorization"] = "Bearer " + apiKey;
                altReq.Timeout = 10000;
                altReq.UserAgent = "DeepSeekMonitor/1.0";

                using (HttpWebResponse altResp = (HttpWebResponse)altReq.GetResponse())
                using (StreamReader altReader = new StreamReader(altResp.GetResponseStream()))
                {
                    string json = altReader.ReadToEnd();
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    Dictionary<string, object> data = js.Deserialize<Dictionary<string, object>>(json);
                    if (data.ContainsKey("balance_infos"))
                    {
                        var arr = data["balance_infos"] as System.Collections.ArrayList;
                        if (arr != null && arr.Count > 0)
                        {
                            var info = arr[0] as Dictionary<string, object>;
                            if (info != null)
                            {
                                if (info.ContainsKey("total_balance"))
                                    balance = Convert.ToDecimal(info["total_balance"]);
                                if (info.ContainsKey("currency"))
                                    currency = info["currency"].ToString();
                            }
                        }
                    }
                }
            }
        }

        private void UpdateTray()
        {
            string txt = "$" + balance.ToString("F2") + " " + FormatTokens(totalTokens);
            if (txt.Length > 63) txt = txt.Substring(0, 63);
            trayIcon.Text = txt;
        }

        private string FormatTokens(long tokens)
        {
            if (tokens < 0) return "N/A";
            if (tokens < 1000) return tokens + " tokens";
            if (tokens < 1000000) return (tokens / 1000.0).ToString("F1") + "K";
            return (tokens / 1000000.0).ToString("F2") + "M";
        }

        private void ShowDetailPopup()
        {
            string msg = "Balance: $" + balance.ToString("F2") + " " + currency + "\n"
                + "Today's Usage: " + FormatTokens(totalTokens) + "\n\n"
                + "Updated: " + DateTime.Now.ToString("HH:mm:ss");

            trayIcon.ShowBalloonTip(5000, "DeepSeek Monitor", msg, ToolTipIcon.Info);
        }

        private Icon CreateIcon()
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.FillRectangle(new SolidBrush(Color.FromArgb(0, 140, 255)), 0, 0, 16, 16);
                using (Font font = new Font("Arial", 7, FontStyle.Bold))
                {
                    g.DrawString("DS", font, Brushes.White, 1, 2);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (refreshTimer != null) refreshTimer.Dispose();
                if (trayIcon != null) trayIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

