using System.Windows;

namespace PCsleePtime;

public partial class App : System.Windows.Application
{
    private static bool _isDarkTheme = false;

    public static bool IsDarkTheme => _isDarkTheme;

    public static void ToggleTheme()
    {
        _isDarkTheme = !_isDarkTheme;
        var dict = new ResourceDictionary();
        dict.Source = new Uri(_isDarkTheme
            ? "Themes/DarkTheme.xaml"
            : "Themes/LightTheme.xaml", UriKind.Relative);

        Current.Resources.MergedDictionaries.Clear();
        Current.Resources.MergedDictionaries.Add(dict);
    }
}
