using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using NextLearn.Desktop.ViewModels;

namespace NextLearn.Desktop.Services;

public class KeyboardHandler
{
    private readonly MainWindowViewModel _vm;
    private readonly IKeyBindingService _bindingService;
    private Dictionary<(Key, KeyModifiers, string?), KeyboardActionKind> _lookup = [];
    private HashSet<(Key, KeyModifiers)> _textBoxAllowed = [];
    private List<(List<(Key, KeyModifiers)> keys, KeyboardActionKind action)> _chords = [];
    private List<(Key, KeyModifiers)> _pendingChord = [];
    private List<(Key, KeyModifiers)>? _lastCompletedChord;

    public KeyboardHandler(MainWindowViewModel vm, IKeyBindingService bindingService)
    {
        _vm = vm;
        _bindingService = bindingService;
        RebuildLookup();
    }

    public bool IsChordPending => _pendingChord.Count > 0;

    public IReadOnlyList<(Key key, KeyModifiers modifiers)> PendingChord => _pendingChord;

    public IReadOnlyList<(Key key, KeyModifiers modifiers)>? LastCompletedChord => _lastCompletedChord;

    public void CancelChord()
    {
        _pendingChord.Clear();
        _lastCompletedChord = null;
    }

    public void RebuildLookup()
    {
        _lookup = [];
        _textBoxAllowed = [];
        _chords = [];

        foreach (var b in _bindingService.CurrentBindings)
        {
            if (b.Chords is { Count: > 1 })
            {
                var chordKeys = new List<(Key, KeyModifiers)>();
                var valid = true;
                foreach (var c in b.Chords)
                {
                    if (Enum.TryParse<Key>(c.Key, true, out var parsedKey))
                    {
                        chordKeys.Add((parsedKey, ParseModifiers(c.Modifiers)));
                    }
                    else
                    {
                        valid = false;
                        break;
                    }
                }

                if (valid && chordKeys.Count > 1)
                {
                    _chords.Add((chordKeys, b.Action));
                }

                continue;
            }

            if (!Enum.TryParse<Key>(b.Key, true, out var key))
            {
                continue;
            }

            var mods = ParseModifiers(b.Modifiers);
            var ctx = string.IsNullOrEmpty(b.Context) ? null : b.Context;
            _lookup.TryAdd((key, mods, ctx), b.Action);

            if (b.TextBox)
            {
                _textBoxAllowed.Add((key, mods));
            }
        }
    }

    public KeyboardActionKind HandleKey(Key key, KeyModifiers modifiers, bool isTextBox)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            return KeyboardActionKind.None;
        }

        if (!_vm.IsCommandPaletteOpen)
        {
            if (key == Key.OemSemicolon && modifiers == KeyModifiers.Shift && _bindingService.ActiveProfile != "Emacs")
            {
                return KeyboardActionKind.OpenCommandPalette;
            }

            if (key == Key.X && modifiers == KeyModifiers.Alt && _bindingService.ActiveProfile == "Emacs")
            {
                return KeyboardActionKind.OpenCommandPalette;
            }
        }

        if (key == Key.Escape)
        {
            return HandleEscape(isTextBox);
        }

        if (key == Key.G && modifiers == KeyModifiers.Control && _bindingService.ActiveProfile == "Emacs")
        {
            return HandleEscape(isTextBox);
        }

        // When a chord is already pending, check chord completion/extension BEFORE
        // direct lookup so that chords take priority over any direct bindings.
        if (_pendingChord.Count > 0)
        {
            var current = new List<(Key, KeyModifiers)>(_pendingChord) { (key, modifiers) };

            var complete = _chords.FirstOrDefault(c => c.keys.SequenceEqual(current));
            if (complete.action != default)
            {
                _lastCompletedChord = current;
                CancelChord();
                return complete.action;
            }

            if (_chords.Any(c => c.keys.Take(current.Count).SequenceEqual(current)))
            {
                _pendingChord = current;
                return KeyboardActionKind.ChordPrefix;
            }

            // Key doesn't match any chord — cancel and fall through to direct lookup
            CancelChord();
        }

        var contexts = new List<string?> { null };

        if (_vm.IsImageOverlayOpen)
        {
            contexts.Insert(0, "ImageOverlay");
        }

        contexts.Insert(0, _vm.IsLearning ? "Learning" : "Home");

        foreach (var ctx in contexts)
        {
            if (_lookup.TryGetValue((key, modifiers, ctx), out var action))
            {
                if (isTextBox && !_textBoxAllowed.Contains((key, modifiers)))
                {
                    continue;
                }

                CancelChord();

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

        if (isTextBox)
        {
            CancelChord();
            return KeyboardActionKind.None;
        }

        // Start a new chord only if one wasn't already pending (handled above)
        if (_pendingChord.Count == 0)
        {
            if (_chords.Any(c => c.keys[0] == (key, modifiers)))
            {
                _pendingChord = [(key, modifiers)];
                return KeyboardActionKind.ChordPrefix;
            }
        }

        return KeyboardActionKind.None;
    }

    private KeyboardActionKind HandleEscape(bool isTextBox = false)
    {
        CancelChord();

        if (_vm.IsCommandPaletteOpen)
        {
            return KeyboardActionKind.CloseCommandPalette;
        }

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

        if (_vm.IsTagInferenceOpen)
        {
            return KeyboardActionKind.CloseTagInference;
        }

        if (_vm.IsHeatmapOpen)
        {
            return KeyboardActionKind.CloseHeatmap;
        }

        if (_vm.IsMarketplaceOpen)
        {
            return KeyboardActionKind.CloseMarketplace;
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
