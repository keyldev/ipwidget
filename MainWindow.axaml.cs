using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using IpWidget.Services;

namespace IpWidget;

public partial class MainWindow : Window
{
    private readonly IpService _service = new();
    private readonly GeoService _geo = new();
    private readonly FlagService _flags = new();
    private readonly Settings _settings = Settings.Load();
    private readonly Dictionary<string, SourceRow> _rows = new();
    private CancellationTokenSource? _inflight;
    private DispatcherTimer? _autoTimer;
    private bool _busy;
    private string? _lastCountryCode;

    /// <summary>Set by App; performs a real application shutdown (window lives in tray).</summary>
    public Action? RequestQuit { get; set; }

    // full view
    private TextBlock _ipText = null!, _statusText = null!, _checkLabel = null!;
    private ItemsControl _sourcesList = null!;
    private Button _checkBtn = null!;
    private PathIcon _pinIcon = null!;
    private StackPanel _geoPanel = null!;
    private Image _flagImg = null!;
    private TextBlock _geoText = null!;
    private Border _vpnChip = null!;
    private PathIcon _vpnIcon = null!;
    private TextBlock _vpnText = null!;

    // hud view
    private Grid _hudView = null!;
    private Border _fullCard = null!;
    private Border _settingsOverlay = null!;
    private Image _hudFlag = null!;
    private TextBlock _hudIp = null!, _hudCountry = null!;
    private StackPanel _hudControls = null!;

    // settings controls
    private RadioButton _closeTrayRadio = null!, _closeQuitRadio = null!;
    private CheckBox _topmostChk = null!, _flagChk = null!, _compactChk = null!;

    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#B9C7EC"));
    private static readonly IBrush Accent = new SolidColorBrush(Color.Parse("#4FD1FF"));

    public MainWindow()
    {
        InitializeComponent();

        _ipText = this.FindControl<TextBlock>("IpText")!;
        _statusText = this.FindControl<TextBlock>("StatusText")!;
        _checkLabel = this.FindControl<TextBlock>("CheckLabel")!;
        _sourcesList = this.FindControl<ItemsControl>("SourcesList")!;
        _checkBtn = this.FindControl<Button>("CheckBtn")!;
        _pinIcon = this.FindControl<PathIcon>("PinIcon")!;
        _geoPanel = this.FindControl<StackPanel>("GeoPanel")!;
        _flagImg = this.FindControl<Image>("FlagImg")!;
        _geoText = this.FindControl<TextBlock>("GeoText")!;
        _vpnChip = this.FindControl<Border>("VpnChip")!;
        _vpnIcon = this.FindControl<PathIcon>("VpnIcon")!;
        _vpnText = this.FindControl<TextBlock>("VpnText")!;

        _hudView = this.FindControl<Grid>("HudView")!;
        _fullCard = this.FindControl<Border>("FullCard")!;
        _settingsOverlay = this.FindControl<Border>("SettingsOverlay")!;
        _hudFlag = this.FindControl<Image>("HudFlag")!;
        _hudIp = this.FindControl<TextBlock>("HudIp")!;
        _hudCountry = this.FindControl<TextBlock>("HudCountry")!;
        _hudControls = this.FindControl<StackPanel>("HudControls")!;

        _closeTrayRadio = this.FindControl<RadioButton>("CloseTrayRadio")!;
        _closeQuitRadio = this.FindControl<RadioButton>("CloseQuitRadio")!;
        _topmostChk = this.FindControl<CheckBox>("TopmostChk")!;
        _flagChk = this.FindControl<CheckBox>("FlagChk")!;
        _compactChk = this.FindControl<CheckBox>("CompactChk")!;

        // ---- full view wiring ----
        this.FindControl<Grid>("TitleBar")!.PointerPressed += OnDrag;
        this.FindControl<Button>("SettingsBtn")!.Click += (_, _) => ShowSettings(true);
        this.FindControl<Button>("CompactBtn")!.Click += (_, _) => SetCompact(true);
        this.FindControl<Button>("PinBtn")!.Click += OnPin;
        this.FindControl<Button>("MinBtn")!.Click += (_, _) => Hide();
        this.FindControl<Button>("CloseBtn")!.Click += (_, _) => OnCloseRequested();
        _checkBtn.Click += async (_, _) => await CheckAsync();
        this.FindControl<Button>("CopyBtn")!.Click += OnCopy;
        this.FindControl<ToggleButton>("AutoBtn")!.IsCheckedChanged += OnAutoToggled;

        // ---- hud view wiring ----
        _hudView.PointerPressed += OnDrag;
        _hudView.PointerEntered += (_, _) => _hudControls.Opacity = 1;
        _hudView.PointerExited += (_, _) => _hudControls.Opacity = 0;
        this.FindControl<Button>("HudRefreshBtn")!.Click += async (_, _) => await CheckAsync();
        this.FindControl<Button>("HudExpandBtn")!.Click += (_, _) => SetCompact(false);
        this.FindControl<Button>("HudCloseBtn")!.Click += (_, _) => OnCloseRequested();

        // ---- settings wiring ----
        this.FindControl<Button>("SettingsBackBtn")!.Click += (_, _) => ShowSettings(false);
        _closeTrayRadio.IsCheckedChanged += OnSettingChanged;
        _closeQuitRadio.IsCheckedChanged += OnSettingChanged;
        _topmostChk.IsCheckedChanged += OnSettingChanged;
        _flagChk.IsCheckedChanged += OnSettingChanged;
        _compactChk.IsCheckedChanged += OnCompactCheckChanged;

        ApplySettingsToUi();
        BuildRows();

        Opened += OnOpened;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (_settings.X is { } x && _settings.Y is { } y)
            Position = new PixelPoint(x, y);

        Topmost = _settings.Topmost;
        _pinIcon.Foreground = Topmost ? Accent : Muted;
        SetCompact(_settings.Compact, save: false);

        await CheckAsync();
    }

