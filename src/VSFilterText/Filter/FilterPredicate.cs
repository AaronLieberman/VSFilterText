using System;
using VSFilterText.State;

namespace VSFilterText.Filter;

/// <summary>
/// Sole place filter semantics live. v1: ordinal case-insensitive substring match.
/// EXTENSION POINT: regex, case sensitivity, invert, whole-word all branch here.
/// </summary>
internal static class FilterPredicate
{
    public static bool IsMatch(string line, FilterState state)
    {
        var q = state.Text;
        if (string.IsNullOrEmpty(q)) return true;
        return line.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
