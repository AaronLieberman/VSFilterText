using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text.Editor;

namespace VSFilterText.Editor;

/// <summary>
/// Top-of-view WPF margin: query textbox + match count. Mounted only on filter-doc views
/// (restricted by role in <see cref="FilterQueryMarginProvider"/>).
/// </summary>
internal partial class FilterQueryMargin : UserControl, IWpfTextViewMargin
{
    public const string MarginName = "VSFilterText.FilterQueryMargin";

    private bool _disposed;

    public FilterQueryMargin(FilterQueryMarginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Loaded += (_, _) => QueryBox.Focus();
    }

    public FrameworkElement VisualElement
    {
        get
        {
            ThrowIfDisposed();
            return this;
        }
    }

    public double MarginSize
    {
        get
        {
            ThrowIfDisposed();
            return ActualHeight;
        }
    }

    public bool Enabled
    {
        get
        {
            ThrowIfDisposed();
            return true;
        }
    }

    public ITextViewMargin? GetTextViewMargin(string marginName)
        => string.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(MarginName);
    }
}
