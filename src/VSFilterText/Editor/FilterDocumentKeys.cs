namespace VSFilterText.Editor;

/// <summary>
/// Keys used to stash per-filter-view data in the view's property bag, so MEF-discovered
/// components (margin, double-click handler) can find their per-document state.
/// </summary>
internal static class FilterDocumentKeys
{
    public const string SourceMoniker = "VSFilterText.SourceMoniker";
}
