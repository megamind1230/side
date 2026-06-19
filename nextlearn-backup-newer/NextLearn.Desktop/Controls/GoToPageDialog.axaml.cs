using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using NextLearn.Desktop.ViewModels;

namespace NextLearn.Desktop.Controls;

public partial class GoToPageDialog : UserControl
{
    public GoToPageDialog()
    {
        InitializeComponent();

        if (DataContext is MainWindowViewModel vm)
        {
            SubscribeToVm(vm);
        }

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                SubscribeToVm(vm);
            }
        };
    }

    private void SubscribeToVm(MainWindowViewModel vm)
    {
        vm.LearningViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LearningViewModel.IsGoToPageOpen) && vm.LearningViewModel.IsGoToPageOpen)
            {
                DispatcherTimer.RunOnce(
                    () =>
                    {
                        var tb = this.FindControl<TextBox>("GoToPageTextBox");
                        tb?.Focus();
                        tb?.SelectAll();
                    },
                    TimeSpan.FromMilliseconds(50));
            }
        };
    }

    private void CloseOnBackdrop(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.LearningViewModel.CancelGoToPageCommand.Execute(null);
        }
    }

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel vm)
        {
            vm.LearningViewModel.GoToPageCommand.Execute(null);
            e.Handled = true;
        }
    }
}
