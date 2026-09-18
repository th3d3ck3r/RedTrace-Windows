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
    public static readonly SolidColorBrush RedBrush = new(Red);
    public static readonly SolidColorBrush TextBrush = Brush("#FFFF5362");
    public static readonly SolidColorBrush WhiteBrush = Brush("#FFF4F4F6");
    public static readonly SolidColorBrush MutedBrush = Brush("#FF8D8E98");
    public static readonly SolidColorBrush PanelBrush = Brush("#B80A0B0F");
    public static readonly SolidColorBrush RaisedBrush = Brush("#B8121319");
    public static readonly SolidColorBrush HairlineBrush = Brush("#3DFFFFFF");

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
    public static string DisplayName(PanelMode mode) => mode switch { PanelMode.Watch => "WATCH", PanelMode.Run => "RUN", PanelMode.Codex => "CHATGPT", _ => "BTOP" };

    public static TextBox Terminal(bool readOnly = true) => new()
    {
        IsReadOnly = readOnly,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        FontFamily = new FontFamily("Cascadia Mono"),
        FontSize = 12,
        Foreground = TextBrush,
        Background = new SolidColorBrush(Colors.Transparent),
        BorderThickness = new Thickness(0),
        Padding = new Thickness(12, 10, 12, 10),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };

    public static Button IconButton(string glyph, string tip, Action action, SolidColorBrush? accent = null)
    {
        var button = new Button
        {
            Content = new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 13 },
            Width = 31,
            Height = 30,
            Padding = new Thickness(0),
            Margin = new Thickness(2, 0, 2, 0),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Foreground = accent ?? Brush("#FFD0D1D7"),
            CornerRadius = new CornerRadius(7)
        };
        ToolTipService.SetToolTip(button, tip);
        button.Click += (_, _) => action();
        return button;
    }

    public static MenuFlyout Menu()
    {
        var style = new Style(typeof(MenuFlyoutPresenter));
        style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(11)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#F215161C")));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#6AFFFFFF")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(5)));
        return new MenuFlyout { MenuFlyoutPresenterStyle = style };
    }

    public static TextBlock SmallLabel(string text, SolidColorBrush brush) => new()
    {
        Text = text,
        Foreground = brush,
        FontFamily = new FontFamily("Cascadia Mono"),
        FontSize = 10,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        VerticalAlignment = VerticalAlignment.Center
    };

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
