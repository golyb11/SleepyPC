using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PCsleePtime;

public partial class MainWindow : Window
{
    private DispatcherTimer _timer = null!;
    private DateTime _targetTime;
    private bool _isTimerRunning;
    private bool _notificationShown;
    private WinForms.NotifyIcon _trayIcon = null!;
    private NotificationWindow? _notificationWindow;

    private enum PowerAction { Shutdown, Sleep, Restart }
    private PowerAction _selectedAction = PowerAction.Shutdown;

    public MainWindow()
    {
        InitializeComponent();
        InitializeTimer();
        InitializeTrayIcon();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fadeIn);
    }

    private void InitializeTimer()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new WinForms.NotifyIcon();
        _trayIcon.Text = "SleepyPC";
        _trayIcon.Icon = CreateTrayIcon();
        _trayIcon.Visible = true;
        _trayIcon.DoubleClick += TrayIcon_DoubleClick;

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Развернуть", null, (_, _) => ShowFromTray());
        menu.Items.Add("Отменить таймер", null, (_, _) => CancelTimer());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => ExitApplication());
        _trayIcon.ContextMenuStrip = menu;
    }

    private System.Drawing.Icon CreateTrayIcon()
    {
        var bmp = new System.Drawing.Bitmap(32, 32);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(108, 99, 255));
            g.FillEllipse(brush, 2, 2, 28, 28);
            using var font = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold);
            using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            var sf = new System.Drawing.StringFormat
            {
                Alignment = System.Drawing.StringAlignment.Center,
                LineAlignment = System.Drawing.StringAlignment.Center
            };
            g.DrawString("S", font, textBrush, new System.Drawing.RectangleF(0, 0, 32, 32), sf);
        }
        var handle = bmp.GetHicon();
        return System.Drawing.Icon.FromHandle(handle);
    }

    private void TrayIcon_DoubleClick(object? sender, EventArgs e) => ShowFromTray();

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        _trayIcon.ShowBalloonTip(2000, "SleepyPC", "Приложение свёрнуто в трей", WinForms.ToolTipIcon.Info);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    private void ThemeToggle_Click(object sender, RoutedEventArgs e) => App.ToggleTheme();

    private void TimeMode_Changed(object sender, RoutedEventArgs e)
    {
        if (CountdownPanel == null || ExactTimePanel == null) return;

        if (RbCountdown.IsChecked == true)
        {
            CountdownPanel.Visibility = Visibility.Visible;
            ExactTimePanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            CountdownPanel.Visibility = Visibility.Collapsed;
            ExactTimePanel.Visibility = Visibility.Visible;
        }
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int minutes))
        {
            RbCountdown.IsChecked = true;
            TbHours.Text = (minutes / 60).ToString();
            TbMinutes.Text = (minutes % 60).ToString();
            StartTimer(TimeSpan.FromMinutes(minutes));
        }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        UpdateSelectedAction();
        TimeSpan duration;

        if (RbCountdown.IsChecked == true)
        {
            if (!int.TryParse(TbHours.Text, out int hours) || !int.TryParse(TbMinutes.Text, out int mins))
            {
                ShowError("Введите корректное время");
                return;
            }
            duration = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(mins);
        }
        else
        {
            if (!int.TryParse(TbExactHour.Text, out int hour) || !int.TryParse(TbExactMinute.Text, out int min)
                || hour < 0 || hour > 23 || min < 0 || min > 59)
            {
                ShowError("Введите корректное время (0-23 : 0-59)");
                return;
            }
            var target = DateTime.Today.AddHours(hour).AddMinutes(min);
            if (target <= DateTime.Now)
                target = target.AddDays(1);
            duration = target - DateTime.Now;
        }

        if (duration.TotalSeconds < 10)
        {
            ShowError("Минимальное время — 10 секунд");
            return;
        }

        StartTimer(duration);
    }

    private void UpdateSelectedAction()
    {
        if (RbSleep.IsChecked == true) _selectedAction = PowerAction.Sleep;
        else if (RbRestart.IsChecked == true) _selectedAction = PowerAction.Restart;
        else _selectedAction = PowerAction.Shutdown;
    }

    private void StartTimer(TimeSpan duration)
    {
        UpdateSelectedAction();
        _targetTime = DateTime.Now + duration;
        _isTimerRunning = true;
        _notificationShown = false;

        string actionText = _selectedAction switch
        {
            PowerAction.Shutdown => "ПК выключится через",
            PowerAction.Sleep => "ПК уснёт через",
            PowerAction.Restart => "ПК перезагрузится через",
            _ => "Действие через"
        };
        TbActionInfo.Text = actionText;
        TbTargetTime.Text = $"Запланировано на {_targetTime:HH:mm:ss}";

        SwitchToTimerView();
        _timer.Start();
        UpdateTrayTooltip();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var remaining = _targetTime - DateTime.Now;

        if (remaining.TotalSeconds <= 0)
        {
            _timer.Stop();
            _isTimerRunning = false;
            ExecuteAction();
            SwitchToSetupView();
            return;
        }

        TbCountdown.Text = $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        UpdateTrayTooltip();

        if (!_notificationShown && remaining.TotalSeconds <= 60 && remaining.TotalSeconds > 59)
        {
            _notificationShown = true;
            ShowNotification();
        }
    }

    private void ShowNotification()
    {
        _notificationWindow = new NotificationWindow(_selectedAction.ToString());
        _notificationWindow.PostponeRequested += OnPostponeRequested;
        _notificationWindow.Show();
    }

    private void OnPostponeRequested(object? sender, EventArgs e)
    {
        _targetTime = _targetTime.AddMinutes(15);
        _notificationShown = false;
        TbTargetTime.Text = $"Запланировано на {_targetTime:HH:mm:ss}";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => CancelTimer();

    private void CancelTimer()
    {
        _timer.Stop();
        _isTimerRunning = false;
        _notificationWindow?.Close();
        _notificationWindow = null;
        SwitchToSetupView();
        _trayIcon.Text = "SleepyPC — таймер не активен";
    }

    private void SwitchToTimerView()
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        fadeOut.Completed += (_, _) =>
        {
            SetupPanel.Visibility = Visibility.Collapsed;
            TimerPanel.Visibility = Visibility.Visible;
            TimerPanel.Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            TimerPanel.BeginAnimation(OpacityProperty, fadeIn);
        };
        SetupPanel.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void SwitchToSetupView()
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        fadeOut.Completed += (_, _) =>
        {
            TimerPanel.Visibility = Visibility.Collapsed;
            SetupPanel.Visibility = Visibility.Visible;
            SetupPanel.Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            SetupPanel.BeginAnimation(OpacityProperty, fadeIn);
        };
        TimerPanel.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void UpdateTrayTooltip()
    {
        if (_isTimerRunning)
        {
            var remaining = _targetTime - DateTime.Now;
            string action = _selectedAction switch
            {
                PowerAction.Shutdown => "Выключение",
                PowerAction.Sleep => "Сон",
                PowerAction.Restart => "Перезагрузка",
                _ => ""
            };
            string text = $"SleepyPC — {action} через {(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
            _trayIcon.Text = text.Length > 63 ? text[..63] : text;
        }
        else
        {
            _trayIcon.Text = "SleepyPC — таймер не активен";
        }
    }

    private void ExecuteAction()
    {
        switch (_selectedAction)
        {
            case PowerAction.Shutdown:
                Process.Start(new ProcessStartInfo("shutdown", "/s /t 0") { CreateNoWindow = true });
                break;
            case PowerAction.Restart:
                Process.Start(new ProcessStartInfo("shutdown", "/r /t 0") { CreateNoWindow = true });
                break;
            case PowerAction.Sleep:
                WinForms.Application.SetSuspendState(WinForms.PowerState.Suspend, false, false);
                break;
        }
    }

    private void ShowError(string message)
    {
        System.Windows.MessageBox.Show(message, "SleepyPC", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
