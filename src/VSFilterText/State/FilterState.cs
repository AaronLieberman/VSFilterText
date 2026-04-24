using System;

namespace VSFilterText.State;

/// <summary>
/// Per-filter-document mutable state. Owned by a single <c>FilterDocument</c>.
/// </summary>
internal sealed class FilterState
{
    private string _text = string.Empty;
    private int _matchCount;

    public event EventHandler? Changed;

    public string Text
    {
        get => _text;
        set
        {
            value ??= string.Empty;
            if (_text == value) return;
            _text = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public int MatchCount
    {
        get => _matchCount;
        set
        {
            if (_matchCount == value) return;
            _matchCount = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    // EXTENSION POINT: case sensitivity, regex, invert, whole-word — add fields here
    // and consume them inside FilterPredicate.IsMatch.
}