    // ---------- tray hooks ----------
    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public void TriggerCheck() => _ = CheckAsync();

    // ---------- mode / settings ----------
    private void SetCompact(bool compact, bool save = true)
    {
        _settings.Compact = compact;
        _settingsOverlay.IsVisible = false;
        _fullCard.IsVisible = !compact;
        _hudView.IsVisible = compact;

        if (compact)
        {
            Width = 250;
            Height = 128;
        }
        else
        {
            Width = 340;
            Height = 452;
        }

        if (_compactChk.IsChecked != compact) _compactChk.IsChecked = compact;
        if (save) Save();
    }

    private void ShowSettings(bool show)
    {
        if (show) ApplySettingsToUi();
        _settingsOverlay.IsVisible = show;
    }

    private void ApplySettingsToUi()
    {
        _closeTrayRadio.IsChecked = _settings.CloseToTray;
        _closeQuitRadio.IsChecked = !_settings.CloseToTray;
        _topmostChk.IsChecked = _settings.Topmost;
        _flagChk.IsChecked = _settings.ShowFlag;
        _compactChk.IsChecked = _settings.Compact;
    }

    private void OnSettingChanged(object? sender, RoutedEventArgs e)
    {
        _settings.CloseToTray = _closeTrayRadio.IsChecked == true;
        _settings.Topmost = _topmostChk.IsChecked == true;
        _settings.ShowFlag = _flagChk.IsChecked == true;

        Topmost = _settings.Topmost;
        _pinIcon.Foreground = Topmost ? Accent : Muted;

        if (!_settings.ShowFlag)
        {
            _flagImg.IsVisible = false;
            _hudFlag.IsVisible = false;
        }
        else if (_lastCountryCode is not null)
        {
            _ = LoadFlagAsync(_lastCountryCode);
        }

        Save();
    }

