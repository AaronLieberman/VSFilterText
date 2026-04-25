using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSFilterText.Editor;

/// <summary>
/// Minimal <see cref="IVsWindowPane"/> host around a <see cref="FilterDocument"/>.
/// Also implements <see cref="IVsPersistDocData"/> so VS never treats the filter doc as dirty
/// or attempts to save it.
///
/// NEEDS VERIFICATION: some VSSDK templates inherit from WindowPane (managed helper) which
/// supplies default command routing. For a read-only prototype, the direct IVsWindowPane
/// implementation below is the simplest correct shape.
/// </summary>
internal sealed class FilterDocumentPane : IVsWindowPane, IVsPersistDocData, IDisposable
{
    private readonly FilterDocument _document;
    private System.Windows.Interop.HwndSource? _hwndSource;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _disposed;

    public FilterDocumentPane(FilterDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    public FilterDocument Document
    {
        get
        {
            ThrowIfDisposed();
            return _document;
        }
    }

    // ---- IVsWindowPane ----

    public int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp) => VSConstants.S_OK;

    public int CreatePaneWindow(IntPtr hwndParent, int x, int y, int cx, int cy, out IntPtr hwnd)
    {
        ThrowIfDisposed();
        var host = _document.ViewHost.HostControl;
        var parameters = new System.Windows.Interop.HwndSourceParameters("VSFilterTextPane")
        {
            PositionX = x,
            PositionY = y,
            Width = cx,
            Height = cy,
            ParentWindow = hwndParent,
            WindowStyle = unchecked((int)0x40000000 /*WS_CHILD*/ | 0x10000000 /*WS_VISIBLE*/ | 0x02000000 /*WS_CLIPCHILDREN*/)
        };
        _hwndSource = new System.Windows.Interop.HwndSource(parameters) { RootVisual = host };
        _hwnd = _hwndSource.Handle;
        hwnd = _hwnd;
        return VSConstants.S_OK;
    }

    public int GetDefaultSize(SIZE[] pSize)
    {
        if (pSize is { Length: > 0 })
        {
            pSize[0].cx = 600;
            pSize[0].cy = 400;
        }
        return VSConstants.S_OK;
    }

    public int ClosePane()
    {
        Dispose();
        return VSConstants.S_OK;
    }

    public int LoadViewState(IStream pStream) => VSConstants.S_OK;

    public int SaveViewState(IStream pStream) => VSConstants.S_OK;

    public int TranslateAccelerator(MSG[] lpmsg) => VSConstants.S_FALSE;

    // ---- IVsPersistDocData (never dirty, never saves) ----

    public int GetGuidEditorType(out Guid pClassID)
    {
        pClassID = FilterEditorFactory.EditorFactoryGuid;
        return VSConstants.S_OK;
    }

    public int IsDocDataDirty(out int pfDirty)
    {
        pfDirty = 0;
        return VSConstants.S_OK;
    }

    public int SetUntitledDocPath(string pszDocDataPath) => VSConstants.S_OK;

    public int LoadDocData(string pszMkDocument) => VSConstants.S_OK;

    public int SaveDocData(VSSAVEFLAGS dwSave, out string pbstrMkDocumentNew, out int pfSaveCanceled)
    {
        pbstrMkDocumentNew = string.Empty;
        pfSaveCanceled = 1;
        return VSConstants.S_OK;
    }

    public int Close()
    {
        Dispose();
        return VSConstants.S_OK;
    }

    public int OnRegisterDocData(uint docCookie, IVsHierarchy pHierNew, uint itemidNew) => VSConstants.S_OK;

    public int RenameDocData(uint grfAttribs, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew) => VSConstants.S_OK;

    public int IsDocDataReloadable(out int pfReloadable)
    {
        pfReloadable = 0;
        return VSConstants.S_OK;
    }

    public int ReloadDocData(uint grfFlags) => VSConstants.S_OK;

    // ---- IDisposable ----

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hwndSource?.Dispose();
        _hwndSource = null;
        _document.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FilterDocumentPane));
    }
}
