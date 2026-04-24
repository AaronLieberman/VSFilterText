using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;
using VSFilterText.Commands;
using VSFilterText.Editor;

namespace VSFilterText;

/// <summary>
/// Root package. Registers the filter editor factory, the Ctrl+Alt+F command, and the RDT
/// lifetime tracker.
/// </summary>
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("VSFilterText", "Read-only side-by-side filtered view of the active document.", "0.1.0")]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideEditorFactory(typeof(FilterEditorFactory), 101, TrustLevel = __VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted)]
[ProvideEditorExtension(typeof(FilterEditorFactory), ".vsfiltertext", 50)]
[ProvideEditorLogicalView(typeof(FilterEditorFactory), "{7651A700-06E5-11D1-8EBD-00A0C90F26EA}" /* LOGVIEWID_Primary */)]
[Guid(PackageGuidString)]
[ProvideAutoLoad(Microsoft.VisualStudio.VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(Microsoft.VisualStudio.VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
public sealed class VSFilterTextPackage : AsyncPackage
{
    public const string PackageGuidString = "A5B8C9D0-1E2F-3A4B-5C6D-7E8F9A0B1C2D";

    private FilterDocumentLifetime? _lifetime;

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await base.InitializeAsync(cancellationToken, progress);
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        RegisterEditorFactory(new FilterEditorFactory(this));

        var rdt = (IVsRunningDocumentTable)await GetServiceAsync(typeof(SVsRunningDocumentTable));
        _lifetime = new FilterDocumentLifetime(rdt);

        if (await GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
        {
            OpenFilterViewCommand.Register(commandService, this);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _lifetime?.Dispose();
            _lifetime = null;
        }
        base.Dispose(disposing);
    }
}