    private void OnCompactCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (_compactChk.IsChecked is { } want && want != _settings.Compact)
            SetCompact(want);
    }

    private void OnCloseRequested()
    {
        if (_settings.CloseToTray) Hide();
        else (RequestQuit ?? Close)();
    }

    private void Save()
    {
        var p = Position;
        _settings.X = p.X;
        _settings.Y = p.Y;
        _settings.Save();
    }

    // ---------- checking ----------
    private void BuildRows()
    {
        var panel = new List<Control>();
        foreach (var r in _service.CreatePending())
        {
            var row = new SourceRow(r.Name);
            _rows[r.Name] = row;
            panel.Add(row.Root);
        }
        _sourcesList.ItemsSource = panel;
    }

    private async Task CheckAsync()
    {
        if (_busy) return;
        _busy = true;
        _checkBtn.IsEnabled = false;
        _checkLabel.Text = "Проверяю…";
        _statusText.Text = "опрашиваю источники…";
        _geoPanel.IsVisible = false;
        _vpnChip.IsVisible = false;

        _inflight?.Cancel();
        _inflight = new CancellationTokenSource();
        var ct = _inflight.Token;

        var results = _service.CreatePending();
        foreach (var r in results)
            _rows[r.Name].SetPending();

        try
        {
            var consensus = await _service.CheckAllAsync(results, r =>
                Dispatcher.UIThread.Post(() => _rows[r.Name].Update(r)), ct);

            if (ct.IsCancellationRequested) return;

            int ok = 0;
            foreach (var r in results)
                if (r.State == SourceState.Ok) ok++;

            if (consensus is not null)
            {
                _ipText.Text = consensus;
                _hudIp.Text = consensus;
                _statusText.Text = $"подтверждено {ok}/{results.Count} источниками";
                await LoadGeoAsync(consensus, ct);
            }
            else
            {
                _ipText.Text = "—";
                _hudIp.Text = "нет сети";
                _statusText.Text = "не удалось определить IP (нет сети?)";
            }
        }
        finally
        {
            _busy = false;
            _checkBtn.IsEnabled = true;
            _checkLabel.Text = "Проверить";
        }
    }

    private async Task LoadGeoAsync(string ip, CancellationToken ct)
    {
        var info = await _geo.LookupAsync(ip, ct);
        if (ct.IsCancellationRequested || info is null) return;

        _geoText.Text = string.IsNullOrWhiteSpace(info.Location)
            ? info.Provider
            : $"{info.Location} · {info.Provider}";
        _geoPanel.IsVisible = true;
        _hudCountry.Text = info.Location;

        _lastCountryCode = info.CountryCode;
        if (_settings.ShowFlag && info.CountryCode is not null)
            await LoadFlagAsync(info.CountryCode, ct);

        if (info.IsHosting)
        {
            _vpnChip.Background = new SolidColorBrush(Color.Parse("#33FFB020"));
            _vpnChip.BorderThickness = new Thickness(1);
            _vpnChip.BorderBrush = new SolidColorBrush(Color.Parse("#66FFB020"));
            _vpnIcon.Data = (Geometry)this.FindResource("IconShieldAlert")!;
            _vpnIcon.Foreground = new SolidColorBrush(Color.Parse("#FFC85A"));
            _vpnText.Foreground = new SolidColorBrush(Color.Parse("#FFD98A"));
            _vpnText.Text = "Похоже на VPN / хостинг";
        }
        else
        {
            _vpnChip.Background = new SolidColorBrush(Color.Parse("#2633E38B"));
            _vpnChip.BorderThickness = new Thickness(1);
            _vpnChip.BorderBrush = new SolidColorBrush(Color.Parse("#5533E38B"));
            _vpnIcon.Data = (Geometry)this.FindResource("IconShieldCheck")!;
            _vpnIcon.Foreground = new SolidColorBrush(Color.Parse("#3BE38B"));
            _vpnText.Foreground = new SolidColorBrush(Color.Parse("#8CF0BE"));
            _vpnText.Text = "Резидентский IP";
        }
        _vpnChip.IsVisible = true;
    }

    private async Task LoadFlagAsync(string cc, CancellationToken ct = default)
    {
        Bitmap? bmp = await _flags.GetAsync(cc, ct);
        if (ct.IsCancellationRequested) return;

        if (bmp is null)
        {
            _flagImg.IsVisible = false;
            _hudFlag.IsVisible = false;
            return;
        }

        _flagImg.Source = bmp;
        _flagImg.IsVisible = true;
        _hudFlag.Source = bmp;
        _hudFlag.IsVisible = _settings.ShowFlag;
    }

    // ---------- misc handlers ----------
    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnPin(object? sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        _settings.Topmost = Topmost;
        _pinIcon.Foreground = Topmost ? Accent : Muted;
        Save();
    }

    private async void OnCopy(object? sender, RoutedEventArgs e)
    {
        var ip = _ipText.Text;
        if (string.IsNullOrWhiteSpace(ip) || ip == "—") return;
        if (Clipboard is { } clip)
        {
            await clip.SetTextAsync(ip);
            _statusText.Text = $"скопировано: {ip}";
        }
    }

    private void OnAutoToggled(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: true })
        {
            _autoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _autoTimer.Tick += async (_, _) => await CheckAsync();
            _autoTimer.Start();
        }
        else
        {
            _autoTimer?.Stop();
            _autoTimer = null;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Save();
        _inflight?.Cancel();
        _autoTimer?.Stop();
        _service.Dispose();
        _geo.Dispose();
        _flags.Dispose();
        base.OnClosed(e);
    }
}

