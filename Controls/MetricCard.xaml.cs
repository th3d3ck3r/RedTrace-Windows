using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace RedTrace.Windows.Controls;

public sealed partial class MetricCard : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(MetricCard), new PropertyMetadata(string.Empty, OnLabelChanged));

    public static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(
        nameof(DisplayValue), typeof(string), typeof(MetricCard), new PropertyMetadata("—", OnDisplayValueChanged));

    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush), typeof(SolidColorBrush), typeof(MetricCard), new PropertyMetadata(null, OnAccentBrushChanged));

    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(MetricCard), new PropertyMetadata(0d, OnProgressChanged));

    public static readonly DependencyProperty IsProgressVisibleProperty = DependencyProperty.Register(
        nameof(IsProgressVisible), typeof(bool), typeof(MetricCard), new PropertyMetadata(true, OnProgressVisibilityChanged));

    public static readonly DependencyProperty TrendContentProperty = DependencyProperty.Register(
        nameof(TrendContent), typeof(UIElement), typeof(MetricCard), new PropertyMetadata(null, OnTrendContentChanged));

    public static readonly DependencyProperty HeaderContentProperty = DependencyProperty.Register(
        nameof(HeaderContent), typeof(UIElement), typeof(MetricCard), new PropertyMetadata(null, OnHeaderContentChanged));

    public MetricCard()
    {
        InitializeComponent();
        ApplySharedResources();
        ApplyLabel(Label);
        ApplyDisplayValue(DisplayValue);
        ApplyAccent(AccentBrush ?? Ui.RedBrush);
        ApplyProgress(Progress);
        ApplyProgressVisibility(IsProgressVisible);
        TrendContentPresenter.Content = TrendContent;
        HeaderContentPresenter.Content = HeaderContent;
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string DisplayValue
    {
        get => (string)GetValue(DisplayValueProperty);
        set => SetValue(DisplayValueProperty, value);
    }

    public SolidColorBrush? AccentBrush
    {
        get => (SolidColorBrush?)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public bool IsProgressVisible
    {
        get => (bool)GetValue(IsProgressVisibleProperty);
        set => SetValue(IsProgressVisibleProperty, value);
    }

    public UIElement? TrendContent
    {
        get => (UIElement?)GetValue(TrendContentProperty);
        set => SetValue(TrendContentProperty, value);
    }

    public UIElement? HeaderContent
    {
        get => (UIElement?)GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public Border ResizeHandle => ResizeGrip;

    private void ApplySharedResources()
    {
        if (Ui.StyleResource("RedTraceMetricCardStyle") is Style cardStyle) CardBorder.Style = cardStyle;
        else
        {
            CardBorder.Background = Ui.RaisedBrush;
            CardBorder.BorderBrush = Ui.RedBrush;
            CardBorder.BorderThickness = new Thickness(1);
            CardBorder.CornerRadius = new CornerRadius(9);
            CardBorder.Padding = new Thickness(10, 8);
        }
        if (Ui.StyleResource("RedTraceLabelStyle") is Style labelStyle) LabelText.Style = labelStyle;
        ValueText.FontFamily = Ui.Resource("RedTraceMonoFontFamily", new FontFamily("Cascadia Mono"));
        ValueText.Foreground = Ui.WhiteBrush;
        ProgressIndicator.Background = Ui.HairlineBrush;
    }

    private static void OnLabelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((MetricCard)sender).ApplyLabel(args.NewValue as string ?? string.Empty);

    private static void OnDisplayValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((MetricCard)sender).ApplyDisplayValue(args.NewValue as string ?? "—");

    private static void OnAccentBrushChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((MetricCard)sender).ApplyAccent(args.NewValue as SolidColorBrush ?? Ui.RedBrush);

    private static void OnProgressChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((MetricCard)sender).ApplyProgress(args.NewValue is double value ? value : 0);

    private static void OnProgressVisibilityChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((MetricCard)sender).ApplyProgressVisibility(args.NewValue is true);

    private static void OnTrendContentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((MetricCard)sender).TrendContentPresenter.Content = args.NewValue;

    private static void OnHeaderContentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((MetricCard)sender).HeaderContentPresenter.Content = args.NewValue;

    private void ApplyLabel(string value) => LabelText.Text = value.ToUpperInvariant();

    private void ApplyDisplayValue(string value) => ValueText.Text = value;

    private void ApplyAccent(SolidColorBrush brush)
    {
        CardBorder.BorderBrush = brush;
        LabelText.Foreground = brush;
        ProgressIndicator.Foreground = brush;
        ResizeGlyph.Foreground = brush;
    }

    private void ApplyProgress(double value) => ProgressIndicator.Value = Math.Clamp(value, 0, 100);

    private void ApplyProgressVisibility(bool visible) =>
        ProgressIndicator.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
}
