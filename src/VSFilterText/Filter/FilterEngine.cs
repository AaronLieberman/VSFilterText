using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Projection;
using VSFilterText.State;

namespace VSFilterText.Filter;

/// <summary>
/// Walks the source snapshot, computes the set of keep-spans (one per matching line),
/// and applies them to the elision buffer. Used for the initial seed and for every
/// subsequent filter change or source edit.
/// </summary>
internal sealed class FilterEngine
{
    private readonly IElisionBuffer _elisionBuffer;
    private readonly ITextBuffer _sourceBuffer;
    private readonly FilterState _state;

    public FilterEngine(ITextBuffer sourceBuffer, IElisionBuffer elisionBuffer, FilterState state)
    {
        _sourceBuffer = sourceBuffer ?? throw new ArgumentNullException(nameof(sourceBuffer));
        _elisionBuffer = elisionBuffer ?? throw new ArgumentNullException(nameof(elisionBuffer));
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public int LastMatchCount { get; private set; }

    /// <summary>
    /// Recomputes keep-spans and pushes them to the elision buffer. Empty filter keeps all lines.
    /// </summary>
    public void Apply()
    {
        var snapshot = _sourceBuffer.CurrentSnapshot;
        var keep = ComputeKeepSpans(snapshot, _state);

        // NEEDS VERIFICATION: exact signatures of ElideSpans / ExpandSpans. Depending on VSSDK
        // version the API may take NormalizedSpanCollection, IEnumerable<Span>, or require
        // first ExpandSpans(all) then ElideSpans(hide). The safe pattern below re-seeds by
        // expanding everything and then eliding the complement.
        var all = new Span(0, snapshot.Length);
        _elisionBuffer.ExpandSpans(new NormalizedSpanCollection(all));

        var hide = ComputeComplement(all, keep);
        if (hide.Count > 0)
        {
            _elisionBuffer.ElideSpans(new NormalizedSpanCollection(hide));
        }

        LastMatchCount = CountMatches(keep, snapshot);
        _state.MatchCount = LastMatchCount;
    }

    private static List<Span> ComputeKeepSpans(ITextSnapshot snapshot, FilterState state)
    {
        var keep = new List<Span>();

        // Empty filter: keep everything as one span.
        if (string.IsNullOrEmpty(state.Text))
        {
            if (snapshot.Length > 0) keep.Add(new Span(0, snapshot.Length));
            return keep;
        }

        var lineCount = snapshot.LineCount;
        int? runStart = null;
        int runEnd = 0;

        for (var i = 0; i < lineCount; i++)
        {
            var line = snapshot.GetLineFromLineNumber(i);
            var text = line.GetText();
            if (FilterPredicate.IsMatch(text, state))
            {
                var spanStart = line.Start.Position;
                var spanEnd = line.EndIncludingLineBreak.Position;
                if (runStart is null)
                {
                    runStart = spanStart;
                    runEnd = spanEnd;
                }
                else if (spanStart == runEnd)
                {
                    runEnd = spanEnd;
                }
                else
                {
                    keep.Add(Span.FromBounds(runStart.Value, runEnd));
                    runStart = spanStart;
                    runEnd = spanEnd;
                }
            }
        }

        if (runStart is not null)
        {
            keep.Add(Span.FromBounds(runStart.Value, runEnd));
        }

        return keep;
    }

    private static List<Span> ComputeComplement(Span whole, IReadOnlyList<Span> keep)
    {
        var result = new List<Span>();
        var cursor = whole.Start;
        foreach (var k in keep)
        {
            if (k.Start > cursor)
            {
                result.Add(Span.FromBounds(cursor, k.Start));
            }
            cursor = k.End;
        }
        if (cursor < whole.End)
        {
            result.Add(Span.FromBounds(cursor, whole.End));
        }
        return result;
    }

    private static int CountMatches(List<Span> keepSpans, ITextSnapshot snapshot)
    {
        var count = 0;
        foreach (var span in keepSpans)
        {
            var startLine = snapshot.GetLineNumberFromPosition(span.Start);
            // The keep-span ends on an exclusive position. The last included line is the one
            // whose EndIncludingLineBreak is >= span.End — equivalent to the line containing
            // position (span.End - 1), provided the span is non-empty.
            if (span.Length == 0) continue;
            var endLine = snapshot.GetLineNumberFromPosition(span.End - 1);
            count += endLine - startLine + 1;
        }
        return count;
    }
}
