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
    private static readonly Color SidebarColor = Color.FromArgb(23, 33, 54);
    private static readonly Color SidebarMuted = Color.FromArgb(158, 171, 194);
    private static readonly Color Accent = Color.FromArgb(57, 112, 235);
    private static readonly Color PageColor = Color.FromArgb(244, 246, 251);
    private static readonly Color CardColor = Color.White;
    private static readonly Color TextColor = Color.FromArgb(32, 41, 58);
    private static readonly Color MutedText = Color.FromArgb(112, 123, 144);

    private readonly HttpClient _http = new() { BaseAddress = new Uri(ApiBase), Timeout = TimeSpan.FromSeconds(10) };
    private readonly string _appDir = AppContext.BaseDirectory;
    private Process? _core;
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = PageColor, Padding = new Padding(26, 20, 26, 22) };
    private readonly Label _pageTitle = new() { AutoSize = true, Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold), ForeColor = TextColor };
    private readonly Label _pageSubtitle = new() { AutoSize = true, Font = new Font("Microsoft YaHei UI", 9F), ForeColor = MutedText };
    private readonly DataGridView _grid = new();
    private readonly Label _taskStatus = new() { Dock = DockStyle.Bottom, Height = 26, TextAlign = ContentAlignment.MiddleLeft, ForeColor = MutedText };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 3000 };
    private readonly Panel _pageTasks = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Panel _pageSettings = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Panel _pageAbout = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly TextBox _dirBox = new() { Width = 520, Font = new Font("Microsoft YaHei UI", 10F), BorderStyle = BorderStyle.FixedSingle };
    private readonly NumericUpDown _proxyPort = new() { Minimum = 1, Maximum = 65535, Width = 120, Font = new Font("Microsoft YaHei UI", 10F) };
    private readonly Label _statTotal = new() { Text = "0", AutoSize = true, Font = new Font("Segoe UI", 24F, FontStyle.Bold), ForeColor = TextColor };
    private readonly Label _statDownloading = new() { Text = "0", AutoSize = true, Font = new Font("Segoe UI", 24F, FontStyle.Bold), ForeColor = Accent };
    private readonly Label _statFinished = new() { Text = "0", AutoSize = true, Font = new Font("Segoe UI", 24F, FontStyle.Bold), ForeColor = Color.FromArgb(34, 160, 110) };
    private readonly List<(Button Button, string Key)> _navButtons = new();
    private readonly string _configPath;

    public MainForm()
    {
        _configPath = Path.Combine(_appDir, "config.yaml");
        Text = "Fox-wx-channel";
        Size = new Size(1220, 800);
        MinimumSize = new Size(1000, 660);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = PageColor;
        Font = new Font("Microsoft YaHei UI", 9F);
        DoubleBuffered = true;

        var main = new Panel { Dock = DockStyle.Fill, BackColor = PageColor };
        var header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = CardColor, Padding = new Padding(30, 0, 30, 0) };
        _pageTitle.Location = new Point(0, 22);
        _pageSubtitle.Location = new Point(2, 56);
        header.Controls.Add(_pageTitle);
        header.Controls.Add(_pageSubtitle);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 360, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 28, 0, 0) };
        var btnOpenDir = MakeButton("打开下载目录", 128, false);
        var btnRefresh = MakeButton("刷新", 84, false);
        btnRefresh.Click += async (_, _) => await LoadTasksAsync();
        btnOpenDir.Click += (_, _) => OpenDownloadDir();
        actions.Controls.Add(btnOpenDir);
        actions.Controls.Add(btnRefresh);
        header.Controls.Add(actions);

        main.Controls.Add(_content);
        main.Controls.Add(header);

        var sidebar = new Panel { Dock = DockStyle.Left, Width = 216, BackColor = SidebarColor };
        var brand = new Label
        {
            Text = "Fox",
            Dock = DockStyle.Top,
            Height = 92,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(26, 0, 0, 0),
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = Color.White
        };
        var brandSub = new Label
        {
            Text = "WX CHANNEL",
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(28, 0, 0, 0),
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = SidebarMuted
        };
        sidebar.Controls.Add(brandSub);
        sidebar.Controls.Add(brand);

        var nav = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false, Height = 280, Padding = new Padding(14, 18, 14, 0) };
        var navTasks = MakeNavButton("下载任务", "tasks");
        var navSettings = MakeNavButton("设置", "settings");
        var navAbout = MakeNavButton("关于", "about");
        navTasks.Click += (_, _) => ShowPage("tasks");
        navSettings.Click += (_, _) => ShowPage("settings");
        navAbout.Click += (_, _) => ShowPage("about");
        nav.Controls.Add(navTasks);
        nav.Controls.Add(navSettings);
        nav.Controls.Add(navAbout);
        sidebar.Controls.Add(nav);

        var footer = new Label
        {
            Text = "v0.1.0-fox",
            Dock = DockStyle.Bottom,
            Height = 46,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(28, 0, 0, 0),
            ForeColor = SidebarMuted,
            Font = new Font("Segoe UI", 8.5F)
        };
        sidebar.Controls.Add(footer);

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

    private static Button MakeButton(string text, int width, bool primary)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 36,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : Color.White,
            ForeColor = primary ? Color.White : TextColor,
            Font = new Font("Microsoft YaHei UI", 9F),
            Margin = new Padding(10, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(216, 222, 233);
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private Button MakeNavButton(string text, string key)
    {
        var button = new Button
        {
            Text = "   " + text,
            Width = 188,
            Height = 44,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = SidebarColor,
            ForeColor = SidebarMuted,
            Font = new Font("Microsoft YaHei UI", 10F),
            Margin = new Padding(0, 3, 0, 3),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(34, 48, 76);
        _navButtons.Add((button, key));
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
        _pageSubtitle.Text = page switch
        {
            "settings" => "修改保存位置与网络端口，保存后重启程序生效",
            "about" => "版本信息与安装位置",
            _ => "查看、开始、暂停和清理视频号下载任务"
        };
        foreach (var (button, key) in _navButtons)
        {
            var active = key == page;
            button.BackColor = active ? Accent : SidebarColor;
            button.ForeColor = active ? Color.White : SidebarMuted;
        }
        if (page == "tasks") _ = LoadTasksAsync();
    }

    private void BuildTasksPage()
    {
        var stats = new TableLayoutPanel { Dock = DockStyle.Top, Height = 116, ColumnCount = 3, RowCount = 1, BackColor = PageColor };
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        stats.Controls.Add(MakeStatCard("全部任务", _statTotal, "当前保留的下载记录"), 0, 0);
        stats.Controls.Add(MakeStatCard("正在下载", _statDownloading, "等待中与下载中的任务"), 1, 0);
        stats.Controls.Add(MakeStatCard("已完成", _statFinished, "已成功保存到本地"), 2, 0);

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = PageColor };
        var startAll = MakeButton("全部开始", 96, true);
        var pauseAll = MakeButton("全部暂停", 96, false);
        var delete = MakeButton("删除选中", 96, false);
        startAll.Location = new Point(0, 10);
        pauseAll.Location = new Point(106, 10);
        delete.Location = new Point(212, 10);
        startAll.Click += async (_, _) => await PostAsync("/api/v1/download_task/start_all", "{}");
        pauseAll.Click += async (_, _) => await PostAsync("/api/v1/download_task/pause_all", "{}");
        delete.Click += async (_, _) => await DeleteSelectedAsync();
        toolbar.Controls.AddRange(new Control[] { startAll, pauseAll, delete });

        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = CardColor;
        _grid.BorderStyle = BorderStyle.None;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.ColumnHeadersHeight = 42;
        _grid.RowTemplate.Height = 38;
        _grid.GridColor = Color.FromArgb(239, 242, 247);
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = MutedText;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(248, 250, 253);
        _grid.DefaultCellStyle.BackColor = CardColor;
        _grid.DefaultCellStyle.ForeColor = TextColor;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(232, 240, 255);
        _grid.DefaultCellStyle.SelectionForeColor = TextColor;
        _grid.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 253, 255);
        _grid.Columns.Add("id", "ID");
        _grid.Columns.Add("name", "名称");
        _grid.Columns.Add("status", "状态");
        _grid.Columns.Add("progress", "进度");
        _grid.Columns.Add("size", "大小");
        _grid.Columns.Add("updated", "更新时间");
        _grid.Columns["id"]!.FillWeight = 7;
        _grid.Columns["name"]!.FillWeight = 48;
        _grid.Columns["status"]!.FillWeight = 13;
        _grid.Columns["progress"]!.FillWeight = 11;
        _grid.Columns["size"]!.FillWeight = 11;
        _grid.Columns["updated"]!.FillWeight = 20;
        _grid.CellDoubleClick += (_, _) => OpenSelectedTaskDir();

        _pageTasks.Controls.Add(_grid);
        _pageTasks.Controls.Add(_taskStatus);
        _pageTasks.Controls.Add(toolbar);
        _pageTasks.Controls.Add(stats);
    }

    private Control MakeStatCard(string title, Label value, string subtitle)
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = CardColor, Margin = new Padding(0, 0, 14, 0), Padding = new Padding(22, 14, 22, 12) };
        var titleLabel = new Label { Text = title, Dock = DockStyle.Top, Height = 24, ForeColor = MutedText, Font = new Font("Microsoft YaHei UI", 9F) };
        value.Dock = DockStyle.Top;
        value.Height = 46;
        var subLabel = new Label { Text = subtitle, Dock = DockStyle.Top, Height = 20, ForeColor = Color.FromArgb(150, 158, 175), Font = new Font("Microsoft YaHei UI", 8F) };
        card.Controls.Add(subLabel);
        card.Controls.Add(value);
        card.Controls.Add(titleLabel);
        return card;
    }

    private void BuildSettingsPage()
    {
        var card = new Panel { Dock = DockStyle.Top, Height = 360, BackColor = CardColor, Padding = new Padding(30, 24, 30, 24) };
        var heading = new Label { Text = "下载设置", Location = new Point(30, 24), AutoSize = true, Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold), ForeColor = TextColor };
        var dirLabel = new Label { Text = "视频保存目录", Location = new Point(30, 74), AutoSize = true, ForeColor = TextColor };
        _dirBox.Location = new Point(30, 100);
        var browse = MakeButton("浏览...", 90, false);
        browse.Location = new Point(562, 98);
        browse.Click += BrowseDir;

        var portLabel = new Label { Text = "代理端口", Location = new Point(30, 150), AutoSize = true, ForeColor = TextColor };
        _proxyPort.Location = new Point(30, 176);
        var apiLabel = new Label { Text = "管理端口固定为 2022", Location = new Point(170, 181), AutoSize = true, ForeColor = MutedText };
        var hint = new Label
        {
            Text = "保存后重启程序生效。保存目录也可填写 %UserDownloads%（系统下载文件夹）。",
            Location = new Point(30, 226),
            AutoSize = true,
            ForeColor = MutedText
        };
        var save = MakeButton("保存设置", 110, true);
        save.Location = new Point(30, 270);
        save.Click += SaveSettings;
        var openConfig = MakeButton("打开配置文件", 126, false);
        openConfig.Location = new Point(150, 270);
        openConfig.Click += (_, _) => OpenPath(_configPath);
        var openInstall = MakeButton("打开安装目录", 126, false);
        openInstall.Location = new Point(286, 270);
        openInstall.Click += (_, _) => OpenPath(_appDir);

        card.Controls.AddRange(new Control[] { heading, dirLabel, _dirBox, browse, portLabel, _proxyPort, apiLabel, hint, save, openConfig, openInstall });
        var path = new Label
        {
            Text = $"安装目录：{_appDir}\r\n配置文件：{_configPath}",
            Dock = DockStyle.Top,
            Height = 64,
            Padding = new Padding(4, 14, 0, 0),
            ForeColor = MutedText
        };
        _pageSettings.Controls.Add(card);
        _pageSettings.Controls.Add(path);
    }

    private void BuildAboutPage()
    {
        var card = new Panel { Dock = DockStyle.Top, Height = 330, BackColor = CardColor, Padding = new Padding(30, 24, 30, 24) };
        var title = new Label { Text = "Fox-wx-channel", Location = new Point(30, 24), AutoSize = true, Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = TextColor };
        var version = new Label { Text = "v0.1.0-fox", Location = new Point(30, 70), AutoSize = true, ForeColor = MutedText };
        var desc = new Label
        {
            Text = "微信视频号下载工具，本机自用、非商业版本。\r\n基于 ltaoo/wx_channels_download v261123 改造。",
            Location = new Point(30, 112),
            AutoSize = true,
            ForeColor = TextColor
        };
        var github = new LinkLabel { Text = "https://github.com/foxnangang/fox-wx-channel", Location = new Point(30, 168), AutoSize = true, LinkColor = Accent };
        github.Click += (_, _) => OpenPath("https://github.com/foxnangang/fox-wx-channel");
        var path = new Label
        {
            Text = $"安装目录：{_appDir}\r\n配置文件：{_configPath}",
            Location = new Point(30, 212),
            AutoSize = true,
            ForeColor = MutedText
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
            var downloading = 0;
            var finished = 0;
            foreach (var item in list.EnumerateArray())
            {
                var id = item.GetProperty("id").GetInt32();
                var name = item.TryGetProperty("name", out var nameValue) ? nameValue.GetString() ?? "" : "";
                var status = item.TryGetProperty("status", out var statusValue) ? statusValue.GetInt32() : 0;
                var progress = item.TryGetProperty("progress", out var progressValue) ? progressValue.GetDouble() : 0;
                var size = item.TryGetProperty("size", out var sizeValue) ? sizeValue.GetInt64() : 0;
                var updated = item.TryGetProperty("updated_at", out var updatedValue) ? updatedValue.GetInt64() : 0;
                if (status is 0 or 1 or 2 or 4) downloading++;
                if (status == 5) finished++;
                var rowIndex = _grid.Rows.Add(id, name, StatusText(status), $"{progress:0.#}%", FormatSize(size), FormatTime(updated));
                _grid.Rows[rowIndex].Tag = TaskDir(item);
            }
            _statTotal.Text = total.ToString();
            _statDownloading.Text = downloading.ToString();
            _statFinished.Text = finished.ToString();
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
