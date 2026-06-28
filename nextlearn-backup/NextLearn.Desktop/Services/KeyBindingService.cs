using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NextLearn.Desktop.Models;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NextLearn.Desktop.Services;

public class KeyBindingService : IKeyBindingService
{
    private const string TemplateYaml =
    """
# =============================================================================
# nextlearn keyboard shortcuts
# =============================================================================
# To customize:
#   1. Save as ~/.config/nextlearn/keybindings.yaml
#   2. Select "Custom" in Settings -> Key Bindings
#   3. Click Save
#
# Field reference:
#   action    — what the shortcut does
#   key       — Avalonia key name (comments explain the real key)
#   modifiers — "Control", "Shift", "Alt", "Control+Shift", or "" (none)
#   chords    — list of { key, modifiers } for multi-key sequences (optional)
#   context   — "Learning", "Home", "ImageOverlay", or omit for global
#   textBox   — set true to allow in text fields (default: false)
#   _comment  — description shown in the shortcuts handbook
#
# Available actions:
#   NextPage, PreviousPage, ScrollDown, ScrollUp, ScrollLeft, ScrollRight,
#   NavigateHome, OpenSettings, ToggleShortcutsHandbook, OpenGoToPage,
#   ZoomTextIn, ZoomTextOut, ResetTextZoom, ZoomIn, ZoomOut, ResetZoom,
#   NextImage, PreviousImage, FocusSearchBar, FocusSearchWithClear,
#   ScrollDeckListDown, ScrollDeckListUp,
#   ZoomHeatmapIn, ZoomHeatmapOut, ZoomHeatmapReset,
#   OpenDocumentation, OpenCommandPalette, CloseCommandPalette,
#   OpenDecksFolder, ToggleSidebar, CloseSidebar,
#   ShowPinnedView, ShowArchivedView, ShowHeatmap,
#   NavigateToMarketplace, CloseMarketplace,
#   OpenSettings, CloseSettings, ExitSettingsHome
#
# Multi-key chords: use the "chords" field instead of "key" + "modifiers"
#   Example — g then i to focus and clear search:
#     - action: FocusSearchWithClear
#       chords:
#         - { key: G, modifiers: "" }
#         - { key: I, modifiers: "" }
#       context: Home
#       _comment: "Focus and clear search bar (g then i)"
#
# Non-configurable:
#   Escape / Ctrl+G -> closes overlays (GoToPage > Handbook > Image > ...)
# =============================================================================

bindings:
  # ── Image Overlay ──────────────────────────────────────────────

  # Ctrl++ = zoom in
  - action: ZoomIn
    key: OemPlus       # + key
    modifiers: Control
    context: ImageOverlay
    textBox: true
    _comment: "Zoom in on image"

  # Ctrl+- = zoom out
  - action: ZoomOut
    key: OemMinus      # - key
    modifiers: Control
    context: ImageOverlay
    textBox: true
    _comment: "Zoom out on image"

  # Ctrl+0 = reset zoom
  - action: ResetZoom
    key: D0            # 0 key (top row)
    modifiers: Control
    context: ImageOverlay
    textBox: true
    _comment: "Reset image zoom"

  # Ctrl+Numpad0 = reset zoom
  - action: ResetZoom
    key: NumPad0       # 0 key (numpad)
    modifiers: Control
    context: ImageOverlay
    textBox: true
    _comment: "Reset image zoom"

  # Shift+N = next image
  - action: NextImage
    key: N
    modifiers: Shift
    context: ImageOverlay
    _comment: "Next image in overlay"

  # Shift+P = previous image
  - action: PreviousImage
    key: P
    modifiers: Shift
    context: ImageOverlay
    _comment: "Previous image in overlay"

  # ── Learning ───────────────────────────────────────────────────

  # n / Right = next page
  - action: NextPage
    key: N
    modifiers: ""
    context: Learning
    _comment: "Next page"
  - action: NextPage
    key: Right
    modifiers: ""
    context: Learning
    _comment: "Next page"

  # p / Left = previous page
  - action: PreviousPage
    key: P
    modifiers: ""
    context: Learning
    _comment: "Previous page"
  - action: PreviousPage
    key: Left
    modifiers: ""
    context: Learning
    _comment: "Previous page"

  # j / k = scroll up/down
  - action: ScrollDown
    key: J
    modifiers: ""
    context: Learning
    _comment: "Scroll content down"
  - action: ScrollUp
    key: K
    modifiers: ""
    context: Learning
    _comment: "Scroll content up"

  # h / l = scroll left/right
  - action: ScrollLeft
    key: H
    modifiers: ""
    context: Learning
    _comment: "Scroll content left"
  - action: ScrollRight
    key: L
    modifiers: ""
    context: Learning
    _comment: "Scroll content right"

  # q / d = exit to home
  - action: NavigateHome
    key: Q
    modifiers: ""
    context: Learning
    _comment: "Exit to home"
  - action: NavigateHome
    key: D
    modifiers: ""
    context: Learning
    _comment: "Exit to home"

  # Ctrl+G = go to page dialog
  - action: OpenGoToPage
    key: G
    modifiers: Control
    context: Learning
    textBox: true
    _comment: "Open go-to-page dialog"

  # ── Home ────────────────────────────────────────────────────────

  # g then i = focus and clear search
  - action: FocusSearchWithClear
    chords:
      - { key: G, modifiers: "" }
      - { key: I, modifiers: "" }
    context: Home
    _comment: "Focus and clear search bar (g then i)"

  # q / d = go home
  - action: NavigateHome
    key: Q
    modifiers: ""
    context: Home
    _comment: "Go home"
  - action: NavigateHome
    key: D
    modifiers: ""
    context: Home
    _comment: "Go home"

  # j / k = scroll deck list
  - action: ScrollDeckListDown
    key: J
    modifiers: ""
    context: Home
    _comment: "Scroll deck list down"
  - action: ScrollDeckListUp
    key: K
    modifiers: ""
    context: Home
    _comment: "Scroll deck list up"

  # / = focus search bar
  - action: FocusSearchBar
    key: Oem2          # / key
    modifiers: ""
    context: Home
    _comment: "Focus search bar (/)"

  # ── Global ──────────────────────────────────────────────────────

  # Ctrl+Shift++ = zoom text in
  - action: ZoomTextIn
    key: OemPlus       # + key
    modifiers: Control+Shift
    textBox: true
    _comment: "Zoom text in (also heatmap in)"

  # Ctrl+Shift+- = zoom text out
  - action: ZoomTextOut
    key: OemMinus      # - key
    modifiers: Control+Shift
    textBox: true
    _comment: "Zoom text out (also heatmap out)"

  # Ctrl+Shift+0 = reset text zoom
  - action: ResetTextZoom
    key: D0            # 0 key (top row)
    modifiers: Control+Shift
    textBox: true
    _comment: "Reset text zoom (also heatmap reset)"
  - action: ResetTextZoom
    key: NumPad0       # 0 key (numpad)
    modifiers: Control+Shift
    textBox: true
    _comment: "Reset text zoom (also heatmap reset)"

  # Ctrl+, = open settings
  - action: OpenSettings
    key: OemComma      # , key
    modifiers: Control
    textBox: true
    _comment: "Open settings (Ctrl+,)"

  # ? or Shift+/ = toggle shortcuts handbook
  - action: ToggleShortcutsHandbook
    key: Oem2          # / key (with Shift = ?)
    modifiers: Shift
    _comment: "Toggle shortcuts handbook (? or Shift+/)"
""";

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private static readonly string[] Profiles = ["Vim", "Emacs", "VS Code", "Custom"];

