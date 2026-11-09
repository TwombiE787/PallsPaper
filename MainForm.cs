using System.Text.Json;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;

namespace PallsPaper;

internal sealed class MainForm : Form
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunRegistryValue = "PallsPaper";

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".gif", ".htm", ".html", ".jpeg", ".jpg",
            ".mp4", ".ogv", ".png", ".svg", ".webm", ".webp"
        };

    private readonly TextBox _addressBox = new()
    {
        BackColor = UiColors.Input,
        BorderStyle = BorderStyle.FixedSingle,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 10),
        ForeColor = UiColors.Text,
        PlaceholderText = "https://example.com/wallpaper"
    };
    private readonly Label _fileLabel = CreateLabel("Noch keine Datei ausgewählt", UiColors.Muted);
    private readonly Label _activeLabel = CreateLabel("Kein aktives Wallpaper", UiColors.Text, 12, true);
    private readonly Label _monitorLabel = CreateLabel("Deine Displays sind bereit", UiColors.Muted);
    private readonly Label _statusLabel = CreateLabel("Wähle ein Wallpaper und klicke auf Starten.", UiColors.Muted);
    private readonly StatusDot _statusDot = new() { DotColor = UiColors.Muted, Size = new Size(10, 10) };
    private readonly Button _startButton = CreateButton("▶   Starten", primary: true);
    private readonly Button _stopButton = CreateButton("■   Stoppen");
    private readonly CheckBox _autoStartCheckBox = new()
    {
        AutoSize = true,
        BackColor = UiColors.Background,
        ForeColor = UiColors.Muted,
        Text = "Mit Windows starten",
        UseVisualStyleBackColor = false
    };
    private readonly List<WallpaperWindow> _wallpapers = [];
    private readonly AppSettings _settings;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _trayStopItem;
    private string? _selectedFile;
    private bool _isStarting;
    private bool _isClosing;
    private bool _cancelStart;
    private bool _exitRequested;
    private bool _changingAutoStart;

    public MainForm(bool launchInBackground)
    {
        _settings = LoadSettings();
        _selectedFile = _settings.LastWallpaperIsWebAddress ? null : _settings.LastWallpaper;
        if (_settings.LastWallpaperIsWebAddress)
        {
            _addressBox.Text = _settings.LastWallpaper ?? string.Empty;
        }

        Text = "PallsPaper";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(640, 590);
        ClientSize = new Size(700, 640);
        MaximizeBox = false;
        ShowIcon = true;
        Icon = Branding.CreateIcon();
        Font = new Font("Segoe UI", 9.5f);
        BackColor = UiColors.Background;
        ForeColor = UiColors.Text;

        var trayMenu = new ContextMenuStrip
        {
            BackColor = UiColors.Card,
            ForeColor = UiColors.Text,
            ShowImageMargin = false
        };
        var openItem = new ToolStripMenuItem("PallsPaper öffnen");
        _trayStopItem = new ToolStripMenuItem("Wallpaper stoppen");
        var exitItem = new ToolStripMenuItem("Beenden");
        openItem.Click += (_, _) => ShowWindow();
        _trayStopItem.Click += (_, _) => StopWallpaperRequested();
        exitItem.Click += (_, _) => ExitApplication();
        trayMenu.Items.AddRange([openItem, _trayStopItem, new ToolStripSeparator(), exitItem]);

        _trayIcon = new NotifyIcon
        {
            ContextMenuStrip = trayMenu,
            Icon = Branding.CreateIcon(),
            Text = "PallsPaper",
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowWindow();

        Controls.Add(BuildInterface());
        _startButton.Click += async (_, _) => await StartWallpaperAsync();
        _stopButton.Click += (_, _) => StopWallpaperRequested();
        _autoStartCheckBox.Checked = IsAutoStartEnabled();
        _autoStartCheckBox.CheckedChanged += AutoStartCheckBoxCheckedChanged;
        _addressBox.KeyDown += async (_, eventArgs) =>
        {
            if (eventArgs.KeyCode == Keys.Enter)
            {
                eventArgs.SuppressKeyPress = true;
                await StartWallpaperAsync();
            }
        };

        AllowDrop = true;
        DragEnter += HandleDragEnter;
        DragDrop += HandleDragDrop;
        FormClosing += HandleFormClosing;
        UpdatePlaybackControls();
        Shown += async (_, _) =>
        {
            if (launchInBackground)
            {
                Hide();
            }

            if (_settings.LastWallpaper is not null
                && (_settings.LastWallpaperIsWebAddress || File.Exists(_settings.LastWallpaper)))
            {
                await StartWallpaperAsync();
            }
        };
    }

    private Control BuildInterface()
    {
        var page = new TableLayoutPanel
        {
            BackColor = UiColors.Background,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 26, 28, 20),
            RowCount = 5
        };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(BuildHeader(), 0, 0);
        page.Controls.Add(BuildPlaybackSection(), 0, 1);
        page.Controls.Add(BuildFileSection(), 0, 2);
        page.Controls.Add(BuildWebSection(), 0, 3);
        page.Controls.Add(BuildFooter(), 0, 4);
        return page;
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            BackColor = UiColors.Background,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 15)
        };
        var logo = new GlyphIcon(GlyphKind.Play, UiColors.Accent, 30)
        {
            Location = new Point(2, 8),
            Size = new Size(30, 30)
        };
        var title = CreateLabel("PallsPaper", UiColors.Text, 19, true);
        title.Location = new Point(54, 2);
        title.AutoSize = true;
        var subtitle = CreateLabel("Animierte Wallpaper für deinen Desktop", UiColors.Muted);
        subtitle.Location = new Point(56, 35);
        subtitle.AutoSize = true;
        header.Controls.AddRange([logo, title, subtitle]);
        return header;
    }

    private Control BuildPlaybackSection()
    {
        var card = CreateCard();
        var layout = new TableLayoutPanel
        {
            BackColor = UiColors.Card,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(17, 13, 17, 12),
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        var heading = CreateSectionHeading("AKTUELLER DESKTOP");
        var active = new TableLayoutPanel
        {
            BackColor = UiColors.Card,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            RowCount = 2
        };
        active.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
        active.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        active.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        active.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        var monitorIcon = new GlyphIcon(GlyphKind.Monitor, UiColors.Accent, 22)
        {
            Anchor = AnchorStyles.Left,
            BackColor = Color.Transparent
        };
        active.Controls.Add(monitorIcon, 0, 0);
        active.SetRowSpan(monitorIcon, 2);
        _activeLabel.Dock = DockStyle.Fill;
        _monitorLabel.Dock = DockStyle.Fill;
        active.Controls.Add(_activeLabel, 1, 0);
        active.Controls.Add(_monitorLabel, 1, 1);

        var buttons = new FlowLayoutPanel
        {
            BackColor = UiColors.Card,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 4, 0, 0),
            WrapContents = false
        };
        _startButton.Size = new Size(128, 34);
        _stopButton.Size = new Size(128, 34);
        buttons.Controls.Add(_startButton);
        buttons.Controls.Add(_stopButton);
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(active, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildFileSection()
    {
        var card = CreateCard();
        var layout = new TableLayoutPanel
        {
            BackColor = UiColors.Card,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(17, 12, 17, 12),
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

        var heading = CreateSectionHeading("LOKALE DATEI");
        var fileRow = new TableLayoutPanel
        {
            BackColor = UiColors.Card,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            RowCount = 1
        };
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        _fileLabel.Dock = DockStyle.Fill;
        _fileLabel.TextAlign = ContentAlignment.MiddleLeft;
        var browseButton = CreateButton("Ordner / Datei wählen");
        browseButton.Dock = DockStyle.Fill;
        browseButton.Margin = new Padding(8, 0, 0, 0);
        browseButton.Click += (_, _) => ChooseFile();
        fileRow.Controls.Add(_fileLabel, 0, 0);
        fileRow.Controls.Add(browseButton, 1, 0);
        var hint = CreateLabel("MP4, WebM, GIF, Bilder oder HTML", UiColors.Muted, 8.5f);
        hint.Dock = DockStyle.Fill;
        hint.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(fileRow, 0, 1);
        layout.Controls.Add(hint, 0, 2);
        card.Controls.Add(layout);
        card.AllowDrop = true;
        card.DragEnter += HandleDragEnter;
        card.DragDrop += HandleDragDrop;
        return card;
    }

    private Control BuildWebSection()
    {
        var card = CreateCard();
        var layout = new TableLayoutPanel
        {
            BackColor = UiColors.Card,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(17, 12, 17, 12),
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var heading = CreateSectionHeading("WEB-WALLPAPER");
        var inputFrame = new Panel
        {
            BackColor = UiColors.Input,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 3, 0, 0),
            Padding = new Padding(8, 5, 8, 5)
        };
        _addressBox.Dock = DockStyle.Fill;
        inputFrame.Controls.Add(_addressBox);
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(inputFrame, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildFooter()
    {
        var footer = new Panel
        {
            BackColor = UiColors.Background,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 12, 0, 0)
        };
        var separator = new Panel
        {
            BackColor = UiColors.Border,
            Dock = DockStyle.Top,
            Height = 1
        };
        var bottom = new TableLayoutPanel
        {
            BackColor = UiColors.Background,
            ColumnCount = 2,
            Dock = DockStyle.Bottom,
            Height = 40,
            Margin = new Padding(0),
            RowCount = 1
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var status = new TableLayoutPanel
        {
            BackColor = UiColors.Background,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            RowCount = 1
        };
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _statusDot.Anchor = AnchorStyles.Left;
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.AutoEllipsis = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        status.Controls.Add(_statusDot, 0, 0);
        status.Controls.Add(_statusLabel, 1, 0);
        _autoStartCheckBox.Dock = DockStyle.Fill;
        _autoStartCheckBox.Margin = new Padding(10, 0, 0, 0);
        bottom.Controls.Add(status, 0, 0);
        bottom.Controls.Add(_autoStartCheckBox, 1, 0);
        footer.Controls.Add(bottom);
        footer.Controls.Add(separator);
        return footer;
    }

    private static Panel CreateCard() => new()
    {
        BackColor = UiColors.Card,
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 0, 0, 12),
        Padding = new Padding(1)
    };

    private static Label CreateSectionHeading(string text) => CreateLabel(text, UiColors.Muted, 8.5f, true);

    private static Label CreateLabel(string text, Color color, float size = 9.5f, bool bold = false) => new()
    {
        AutoEllipsis = true,
        BackColor = Color.Transparent,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        ForeColor = color,
        Text = text
    };

    private static Button CreateButton(string text, bool primary = false)
    {
        var button = new Button
        {
            BackColor = primary ? UiColors.Accent : UiColors.Button,
            FlatStyle = FlatStyle.Flat,
            ForeColor = UiColors.Text,
            Margin = new Padding(0, 0, 8, 0),
            Text = text,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = primary ? UiColors.Accent : UiColors.Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = primary ? UiColors.AccentHover : UiColors.ButtonHover;
        return button;
    }

    private async Task StartWallpaperAsync()
    {
        if (_isStarting)
        {
            return;
        }

        var address = _addressBox.Text.Trim();
        var isWebAddress = address.Length > 0;
        string source;

        if (isWebAddress)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                MessageBox.Show(this, "Gib eine gültige Webadresse mit http:// oder https:// ein.", "Ungültige Adresse",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            source = uri.AbsoluteUri;
        }
        else if (_selectedFile is not null && File.Exists(_selectedFile))
        {
            if (!SupportedExtensions.Contains(Path.GetExtension(_selectedFile)))
            {
                MessageBox.Show(this, "Dieses Dateiformat wird nicht unterstützt.", "Nicht unterstütztes Format",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            source = _selectedFile;
        }
        else
        {
            MessageBox.Show(this, "Wähle zuerst eine Wallpaper-Datei oder gib eine Webadresse ein.", "Kein Wallpaper ausgewählt",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _isStarting = true;
        _cancelStart = false;
        _startButton.Enabled = false;
        SetStatus("Wallpaper wird gestartet...", UiColors.Warning);
        try
        {
            StopWallpapers();
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PallsPaper",
                "WebView2");
            CoreWebView2Environment? environment = null;
            if (!WallpaperWindow.UsesNativeImageRenderer(source, isWebAddress))
            {
                environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            }

            if (_isClosing || _cancelStart)
            {
                return;
            }

            foreach (var screen in Screen.AllScreens)
            {
                var wallpaper = new WallpaperWindow();
                _wallpapers.Add(wallpaper);
                DesktopWallpaperHost.Attach(wallpaper, screen.Bounds);
                await wallpaper.LoadWallpaperAsync(environment, source, isWebAddress);
            }

            if (!_isClosing && !_cancelStart)
            {
                _settings.LastWallpaper = source;
                _settings.LastWallpaperIsWebAddress = isWebAddress;
                _activeLabel.Text = isWebAddress ? source : Path.GetFileName(source);
                _monitorLabel.Text = $"Wird auf {Screen.AllScreens.Length} Monitor(en) angezeigt";
                _fileLabel.Text = isWebAddress ? "Web-Wallpaper aktiv" : Path.GetFileName(source);
                SetStatus("Wallpaper läuft und ist auf deinem Desktop aktiv.", UiColors.Success);
                UpdatePlaybackControls();
                try
                {
                    SaveSettings();
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    SetStatus($"Wallpaper läuft, aber Einstellungen konnten nicht gespeichert werden: {exception.Message}",
                        UiColors.Warning);
                }
            }
        }
        catch (Exception exception)
        {
            StopWallpapers();
            if (!_isClosing && !_cancelStart)
            {
                SetStatus("Wallpaper konnte nicht gestartet werden.", UiColors.Error);
                MessageBox.Show(this,
                    $"PallsPaper konnte das Wallpaper nicht starten.\n\n{exception.Message}\n\n" +
                    "Für die Wiedergabe muss die Microsoft Edge WebView2 Runtime installiert sein.",
                    "Start fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            _isStarting = false;
            if (_cancelStart && !_isClosing)
            {
                SetStatus("Wallpaper gestoppt.", UiColors.Muted);
            }

            if (!IsDisposed && !Disposing)
            {
                _startButton.Enabled = true;
                UpdatePlaybackControls();
            }
        }
    }

    private void StopWallpapers()
    {
        foreach (var wallpaper in _wallpapers)
        {
            wallpaper.Close();
            wallpaper.Dispose();
        }

        _wallpapers.Clear();
        if (!_isStarting)
        {
            SetStatus("Wallpaper gestoppt. Dein Desktop ist wieder frei.", UiColors.Muted);
        }

        _activeLabel.Text = "Kein aktives Wallpaper";
        _monitorLabel.Text = "Deine Displays sind bereit";
        UpdatePlaybackControls();
    }

    private void StopWallpaperRequested()
    {
        _cancelStart = true;
        StopWallpapers();
    }

    private void UpdatePlaybackControls()
    {
        var active = _wallpapers.Count > 0;
        _stopButton.Enabled = active;
        _trayStopItem.Enabled = active;
        _statusDot.DotColor = active ? UiColors.Success : UiColors.Muted;
        _statusDot.Invalidate();
    }

    private void ChooseFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Wallpaper auswählen",
            Filter = "Unterstützte Dateien|*.bmp;*.gif;*.htm;*.html;*.jpeg;*.jpg;*.mp4;*.ogv;*.png;*.svg;*.webm;*.webp|Alle Dateien|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetSelectedFile(dialog.FileName);
        }
    }

    private void SetSelectedFile(string path)
    {
        if (!File.Exists(path) || !SupportedExtensions.Contains(Path.GetExtension(path)))
        {
            SetStatus("Diese Datei wird nicht als Wallpaper unterstützt.", UiColors.Error);
            return;
        }

        _selectedFile = path;
        _fileLabel.Text = Path.GetFileName(path);
        _fileLabel.ForeColor = UiColors.Text;
        _addressBox.Clear();
        SetStatus("Datei bereit. Klicke auf Starten.", UiColors.Muted);
    }

    private void HandleDragEnter(object? sender, DragEventArgs eventArgs)
    {
        if (eventArgs.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            eventArgs.Effect = DragDropEffects.Copy;
        }
    }

    private void HandleDragDrop(object? sender, DragEventArgs eventArgs)
    {
        if (eventArgs.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            SetSelectedFile(files[0]);
        }
    }

    private void HandleFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (!_exitRequested && eventArgs.CloseReason == CloseReason.UserClosing)
        {
            eventArgs.Cancel = true;
            Hide();
            _trayIcon.ShowBalloonTip(
                1600,
                "PallsPaper läuft weiter",
                "Das Wallpaper bleibt aktiv. Zum Beenden das Infobereich-Symbol öffnen.",
                ToolTipIcon.Info);
            return;
        }

        _isClosing = true;
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Icon?.Dispose();
        StopWallpapers();
    }

    private void ShowWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    private void UpdateAutoStart()
    {
        if (_changingAutoStart)
        {
            return;
        }

        try
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(RunRegistryKey);
            if (_autoStartCheckBox.Checked)
            {
                runKey.SetValue(RunRegistryValue, $"\"{Application.ExecutablePath}\" --background");
                _settings.StartWithWindows = true;
            }
            else
            {
                runKey.DeleteValue(RunRegistryValue, throwOnMissingValue: false);
                _settings.StartWithWindows = false;
            }

            SaveSettings();
            SetStatus(
                _autoStartCheckBox.Checked ? "PallsPaper startet künftig mit Windows." : "Windows-Autostart deaktiviert.",
                UiColors.Muted);
        }
        catch (Exception exception)
        {
            _changingAutoStart = true;
            _autoStartCheckBox.Checked = !_autoStartCheckBox.Checked;
            _changingAutoStart = false;
            SetStatus($"Autostart konnte nicht geändert werden: {exception.Message}", UiColors.Error);
        }
    }

    private void AutoStartCheckBoxCheckedChanged(object? sender, EventArgs eventArgs) => UpdateAutoStart();

    private bool IsAutoStartEnabled()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: false);
        return runKey?.GetValue(RunRegistryValue) is string;
    }

    private AppSettings LoadSettings()
    {
        var settingsPath = GetSettingsPath();
        if (!File.Exists(settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath)) ?? new AppSettings();
        }
        catch (JsonException exception)
        {
            MessageBox.Show(
                $"Die PallsPaper-Einstellungen konnten nicht gelesen werden.\n\n{exception.Message}",
                "Einstellungen fehlerhaft", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return new AppSettings();
        }
        catch (IOException exception)
        {
            MessageBox.Show(
                $"Die PallsPaper-Einstellungen konnten nicht gelesen werden.\n\n{exception.Message}",
                "Einstellungen nicht verfügbar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return new AppSettings();
        }
    }

    private void SaveSettings()
    {
        var settingsPath = GetSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string GetSettingsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PallsPaper",
        "settings.json");

    private void SetStatus(string text, Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = color;
    }

    private static class UiColors
    {
        public static readonly Color Background = Color.FromArgb(20, 23, 32);
        public static readonly Color Card = Color.FromArgb(31, 36, 48);
        public static readonly Color Input = Color.FromArgb(22, 26, 37);
        public static readonly Color Border = Color.FromArgb(53, 60, 75);
        public static readonly Color Text = Color.FromArgb(236, 239, 246);
        public static readonly Color Muted = Color.FromArgb(158, 166, 181);
        public static readonly Color Accent = Color.FromArgb(91, 112, 235);
        public static readonly Color AccentHover = Color.FromArgb(106, 127, 249);
        public static readonly Color Button = Color.FromArgb(44, 51, 66);
        public static readonly Color ButtonHover = Color.FromArgb(56, 64, 81);
        public static readonly Color Success = Color.FromArgb(82, 202, 151);
        public static readonly Color Warning = Color.FromArgb(234, 183, 91);
        public static readonly Color Error = Color.FromArgb(237, 112, 122);
    }

    private sealed class StatusDot : Control
    {
        public Color DotColor { get; set; } = UiColors.Muted;

        public StatusDot()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            using var brush = new SolidBrush(DotColor);
            eventArgs.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            eventArgs.Graphics.FillEllipse(brush, 1, 1, Width - 2, Height - 2);
        }
    }
}
