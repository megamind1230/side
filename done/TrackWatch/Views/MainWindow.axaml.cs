using Avalonia.Controls;
using TrackWatch.ViewModels;

namespace TrackWatch.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}