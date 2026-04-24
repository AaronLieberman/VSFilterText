using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VSFilterText.Editor;

namespace VSFilterText.Commands;

/// <summary>
/// Hotkey <c>Ctrl+Alt+F</c> handler. Resolves the active document, then either focuses an
/// existing filter tab or opens a new one on <c>vsfiltertext://&lt;source&gt;</c>.
/// </summary>
internal sealed class OpenFilterViewCommand
{
    public static readonly Guid CommandSet = new("B6C7D8E9-2F3A-4B5C-6D7E-8F9A0B1C2D3E");
    public const int CommandId = 0x0100;

    private readonly IServiceProvider _services;

    private OpenFilterViewCommand(IServiceProvider services)
    {
        _services = services;
    }

    public static void Register(OleMenuCommandService commandService, IServiceProvider services)
    {
        if (commandService is null) throw new ArgumentNullException(nameof(commandService));

        var instance = new OpenFilterViewCommand(services);
        var commandId = new CommandID(CommandSet, CommandId);
        commandService.AddCommand(new MenuCommand(instance.Execute, commandId));
    }

    private void Execute(object? sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var sourceMoniker = ResolveActiveDocumentMoniker();
        if (string.IsNullOrEmpty(sourceMoniker)) return;

        var openDoc = _services.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
        if (openDoc is null) return;

        var filterMoniker = FilterEditorFactory.BuildMoniker(sourceMoniker!);
        var editorFactoryGuid = FilterEditorFactory.EditorFactoryGuid;
        var logicalView = VSConstants.LOGVIEWID_Primary;

        // OpenSpecificEditor focuses the existing frame when the moniker is already open;
        // otherwise it dispatches to our FilterEditorFactory to create a new one.
        var hr = openDoc.OpenSpecificEditor(
            (uint)__VSOSPFLAGS.OSP_OpenDocument,
            filterMoniker,
            ref editorFactoryGuid,
            null,
            ref logicalView,
            "VSFilterText Filter View",
            null,
            VSConstants.VSITEMID_NIL,
            IntPtr.Zero,
            null,
            out var frame);

        if (ErrorHandler.Succeeded(hr) && frame is not null)
        {
            frame.Show();
        }
    }

    private string? ResolveActiveDocumentMoniker()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var monitorSelection = _services.GetService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
        if (monitorSelection is null) return null;

        if (ErrorHandler.Failed(monitorSelection.GetCurrentElementValue(
                (uint)VSConstants.VSSELELEMID.SEID_DocumentFrame,
                out var frameObj))
            || frameObj is not IVsWindowFrame frame)
        {
            return null;
        }

        if (frame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out var monikerObj) != VSConstants.S_OK
            || monikerObj is not string moniker)
        {
            return null;
        }

        // Don't open a filter on a filter.
        return FilterEditorFactory.TryParseMoniker(moniker, out _) ? null : moniker;
    }
}