    private readonly string _configDir;
    private readonly string _templateFilePath;
    private readonly string _customFilePath;
    private string _activeProfile;
    private IReadOnlyList<KeyBinding> _currentBindings;

    public KeyBindingService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "nextlearn"))
    {
    }

    public KeyBindingService(string configDir, string? profile = null)
    {
        _configDir = configDir;
        _customFilePath = Path.Combine(configDir, "keybindings.yaml");
        _templateFilePath = Path.Combine(configDir, "keybindings.yaml~");
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
            File.WriteAllText(_templateFilePath, TemplateYaml);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
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
                Bindings = bindings.ToList(),
            };
            var yaml = YamlSerializer.Serialize(doc);
            File.WriteAllText(_customFilePath, yaml);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or YamlException)
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
            "VS Code" => VSCodeBindings(),
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

            var yaml = File.ReadAllText(_customFilePath);
            var doc = YamlDeserializer.Deserialize<KeyBindingsDocument>(yaml);
            return doc?.Bindings ?? VimBindings().ToList();
        }
        catch (Exception ex) when (ex is YamlException or IOException or UnauthorizedAccessException)
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

    private static KeyBinding M(
        string? ctx,
        KeyboardActionKind action,
        string comment,
        string k1,
        string m1,
        string k2,
        string m2 = "",
        bool txt = false)
    {
        return new KeyBinding
        {
            Action = action,
            Chords = [new KeyChord { Key = k1, Modifiers = m1 }, new KeyChord { Key = k2, Modifiers = m2 }],
            Context = ctx,
            TextBox = txt,
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
            M(
                "Home",
                KeyboardActionKind.FocusSearchWithClear,
                "Focus and clear search bar (g then i)",
                "G",
                string.Empty,
                "I",
                string.Empty),
            G("Home", KeyboardActionKind.NavigateHome, "Q", string.Empty, "Go home"),
            G("Home", KeyboardActionKind.NavigateHome, "D", string.Empty, "Go home"),
            G("Home", KeyboardActionKind.ScrollDeckListDown, "J", string.Empty, "Scroll deck list down"),
            G("Home", KeyboardActionKind.ScrollDeckListUp, "K", string.Empty, "Scroll deck list up"),
            G("Home", KeyboardActionKind.FocusSearchBar, "Oem2", string.Empty, "Focus search bar (/)"),

            // ── Global ────────────────────────────────────────────────
            G(null, KeyboardActionKind.ZoomTextIn, "OemPlus", "Control+Shift", "Zoom text in (also heatmap in)", true),
            G(null, KeyboardActionKind.ZoomTextOut, "OemMinus", "Control+Shift", "Zoom text out (also heatmap out)", true),
            G(null, KeyboardActionKind.ResetTextZoom, "D0", "Control+Shift", "Reset text zoom (also heatmap reset)", true),
            G(null, KeyboardActionKind.ResetTextZoom, "NumPad0", "Control+Shift", "Reset text zoom (also heatmap reset)", true),
            G(null, KeyboardActionKind.OpenSettings, "OemComma", "Control", "Open settings (Ctrl+,)", true),
            G(null, KeyboardActionKind.ToggleShortcutsHandbook, "Oem2", "Shift", "Toggle shortcuts handbook (? or Shift+/)"),
            G(null, KeyboardActionKind.OpenDocumentation, "F1", string.Empty, "Open documentation"),
            G(null, KeyboardActionKind.ToggleSidebar, "S", string.Empty, "Toggle sidebar"),
            G(null, KeyboardActionKind.OpenDecksFolder, "O", string.Empty, "Open decks folder"),
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
            M("Learning", KeyboardActionKind.NavigateHome, "Exit to home (C-x q)", "X", "Control", "Q"),
            M("Learning", KeyboardActionKind.NavigateHome, "Exit to home (C-x q)", "X", "Control", "D"),
            M("Learning", KeyboardActionKind.OpenGoToPage, "Go to page (C-x g)", "X", "Control", "G", txt: true),

            // ── Home ──────────────────────────────────────────────────
            G("Home", KeyboardActionKind.FocusSearchBar, "S", "Control", "Focus search bar (C-s)", true),
            M("Home", KeyboardActionKind.FocusSearchWithClear, "Focus and clear search (C-x i)", "X", "Control", "I"),
            M("Home", KeyboardActionKind.NavigateHome, "Go home (C-x q)", "X", "Control", "Q"),
            M("Home", KeyboardActionKind.NavigateHome, "Go home (C-x q)", "X", "Control", "D"),
            M("Home", KeyboardActionKind.ShowPinnedView, "Show pinned (C-x p)", "X", "Control", "P"),
            M("Home", KeyboardActionKind.ShowArchivedView, "Show archived (C-x a)", "X", "Control", "A"),
            M("Home", KeyboardActionKind.ShowHeatmap, "Show heatmap (C-x h)", "X", "Control", "H"),
            M("Home", KeyboardActionKind.OpenDecksFolder, "Open decks folder (C-c o)", "C", "Control", "O"),
            M("Home", KeyboardActionKind.NavigateToMarketplace, "Open marketplace (C-c m)", "C", "Control", "M"),
            G("Home", KeyboardActionKind.ScrollDeckListDown, "V", "Control", "Scroll deck list down (C-v)", true),
            G("Home", KeyboardActionKind.ScrollDeckListUp, "V", "Alt", "Scroll deck list up (M-v)", true),

            // ── Global ────────────────────────────────────────────────
            G(null, KeyboardActionKind.ZoomTextIn, "OemPlus", "Control+Shift", "Zoom text in (also heatmap in)", true),
            G(null, KeyboardActionKind.ZoomTextOut, "OemMinus", "Control+Shift", "Zoom text out (also heatmap out)", true),
            G(null, KeyboardActionKind.ResetTextZoom, "D0", "Control+Shift", "Reset text zoom (also heatmap reset)", true),
            G(null, KeyboardActionKind.ResetTextZoom, "NumPad0", "Control+Shift", "Reset text zoom (also heatmap reset)", true),
            M(null, KeyboardActionKind.OpenSettings, "Open settings (C-c C-s)", "C", "Control", "S", "Control", txt: true),
            M(null, KeyboardActionKind.ToggleShortcutsHandbook, "Toggle handbook (C-h ?)", "H", "Control", "Oem2", "Shift"),
            M(null, KeyboardActionKind.OpenDocumentation, "Open docs (C-h d)", "H", "Control", "D"),
            M(null, KeyboardActionKind.ToggleSidebar, "Toggle sidebar (C-c s)", "C", "Control", "S"),
        };
    }

    private static List<KeyBinding> VSCodeBindings()
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
            G("Learning", KeyboardActionKind.NextPage, "Right", "Alt", "Next page (Alt+Right)"),
            G("Learning", KeyboardActionKind.PreviousPage, "P", string.Empty, "Previous page"),
            G("Learning", KeyboardActionKind.PreviousPage, "Left", string.Empty, "Previous page"),
            G("Learning", KeyboardActionKind.PreviousPage, "Left", "Alt", "Previous page (Alt+Left)"),
            G("Learning", KeyboardActionKind.ScrollDown, "J", string.Empty, "Scroll content down"),
            G("Learning", KeyboardActionKind.ScrollUp, "K", string.Empty, "Scroll content up"),
            G("Learning", KeyboardActionKind.ScrollLeft, "H", string.Empty, "Scroll content left"),
            G("Learning", KeyboardActionKind.ScrollRight, "L", string.Empty, "Scroll content right"),
            G("Learning", KeyboardActionKind.NavigateHome, "W", "Control", "Close deck / go home (Ctrl+W)"),
            G("Learning", KeyboardActionKind.NavigateHome, "Q", string.Empty, "Exit to home"),
            G("Learning", KeyboardActionKind.NavigateHome, "D", string.Empty, "Exit to home"),
            G("Learning", KeyboardActionKind.OpenGoToPage, "G", "Control", "Go to page (Ctrl+G)", true),

            // ── Home ──────────────────────────────────────────────────
            M("Home", KeyboardActionKind.FocusSearchWithClear, "Focus and clear search (g then i)", "G", string.Empty, "I", string.Empty),
            G("Home", KeyboardActionKind.NavigateHome, "Q", string.Empty, "Go home"),
            G("Home", KeyboardActionKind.NavigateHome, "D", string.Empty, "Go home"),
            G("Home", KeyboardActionKind.ScrollDeckListDown, "J", string.Empty, "Scroll deck list down"),
            G("Home", KeyboardActionKind.ScrollDeckListUp, "K", string.Empty, "Scroll deck list up"),
            G("Home", KeyboardActionKind.FocusSearchBar, "Oem2", string.Empty, "Focus search bar (/)"),
            G("Home", KeyboardActionKind.FocusSearchBar, "F", "Control", "Focus search bar (Ctrl+F)"),

            // ── Global ────────────────────────────────────────────────
            G(null, KeyboardActionKind.OpenCommandPalette, "P", "Control", "Open command palette (Ctrl+P)"),
            G(null, KeyboardActionKind.ShowPinnedView, "P", "Control+Shift", "Show pinned (Ctrl+Shift+P)"),
            G(null, KeyboardActionKind.ShowArchivedView, "A", "Control+Shift", "Show archived (Ctrl+Shift+A)"),
            G(null, KeyboardActionKind.ShowHeatmap, "H", "Control+Shift", "Show heatmap (Ctrl+Shift+H)"),
            G(null, KeyboardActionKind.NavigateToMarketplace, "M", "Control+Shift", "Open marketplace (Ctrl+Shift+M)"),
            G(null, KeyboardActionKind.OpenDecksFolder, "O", "Control", "Open decks folder (Ctrl+O)"),
            G(null, KeyboardActionKind.ToggleSidebar, "B", "Control", "Toggle sidebar (Ctrl+B)"),
            G(null, KeyboardActionKind.OpenSettings, "OemComma", "Control", "Open settings (Ctrl+,)", true),
            G(null, KeyboardActionKind.ToggleShortcutsHandbook, "Oem2", "Shift", "Toggle shortcuts handbook (? or Shift+/)"),
            M(null, KeyboardActionKind.ToggleShortcutsHandbook, "Keyboard shortcuts (Ctrl+K Ctrl+S)", "K", "Control", "S", "Control"),
            G(null, KeyboardActionKind.OpenDocumentation, "F1", string.Empty, "Open documentation"),
            G(null, KeyboardActionKind.ZoomTextIn, "OemPlus", "Control+Shift", "Zoom text in (also heatmap in)", true),
            G(null, KeyboardActionKind.ZoomTextOut, "OemMinus", "Control+Shift", "Zoom text out (also heatmap out)", true),
            G(null, KeyboardActionKind.ResetTextZoom, "D0", "Control+Shift", "Reset text zoom (also heatmap reset)", true),
            G(null, KeyboardActionKind.ResetTextZoom, "NumPad0", "Control+Shift", "Reset text zoom (also heatmap reset)", true),
        };
    }
}
