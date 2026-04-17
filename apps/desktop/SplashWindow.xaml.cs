using System.Windows;

namespace Central.Desktop;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    public void UpdateStatus(string text, int progress)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = text;
            ProgressBar.Value = progress;
        });
    }
}
