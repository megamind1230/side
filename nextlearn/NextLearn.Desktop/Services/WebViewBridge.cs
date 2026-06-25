using System;
using System.IO;
using System.Linq;
using Avalonia.Input;
using Serilog;
using NwvControl = NativeWebView.Controls.NativeWebView;

namespace NextLearn.Desktop.Services;

public class WebViewBridge
{
    private readonly NwvControl _webView;

    public WebViewBridge(NwvControl webView)
    {
        _webView = webView;
    }

    public void LoadHtml(string html)
    {
        if (html == null)
        {
            throw new ArgumentNullException(nameof(html));
        }

        try
        {
            if (_webView == null)
            {
                return;
            }

            var appAssets = Path.Combine(AppContext.BaseDirectory, "Assets");
            var cssPath = Path.Combine(appAssets, "atom-one-dark.min.css");
            var jsPath = Path.Combine(appAssets, "custom-highlight.js");
            var katexDir = Path.Combine(appAssets, "katex");
            var katexCssPath = Path.Combine(katexDir, "katex.min.css");
            var katexJsPath = Path.Combine(katexDir, "katex.min.js");
            var katexAutoRenderPath = Path.Combine(katexDir, "katex-auto-render.min.js");

            if (File.Exists(cssPath))
            {
                var css = File.ReadAllText(cssPath);
                html = html.Replace("<!--HIGHLIGHT_CSS-->", css);
            }
            else
            {
                html = html.Replace("<!--HIGHLIGHT_CSS-->", string.Empty);
            }

            if (File.Exists(jsPath))
            {
                var js = File.ReadAllText(jsPath);
                html = html.Replace("/* HIGHLIGHT_JS */", js);
            }
            else
            {
                html = html.Replace("<script>hljs.highlightAll();</script>", string.Empty);
                html = html.Replace("/* HIGHLIGHT_JS */", string.Empty);
            }

            if (File.Exists(katexCssPath))
            {
                var katexCss = File.ReadAllText(katexCssPath);
                html = html.Replace("<!--KATEX_CSS-->", katexCss);
            }
            else
            {
                html = html.Replace("<!--KATEX_CSS-->", string.Empty);
            }

            if (File.Exists(katexJsPath) && File.Exists(katexAutoRenderPath))
            {
                var katexJs = File.ReadAllText(katexJsPath);
                var katexAutoRender = File.ReadAllText(katexAutoRenderPath);
                var katexScript = katexJs + "\n" + katexAutoRender + "\n"
                    + "document.addEventListener('DOMContentLoaded',function(){renderMathInElement(document.body,{delimiters:[{left:'$$',right:'$$',display:true},{left:'$',right:'$',display:false},{left:'\\\\\\(',right:'\\\\\\)',display:false},{left:'\\\\\\[',right:'\\\\\\]',display:true}],throwOnError:false});});";
                html = html.Replace("/* KATEX_AUTO_RENDER */", katexScript);
            }
            else
            {
                html = html.Replace("/* KATEX_AUTO_RENDER */", string.Empty);
            }

            var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(html));
            _webView.Source = new Uri($"data:text/html;base64,{base64}");
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load HTML content into WebView");
        }
    }

    public void SetVisible(bool visible)
    {
        if (_webView != null)
        {
            _webView.IsVisible = visible;
        }
    }

    public void ScrollBy(int x, int y)
    {
        if (_webView != null)
        {
            _ = _webView.ExecuteScriptAsync($"window.scrollBy({x}, {y})");
        }
    }

    public void SetFontScale(double scale)
    {
        if (_webView != null)
        {
            var pct = (int)(scale * 100);
            _ = _webView.ExecuteScriptAsync($"document.body.style.fontSize = '{pct}%'");
        }
    }

    public void SetFontFamily(string fontFamily)
    {
        if (fontFamily == null)
        {
            throw new ArgumentNullException(nameof(fontFamily));
        }

        if (_webView != null)
        {
            var escaped = fontFamily.Replace("'", "\\'");
            _ = _webView.ExecuteScriptAsync($"document.body.style.fontFamily = '{escaped}', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif");
        }
    }

    public static (string? path, string? error)? DecodeImageUri(Uri uri)
    {
        if (uri == null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        var encodedPath = uri.AbsolutePath.TrimStart('/');
        byte[] pathBytes;
        string path;
        try
        {
            pathBytes = Convert.FromBase64String(encodedPath);
            path = System.Text.Encoding.UTF8.GetString(pathBytes);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to decode img.local URI");
            return null;
        }

        if (!File.Exists(path))
        {
            Log.Error("Image file not found: {Path}", path);
            return null;
        }

        return (path, null);
    }

    public static (Key key, KeyModifiers modifiers)? DecodeKeyUri(Uri uri)
    {
        if (uri == null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        var path = uri.AbsolutePath.TrimStart('/');
        var parts = path.Split('/');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var keyStr = Uri.UnescapeDataString(parts[0]);
            var modStr = parts[1];

            Key key = keyStr switch
            {
                "n" or "N" => Key.N,
                "p" or "P" => Key.P,
                "j" or "J" => Key.J,
                "k" or "K" => Key.K,
                "h" or "H" => Key.H,
                "l" or "L" => Key.L,
                "q" or "Q" => Key.Q,
                "d" or "D" => Key.D,
                "e" or "E" => Key.E,
                "i" or "I" => Key.I,
                "g" or "G" => Key.G,
                "Escape" => Key.Escape,
                "?" => Key.Oem2,
                "/" => Key.Oem2,
                "Enter" => Key.Enter,
                "," => Key.OemComma,
                "=" => Key.OemPlus,
                "+" => Key.OemPlus,
                "-" => Key.OemMinus,
                "_" => Key.OemMinus,
                "0" => Key.D0,
                ")" => Key.D0,
                "ArrowRight" => Key.Right,
                "ArrowLeft" => Key.Left,
                _ => Key.None,
            };

            if (key == Key.None)
            {
                return null;
            }

            var mods = KeyModifiers.None;
            if (modStr.Contains('C'))
            {
                mods |= KeyModifiers.Control;
            }

            if (modStr.Contains('S'))
            {
                mods |= KeyModifiers.Shift;
            }

            if (modStr.Contains('A'))
            {
                mods |= KeyModifiers.Alt;
            }

            return (key, mods);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to decode key.local URI: {Uri}", uri);
            return null;
        }
    }

    public static string? DecodeOpenUrl(Uri uri)
    {
        if (uri == null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        var path = uri.AbsolutePath.TrimStart('/');

        var lastSlash = path.LastIndexOf('/');
        if (lastSlash >= 0)
        {
            var lastSegment = path[(lastSlash + 1)..];
            if (lastSegment.All(char.IsDigit))
            {
                path = path[..lastSlash];
            }
        }

        return Uri.UnescapeDataString(path);
    }
}
