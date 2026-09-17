using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System.Text.Json;
using Windows.UI;

namespace RedTrace.Windows;

public static class Ui
{
    public static readonly Color Red = Color.FromArgb(255, 255, 59, 77);
    private static readonly SolidColorBrush FallbackRedBrush = new(Red);
    private static readonly SolidColorBrush FallbackTextBrush = Brush("#FFDE3650");
    private static readonly SolidColorBrush FallbackWhiteBrush = Brush("#FFF2F2F4");
    private static readonly SolidColorBrush FallbackMutedBrush = Brush("#FF8B8D96");
    private static readonly SolidColorBrush FallbackPanelBrush = Brush("#E60A0B0F");
    private static readonly SolidColorBrush FallbackRaisedBrush = Brush("#E6121318");
    private static readonly SolidColorBrush FallbackHairlineBrush = Brush("#35FFFFFF");

    public static SolidColorBrush RedBrush => Resource("RedTraceAccentBrush", FallbackRedBrush);
    public static SolidColorBrush TextBrush => Resource("RedTraceDataTextBrush", FallbackTextBrush);
    public static SolidColorBrush WhiteBrush => Resource("RedTracePrimaryTextBrush", FallbackWhiteBrush);
    public static SolidColorBrush MutedBrush => Resource("RedTraceSecondaryTextBrush", FallbackMutedBrush);
    public static SolidColorBrush PanelBrush => Resource("RedTracePanelBrush", FallbackPanelBrush);
    public static SolidColorBrush RaisedBrush => Resource("RedTraceRaisedBrush", FallbackRaisedBrush);
    public static SolidColorBrush HairlineBrush => Resource("RedTraceHairlineBrush", FallbackHairlineBrush);

    public static SolidColorBrush Brush(string value)
    {
        value = value.TrimStart('#');
        var a = value.Length == 8 ? Convert.ToByte(value[..2], 16) : (byte)255;
        var offset = value.Length == 8 ? 2 : 0;
        return new SolidColorBrush(Color.FromArgb(a, Convert.ToByte(value.Substring(offset, 2), 16), Convert.ToByte(value.Substring(offset + 2, 2), 16), Convert.ToByte(value.Substring(offset + 4, 2), 16)));
    }

    public static SolidColorBrush Accent(PanelMode mode) => mode switch
    {
        PanelMode.Run => Brush("#FFFFB000"),
        PanelMode.Codex => Brush("#FFFF2D91"),
        PanelMode.Btop => Brush("#FF27DDE5"),
        _ => RedBrush
    };

    public static string Icon(PanelMode mode) => mode switch { PanelMode.Watch => "", PanelMode.Run => "", PanelMode.Codex => "", _ => "" };

    internal static T Resource<T>(string key, T fallback)
    {
        try
        {
            if (Application.Current?.Resources[key] is T value) return value;
        }
        catch { /* App resources are unavailable in design-time or early startup. */ }
        return fallback;
    }

    internal static Style? StyleResource(string key)
    {
        try { return Application.Current?.Resources[key] as Style; }
        catch { return null; }
    }

    public static TextBox Terminal(bool readOnly = true)
    {
        var box = new TextBox { IsReadOnly = readOnly };
        if (StyleResource("RedTraceTerminalTextBoxStyle") is Style style) box.Style = style;
        else
        {
            box.AcceptsReturn = true;
            box.TextWrapping = TextWrapping.NoWrap;
            box.FontFamily = new FontFamily("Cascadia Mono");
            box.FontSize = 12;
            box.Foreground = TextBrush;
            box.Background = new SolidColorBrush(Colors.Transparent);
            box.BorderThickness = new Thickness(0);
            box.Padding = new Thickness(12, 10, 12, 10);
            box.HorizontalAlignment = HorizontalAlignment.Stretch;
            box.VerticalAlignment = VerticalAlignment.Stretch;
        }
        return box;
    }

