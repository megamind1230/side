using Avalonia.Input;
using NextLearn.Desktop.ViewModels;

namespace NextLearn.Desktop.Services;

public class KeyboardHandler
{
    private readonly MainWindowViewModel _vm;
    private bool _chordPending;

    public KeyboardHandler(MainWindowViewModel vm)
    {
        _vm = vm;
    }

    public void CancelChord()
    {
        _chordPending = false;
    }

    public KeyboardActionKind HandleKey(Key key, KeyModifiers modifiers, bool isTextBox)
    {
        if (modifiers == (KeyModifiers.Control | KeyModifiers.Shift) && key == Key.OemPlus)
        {
            if (_vm.IsHeatmapOpen)
            {
                return KeyboardActionKind.ZoomHeatmapIn;
            }

            return KeyboardActionKind.ZoomTextIn;
        }

        if (modifiers == (KeyModifiers.Control | KeyModifiers.Shift) && key == Key.OemMinus)
        {
            if (_vm.IsHeatmapOpen)
            {
                return KeyboardActionKind.ZoomHeatmapOut;
            }

            return KeyboardActionKind.ZoomTextOut;
        }

        if (modifiers == (KeyModifiers.Control | KeyModifiers.Shift) && key is Key.D0 or Key.NumPad0)
        {
            if (_vm.IsHeatmapOpen)
            {
                return KeyboardActionKind.ZoomHeatmapReset;
            }

            return KeyboardActionKind.ResetTextZoom;
        }

        if (_vm.IsImageOverlayOpen)
        {
            if (modifiers == KeyModifiers.Control && key == Key.OemPlus)
            {
                return KeyboardActionKind.ZoomIn;
            }

            if (modifiers == KeyModifiers.Control && key == Key.OemMinus)
            {
                return KeyboardActionKind.ZoomOut;
            }

            if (modifiers == KeyModifiers.Control && key is Key.D0 or Key.NumPad0)
            {
                return KeyboardActionKind.ResetZoom;
            }

            if (key == Key.N && modifiers.HasFlag(KeyModifiers.Shift))
            {
                return KeyboardActionKind.NextImage;
            }

            if (key == Key.P && modifiers.HasFlag(KeyModifiers.Shift))
            {
                return KeyboardActionKind.PreviousImage;
            }
        }

        if (key == Key.Escape)
        {
            if (_vm.LearningViewModel is { IsGoToPageOpen: true })
            {
                return KeyboardActionKind.CloseGoToPage;
            }

            if (_vm.IsShortcutsHandbookOpen)
            {
                return KeyboardActionKind.CloseShortcutsHandbook;
            }

            if (_vm.IsImageOverlayOpen)
            {
                return KeyboardActionKind.CloseImageOverlay;
            }

            if (_vm.IsArchivedViewOpen)
            {
                return KeyboardActionKind.CloseArchivedView;
            }

            if (_vm.IsPinnedViewOpen)
            {
                return KeyboardActionKind.ClosePinnedView;
            }

            if (_vm.IsHeatmapOpen)
            {
                return KeyboardActionKind.CloseHeatmap;
            }

            if (_vm.IsSettingsOpen)
            {
                return KeyboardActionKind.CloseSettings;
            }

            if (_vm.IsSidebarOpen)
            {
                return KeyboardActionKind.CloseSidebar;
            }
        }

        if (key == Key.OemComma && modifiers.HasFlag(KeyModifiers.Control))
        {
            return KeyboardActionKind.OpenSettings;
        }

        if ((key == Key.D || key == Key.Q) && _vm.IsSettingsOpen)
        {
            return KeyboardActionKind.ExitSettingsHome;
        }

        if (key == Key.Oem2 && modifiers.HasFlag(KeyModifiers.Shift) && !isTextBox)
        {
            return KeyboardActionKind.ToggleShortcutsHandbook;
        }

        if (_vm.IsLearning)
        {
            if (_chordPending)
            {
                CancelChord();
            }

            if (key == Key.G && modifiers.HasFlag(KeyModifiers.Control) && !isTextBox)
            {
                return KeyboardActionKind.OpenGoToPage;
            }

            switch (key)
            {
                case Key.N:
                case Key.Right:
                    if (!isTextBox)
                    {
                        return KeyboardActionKind.NextPage;
                    }

                    break;
                case Key.P:
                case Key.Left:
                    if (!isTextBox)
                    {
                        return KeyboardActionKind.PreviousPage;
                    }

                    break;
                case Key.J:
                    if (!isTextBox)
                    {
                        return KeyboardActionKind.ScrollDown;
                    }

                    break;
                case Key.K:
                    if (!isTextBox)
                    {
                        return KeyboardActionKind.ScrollUp;
                    }

                    break;
                case Key.H:
                    if (!isTextBox)
                    {
                        return KeyboardActionKind.ScrollLeft;
                    }

                    break;
                case Key.L:
                    if (!isTextBox)
                    {
                        return KeyboardActionKind.ScrollRight;
                    }

                    break;
                case Key.Q:
                case Key.D:
                    if (!isTextBox)
                    {
                        return KeyboardActionKind.NavigateHome;
                    }

                    break;
            }
        }
        else
        {
            if (!isTextBox)
            {
                if (_chordPending && key != Key.I && key != Key.G)
                {
                    CancelChord();
                }

                switch (key)
                {
                    case Key.G:
                        _chordPending = true;
                        return KeyboardActionKind.ChordG;
                    case Key.I:
                        if (_chordPending)
                        {
                            CancelChord();
                            return KeyboardActionKind.FocusSearchWithClear;
                        }

                        break;
                    case Key.Q:
                    case Key.D:
                        return KeyboardActionKind.NavigateHome;
                    case Key.J:
                        return KeyboardActionKind.ScrollDeckListDown;
                    case Key.K:
                        return KeyboardActionKind.ScrollDeckListUp;
                    case Key.Oem2:
                        return KeyboardActionKind.FocusSearchBar;
                }
            }
            else if (key == Key.Escape)
            {
                return KeyboardActionKind.ClearFocus;
            }
        }

        return KeyboardActionKind.None;
    }
}
