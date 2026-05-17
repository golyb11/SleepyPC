using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PCsleePtime;

public partial class NotificationWindow : Window
{
    public event EventHandler? PostponeRequested;
    private DispatcherTimer _autoCloseTimer;

    public NotificationWindow(string actionName)
    {
        InitializeComponent();

        string actionText = actionName switch
        {
            "Shutdown" => "выключится",
            "Sleep" => "перейдёт в спящий режим",
            "Restart" => "перезагрузится",
            _ => "выполнит действие"
        };
        TbMessage.Text = $"Действие выполнится через 1 минуту. ПК {actionText}.";

        Loaded += OnLoaded;

        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _autoCloseTimer.Tick += (_, _) =>
        {
            _autoCloseTimer.Stop();
            Close();
        };
        _autoCloseTimer.Start();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 8;
        Top = workArea.Bottom - Height - 8;

        Opacity = 0;
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fadeIn);
    }

    private void Postpone_Click(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        PostponeRequested?.Invoke(this, EventArgs.Empty);

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        fadeOut.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fadeOut);
    }
}