    public static Button IconButton(string glyph, string tip, Action action, SolidColorBrush? accent = null)
    {
        var button = new Button
        {
            Content = new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 13 },
            Foreground = accent ?? WhiteBrush
        };
        if (StyleResource("RedTraceIconButtonStyle") is Style style) button.Style = style;
        else
        {
            button.Width = 31;
            button.Height = 30;
            button.Padding = new Thickness(0);
            button.Margin = new Thickness(2, 0, 2, 0);
            button.Background = new SolidColorBrush(Colors.Transparent);
            button.BorderThickness = new Thickness(0);
            button.CornerRadius = new CornerRadius(7);
        }
        ToolTipService.SetToolTip(button, tip);
        button.Click += (_, _) => action();
        return button;
    }

    public static MenuFlyout Menu()
    {
        var style = StyleResource("RedTraceMenuFlyoutPresenterStyle") ?? new Style(typeof(MenuFlyoutPresenter));
        if (style.Setters.Count == 0)
        {
            style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(11)));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#F0101115")));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, HairlineBrush));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(5)));
        }
        return new MenuFlyout { MenuFlyoutPresenterStyle = style };
    }

    public static TextBlock SmallLabel(string text, SolidColorBrush brush)
    {
        var label = new TextBlock { Text = text, Foreground = brush };
        if (StyleResource("RedTraceLabelStyle") is Style style) label.Style = style;
        else
        {
            label.FontFamily = new FontFamily("Segoe UI Variable Display");
            label.FontSize = 10;
            label.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            label.VerticalAlignment = VerticalAlignment.Center;
        }
        return label;
    }

    public static void Append(TextBox box, string value)
    {
        box.Text += value;
        if (box.Text.Length > 400_000) box.Text = box.Text[^250_000..];
        box.Select(box.Text.Length, 0);
    }
}

public static class Preferences
{
    private static readonly string PathName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RedTrace", "appearance.json");
    public static double Opacity { get; set; } = 0.85;

    public static void Load()
    {
        try
        {
            if (!File.Exists(PathName)) return;
            var value = JsonSerializer.Deserialize<Appearance>(File.ReadAllText(PathName));
            if (value is null) return;
            Opacity = Math.Clamp(value.Opacity, 0.55, 1);
            Ui.TextBrush.Color = Parse(value.Text, Ui.TextBrush.Color);
            Ui.RedBrush.Color = Parse(value.Accent, Ui.RedBrush.Color);
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathName)!);
            File.WriteAllText(PathName, JsonSerializer.Serialize(new Appearance(Opacity, Hex(Ui.TextBrush.Color), Hex(Ui.RedBrush.Color))));
        }
        catch { }
    }

    private static Color Parse(string? value, Color fallback)
    {
        try { return Ui.Brush(value ?? "").Color; } catch { return fallback; }
    }
    private static string Hex(Color value) => $"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}";
    private sealed record Appearance(double Opacity, string Text, string Accent);
}

public sealed class Sparkline : Canvas
{
    private readonly Queue<double> samples = new();
    private readonly SolidColorBrush stroke;
    public Sparkline(SolidColorBrush color)
    {
        stroke = color; Height = 22; MinWidth = 40; IsHitTestVisible = false;
        SizeChanged += (_, _) => Redraw();
    }
    public void Add(double value) { samples.Enqueue(Math.Clamp(value, 0, 100)); while (samples.Count > 60) samples.Dequeue(); Redraw(); }
    private void Redraw()
    {
        Children.Clear(); var values = samples.ToArray(); if (values.Length < 2 || ActualWidth <= 1 || ActualHeight <= 1) return;
        for (var i = 1; i < values.Length; i++)
        {
            var line = new Line
            {
                X1 = (i - 1) * ActualWidth / (values.Length - 1), X2 = i * ActualWidth / (values.Length - 1),
                Y1 = ActualHeight - values[i - 1] / 100 * (ActualHeight - 2) - 1, Y2 = ActualHeight - values[i] / 100 * (ActualHeight - 2) - 1,
                Stroke = stroke, StrokeThickness = 1.5
            };
            Children.Add(line);
        }
    }
}
