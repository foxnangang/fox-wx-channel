using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FoxInstaller;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--uninstall"))
        {
            Uninstaller.Run();
            return;
        }
        if (args.Length >= 2 && args[0] == "--cleanup")
        {
            Uninstaller.Cleanup(args[1]);
            return;
        }
        Application.Run(new SetupForm());
    }
}

internal sealed class SetupForm : Form
{
    private static readonly Color HeaderColor = Color.FromArgb(27, 42, 74);
    private static readonly Color Accent = Color.FromArgb(57, 112, 235);
    private static readonly Color TextColor = Color.FromArgb(32, 41, 58);
    private static readonly Color Muted = Color.FromArgb(112, 123, 144);

    private readonly Panel _pageWelcome = new() { Dock = DockStyle.Fill };
    private readonly Panel _pageProgress = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Panel _pageFinish = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly TextBox _target = new() { Width = 430, Font = new Font("Microsoft YaHei UI", 10F) };
    private readonly CheckBox _desktop = new() { Text = "创建桌面快捷方式", Checked = true, AutoSize = true, Font = new Font("Microsoft YaHei UI", 9F) };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Top, Height = 18, Style = ProgressBarStyle.Continuous };
    private readonly Label _progressLabel = new() { Dock = DockStyle.Top, Height = 30, Text = "正在准备...", ForeColor = Muted };
    private readonly Label _finishLabel = new() { Dock = DockStyle.Top, Height = 46, Text = "安装完成。", Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold), ForeColor = TextColor };
    private readonly Label _finishDetail = new() { Location = new Point(0, 72), AutoSize = true, ForeColor = Muted };
    private readonly CheckBox _run = new() { Text = "立即运行 Fox-wx-channel", Checked = true, AutoSize = true, Font = new Font("Microsoft YaHei UI", 9F) };
    private readonly Button _next = new() { Text = "安装", Width = 106, Height = 38, BackColor = Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei UI", 9F), Cursor = Cursors.Hand };
    private readonly Button _cancel = new() { Text = "取消", Width = 90, Height = 38, BackColor = Color.White, ForeColor = TextColor, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei UI", 9F), Cursor = Cursors.Hand };
    private int _step;

    public SetupForm()
    {
        Text = "Fox-wx-channel 安装程序";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Size = new Size(720, 520);
        MinimumSize = new Size(720, 520);
        MaximumSize = new Size(720, 520);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9F);

        var header = new Panel { Dock = DockStyle.Top, Height = 118, BackColor = HeaderColor };
        var title = new Label { Text = "Fox-wx-channel", Location = new Point(34, 28), AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 20F, FontStyle.Bold) };
        var subtitle = new Label { Text = "微信视频号下载工具 · 安装向导", Location = new Point(38, 72), AutoSize = true, ForeColor = Color.FromArgb(184, 198, 222), Font = new Font("Microsoft YaHei UI", 9.5F) };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(34, 22, 34, 0) };
        BuildWelcome();
        BuildProgress();
        BuildFinish();
        body.Controls.Add(_pageWelcome);
        body.Controls.Add(_pageProgress);
        body.Controls.Add(_pageFinish);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 76, BackColor = Color.FromArgb(248, 250, 253) };
        _next.Location = new Point(560, 19);
        _cancel.Location = new Point(456, 19);
        _next.FlatAppearance.BorderSize = 0;
        _cancel.FlatAppearance.BorderColor = Color.FromArgb(210, 218, 230);
        _next.Click += Next_Click;
        _cancel.Click += (_, _) => Close();
        footer.Controls.Add(_next);
        footer.Controls.Add(_cancel);

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(header);
    }

    private void BuildWelcome()
    {
        _target.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fox-wx-channel");
        var intro = new Label
        {
            Text = "将安装以下程序到你的电脑：",
            Location = new Point(0, 6),
            AutoSize = true,
            ForeColor = TextColor,
            Font = new Font("Microsoft YaHei UI", 10F)
        };
        var line1 = new Label { Text = "•  Fox-wx-channel.exe（原生界面）", Location = new Point(10, 40), AutoSize = true, ForeColor = Muted };
        var line2 = new Label { Text = "•  fox-wx-channel-core.exe（后台服务）", Location = new Point(10, 66), AutoSize = true, ForeColor = Muted };
        var dirLabel = new Label { Text = "安装目录", Location = new Point(0, 112), AutoSize = true, ForeColor = TextColor };
        _target.Location = new Point(0, 138);
        var browse = new Button { Text = "浏览...", Location = new Point(440, 136), Width = 86, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.White };
        browse.FlatAppearance.BorderColor = Color.FromArgb(210, 218, 230);
        browse.Click += Browse_Click;
        _desktop.Location = new Point(0, 190);
        var note = new Label
        {
            Text = "安装包不会覆盖已有的 config.yaml。\r\n运行需要 Microsoft .NET 9 桌面运行时。",
            Location = new Point(0, 232),
            AutoSize = true,
            ForeColor = Muted
        };
        _pageWelcome.Controls.AddRange(new Control[] { intro, line1, line2, dirLabel, _target, browse, _desktop, note });
    }

    private void BuildProgress()
    {
        var title = new Label { Text = "正在安装", Location = new Point(0, 10), AutoSize = true, Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold), ForeColor = TextColor };
        _progress.Location = new Point(0, 66);
        _progress.Width = 600;
        _progressLabel.Location = new Point(0, 98);
        var hint = new Label { Text = "请稍候，正在复制程序文件并创建快捷方式。", Location = new Point(0, 140), AutoSize = true, ForeColor = Muted };
        _pageProgress.Controls.Add(title);
        _pageProgress.Controls.Add(hint);
        _pageProgress.Controls.Add(_progressLabel);
        _pageProgress.Controls.Add(_progress);
    }

    private void BuildFinish()
    {
        _finishLabel.Location = new Point(0, 12);
        _finishDetail.Text = "程序已安装到：\r\n" + _target.Text + "\r\n\r\n你可以从桌面或开始菜单启动。";
        _run.Location = new Point(0, 168);
        var openDir = new Button { Text = "打开安装目录", Location = new Point(190, 162), Width = 126, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.White };
        openDir.FlatAppearance.BorderColor = Color.FromArgb(210, 218, 230);
        openDir.Click += (_, _) => { if (Directory.Exists(_target.Text)) Process.Start(new ProcessStartInfo("explorer.exe", "\"" + _target.Text + "\"") { UseShellExecute = true }); };
        _pageFinish.Controls.Add(_finishLabel);
        _pageFinish.Controls.Add(_finishDetail);
        _pageFinish.Controls.Add(_run);
        _pageFinish.Controls.Add(openDir);
    }

    private static string NormalizeTarget(string path)
    {
        var full = Path.GetFullPath(path.Trim());
        var name = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(name) ||
            name.Equals("Program Files", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Program Files (x86)", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(full, "Fox-wx-channel");
        }
        return full;
    }

    private void Browse_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "选择安装目录", SelectedPath = _target.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK) _target.Text = dialog.SelectedPath;
    }

    private async void Next_Click(object? sender, EventArgs e)
    {
        if (_step == 0)
        {
            if (string.IsNullOrWhiteSpace(_target.Text))
            {
                MessageBox.Show("请选择安装目录。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _target.Text = NormalizeTarget(_target.Text);
            _step = 1;
            _pageWelcome.Visible = false;
            _pageFinish.Visible = false;
            _pageProgress.Visible = true;
            _next.Enabled = false;
            _cancel.Enabled = false;
            try
            {
                await Task.Run(() => Installer.Install(_target.Text, _desktop.Checked, Report));
                _finishLabel.Text = "安装完成。";
                _finishDetail.Text = "程序已安装到：\r\n" + _target.Text + "\r\n\r\n你可以从桌面或开始菜单启动。";
                _pageProgress.Visible = false;
                _pageFinish.Visible = true;
                _next.Text = "完成";
                _next.Enabled = true;
                _cancel.Visible = false;
                _step = 2;
            }
            catch (Exception ex)
            {
                MessageBox.Show("安装失败：" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                _pageProgress.Visible = false;
                _pageWelcome.Visible = true;
                _next.Enabled = true;
                _cancel.Enabled = true;
                _step = 0;
            }
        }
        else if (_step == 2)
        {
            if (_run.Checked)
            {
                var exe = Path.Combine(_target.Text, "Fox-wx-channel.exe");
                if (File.Exists(exe)) Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = _target.Text, UseShellExecute = true });
            }
            Close();
        }
    }

    private void Report(int percent, string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Report(percent, text)));
            return;
        }
        _progress.Value = Math.Clamp(percent, 0, 100);
        _progressLabel.Text = text;
    }
}

