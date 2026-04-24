using System;
using System.Collections.Generic;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSFilterText.Editor;

/// <summary>
/// Tracks open filter documents keyed by their source moniker and closes the filter window
/// automatically when the source document is closed.
///
/// Deduplication of filter windows is delegated to <see cref="IVsUIShellOpenDocument"/>: calling
/// <c>OpenSpecificEditor</c> on an already-open moniker focuses the existing frame.
/// </summary>
internal sealed class FilterDocumentLifetime : IVsRunningDocTableEvents, IDisposable
{
    private readonly IVsRunningDocumentTable _rdt;
    private readonly Dictionary<string, uint> _filterCookiesBySource = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, string> _sourceBySelfCookie = new();
    private readonly Dictionary<uint, IVsWindowFrame> _framesByCookie = new();
    private readonly uint _eventsCookie;
    private bool _disposed;

    public FilterDocumentLifetime(IVsRunningDocumentTable rdt)
    {
        _rdt = rdt ?? throw new ArgumentNullException(nameof(rdt));
        ErrorHandler.ThrowOnFailure(_rdt.AdviseRunningDocTableEvents(this, out _eventsCookie));
    }

    public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var hr = _rdt.GetDocumentInfo(
            docCookie, out _, out _, out _,
            out var moniker, out _, out _, out _);
        if (ErrorHandler.Succeeded(hr)
            && FilterEditorFactory.TryParseMoniker(moniker, out var sourceMoniker))
        {
            _filterCookiesBySource[sourceMoniker] = docCookie;
            _sourceBySelfCookie[docCookie] = sourceMoniker;
        }
        return VSConstants.S_OK;
    }

    public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
    {
        if (pFrame is not null && _sourceBySelfCookie.ContainsKey(docCookie))
        {
            _framesByCookie[docCookie] = pFrame;
        }
        return VSConstants.S_OK;
    }

    public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (dwReadLocksRemaining + dwEditLocksRemaining != 0) return VSConstants.S_OK;

        // Our own filter doc is being closed → drop tracking.
        if (_sourceBySelfCookie.TryGetValue(docCookie, out var sourceMoniker))
        {
            _sourceBySelfCookie.Remove(docCookie);
            _filterCookiesBySource.Remove(sourceMoniker);
            _framesByCookie.Remove(docCookie);
            return VSConstants.S_OK;
        }

        // A source document is being closed → close any attached filter doc.
        var hr = _rdt.GetDocumentInfo(
            docCookie, out _, out _, out _,
            out var closingMoniker, out _, out _, out _);
        if (ErrorHandler.Succeeded(hr)
            && _filterCookiesBySource.TryGetValue(closingMoniker, out var filterCookie)
            && _framesByCookie.TryGetValue(filterCookie, out var filterFrame))
        {
            filterFrame.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave);
        }
        return VSConstants.S_OK;
    }

    public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) => VSConstants.S_OK;
    public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) => VSConstants.S_OK;
    public int OnAfterSave(uint docCookie) => VSConstants.S_OK;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ThreadHelper.ThrowIfNotOnUIThread();
        _rdt.UnadviseRunningDocTableEvents(_eventsCookie);
    }
}
