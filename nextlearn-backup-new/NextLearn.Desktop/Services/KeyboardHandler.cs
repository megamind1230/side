using System;
using System.Collections.Generic;
using Avalonia.Input;
using NextLearn.Desktop.ViewModels;

namespace NextLearn.Desktop.Services;

public class KeyboardHandler
{
    private readonly MainWindowViewModel _vm;
    private readonly IKeyBindingService _bindingService;
    private bool _chordPending;
    private Dictionary<(Key, KeyModifiers, string?), KeyboardActionKind> _lookup = [];
    private HashSet<(Key, KeyModifiers)> _textBoxAllowed = [];

    public KeyboardHandler(MainWindowViewModel vm, IKeyBindingService bindingService)
    {
        _vm = vm;
        _bindingService = bindingService;
        RebuildLookup();
    }

    public void CancelChord()
    {
        _chordPending = false;
    }

    public void RebuildLookup()
    {
        _lookup = [];
        _textBoxAllowed = [];

        foreach (var b in _bindingService.CurrentBindings)
        {
            if (!Enum.TryParse<Key>(b.Key, true, out var parsedKey))
            {
                continue;
            }

            var mods = ParseModifiers(b.Modifiers);
            var ctx = string.IsNullOrEmpty(b.Context) ? null : b.Context;
            _lookup.TryAdd((parsedKey, mods, ctx), b.Action);

            if (b.TextBox)
            {
                _textBoxAllowed.Add((parsedKey, mods));
            }
        }
    }

    public KeyboardActionKind HandleKey(Key key, KeyModifiers modifiers, bool isTextBox)
    {
        // Escape — overlay close chain (before binding table)
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

            if (isTextBox)
            {
                return KeyboardActionKind.ClearFocus;
            }

            return KeyboardActionKind.None;
        }

        // Determine context stack (most specific first)
        var contexts = new List<string?> { null };

        if (_vm.IsImageOverlayOpen)
        {
            contexts.Insert(0, "ImageOverlay");
        }

        contexts.Insert(0, _vm.IsLearning ? "Learning" : "Home");

        // Look up binding across contexts
        foreach (var ctx in contexts)
        {
            if (_lookup.TryGetValue((key, modifiers, ctx), out var action))
            {
                // Skip bindings that don't allow textbox when focus is in one
                if (isTextBox && !_textBoxAllowed.Contains((key, modifiers)))
                {
                    continue;
                }

                // Apply context-sensitive transformations
                if (_vm.IsHeatmapOpen)
                {
                    if (action == KeyboardActionKind.ZoomTextIn)
                    {
                        return KeyboardActionKind.ZoomHeatmapIn;
                    }

                    if (action == KeyboardActionKind.ZoomTextOut)
                    {
                        return KeyboardActionKind.ZoomHeatmapOut;
                    }

                    if (action == KeyboardActionKind.ResetTextZoom)
                    {
                        return KeyboardActionKind.ZoomHeatmapReset;
                    }
                }

                if (_vm.IsSettingsOpen && action == KeyboardActionKind.NavigateHome)
                {
                    return KeyboardActionKind.ExitSettingsHome;
                }

                return action;
            }
        }

        // Chord detection (home screen only, not in textbox)
        if (!_vm.IsLearning && !_vm.IsImageOverlayOpen && !isTextBox)
        {
            if (_chordPending && key != Key.I && key != Key.G)
            {
                CancelChord();
            }

            if (key == Key.G)
            {
                _chordPending = true;
                return KeyboardActionKind.ChordG;
            }

            if (key == Key.I && _chordPending)
            {
                CancelChord();
                return KeyboardActionKind.FocusSearchWithClear;
            }
        }

        return KeyboardActionKind.None;
    }

    internal static KeyModifiers ParseModifiers(string modifiers)
    {
        if (string.IsNullOrWhiteSpace(modifiers))
        {
            return KeyModifiers.None;
        }

        var result = KeyModifiers.None;
        var parts = modifiers.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            result |= part switch
            {
                "Control" => KeyModifiers.Control,
                "Shift" => KeyModifiers.Shift,
                "Alt" => KeyModifiers.Alt,
                "Ctrl" => KeyModifiers.Control,
                _ => KeyModifiers.None,
            };
        }

        return result;
    }
}
