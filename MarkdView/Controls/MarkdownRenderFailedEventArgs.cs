using System;

namespace MarkdView.Controls;

public sealed class MarkdownRenderFailedEventArgs : EventArgs
{
    public MarkdownRenderFailedEventArgs(Exception exception)
    {
        Exception = exception;
    }

    public Exception Exception { get; }
}