/// <summary>One row per source: status dot + name + ip + latency badge.</summary>
internal sealed class SourceRow
{
    public Border Root { get; }
    private readonly Ellipse _dot;
    private readonly TextBlock _ip;
    private readonly TextBlock _latency;

    private static readonly IBrush Pending = new SolidColorBrush(Color.Parse("#5A6488"));
    private static readonly IBrush Ok = new SolidColorBrush(Color.Parse("#3BE38B"));
    private static readonly IBrush Fail = new SolidColorBrush(Color.Parse("#FF6B8B"));
    private static readonly IBrush IpMuted = new SolidColorBrush(Color.Parse("#8FA0C8"));
    private static readonly IBrush IpOk = new SolidColorBrush(Color.Parse("#EAF2FF"));
    private static readonly IBrush IpFail = new SolidColorBrush(Color.Parse("#FF9DB3"));

    public SourceRow(string name)
    {
        _dot = new Ellipse { Width = 9, Height = 9, Fill = Pending, VerticalAlignment = VerticalAlignment.Center };
        _ip = new TextBlock { Text = "…", Foreground = IpMuted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        _latency = new TextBlock { Text = "", Foreground = new SolidColorBrush(Color.Parse("#6E7AA5")), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };
        grid.Children.Add(_dot);
        Grid.SetColumn(_dot, 0);

        var nameBlock = new TextBlock
        {
            Text = name,
            Foreground = new SolidColorBrush(Color.Parse("#CFE3FF")),
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Width = 78,
        };
        grid.Children.Add(nameBlock);
        Grid.SetColumn(nameBlock, 1);

        _ip.Margin = new Thickness(6, 0, 0, 0);
        grid.Children.Add(_ip);
        Grid.SetColumn(_ip, 2);

        grid.Children.Add(_latency);
        Grid.SetColumn(_latency, 3);

        Root = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#12FFFFFF")),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 9),
            Child = grid,
        };
    }

    public void SetPending()
    {
        _dot.Fill = Pending;
        _ip.Text = "…";
        _ip.Foreground = IpMuted;
        _latency.Text = "";
    }

    public void Update(IpSourceResult r)
    {
        switch (r.State)
        {
            case SourceState.Ok:
                _dot.Fill = Ok;
                _ip.Text = r.Ip;
                _ip.Foreground = IpOk;
                _latency.Text = $"{r.ElapsedMs} ms";
                break;
            case SourceState.Failed:
                _dot.Fill = Fail;
                _ip.Text = r.Error ?? "ошибка";
                _ip.Foreground = IpFail;
                _latency.Text = r.ElapsedMs > 0 ? $"{r.ElapsedMs} ms" : "";
                break;
            default:
                SetPending();
                break;
        }
    }
}
