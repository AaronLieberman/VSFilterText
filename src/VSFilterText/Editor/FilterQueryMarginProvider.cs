using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using VSFilterText.Filter;
using VSFilterText.State;

namespace VSFilterText.Editor;

/// <summary>
/// MEF provider that attaches <see cref="FilterQueryMargin"/> to the top of a filter-doc view.
/// The view-role filter ensures this never mounts on normal editors.
/// </summary>
[Export(typeof(IWpfTextViewMarginProvider))]
[Name(FilterQueryMargin.MarginName)]
[MarginContainer(PredefinedMarginNames.Top)]
[ContentType("text")]
[TextViewRole(FilterViewRoles.FilterView)]
[Order(Before = PredefinedMarginNames.HorizontalScrollBar)]
internal sealed class FilterQueryMarginProvider : IWpfTextViewMarginProvider
{
    public IWpfTextViewMargin? CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
    {
        var view = wpfTextViewHost.TextView;

        // Filter state and engine are stashed on the view's property bag by FilterDocument
        // at creation time. Absence here means something's wrong with the compose order.
        if (!view.Properties.TryGetProperty<FilterState>(typeof(FilterState), out var state)) return null;
        if (!view.Properties.TryGetProperty<FilterEngine>(typeof(FilterEngine), out var engine)) return null;

        var vm = new FilterQueryMarginViewModel(state, engine);
        return new FilterQueryMargin(vm);
    }
}