internal static class Installer
{
    public static void Install(string target, bool desktopShortcut, Action<int, string> report)
    {
        Directory.CreateDirectory(target);
        report(2, "正在读取安装文件...");
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")
            ?? throw new InvalidOperationException("安装包资源缺失");
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var total = archive.Entries.Sum(entry => entry.Length);
        long copied = 0;
        var buffer = new byte[1024 * 1024];
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(Path.Combine(target, entry.FullName));
                continue;
            }
            var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            var destination = Path.GetFullPath(Path.Combine(target, relative));
            if (!destination.StartsWith(Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("安装包包含非法路径");
            if (Path.GetFileName(destination).Equals("config.yaml", StringComparison.OrdinalIgnoreCase) && File.Exists(destination))
                continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = entry.Open();
            using var output = File.Create(destination);
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                copied += read;
                var percent = total == 0 ? 50 : (int)(copied * 80 / total) + 5;
                report(percent, "正在复制：" + entry.Name);
            }
        }

        report(88, "正在创建快捷方式...");
        Shortcuts.Create(target, desktopShortcut);
        report(94, "正在写入卸载信息...");
        Uninstaller.Register(target);
        var self = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(self) && File.Exists(self))
        {
            var uninstall = Path.Combine(target, "uninstall.exe");
            if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(uninstall), StringComparison.OrdinalIgnoreCase))
                File.Copy(self, uninstall, true);
        }
        report(100, "安装完成");
    }
}

