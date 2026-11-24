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
    private static readonly Color Accent = Color.FromArgb(45, 108, 223);
    private static readonly Color PageColor = Color.FromArgb(247, 249, 252);
    private static readonly Color TextColor = Color.FromArgb(31, 41, 55);
    private static readonly Color Muted = Color.FromArgb(122, 132, 148);
    private static readonly Color BorderColor = Color.FromArgb(231, 235, 241);

    private readonly HttpClient _http = new() { BaseAddress = new Uri(ApiBase), Timeout = TimeSpan.FromSeconds(10) };
    private readonly string _appDir = AppContext.BaseDirectory;
    private Process? _core;
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = PageColor, Padding = new Padding(20) };
    private readonly Label _pageTitle = new() { AutoSize = true, Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold), ForeColor = TextColor };
    private readonly Label _pageSubtitle = new() { AutoSize = true, Font = new Font("Microsoft YaHei UI", 8.5F), ForeColor = Muted };
    private readonly DataGridView _grid = new();
    private readonly Label _empty = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Text = "还没有任务\r\n播放视频号视频后，点击页面上的下载按钮", ForeColor = Muted, Font = new Font("Microsoft YaHei UI", 10F), Visible = false };
    private readonly Label _count = new() { Dock = DockStyle.Right, Width = 180, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 8, 0), ForeColor = Muted, Font = new Font("Microsoft YaHei UI", 9F) };
    private readonly TextBox _search = new() { Width = 260, Font = new Font("Microsoft YaHei UI", 9.5F) };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 3000 };
    private readonly Panel _pageTasks = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Panel _pageSettings = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Panel _pageAbout = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly TextBox _dirBox = new() { Width = 520, Font = new Font("Microsoft YaHei UI", 9.5F) };
    private readonly NumericUpDown _proxyPort = new() { Minimum = 1, Maximum = 65535, Width = 120, Font = new Font("Microsoft YaHei UI", 9.5F) };
    private readonly List<(Button Button, string Key)> _navButtons = new();
    private readonly string _configPath;
    private List<TaskRow> _rows = new();

    public MainForm()
    {
        _configPath = Path.Combine(_appDir, "config.yaml");
        Text = "Fox-wx-channel";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Size = new Size(1180, 760);
        MinimumSize = new Size(960, 620);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = PageColor;
        Font = new Font("Microsoft YaHei UI", 9F);
        DoubleBuffered = true;

        var main = new Panel { Dock = DockStyle.Fill, BackColor = PageColor };
        var top = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = Color.White, Padding = new Padding(24, 0, 24, 0) };
        _pageTitle.Location = new Point(0, 12);
        _pageSubtitle.Location = new Point(1, 37);
        top.Controls.Add(_pageTitle);
        top.Controls.Add(_pageSubtitle);
        top.Paint += (_, e) => e.Graphics.DrawLine(new Pen(BorderColor), 0, top.Height - 1, top.Width, top.Height - 1);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 300, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 14, 0, 0) };
        var openDir = MakeButton("打开下载目录", 116, false);
        var refresh = MakeButton("刷新", 76, true);
        refresh.Click += async (_, _) => await LoadTasksAsync();
        openDir.Click += (_, _) => OpenDownloadDir();
        actions.Controls.Add(openDir);
        actions.Controls.Add(refresh);
        top.Controls.Add(actions);
        main.Controls.Add(_content);
        main.Controls.Add(top);

        var sidebar = new Panel { Dock = DockStyle.Left, Width = 92, BackColor = Color.White };
        sidebar.Paint += (_, e) => e.Graphics.DrawLine(new Pen(BorderColor), sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
        var logo = new Panel { Location = new Point(26, 18), Size = new Size(40, 40), BackColor = Accent };
        logo.Controls.Add(new Label { Text = "F", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, Font = new Font("Segoe UI", 17F, FontStyle.Bold) });
        sidebar.Controls.Add(logo);

        var nav = new FlowLayoutPanel { Location = new Point(8, 86), Size = new Size(76, 300), FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.White };
        var navTasks = MakeNavButton("⬇", "下载", "tasks");
        var navSettings = MakeNavButton("⚙", "设置", "settings");
        var navAbout = MakeNavButton("ⓘ", "关于", "about");
        navTasks.Click += (_, _) => ShowPage("tasks");
        navSettings.Click += (_, _) => ShowPage("settings");
        navAbout.Click += (_, _) => ShowPage("about");
        nav.Controls.Add(navTasks);
        nav.Controls.Add(navSettings);
        nav.Controls.Add(navAbout);
        sidebar.Controls.Add(nav);
        var version = new Label { Text = "v0.1.0", Dock = DockStyle.Bottom, Height = 34, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Muted, Font = new Font("Segoe UI", 7.5F) };
        sidebar.Controls.Add(version);

        Controls.Add(main);
        Controls.Add(sidebar);

        BuildTasksPage();
        BuildSettingsPage();
        BuildAboutPage();
        _content.Controls.Add(_pageTasks);
        _content.Controls.Add(_pageSettings);
        _content.Controls.Add(_pageAbout);

        _search.TextChanged += (_, _) => RenderRows();
        _timer.Tick += async (_, _) => { if (_pageTasks.Visible) await LoadTasksAsync(); };
        Load += async (_, _) =>
        {
            LoadSettings();
            await EnsureCoreAsync();
            ShowPage("tasks");
            _timer.Start();
        };
        FormClosing += (_, _) => { if (_core is { HasExited: false }) _core.Kill(); };
    }

    private static Button MakeButton(string text, int width, bool primary)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : Color.White,
            ForeColor = primary ? Color.White : TextColor,
            Font = new Font("Microsoft YaHei UI", 9F),
            Margin = new Padding(8, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = primary ? Accent : BorderColor;
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private Button MakeNavButton(string glyph, string label, string key)
    {
        var button = new Button
        {
            Text = glyph + "\n" + label,
            Width = 76,
            Height = 62,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.White,
            ForeColor = Muted,
            Font = new Font("Microsoft YaHei UI", 8.5F),
            Margin = new Padding(0, 0, 0, 6),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(244, 247, 252);
        _navButtons.Add((button, key));
        return button;
    }

    private void ShowPage(string page)
    {
        _pageTasks.Visible = page == "tasks";
        _pageSettings.Visible = page == "settings";
        _pageAbout.Visible = page == "about";
        _pageTitle.Text = page switch { "settings" => "设置", "about" => "关于", _ => "下载任务" };
        _pageSubtitle.Text = page switch
        {
            "settings" => "保存位置与网络端口",
            "about" => "版本与安装信息",
            _ => "查看和管理视频号下载任务"
        };
        foreach (var (button, key) in _navButtons)
        {
            var active = key == page;
            button.BackColor = active ? Color.FromArgb(236, 242, 254) : Color.White;
            button.ForeColor = active ? Accent : Muted;
        }
        if (page == "tasks") _ = LoadTasksAsync();
    }

    private void BuildTasksPage()
    {
        var searchBar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = PageColor };
        _search.Location = new Point(0, 9);
        _search.PlaceholderText = "搜索任务名称";
        _search.BorderStyle = BorderStyle.FixedSingle;
        var startAll = MakeButton("全部开始", 90, true);
        startAll.Location = new Point(276, 9);
        var pauseAll = MakeButton("全部暂停", 90, false);
        pauseAll.Location = new Point(374, 9);
        var delete = MakeButton("删除选中", 90, false);
        delete.Location = new Point(472, 9);
        startAll.Click += async (_, _) => await PostAsync("/api/v1/download_task/start_all", "{}");
        pauseAll.Click += async (_, _) => await PostAsync("/api/v1/download_task/pause_all", "{}");
        delete.Click += async (_, _) => await DeleteSelectedAsync();
        searchBar.Controls.AddRange(new Control[] { _search, startAll, pauseAll, delete, _count });

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
        _grid.ColumnHeadersHeight = 40;
        _grid.RowTemplate.Height = 40;
        _grid.GridColor = Color.FromArgb(240, 242, 246);
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(95, 105, 120);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(249, 250, 252);
        _grid.DefaultCellStyle.BackColor = Color.White;
        _grid.DefaultCellStyle.ForeColor = TextColor;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(234, 242, 255);
        _grid.DefaultCellStyle.SelectionForeColor = TextColor;
        _grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 253, 255);
        _grid.Columns.Add("id", "ID");
        _grid.Columns.Add("name", "名称");
        _grid.Columns.Add("status", "状态");
        _grid.Columns.Add("progress", "进度");
        _grid.Columns.Add("size", "大小");
        _grid.Columns.Add("updated", "更新时间");
        _grid.Columns["id"]!.FillWeight = 6;
        _grid.Columns["name"]!.FillWeight = 50;
        _grid.Columns["status"]!.FillWeight = 12;
        _grid.Columns["progress"]!.FillWeight = 10;
        _grid.Columns["size"]!.FillWeight = 10;
        _grid.Columns["updated"]!.FillWeight = 18;
        _grid.CellDoubleClick += (_, _) => OpenSelectedTaskDir();
        _grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex != _grid.Columns["status"]!.Index || e.Value is not string status) return;
            e.CellStyle.ForeColor = status switch
            {
                "已完成" => Color.FromArgb(22, 150, 100),
                "下载中" => Accent,
                "失败" => Color.FromArgb(210, 70, 70),
                "已暂停" => Color.FromArgb(180, 130, 20),
                _ => TextColor
            };
        };

        var gridCard = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(1) };
        gridCard.Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(BorderColor), 0, 0, gridCard.Width - 1, gridCard.Height - 1);
        gridCard.Controls.Add(_grid);
        gridCard.Controls.Add(_empty);
        _empty.BringToFront();

        _pageTasks.Controls.Add(gridCard);
        _pageTasks.Controls.Add(searchBar);
    }

    private void BuildSettingsPage()
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 420, BackColor = Color.White, Padding = new Padding(28) };
        panel.Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(BorderColor), 0, 0, panel.Width - 1, panel.Height - 1);

        var title = new Label { Text = "下载设置", Location = new Point(28, 24), AutoSize = true, Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold), ForeColor = TextColor };
        var dirLabel = new Label { Text = "视频保存目录", Location = new Point(28, 70), AutoSize = true, ForeColor = TextColor };
        _dirBox.Location = new Point(28, 96);
        var browse = MakeButton("浏览...", 84, false);
        browse.Location = new Point(556, 94);
        browse.Click += BrowseDir;
        var hint = new Label { Text = "可填写绝对路径，或使用 %UserDownloads% 表示系统下载文件夹。", Location = new Point(28, 136), AutoSize = true, ForeColor = Muted };

        var portLabel = new Label { Text = "代理端口", Location = new Point(28, 182), AutoSize = true, ForeColor = TextColor };
        _proxyPort.Location = new Point(28, 208);
        var portHint = new Label { Text = "管理端口固定为 2022，修改后重启程序生效。", Location = new Point(168, 213), AutoSize = true, ForeColor = Muted };

        var save = MakeButton("保存设置", 104, true);
        save.Location = new Point(28, 258);
        save.Click += SaveSettings;
        var openConfig = MakeButton("打开配置文件", 118, false);
        openConfig.Location = new Point(142, 258);
        openConfig.Click += (_, _) => OpenPath(_configPath);
        var openInstall = MakeButton("打开安装目录", 118, false);
        openInstall.Location = new Point(270, 258);
        openInstall.Click += (_, _) => OpenPath(_appDir);

        var info = new Label
        {
            Text = "安装信息\r\n安装目录：" + _appDir + "\r\n配置文件：" + _configPath,
            Location = new Point(28, 318),
            AutoSize = true,
            ForeColor = Muted
        };
        panel.Controls.AddRange(new Control[] { title, dirLabel, _dirBox, browse, hint, portLabel, _proxyPort, portHint, save, openConfig, openInstall, info });
        _pageSettings.Controls.Add(panel);
    }

    private void BuildAboutPage()
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 320, BackColor = Color.White, Padding = new Padding(28) };
        panel.Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(BorderColor), 0, 0, panel.Width - 1, panel.Height - 1);
        var title = new Label { Text = "Fox-wx-channel", Location = new Point(28, 24), AutoSize = true, Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = TextColor };
        var version = new Label { Text = "v0.1.0-fox", Location = new Point(28, 68), AutoSize = true, ForeColor = Muted };
        var desc = new Label
        {
            Text = "微信视频号下载工具，本机自用、非商业版本。\r\n基于 ltaoo/wx_channels_download v261123 改造。",
            Location = new Point(28, 108),
            AutoSize = true,
            ForeColor = TextColor
        };
        var github = new LinkLabel { Text = "https://github.com/foxnangang/fox-wx-channel", Location = new Point(28, 160), AutoSize = true, LinkColor = Accent };
        github.Click += (_, _) => OpenPath("https://github.com/foxnangang/fox-wx-channel");
        var path = new Label { Text = "安装目录：" + _appDir + "\r\n配置文件：" + _configPath, Location = new Point(28, 200), AutoSize = true, ForeColor = Muted };
        panel.Controls.AddRange(new Control[] { title, version, desc, github, path });
        _pageAbout.Controls.Add(panel);
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
            _core = Process.Start(new ProcessStartInfo(corePath) { WorkingDirectory = _appDir, UseShellExecute = false, CreateNoWindow = true });
            for (var i = 0; i < 60 && !IsPortOpen(2022); i++) await Task.Delay(500);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动后台服务失败：{ex.Message}", "Fox-wx-channel", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadTasksAsync()
    {
        if (!IsPortOpen(2022)) { _count.Text = "后台服务未启动"; return; }
        try
        {
            using var response = await _http.GetAsync("/api/v1/download_task/list?page=1&page_size=100");
            var text = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var code) && code.GetInt32() != 0)
            {
                _count.Text = root.TryGetProperty("msg", out var msg) ? msg.GetString() : "加载失败";
                return;
            }
            var data = root.GetProperty("data");
            var list = data.GetProperty("list");
            _rows.Clear();
            foreach (var item in list.EnumerateArray())
            {
                var id = item.GetProperty("id").GetInt32();
                var name = item.TryGetProperty("name", out var nameValue) ? nameValue.GetString() ?? "" : "";
                var status = item.TryGetProperty("status", out var statusValue) ? statusValue.GetInt32() : 0;
                var progress = item.TryGetProperty("progress", out var progressValue) ? progressValue.GetDouble() : 0;
                var size = item.TryGetProperty("size", out var sizeValue) ? sizeValue.GetInt64() : 0;
                var updated = item.TryGetProperty("updated_at", out var updatedValue) ? updatedValue.GetInt64() : 0;
                _rows.Add(new TaskRow(id, name, status, progress, size, updated, TaskDir(item)));
            }
            RenderRows();
        }
        catch (Exception ex)
        {
            _count.Text = "加载失败：" + ex.Message;
        }
    }

    private void RenderRows()
    {
        var keyword = _search.Text.Trim();
        var filtered = string.IsNullOrEmpty(keyword)
            ? _rows
            : _rows.Where(row => row.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
        _grid.Rows.Clear();
        foreach (var row in filtered)
        {
            var index = _grid.Rows.Add(row.Id, row.Name, StatusText(row.Status), $"{row.Progress:0.#}%", FormatSize(row.Size), FormatTime(row.UpdatedAt));
            _grid.Rows[index].Tag = row.Dir;
        }
        _count.Text = $"共 {filtered.Count} 个任务";
        _empty.Visible = filtered.Count == 0;
        if (_empty.Visible) _empty.BringToFront();
    }

    private static string StatusText(int status) => status switch
    {
        0 => "等待中", 1 => "准备中", 2 => "下载中", 3 => "已暂停", 4 => "合并中", 5 => "已完成", 6 => "失败", 7 => "已取消", _ => "未知"
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

    private static string FormatTime(long timestamp)
    {
        if (timestamp <= 0) return "";
        if (timestamp > 9_999_999_999) timestamp /= 1000;
        return DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }

    private static string TaskDir(JsonElement item)
    {
        if (!item.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array) return "";
        foreach (var file in files.EnumerateArray())
        {
            if (file.TryGetProperty("download_dir", out var dir) && !string.IsNullOrWhiteSpace(dir.GetString())) return dir.GetString()!;
            if (file.TryGetProperty("file_path", out var path) && !string.IsNullOrWhiteSpace(path.GetString())) return Path.GetDirectoryName(path.GetString()!) ?? "";
        }
        return "";
    }

    private void OpenSelectedTaskDir()
    {
        if (_grid.CurrentRow?.Tag is string dir && !string.IsNullOrWhiteSpace(dir)) OpenPath(dir);
        else OpenDownloadDir();
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
        if (MessageBox.Show("确定删除选中任务？不会删除已下载文件。", "Fox-wx-channel", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
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
        if (int.TryParse(ConfigHelper.GetValue(_configPath, "proxy", "port", "2023"), out var port)) _proxyPort.Value = Math.Clamp(port, 1, 65535);
    }

    private static bool IsPortOpen(int port)
    {
        using var client = new TcpClient();
        try { client.Connect("127.0.0.1", port); return true; }
        catch { return false; }
    }

    private sealed record TaskRow(int Id, string Name, int Status, double Progress, long Size, long UpdatedAt, string Dir);
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
            if (current == section && line.StartsWith(key + ":")) return line.Substring(key.Length + 1).Trim().Trim('"');
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
