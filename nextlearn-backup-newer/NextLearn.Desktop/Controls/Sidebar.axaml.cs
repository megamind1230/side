using Avalonia.Controls;
using Avalonia.Input;
using NextLearn.Desktop.ViewModels;

namespace NextLearn.Desktop.Controls;

public partial class Sidebar : UserControl
{
    public Sidebar()
    {
        InitializeComponent();
    }

    private void CloseOnBackdrop(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ToggleSidebarCommand.Execute(null);
        }
    }
}
