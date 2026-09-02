using System.Windows;

namespace MarkdView.Interactions;

public sealed class WpfClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        Clipboard.SetText(text);
    }
}
