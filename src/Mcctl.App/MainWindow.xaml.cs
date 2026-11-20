using System.ComponentModel;
using System.Windows;
using Mcctl.App.ViewModels;

namespace Mcctl.App;

public partial class MainWindow : Window
{
    /// <summary>Below this size the window switches to the mini player layout.</summary>
    private static readonly Size CompactThreshold = new(720, 480);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.StartAsync();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    /// <summary>Puts the caret in the search box as soon as it appears, so the user can type right away.</summary>
    private void OnSearchBoxVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && sender is UIElement box)
        {
            Dispatcher.BeginInvoke(() => box.Focus(), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        => ViewModel.IsCompact = e.NewSize.Width < CompactThreshold.Width || e.NewSize.Height < CompactThreshold.Height;

    protected override async void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        await ViewModel.DisposeAsync();
    }
}
