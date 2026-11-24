using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

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
    private const string ApiBase = "http://127.0.0.1:2022";
    private readonly HttpClient _http = new() { BaseAddress = new Uri(ApiBase), Timeout = TimeSpan.FromSeconds(10) };
    private readonly string _appDir = AppContext.BaseDirectory;
    private Process? _core;
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(245, 246, 248), Padding = new Padding(24) };
    private readonly Label _pageTitle = new() { AutoSize = true, Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = Color.FromArgb(30, 30, 30) };
    private readonly DataGridView _grid = new();
    private readonly Label _taskStatus = new() { Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Gray };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 3000 };
    private readonly Panel _pageTasks = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Panel _pageSettings = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Panel _pageAbout = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly TextBox _dirBox = new() { Width = 520 };
    private readonly NumericUpDown _proxyPort = new() { Minimum = 1, Maximum = 65535, Width = 120 };
    private readonly List<Button> _navButtons = new();
    private readonly string _configPath;

    public MainForm()
    {
        _configPath = Path.Combine(_appDir, "config.yaml");
        Text = "Fox-wx-channel";
        Size = new Size(1180, 760);
        MinimumSize = new Size(960, 620);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);

        var main = new Panel { Dock = DockStyle.Fill };
        var top = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.White, Padding = new Padding(24, 0, 24, 0) };
        _pageTitle.Location = new Point(0, 20);
        top.Controls.Add(_pageTitle);

        var topActions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 360, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 14, 0, 0) };
        var btnOpenDir = MakeButton("打开下载目录", 120);
        var btnRefresh = MakeButton("刷新", 80);
        btnRefresh.Click += async (_, _) => await LoadTasksAsync();
        btnOpenDir.Click += (_, _) => OpenDownloadDir();
        topActions.Controls.Add(btnOpenDir);
        topActions.Controls.Add(btnRefresh);
        top.Controls.Add(topActions);

        main.Controls.Add(_content);
        main.Controls.Add(top);

        var sidebar = new Panel { Dock = DockStyle.Left, Width = 196, BackColor = Color.White };
        var brand = new Label
        {
            Text = "Fox-wx-channel",
            Dock = DockStyle.Top,
            Height = 64,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(22, 0, 0, 0),
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 30, 30)
        };
        sidebar.Controls.Add(brand);
        var nav = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false, Height = 260, Padding = new Padding(8, 4, 8, 0) };
        var navTasks = MakeNavButton("下载任务");
        var navSettings = MakeNavButton("设置");
        var navAbout = MakeNavButton("关于");
        navTasks.Click += (_, _) => ShowPage("tasks");
        navSettings.Click += (_, _) => ShowPage("settings");
        navAbout.Click += (_, _) => ShowPage("about");
        nav.Controls.Add(navTasks);
        nav.Controls.Add(navSettings);
        nav.Controls.Add(navAbout);
        sidebar.Controls.Add(nav);
        sidebar.Controls.SetChildIndex(nav, 0);
        sidebar.Controls.SetChildIndex(brand, 1);

        Controls.Add(main);
        Controls.Add(sidebar);

        BuildTasksPage();
        BuildSettingsPage();
        BuildAboutPage();
        _content.Controls.Add(_pageTasks);
        _content.Controls.Add(_pageSettings);
        _content.Controls.Add(_pageAbout);

        _timer.Tick += async (_, _) =>
        {
            if (_pageTasks.Visible) await LoadTasksAsync();
        };
        Load += async (_, _) =>
        {
            LoadSettings();
            await EnsureCoreAsync();
            ShowPage("tasks");
            _timer.Start();
        };
        FormClosing += (_, _) =>
        {
            if (_core is { HasExited: false }) _core.Kill();
        };
    }

    private static Button MakeButton(string text, int width) => new()
    {
        Text = text,
        Width = width,
        Height = 34,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.White,
        ForeColor = Color.FromArgb(40, 40, 40),
        Margin = new Padding(8, 0, 0, 0)
    };

    private Button MakeNavButton(string text)
    {
        var button = new Button
        {
            Text = "  " + text,
            Width = 180,
            Height = 42,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(55, 55, 55),
            Margin = new Padding(0, 2, 0, 2),
            Padding = new Padding(10, 0, 0, 0)
        };
        button.FlatAppearance.BorderSize = 0;
        _navButtons.Add(button);
        return button;
    }

    private void ShowPage(string page)
    {
        _pageTasks.Visible = page == "tasks";
        _pageSettings.Visible = page == "settings";
        _pageAbout.Visible = page == "about";
        _pageTitle.Text = page switch
        {
            "settings" => "设置",
            "about" => "关于",
            _ => "下载任务"
        };
        foreach (var button in _navButtons)
        {
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(55, 55, 55);
        }
        var index = page == "settings" ? 1 : page == "about" ? 2 : 0;
        if (index < _navButtons.Count)
        {
            _navButtons[index].BackColor = Color.FromArgb(235, 242, 255);
            _navButtons[index].ForeColor = Color.FromArgb(30, 90, 200);
        }
        if (page == "tasks") _ = LoadTasksAsync();
    }

    private void BuildTasksPage()
    {
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 48 };
        var startAll = MakeButton("全部开始", 96);
        var pauseAll = MakeButton("全部暂停", 96);
        var delete = MakeButton("删除选中", 96);
        startAll.Location = new Point(0, 6);
        pauseAll.Location = new Point(104, 6);
        delete.Location = new Point(208, 6);
        startAll.Click += async (_, _) => await PostAsync("/api/v1/download_task/start_all", "{}");
        pauseAll.Click += async (_, _) => await PostAsync("/api/v1/download_task/pause_all", "{}");
        delete.Click += async (_, _) => await DeleteSelectedAsync();
        toolbar.Controls.AddRange(new Control[] { startAll, pauseAll, delete });

        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.ColumnHeadersHeight = 38;
        _grid.RowTemplate.Height = 34;
        _grid.GridColor = Color.FromArgb(235, 235, 235);
        _grid.Columns.Add("id", "ID");
        _grid.Columns.Add("name", "名称");
        _grid.Columns.Add("status", "状态");
        _grid.Columns.Add("progress", "进度");
        _grid.Columns.Add("size", "大小");
        _grid.Columns.Add("updated", "更新时间");
        _grid.Columns["id"]!.FillWeight = 8;
        _grid.Columns["name"]!.FillWeight = 46;
        _grid.Columns["status"]!.FillWeight = 14;
        _grid.Columns["progress"]!.FillWeight = 12;
        _grid.Columns["size"]!.FillWeight = 12;
        _grid.Columns["updated"]!.FillWeight = 20;
        _grid.CellDoubleClick += (_, _) => OpenSelectedTaskDir();

        _pageTasks.Controls.Add(_grid);
        _pageTasks.Controls.Add(_taskStatus);
        _pageTasks.Controls.Add(toolbar);
    }

    private void BuildSettingsPage()
    {
        var card = new Panel { Dock = DockStyle.Top, Height = 300, BackColor = Color.White, Padding = new Padding(28) };
        var dirLabel = new Label { Text = "视频保存目录", Location = new Point(28, 24), AutoSize = true };
        _dirBox.Location = new Point(28, 52);
        _dirBox.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        var browse = MakeButton("浏览...", 90);
        browse.Location = new Point(560, 50);
        browse.Click += BrowseDir;

        var portLabel = new Label { Text = "代理端口", Location = new Point(28, 104), AutoSize = true };
        _proxyPort.Location = new Point(28, 132);
        _proxyPort.Maximum = 65535;

        var apiLabel = new Label { Text = "管理端口：2022（固定）", Location = new Point(200, 136), AutoSize = true, ForeColor = Color.Gray };
        var hint = new Label
        {
            Text = "保存后重启程序生效。保存目录也可填写 %UserDownloads%（系统下载文件夹）。",
            Location = new Point(28, 184),
            AutoSize = true,
            ForeColor = Color.Gray
        };
        var save = MakeButton("保存设置", 110);
        save.Location = new Point(28, 224);
        save.Click += SaveSettings;
        var openConfig = MakeButton("打开配置文件", 120);
        openConfig.Location = new Point(148, 224);
        openConfig.Click += (_, _) => OpenPath(_configPath);
        var openInstall = MakeButton("打开安装目录", 120);
        openInstall.Location = new Point(278, 224);
        openInstall.Click += (_, _) => OpenPath(_appDir);

        card.Controls.AddRange(new Control[] { dirLabel, _dirBox, browse, portLabel, _proxyPort, apiLabel, hint, save, openConfig, openInstall });
        var path = new Label
        {
            Text = $"安装目录：{_appDir}\r\n配置文件：{_configPath}",
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(4, 12, 0, 0),
            ForeColor = Color.Gray
        };
        _pageSettings.Controls.Add(card);
        _pageSettings.Controls.Add(path);
    }

    private void BuildAboutPage()
    {
        var card = new Panel { Dock = DockStyle.Top, Height = 300, BackColor = Color.White, Padding = new Padding(28) };
        var title = new Label { Text = "Fox-wx-channel", Location = new Point(28, 24), AutoSize = true, Font = new Font("Segoe UI", 16F, FontStyle.Bold) };
        var version = new Label { Text = "v0.1.0-fox", Location = new Point(28, 66), AutoSize = true, ForeColor = Color.Gray };
        var desc = new Label
        {
            Text = "微信视频号下载工具，本机自用、非商业版本。\r\n基于 ltaoo/wx_channels_download v261123 改造。",
            Location = new Point(28, 104),
            AutoSize = true
        };
        var github = new LinkLabel { Text = "https://github.com/foxnangang/fox-wx-channel", Location = new Point(28, 160), AutoSize = true };
        github.Click += (_, _) => OpenPath("https://github.com/foxnangang/fox-wx-channel");
        var path = new Label
        {
            Text = $"安装目录：{_appDir}\r\n配置文件：{_configPath}",
            Location = new Point(28, 200),
            AutoSize = true,
            ForeColor = Color.Gray
        };
        card.Controls.AddRange(new Control[] { title, version, desc, github, path });
        _pageAbout.Controls.Add(card);
    }

    private async Task EnsureCoreAsync()
    {
        if (IsPortOpen(2022)) return;
        var corePath = Path.Combine(_appDir, "fox-wx-channel-core.exe");
        if (!File.Exists(corePath))
        {
            MessageBox.Show($"找不到后台核心：{corePath}", "Fox-wx-channel", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        try
        {
            _core = Process.Start(new ProcessStartInfo(corePath)
            {
                WorkingDirectory = _appDir,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            for (var i = 0; i < 60 && !IsPortOpen(2022); i++) await Task.Delay(500);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动后台服务失败：{ex.Message}", "Fox-wx-channel", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadTasksAsync()
    {
        if (!IsPortOpen(2022))
        {
            _taskStatus.Text = "后台服务未启动";
            return;
        }
        try
        {
            using var response = await _http.GetAsync("/api/v1/download_task/list?page=1&page_size=100");
            var text = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var code) && code.GetInt32() != 0)
            {
                _taskStatus.Text = root.TryGetProperty("msg", out var msg) ? msg.GetString() : "加载失败";
                return;
            }
            var data = root.GetProperty("data");
            var list = data.GetProperty("list");
            var total = data.TryGetProperty("total", out var totalValue) ? totalValue.GetInt64() : list.GetArrayLength();
            _grid.Rows.Clear();
            foreach (var item in list.EnumerateArray())
            {
                var id = item.GetProperty("id").GetInt32();
                var name = item.TryGetProperty("name", out var nameValue) ? nameValue.GetString() ?? "" : "";
                var status = item.TryGetProperty("status", out var statusValue) ? statusValue.GetInt32() : 0;
                var progress = item.TryGetProperty("progress", out var progressValue) ? progressValue.GetDouble() : 0;
                var size = item.TryGetProperty("size", out var sizeValue) ? sizeValue.GetInt64() : 0;
                var updated = item.TryGetProperty("updated_at", out var updatedValue) ? updatedValue.GetInt64() : 0;
                var rowIndex = _grid.Rows.Add(id, name, StatusText(status), $"{progress:0.#}%", FormatSize(size), FormatTime(updated));
                _grid.Rows[rowIndex].Tag = TaskDir(item);
            }
            _taskStatus.Text = $"共 {total} 个任务";
        }
        catch (Exception ex)
        {
            _taskStatus.Text = "加载失败：" + ex.Message;
        }
    }

    private static string StatusText(int status) => status switch
    {
        0 => "等待中",
        1 => "准备中",
        2 => "下载中",
        3 => "已暂停",
        4 => "合并中",
        5 => "已完成",
        6 => "失败",
        7 => "已取消",
        _ => "未知"
    };

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.#} {units[unit]}";
    }

    private static string FormatTime(long seconds)
    {
        if (seconds <= 0) return "";
        return DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }

    private static string TaskDir(JsonElement item)
    {
        if (!item.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array) return "";
        foreach (var file in files.EnumerateArray())
        {
            if (file.TryGetProperty("download_dir", out var dir) && !string.IsNullOrWhiteSpace(dir.GetString()))
                return dir.GetString()!;
            if (file.TryGetProperty("file_path", out var path) && !string.IsNullOrWhiteSpace(path.GetString()))
                return Path.GetDirectoryName(path.GetString()!) ?? "";
        }
        return "";
    }

    private void OpenSelectedTaskDir()
    {
        if (_grid.CurrentRow?.Tag is string dir && !string.IsNullOrWhiteSpace(dir))
            OpenPath(dir);
        else
            OpenDownloadDir();
    }

    private void OpenDownloadDir()
    {
        var configured = ConfigHelper.GetValue(_configPath, "download", "dir", "%UserDownloads%");
        var dir = configured.Replace("%UserDownloads%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads");
        OpenPath(dir);
    }

    private static void OpenPath(string path)
    {
        try
        {
            if (!path.StartsWith("http"))
            {
                if (path.EndsWith(".yaml") || path.EndsWith(".yml") || path.EndsWith(".txt"))
                {
                    Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
                    return;
                }
                Directory.CreateDirectory(path);
            }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开失败：{ex.Message}", "Fox-wx-channel", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task PostAsync(string url, string json)
    {
        try
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(url, content);
            await LoadTasksAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"操作失败：{ex.Message}", "Fox-wx-channel", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_grid.CurrentRow is null)
        {
            MessageBox.Show("请先选择任务", "Fox-wx-channel", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show("确定删除选中任务？不会删除已下载文件。", "Fox-wx-channel", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        var id = Convert.ToInt32(_grid.CurrentRow.Cells["id"].Value);
        await PostAsync("/api/v1/download_task/delete", $"{{\"task_ids\":[{id}],\"delete_files\":false}}");
    }

    private void BrowseDir(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "选择视频保存目录" };
        if (dialog.ShowDialog(this) == DialogResult.OK) _dirBox.Text = dialog.SelectedPath;
    }

    private void SaveSettings(object? sender, EventArgs e)
    {
        ConfigHelper.SetValue(_configPath, "download", "dir", _dirBox.Text, true);
        ConfigHelper.SetValue(_configPath, "proxy", "port", _proxyPort.Value.ToString(), false);
        MessageBox.Show("设置已保存，重启程序后生效。", "Fox-wx-channel", MessageBoxButtons.OK, MessageBoxIcon.Information);
        LoadSettings();
    }

    private void LoadSettings()
    {
        _dirBox.Text = ConfigHelper.GetValue(_configPath, "download", "dir", "%UserDownloads%");
        if (int.TryParse(ConfigHelper.GetValue(_configPath, "proxy", "port", "2023"), out var port))
            _proxyPort.Value = Math.Clamp(port, 1, 65535);
    }

    private static bool IsPortOpen(int port)
    {
        using var client = new TcpClient();
        try { client.Connect("127.0.0.1", port); return true; }
        catch { return false; }
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
                return line.Substring(key.Length + 1).Trim().Trim('"');
        }
        return fallback;
    }

    public static void SetValue(string path, string section, string key, string value, bool quote)
    {
        var lines = File.ReadAllLines(path).ToList();
        var current = "";
        var written = false;
        var encoded = quote ? "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"" : value;
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
