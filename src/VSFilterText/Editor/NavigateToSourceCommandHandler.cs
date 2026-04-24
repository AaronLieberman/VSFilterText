using System;
using System.ComponentModel.Composition;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace VSFilterText.Editor;

/// <summary>
/// Listens for double-click on a filter-doc view and navigates the source document to the
/// corresponding line.
///
/// NEEDS VERIFICATION: The modern IOleCommandTarget / ICommandHandler path requires adapter-layer
/// plumbing specific to the view. For this prototype we attach a direct WPF mouse handler via
/// <see cref="IWpfTextViewCreationListener"/>. This bypasses the editor command routing, which is
/// acceptable for a read-only view where no other command is competing for double-click.
/// </summary>
[Export(typeof(IWpfTextViewCreationListener))]
[ContentType("text")]
[TextViewRole(FilterViewRoles.FilterView)]
internal sealed class NavigateToSourceCommandHandler : IWpfTextViewCreationListener
{
    public void TextViewCreated(IWpfTextView textView)
    {
        // VisualElement is a FrameworkElement, which doesn't expose MouseDoubleClick.
        // Detect double-click via ClickCount on the preview mouse-down event. WPF mouse
        // handlers always run on the UI thread, but the analyzer doesn't track that
        // across call sites — assert explicitly.
        textView.VisualElement.PreviewMouseLeftButtonDown += (_, e) =>
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (e.ClickCount == 2) OnDoubleClick(textView, e);
        };
    }

    private static void OnDoubleClick(IWpfTextView view, MouseButtonEventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var pointOnView = e.GetPosition(view.VisualElement);
        var textLine = view.TextViewLines.GetTextViewLineContainingYCoordinate(pointOnView.Y + view.ViewportTop);
        if (textLine is null) return;

        var bufferPos = textLine.Start;

        if (view.TextBuffer is not IElisionBuffer elision) return;

        var projection = elision.CurrentSnapshot as IProjectionSnapshot;
        if (projection is null) return;

        var sourcePoints = projection.MapToSourceSnapshots(bufferPos);
        if (sourcePoints == null || sourcePoints.Count == 0) return;

        var sourcePoint = sourcePoints[0];
        var sourceSnapshot = sourcePoint.Snapshot;
        var sourceLineNumber = sourceSnapshot.GetLineNumberFromPosition(sourcePoint.Position);

        if (!view.Properties.TryGetProperty<string>(FilterDocumentKeys.SourceMoniker, out var sourceMoniker)) return;

        NavigateSource(sourceMoniker, sourceLineNumber);
        e.Handled = true;
    }

    private static void NavigateSource(string sourceMoniker, int zeroBasedLine)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var openDoc = ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
        if (openDoc is null) return;

        var logicalView = VSConstants.LOGVIEWID_TextView;
        var hr = openDoc.OpenDocumentViaProject(
            sourceMoniker,
            ref logicalView,
            out _,
            out _,
            out _,
            out var frame);

        if (ErrorHandler.Failed(hr) || frame is null) return;

        frame.Show();

        if (frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out var docViewObj) != VSConstants.S_OK) return;

        var textView = docViewObj switch
        {
            IVsTextView direct => direct,
            IVsCodeWindow codeWin when codeWin.GetPrimaryView(out var primary) == VSConstants.S_OK => primary,
            _ => null
        };

        if (textView is null) return;

        textView.SetCaretPos(zeroBasedLine, 0);
        textView.CenterLines(zeroBasedLine, 1);
    }
}
