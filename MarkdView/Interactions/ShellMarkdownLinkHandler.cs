using System;
using System.Diagnostics;

namespace MarkdView.Interactions;

public sealed class ShellMarkdownLinkHandler : IMarkdownLinkHandler
{
    public void Open(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        Process.Start(new ProcessStartInfo
        {
            FileName = uri.ToString(),
            UseShellExecute = true
        });
    }
}
