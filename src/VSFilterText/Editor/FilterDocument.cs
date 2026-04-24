using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using VSFilterText.Filter;
using VSFilterText.State;

namespace VSFilterText.Editor;

/// <summary>
/// Composes the elision buffer, read-only text view, filter state, and filter engine for a
/// single filter document. Created by <see cref="FilterEditorFactory"/>.
/// </summary>
internal sealed class FilterDocument : IDisposable
{
    private readonly ITextBuffer _sourceBuffer;
    private readonly IElisionBuffer _elisionBuffer;
    private readonly IWpfTextViewHost _viewHost;
    private readonly FilterEngine _engine;
    private readonly FilterState _state;
    private readonly string _sourceMoniker;
    private readonly EventHandler<TextContentChangedEventArgs> _sourceChangedHandler;
    private bool _disposed;

    public FilterDocument(
        ITextBuffer sourceBuffer,
        string sourceMoniker,
        IProjectionBufferFactoryService projectionFactory,
        ITextEditorFactoryService editorFactory)
    {
        _sourceBuffer = sourceBuffer ?? throw new ArgumentNullException(nameof(sourceBuffer));
        _sourceMoniker = sourceMoniker ?? throw new ArgumentNullException(nameof(sourceMoniker));

        _state = new FilterState();

        // Initial seed: keep every source span. Subsequent Apply() calls will elide/expand.
        // NEEDS VERIFICATION: CreateElisionBuffer overload. The signature that takes
        // (IProjectionEditResolver, NormalizedSnapshotSpanCollection, ElisionBufferOptions) is the
        // canonical one; alternate overloads exist on some VSSDK versions.
        var wholeSource = new SnapshotSpan(sourceBuffer.CurrentSnapshot, 0, sourceBuffer.CurrentSnapshot.Length);
        _elisionBuffer = projectionFactory.CreateElisionBuffer(
            projectionEditResolver: null,
            sourceSpans: new NormalizedSnapshotSpanCollection(wholeSource),
            options: ElisionBufferOptions.None);

        // Read-only region covering the entire elision buffer. Belt-and-suspenders alongside the
        // view-role–based read-only-ness.
        using (var edit = _elisionBuffer.CreateReadOnlyRegionEdit())
        {
            edit.CreateReadOnlyRegion(new Span(0, _elisionBuffer.CurrentSnapshot.Length));
            edit.Apply();
        }

        var roles = editorFactory.CreateTextViewRoleSet(
            PredefinedTextViewRoles.Analyzable,
            PredefinedTextViewRoles.Zoomable,
            PredefinedTextViewRoles.Structured,
            PredefinedTextViewRoles.Interactive,
            FilterViewRoles.FilterView);

        var view = editorFactory.CreateTextView(_elisionBuffer, roles);

        _engine = new FilterEngine(sourceBuffer, _elisionBuffer, _state);

        // Stash per-document objects on the view so MEF-discovered parts (margin provider,
        // double-click handler) can find them.
        view.Properties[typeof(FilterState)] = _state;
        view.Properties[typeof(FilterEngine)] = _engine;
        view.Properties[FilterDocumentKeys.SourceMoniker] = _sourceMoniker;

        _viewHost = editorFactory.CreateTextViewHost(view, setFocus: true);

        _sourceChangedHandler = OnSourceChanged;
        _sourceBuffer.Changed += _sourceChangedHandler;

        _engine.Apply();
    }

    public IWpfTextViewHost ViewHost
    {
        get
        {
            ThrowIfDisposed();
            return _viewHost;
        }
    }

    public string SourceMoniker
    {
        get
        {
            ThrowIfDisposed();
            return _sourceMoniker;
        }
    }

    private void OnSourceChanged(object? sender, TextContentChangedEventArgs e)
    {
        // Projection tracks source edits automatically; we only need to re-evaluate which lines
        // now match the filter text.
        _engine.Apply();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _sourceBuffer.Changed -= _sourceChangedHandler;
        _viewHost.Close();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FilterDocument));
    }
}
