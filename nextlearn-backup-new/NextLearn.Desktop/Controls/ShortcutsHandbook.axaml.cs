using Avalonia.Controls;
using Avalonia.Input;
using NextLearn.Desktop.ViewModels;

namespace NextLearn.Desktop.Controls;

public partial class ShortcutsHandbook : UserControl
{
    public ShortcutsHandbook()
    {
        InitializeComponent();
    }

    private void CloseOnBackdrop(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CloseShortcutsHandbookCommand.Execute(null);
        }
    }
}
