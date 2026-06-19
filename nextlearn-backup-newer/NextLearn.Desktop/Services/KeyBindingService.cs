using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public class KeyBindingService : IKeyBindingService
{
    private const string HelpText =
"""
Key binding modifiers format:
  Use Avalonia Key enum names for keys: A-Z, D0-D9, F1-F12, Left, Right, Up, Down,
  OemPlus, OemMinus, OemComma, OemPeriod, Oem2 (/), Escape, Space, etc.
  For letter keys use uppercase single letter: N, P, J, K, etc.

Modifiers — comma-separated keywords:
  Control   →  Ctrl
  Shift     →  Shift
  Alt       →  Alt
  Multiple: "Control+Shift", "Control+Alt", etc.
  No modifier: "" (empty string)

Context — where the binding is active:
  "Learning"    →  study session
  "Home"        →  deck list
  "ImageOverlay"  →  image viewer
  Omit or null  →  global (works everywhere)

Available actions (KeyboardActionKind enum values):
  NextPage, PreviousPage, ScrollDown, ScrollUp, ScrollLeft, ScrollRight,
  NavigateHome, OpenSettings, ToggleShortcutsHandbook, OpenGoToPage,
  ZoomTextIn, ZoomTextOut, ResetTextZoom, ZoomIn, ZoomOut, ResetZoom,
  NextImage, PreviousImage, FocusSearchBar, ScrollDeckListDown, ScrollDeckListUp,
  ZoomHeatmapIn, ZoomHeatmapOut, ZoomHeatmapReset

Unbindable (handled internally):
  Escape → closes overlays in priority order (GoToPage > Handbook > Image > ...)
  g then i chord → focus & clear search on home screen
""";

    private readonly string _configDir;
    private readonly string _templateFilePath;
    private readonly string _customFilePath;
    private string _activeProfile;
    private IReadOnlyList<KeyBinding> _currentBindings;
    private static readonly string[] Profiles = ["Vim", "Emacs", "Custom"];

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public KeyBindingService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "nextlearn"))
    {
    }

    public KeyBindingService(string configDir, string? profile = null)
    {
        _configDir = configDir;
        _customFilePath = Path.Combine(configDir, "keybindings.json");
        _templateFilePath = Path.Combine(configDir, "keybindings.json~");
        _activeProfile = profile ?? "Vim";
        _currentBindings = LoadProfileBindings(_activeProfile);
        WriteTemplate();
    }

    public string ActiveProfile => _activeProfile;

    public IReadOnlyList<string> AvailableProfiles => Profiles;

    public IReadOnlyList<KeyBinding> CurrentBindings => _currentBindings;

    public void SwitchProfile(string name)
    {
        if (!Profiles.Contains(name))
        {
            return;
        }

        _activeProfile = name;
        _currentBindings = LoadProfileBindings(name);
    }

    private void WriteTemplate()
    {
        try
        {
            Directory.CreateDirectory(_configDir);
            var doc = new KeyBindingsDocument
            {
                Help = HelpText,
                Bindings = VimBindings(),
            };
            var json = JsonSerializer.Serialize(doc, JsonOpts);
            File.WriteAllText(_templateFilePath, json);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or JsonException)
        {
        }
    }

    public bool TrySaveCustomBindings(IReadOnlyList<KeyBinding> bindings, out string? error)
    {
        try
        {
            Directory.CreateDirectory(_configDir);
            var doc = new KeyBindingsDocument
            {
                Help = HelpText,
                Bindings = bindings.ToList(),
            };
            var json = JsonSerializer.Serialize(doc, JsonOpts);
            File.WriteAllText(_customFilePath, json);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or JsonException)
        {
            error = ex.Message;
            return false;
        }
    }

    private IReadOnlyList<KeyBinding> LoadProfileBindings(string profile)
    {
        return profile switch
        {
            "Vim" => VimBindings(),
            "Emacs" => EmacsBindings(),
            "Custom" => LoadCustomBindings(),
            _ => VimBindings(),
        };
    }

    private IReadOnlyList<KeyBinding> LoadCustomBindings()
    {
        try
        {
            if (!File.Exists(_customFilePath))
            {
                var vim = VimBindings().ToList();
                TrySaveCustomBindings(vim, out _);
                return vim;
            }

            var json = File.ReadAllText(_customFilePath);
            var doc = JsonSerializer.Deserialize<KeyBindingsDocument>(json, JsonOpts);
            return doc?.Bindings ?? VimBindings().ToList();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return VimBindings().ToList();
        }
    }

    private static KeyBinding G(string? context, KeyboardActionKind action, string key, string modifiers, string comment, bool allowInTextBox = false)
    {
        return new KeyBinding
        {
            Action = action,
            Key = key,
            Modifiers = modifiers,
            Context = context,
            TextBox = allowInTextBox,
            Comment = comment,
        };
    }

    private static List<KeyBinding> VimBindings()
    {
        return new List<KeyBinding>
        {
            // ── Image Overlay ──────────────────────────────────────────
            G("ImageOverlay", KeyboardActionKind.ZoomIn, "OemPlus", "Control", "Zoom in on image", true),
            G("ImageOverlay", KeyboardActionKind.ZoomOut, "OemMinus", "Control", "Zoom out on image", true),
            G("ImageOverlay", KeyboardActionKind.ResetZoom, "D0", "Control", "Reset image zoom", true),
            G("ImageOverlay", KeyboardActionKind.ResetZoom, "NumPad0", "Control", "Reset image zoom", true),
            G("ImageOverlay", KeyboardActionKind.NextImage, "N", "Shift", "Next image in overlay"),
            G("ImageOverlay", KeyboardActionKind.PreviousImage, "P", "Shift", "Previous image in overlay"),

            // ── Learning ──────────────────────────────────────────────
            G("Learning", KeyboardActionKind.NextPage, "N", string.Empty, "Next page"),
            G("Learning", KeyboardActionKind.NextPage, "Right", string.Empty, "Next page"),
            G("Learning", KeyboardActionKind.PreviousPage, "P", string.Empty, "Previous page"),
            G("Learning", KeyboardActionKind.PreviousPage, "Left", string.Empty, "Previous page"),
            G("Learning", KeyboardActionKind.ScrollDown, "J", string.Empty, "Scroll content down"),
            G("Learning", KeyboardActionKind.ScrollUp, "K", string.Empty, "Scroll content up"),
            G("Learning", KeyboardActionKind.ScrollLeft, "H", string.Empty, "Scroll content left"),
            G("Learning", KeyboardActionKind.ScrollRight, "L", string.Empty, "Scroll content right"),
            G("Learning", KeyboardActionKind.NavigateHome, "Q", string.Empty, "Exit to home"),
            G("Learning", KeyboardActionKind.NavigateHome, "D", string.Empty, "Exit to home"),
            G("Learning", KeyboardActionKind.OpenGoToPage, "G", "Control", "Open go-to-page dialog", true),

            // ── Home ──────────────────────────────────────────────────
            G("Home", KeyboardActionKind.NavigateHome, "Q", string.Empty, "Go home (no-op from home)"),
            G("Home", KeyboardActionKind.NavigateHome, "D", string.Empty, "Go home (no-op from home)"),
            G("Home", KeyboardActionKind.ScrollDeckListDown, "J", string.Empty, "Scroll deck list down"),
            G("Home", KeyboardActionKind.ScrollDeckListUp, "K", string.Empty, "Scroll deck list up"),
            G("Home", KeyboardActionKind.FocusSearchBar, "Oem2", string.Empty, "Focus search bar (/)"),

            // ── Global ────────────────────────────────────────────────
            G(null, KeyboardActionKind.ZoomTextIn, "OemPlus", "Control+Shift", "Zoom text in (also heatmap in)", true),
            G(null, KeyboardActionKind.ZoomTextOut, "OemMinus", "Control+Shift", "Zoom text out (also heatmap out)", true),
            G(null, KeyboardActionKind.ResetTextZoom, "D0", "Control+Shift", "Reset text zoom (also heatmap reset)", true),
            G(null, KeyboardActionKind.ResetTextZoom, "NumPad0", "Control+Shift", "Reset text zoom (also heatmap reset)", true),
            G(null, KeyboardActionKind.OpenSettings, "OemComma", "Control", "Open settings (Ctrl+,)", true),
            G(null, KeyboardActionKind.ToggleShortcutsHandbook, "Oem2", "Shift", "Toggle shortcuts handbook (?/Shift+/)"),
        };
    }

    private static List<KeyBinding> EmacsBindings()
    {
        return new List<KeyBinding>
        {
            // ── Image Overlay ──────────────────────────────────────────
            G("ImageOverlay", KeyboardActionKind.ZoomIn, "OemPlus", "Control", "Zoom in on image", true),
            G("ImageOverlay", KeyboardActionKind.ZoomOut, "OemMinus", "Control", "Zoom out on image", true),
            G("ImageOverlay", KeyboardActionKind.ResetZoom, "D0", "Control", "Reset image zoom", true),
            G("ImageOverlay", KeyboardActionKind.ResetZoom, "NumPad0", "Control", "Reset image zoom", true),
            G("ImageOverlay", KeyboardActionKind.NextImage, "N", "Shift", "Next image in overlay"),
            G("ImageOverlay", KeyboardActionKind.PreviousImage, "P", "Shift", "Previous image in overlay"),

            // ── Learning ──────────────────────────────────────────────
            G("Learning", KeyboardActionKind.NextPage, "N", "Control", "Next page (C-n)", true),
            G("Learning", KeyboardActionKind.NextPage, "Right", string.Empty, "Next page"),
            G("Learning", KeyboardActionKind.PreviousPage, "P", "Control", "Previous page (C-p)", true),
            G("Learning", KeyboardActionKind.PreviousPage, "Left", string.Empty, "Previous page"),
            G("Learning", KeyboardActionKind.ScrollDown, "V", "Control", "Scroll down (C-v)", true),
            G("Learning", KeyboardActionKind.ScrollUp, "V", "Alt", "Scroll up (M-v)", true),
            G("Learning", KeyboardActionKind.ScrollLeft, "B", "Control", "Scroll left (C-b)", true),
            G("Learning", KeyboardActionKind.ScrollRight, "F", "Control", "Scroll right (C-f)", true),
            G("Learning", KeyboardActionKind.NavigateHome, "Q", string.Empty, "Exit to home"),
            G("Learning", KeyboardActionKind.NavigateHome, "D", string.Empty, "Exit to home"),
            G("Learning", KeyboardActionKind.OpenGoToPage, "G", "Alt", "Go to page (M-g g)", true),

            // ── Home ──────────────────────────────────────────────────
            G("Home", KeyboardActionKind.NavigateHome, "Q", string.Empty, "Go home"),
            G("Home", KeyboardActionKind.NavigateHome, "D", string.Empty, "Go home"),
            G("Home", KeyboardActionKind.ScrollDeckListDown, "V", "Control", "Scroll deck list down (C-v)", true),
            G("Home", KeyboardActionKind.ScrollDeckListUp, "V", "Alt", "Scroll deck list up (M-v)", true),
            G("Home", KeyboardActionKind.FocusSearchBar, "S", "Control", "Focus search bar (C-s)", true),

            // ── Global ────────────────────────────────────────────────
            G(null, KeyboardActionKind.ZoomTextIn, "OemPlus", "Control+Shift", "Zoom text in (also heatmap in)", true),
            G(null, KeyboardActionKind.ZoomTextOut, "OemMinus", "Control+Shift", "Zoom text out (also heatmap out)", true),
            G(null, KeyboardActionKind.ResetTextZoom, "D0", "Control+Shift", "Reset text zoom (also heatmap reset)", true),
            G(null, KeyboardActionKind.ResetTextZoom, "NumPad0", "Control+Shift", "Reset text zoom (also heatmap reset)", true),
            G(null, KeyboardActionKind.OpenSettings, "C", "Control", "Open settings (C-c)", true),
            G(null, KeyboardActionKind.ToggleShortcutsHandbook, "H", "Control", "Toggle shortcuts handbook", true),
        };
    }
}
