using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace RedTrace.Windows.Controls;

public sealed class MetricCard : Grid
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

    private readonly Border cardBorder = new();
    private readonly TextBlock labelText = new();
    private readonly TextBlock valueText = new();
    private readonly ContentPresenter trendContentPresenter = new();
    private readonly ContentPresenter headerContentPresenter = new();
    private readonly Grid progressTrack = new() { Height = 3, MinHeight = 3 };
    private readonly Border progressFill = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ColumnDefinition progressColumn = new();
    private readonly ColumnDefinition progressRemainder = new();
    private readonly Border resizeGrip = new() { Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Background = new SolidColorBrush(Colors.Transparent), CornerRadius = new CornerRadius(5) };
    private readonly TextBlock resizeGlyph = new() { Text = "◢", FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    public MetricCard()
    {
        BuildVisualTree();
        ApplySharedResources();
        ApplyLabel(Label);
        ApplyDisplayValue(DisplayValue);
        ApplyAccent(AccentBrush ?? Ui.RedBrush);
        ApplyProgress(Progress);
        ApplyProgressVisibility(IsProgressVisible);
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

    public Border ResizeHandle => resizeGrip;

    private void BuildVisualTree()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { ColumnSpacing = 6 };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(labelText);
        headerContentPresenter.HorizontalAlignment = HorizontalAlignment.Right;
        headerContentPresenter.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(headerContentPresenter, 1);
        header.Children.Add(headerContentPresenter);
        root.Children.Add(header);

        var data = new Grid { Margin = new Thickness(0, 3, 0, 4) };
        data.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        data.RowDefinitions.Add(new RowDefinition());
        valueText.FontSize = 23;
        valueText.FontWeight = FontWeights.SemiBold;
        valueText.IsTextSelectionEnabled = false;
        valueText.TextTrimming = TextTrimming.CharacterEllipsis;
        data.Children.Add(valueText);
        trendContentPresenter.Margin = new Thickness(0, 3, 0, 0);
        trendContentPresenter.HorizontalAlignment = HorizontalAlignment.Stretch;
        trendContentPresenter.VerticalAlignment = VerticalAlignment.Stretch;
        Grid.SetRow(trendContentPresenter, 1);
        data.Children.Add(trendContentPresenter);
        Grid.SetRow(data, 1);
        root.Children.Add(data);

        progressTrack.ColumnDefinitions.Add(progressColumn);
        progressTrack.ColumnDefinitions.Add(progressRemainder);
        progressTrack.Children.Add(progressFill);
        Grid.SetRow(progressTrack, 2);
        root.Children.Add(progressTrack);
        resizeGrip.Child = resizeGlyph;
        Grid.SetRow(resizeGrip, 1);
        root.Children.Add(resizeGrip);

        cardBorder.Child = root;
        Children.Add(cardBorder);
    }

    private void ApplySharedResources()
    {
        cardBorder.Background = Ui.RaisedBrush;
        cardBorder.BorderBrush = Ui.RedBrush;
        cardBorder.BorderThickness = new Thickness(1);
        cardBorder.CornerRadius = Ui.Resource("RedTraceCardCornerRadius", new CornerRadius(9));
        cardBorder.Padding = Ui.Resource("RedTraceCardPadding", new Thickness(10, 8, 10, 8));
        labelText.FontFamily = Ui.Resource("RedTraceSansFontFamily", new FontFamily("Segoe UI Variable Display"));
        labelText.FontSize = 10;
        labelText.FontWeight = FontWeights.SemiBold;
        labelText.CharacterSpacing = 60;
        labelText.VerticalAlignment = VerticalAlignment.Center;
        valueText.FontFamily = Ui.Resource("RedTraceMonoFontFamily", new FontFamily("Cascadia Mono"));
        valueText.Foreground = Ui.WhiteBrush;
        progressTrack.Background = Ui.HairlineBrush;
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
        ((MetricCard)sender).trendContentPresenter.Content = args.NewValue;

    private static void OnHeaderContentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((MetricCard)sender).headerContentPresenter.Content = args.NewValue;

    private void ApplyLabel(string value) => labelText.Text = value.ToUpperInvariant();

    private void ApplyDisplayValue(string value) => valueText.Text = value;

    private void ApplyAccent(SolidColorBrush brush)
    {
        cardBorder.BorderBrush = brush;
        labelText.Foreground = brush;
        progressFill.Background = brush;
        resizeGlyph.Foreground = brush;
    }

    private void ApplyProgress(double value)
    {
        value = Math.Clamp(value, 0, 100);
        progressColumn.Width = new GridLength(value, GridUnitType.Star);
        progressRemainder.Width = new GridLength(100 - value, GridUnitType.Star);
    }

    private void ApplyProgressVisibility(bool visible) =>
        progressTrack.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
}
