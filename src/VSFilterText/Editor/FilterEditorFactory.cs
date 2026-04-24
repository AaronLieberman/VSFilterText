using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;

namespace VSFilterText.Editor;

/// <summary>
/// Creates filter-document windows for monikers shaped like
/// <c>vsfiltertext://&lt;source-path&gt;</c>.
///
/// NEEDS VERIFICATION: custom editor factory registration sometimes requires extra flags on
/// <c>IVsEditorFactoryNotify</c>; the pattern below is the minimal one used by read-only custom
/// editors.
/// </summary>
[Guid(EditorFactoryGuidString)]
internal sealed class FilterEditorFactory : IVsEditorFactory, IDisposable
{
    public const string EditorFactoryGuidString = "7F1D4D3A-9A1A-5E4F-1A2B-3C4D5E6F7A8B";
    public static readonly Guid EditorFactoryGuid = new(EditorFactoryGuidString);

    public const string MonikerScheme = "vsfiltertext://";

    private readonly IServiceProvider _packageServiceProvider;
    private Microsoft.VisualStudio.OLE.Interop.IServiceProvider? _site;

    public FilterEditorFactory(IServiceProvider packageServiceProvider)
    {
        _packageServiceProvider = packageServiceProvider ?? throw new ArgumentNullException(nameof(packageServiceProvider));
    }

    public static string BuildMoniker(string sourcePath)
        => MonikerScheme + Uri.EscapeDataString(sourcePath);

    public static bool TryParseMoniker(string moniker, out string sourcePath)
    {
        if (!string.IsNullOrEmpty(moniker) && moniker.StartsWith(MonikerScheme, StringComparison.OrdinalIgnoreCase))
        {
            sourcePath = Uri.UnescapeDataString(moniker.Substring(MonikerScheme.Length));
            return true;
        }
        sourcePath = string.Empty;
        return false;
    }

    public int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp)
    {
        _site = psp;
        return VSConstants.S_OK;
    }

    public int Close()
    {
        Dispose();
        return VSConstants.S_OK;
    }

    public int MapLogicalView(ref Guid rguidLogicalView, out string? pbstrPhysicalView)
    {
        pbstrPhysicalView = null;
        return rguidLogicalView == VSConstants.LOGVIEWID_Primary
               || rguidLogicalView == VSConstants.LOGVIEWID_TextView
            ? VSConstants.S_OK
            : VSConstants.E_NOTIMPL;
    }

    public int CreateEditorInstance(
        uint grfCreateDoc,
        string pszMkDocument,
        string? pszPhysicalView,
        IVsHierarchy pvHier,
        uint itemid,
        IntPtr punkDocDataExisting,
        out IntPtr ppunkDocView,
        out IntPtr ppunkDocData,
        out string pbstrEditorCaption,
        out Guid pguidCmdUI,
        out int pgrfCDW)
    {
        ppunkDocView = IntPtr.Zero;
        ppunkDocData = IntPtr.Zero;
        pbstrEditorCaption = string.Empty;
        pguidCmdUI = EditorFactoryGuid;
        pgrfCDW = 0;

        if (!TryParseMoniker(pszMkDocument, out var sourcePath))
        {
            return VSConstants.VS_E_UNSUPPORTEDFORMAT;
        }

        ThreadHelper.ThrowIfNotOnUIThread();

        var componentModel = (IComponentModel)_packageServiceProvider.GetService(typeof(SComponentModel));
        if (componentModel is null) return VSConstants.E_FAIL;

        var projectionFactory = componentModel.GetService<IProjectionBufferFactoryService>();
        var editorFactory = componentModel.GetService<ITextEditorFactoryService>();
        var adaptersFactory = componentModel.GetService<IVsEditorAdaptersFactoryService>();

        var sourceBuffer = ResolveSourceBuffer(sourcePath, adaptersFactory);
        if (sourceBuffer is null)
        {
            return VSConstants.E_FAIL;
        }

        var document = new FilterDocument(sourceBuffer, sourcePath, projectionFactory, editorFactory);
        var pane = new FilterDocumentPane(document);

        ppunkDocView = Marshal.GetIUnknownForObject(pane);
        ppunkDocData = Marshal.GetIUnknownForObject(pane);
        pbstrEditorCaption = $"{Path.GetFileName(sourcePath)} [Filtered]";
        return VSConstants.S_OK;
    }

    private ITextBuffer? ResolveSourceBuffer(string sourcePath, IVsEditorAdaptersFactoryService adaptersFactory)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var rdt = (IVsRunningDocumentTable)_packageServiceProvider.GetService(typeof(SVsRunningDocumentTable));
        if (rdt is null) return null;

        var hr = rdt.FindAndLockDocument(
            (uint)_VSRDTFLAGS.RDT_NoLock,
            sourcePath,
            out _,
            out _,
            out var docData,
            out _);
        if (ErrorHandler.Failed(hr) || docData == IntPtr.Zero) return null;

        try
        {
            var docObj = Marshal.GetObjectForIUnknown(docData);
            return docObj is IVsTextLines textLines
                ? adaptersFactory.GetDocumentBuffer(textLines)
                : null;
        }
        finally
        {
            Marshal.Release(docData);
        }
    }

    public void Dispose()
    {
        _site = null;
    }
}
