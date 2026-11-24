using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace FoxDesktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private readonly string _appDir;
    private Process? _backend;

    public MainForm()
    {
        _appDir = AppContext.BaseDirectory;
        Text = "Fox-wx-channel";
        Size = new Size(1200, 800);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        Controls.Add(_webView);

        var top = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.FromArgb(246, 247, 249) };
        var title = new Label { Text = "Fox-wx-channel", AutoSize = true, Location = new Point(16, 14), Font = new Font("Segoe UI", 11F, FontStyle.Bold) };
        var btnSettings = new Button { Text = "设置", Size = new Size(80, 30), Location = new Point(830, 8) };
        var btnRefresh = new Button { Text = "刷新", Size = new Size(80, 30), Location = new Point(920, 8) };
        var btnExit = new Button { Text = "退出", Size = new Size(80, 30), Location = new Point(1010, 8) };
        btnSettings.Click += (_, _) => new SettingsForm(_appDir).ShowDialog(this);
        btnRefresh.Click += (_, _) => _webView.Reload();
        btnExit.Click += (_, _) => Close();
        top.Controls.AddRange(new Control[] { title, btnSettings, btnRefresh, btnExit });
        Controls.Add(top);

        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        if (!IsPortOpen(2022))
        {
            var corePath = Path.Combine(_appDir, "fox-wx-channel-core.exe");
            if (File.Exists(corePath))
            {
                var psi = new ProcessStartInfo(corePath) { WorkingDirectory = _appDir };
                if (IsAdmin())
                {
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                }
                else
                {
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                }

                try
                {
                    _backend = Process.Start(psi);
                    for (var i = 0; i < 60 && !IsPortOpen(2022); i++) await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"启动后台服务失败：{ex.Message}", "Fox-wx-channel", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show($"找不到核心程序：{corePath}", "Fox-wx-channel", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        await _webView.EnsureCoreWebView2Async();
        _webView.CoreWebView2.Navigate("http://127.0.0.1:2022");
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_backend is { HasExited: false }) _backend.Kill();
    }

    private static bool IsPortOpen(int port)
    {
        using var client = new TcpClient();
        try
        {
            client.Connect("127.0.0.1", port);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}

internal sealed class SettingsForm : Form
{
    private readonly string _configPath;
    private readonly TextBox _dir = new() { Dock = DockStyle.Top, Height = 28 };
    private readonly NumericUpDown _proxyPort = new() { Minimum = 1, Maximum = 65535, Width = 100 };

    public SettingsForm(string appDir)
    {
        _configPath = Path.Combine(appDir, "config.yaml");
        Text = "设置";
        Size = new Size(620, 300);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
        var dirLabel = new Label { Text = "视频保存目录", Location = new Point(20, 20), AutoSize = true };
        _dir.Location = new Point(20, 45);
        _dir.Width = 430;
        var browse = new Button { Text = "浏览...", Location = new Point(465, 43), Size = new Size(90, 32) };
        browse.Click += Browse_Click;

        var portLabel = new Label { Text = "代理端口", Location = new Point(20, 100), AutoSize = true };
        _proxyPort.Location = new Point(20, 125);
        _proxyPort.Value = 2023;

        var hint = new Label
        {
            Text = "修改后重启程序生效。路径也可填写 %UserDownloads%（系统下载文件夹）。",
            Location = new Point(20, 170),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        var save = new Button { Text = "保存", Location = new Point(350, 220), Size = new Size(100, 34) };
        var cancel = new Button { Text = "取消", Location = new Point(465, 220), Size = new Size(90, 34) };
        save.Click += Save_Click;
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        panel.Controls.AddRange(new Control[] { dirLabel, _dir, browse, portLabel, _proxyPort, hint, save, cancel });
        Controls.Add(panel);

        Load += SettingsForm_Load;
    }

    private void SettingsForm_Load(object? sender, EventArgs e)
    {
        if (!File.Exists(_configPath)) return;
        _dir.Text = ConfigHelper.GetValue(_configPath, "download", "dir", "%UserDownloads%");
        if (int.TryParse(ConfigHelper.GetValue(_configPath, "proxy", "port", "2023"), out var port))
            _proxyPort.Value = port;
    }

    private void Browse_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "选择视频保存目录", SelectedPath = _dir.Text.StartsWith("%") ? "" : _dir.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK) _dir.Text = dialog.SelectedPath;
    }

    private void Save_Click(object? sender, EventArgs e)
    {
        ConfigHelper.SetValue(_configPath, "download", "dir", _dir.Text, quote: true);
        ConfigHelper.SetValue(_configPath, "proxy", "port", _proxyPort.Value.ToString(), quote: false);
        DialogResult = DialogResult.OK;
    }
}

internal static class ConfigHelper
{
    public static string GetValue(string path, string section, string key, string fallback)
    {
        if (!File.Exists(path)) return fallback;
        var current = "";
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.TrimStart();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            if (!line.StartsWith(" ") && !line.StartsWith("\t") && line.EndsWith(":"))
            {
                current = line.TrimEnd(':').Trim();
                continue;
            }
            if (current == section && line.StartsWith(key + ":"))
            {
                return line.Substring(key.Length + 1).Trim().Trim('"');
            }
        }
        return fallback;
    }

    public static void SetValue(string path, string section, string key, string value, bool quote)
    {
        var lines = File.ReadAllLines(path).ToList();
        var current = "";
        var written = false;
        var encoded = quote
            ? "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
            : value;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0 || trimmed.StartsWith("#")) continue;
            if (!trimmed.StartsWith(" ") && !trimmed.StartsWith("\t") && trimmed.EndsWith(":"))
            {
                current = trimmed.TrimEnd(':').Trim();
                continue;
            }
            if (current == section && trimmed.StartsWith(key + ":"))
            {
                var colon = line.IndexOf(':');
                lines[i] = line.Substring(0, colon + 1) + " " + encoded;
                written = true;
                break;
            }
        }
        if (!written)
        {
            if (!lines.Any(l => l.Trim() == section + ":")) lines.Add(section + ":");
            lines.Add("  " + key + ": " + encoded);
        }
        File.WriteAllLines(path, lines);
    }
}
