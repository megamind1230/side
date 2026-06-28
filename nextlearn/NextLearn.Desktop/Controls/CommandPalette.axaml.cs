using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.ViewModels;

namespace NextLearn.Desktop.Controls;

public partial class CommandPalette : UserControl
{
    public CommandPalette()
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
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsCommandPaletteOpen) && vm.IsCommandPaletteOpen)
            {
                DispatcherTimer.RunOnce(
                    () =>
                    {
                        var tb = this.FindControl<TextBox>("CommandInput");
                        tb?.Focus();
                    },
                    TimeSpan.FromMilliseconds(50));
            }

            if (e.PropertyName == nameof(MainWindowViewModel.SelectedCommand))
            {
                DispatcherTimer.RunOnce(
                    () =>
                    {
                        var lb = this.FindControl<ListBox>("CommandListBox");
                        if (lb?.SelectedItem != null)
                        {
                            lb.ScrollIntoView(lb.SelectedItem);
                        }
                    },
                    TimeSpan.FromMilliseconds(50));
            }
        };
    }

    private void CloseOnBackdrop(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CloseCommandPalette();
        }
    }

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                vm.CloseCommandPalette();
                e.Handled = true;
                break;

            case Key.Enter:
                vm.ExecuteSelectedCommand();
                e.Handled = true;
                break;

            case Key.Down:
                vm.SelectNextCommand();
                e.Handled = true;
                break;

            case Key.Up:
                vm.SelectPreviousCommand();
                e.Handled = true;
                break;

            case Key.Tab:
                vm.SelectNextCommand();
                e.Handled = true;
                break;
        }
    }

    private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is CommandPaletteEntry entry
            && DataContext is MainWindowViewModel vm)
        {
            vm.CloseCommandPalette();
            entry.Execute();
            e.Handled = true;
        }
    }
}
