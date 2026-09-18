using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace RedTrace.Windows;

/// Focusable ConPTY surface. RichEditBox is used only as a renderer; edits go to ConPTY.
public sealed class TerminalView : RichEditBox, IDisposable
{
    readonly StringBuilder pending = new(); readonly Microsoft.UI.Dispatching.DispatcherQueueTimer frame;
    Action<byte[]>? sender; bool disposed;
    public TerminalView(Action<byte[]> send)
    {
        sender = send; IsReadOnly = true; IsSpellCheckEnabled = false; IsTextPredictionEnabled = false;
        FontFamily = new FontFamily("Cascadia Mono"); FontSize = 12; Foreground = Ui.TextBrush;
        Background = new SolidColorBrush(global::Windows.UI.Colors.Transparent); BorderThickness = new Thickness(0);
        Padding = new Thickness(12, 10, 12, 10); HorizontalAlignment = HorizontalAlignment.Stretch; VerticalAlignment = VerticalAlignment.Stretch;
        frame = DispatcherQueue.CreateTimer(); frame.Interval = TimeSpan.FromMilliseconds(16); frame.Tick += (_, _) => Flush();
        KeyDown += OnKeyDown; CharacterReceived += OnCharacter; Paste += OnPaste; SizeChanged += (_, _) => ResizeTerminal();
    }
    public void Append(string text) { if (disposed) return; lock (pending) pending.Append(text); if (!frame.IsRunning) frame.Start(); }
    public void ClearTerminal() { Document.SetText(Microsoft.UI.Text.TextSetOptions.None, ""); }
    void Flush()
    {
        string text; lock (pending) { text = pending.ToString(); pending.Clear(); }
        if (text.Length == 0) { frame.Stop(); return; }
        Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out var current);
        Document.SetText(Microsoft.UI.Text.TextSetOptions.None, (current + text)[^Math.Min(250_000, current.Length + text.Length)..]);
        Document.Selection.SetRange(int.MaxValue, int.MaxValue);
    }
    void OnCharacter(UIElement sender, CharacterReceivedRoutedEventArgs e) { if (e.Character >= 32) { Send(Encoding.UTF8.GetBytes(char.ConvertFromUtf32((int)e.Character))); e.Handled = true; } }
    void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        string? sequence = e.Key switch { VirtualKey.Enter => "\r", VirtualKey.Tab => "\t", VirtualKey.Escape => "\x1b", VirtualKey.Back => "\x7f", VirtualKey.Up => "\x1b[A", VirtualKey.Down => "\x1b[B", VirtualKey.Right => "\x1b[C", VirtualKey.Left => "\x1b[D", VirtualKey.Home => "\x1b[H", VirtualKey.End => "\x1b[F", VirtualKey.Delete => "\x1b[3~", VirtualKey.PageUp => "\x1b[5~", VirtualKey.PageDown => "\x1b[6~", _ => null };
        if (sequence is not null) { Send(Encoding.UTF8.GetBytes(sequence)); e.Handled = true; }
    }
    void OnPaste(object sender, TextControlPasteEventArgs e) { var data = Clipboard.GetContent(); if (data.Contains(StandardDataFormats.Text)) { _ = PasteAsync(data); e.Handled = true; } }
    async Task PasteAsync(DataPackageView data) { var text = await data.GetTextAsync(); Send(Encoding.UTF8.GetBytes(text)); }
    void Send(byte[] bytes) => sender?.Invoke(bytes);
    void ResizeTerminal() { /* Runner supplies its model dimensions; layout events are intentionally cheap. */ }
    public void Dispose() { disposed = true; frame.Stop(); sender = null; KeyDown -= OnKeyDown; CharacterReceived -= OnCharacter; Paste -= OnPaste; }
}