internal static class Shortcuts
{
    public static void Create(string target, bool desktopShortcut)
    {
        var exe = Path.Combine(target, "Fox-wx-channel.exe");
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("无法创建快捷方式");
        dynamic shell = Activator.CreateInstance(shellType)!;
        var menuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs\Fox-wx-channel");
        Directory.CreateDirectory(menuDir);
        CreateLink(shell, Path.Combine(menuDir, "Fox-wx-channel.lnk"), exe, target);
        if (desktopShortcut)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            CreateLink(shell, Path.Combine(desktop, "Fox-wx-channel.lnk"), exe, target);
        }
    }

    private static void CreateLink(dynamic shell, string linkPath, string exe, string workingDirectory)
    {
        dynamic link = shell.CreateShortcut(linkPath);
        link.TargetPath = exe;
        link.WorkingDirectory = workingDirectory;
        link.IconLocation = exe + ",0";
        link.Description = "Fox-wx-channel";
        link.Save();
    }

    public static void Remove()
    {
        var desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Fox-wx-channel.lnk");
        if (File.Exists(desktop)) File.Delete(desktop);
        var menuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs\Fox-wx-channel");
        if (Directory.Exists(menuDir)) Directory.Delete(menuDir, true);
    }
}

internal static class Uninstaller
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Fox-wx-channel";

    public static void Register(string target)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue("DisplayName", "Fox-wx-channel");
        key.SetValue("DisplayVersion", "0.1.0-fox");
        key.SetValue("InstallLocation", target);
        key.SetValue("DisplayIcon", Path.Combine(target, "Fox-wx-channel.exe"));
        key.SetValue("UninstallString", "\"" + Path.Combine(target, "uninstall.exe") + "\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    public static void Run()
    {
        if (MessageBox.Show("确定卸载 Fox-wx-channel？\r\n已下载的视频不会删除。", "卸载 Fox-wx-channel", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        var target = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        Shortcuts.Remove();
        try { Registry.CurrentUser.DeleteSubKeyTree(KeyPath, false); } catch { }
        var tempDir = Path.Combine(Path.GetTempPath(), "fox-uninstall-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempExe = Path.Combine(tempDir, "uninstall.exe");
        File.Copy(Environment.ProcessPath!, tempExe, true);
        Process.Start(new ProcessStartInfo(tempExe, "--cleanup \"" + target + "\"") { UseShellExecute = true });
        Application.Exit();
    }

    public static void Cleanup(string target)
    {
        System.Threading.Thread.Sleep(600);
        for (var i = 0; i < 10; i++)
        {
            try
            {
                if (Directory.Exists(target)) Directory.Delete(target, true);
                break;
            }
            catch
            {
                System.Threading.Thread.Sleep(400);
            }
        }
        var tempDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        Process.Start(new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"" + tempDir + "\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }
}
