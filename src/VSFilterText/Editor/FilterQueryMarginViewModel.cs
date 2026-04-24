using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using VSFilterText.Filter;
using VSFilterText.State;

namespace VSFilterText.Editor;

/// <summary>
/// Debounces user input and pushes the resulting query text into <see cref="FilterState"/>,
/// triggering a re-apply through <see cref="FilterEngine"/>. Also mirrors match count out.
/// </summary>
internal sealed class FilterQueryMarginViewModel : INotifyPropertyChanged
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(100);

    private readonly FilterState _state;
    private readonly FilterEngine _engine;
    private readonly DispatcherTimer _debounce;

    private string _text = string.Empty;
    private int _matchCount;

    public FilterQueryMarginViewModel(FilterState state, FilterEngine engine)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        _debounce = new DispatcherTimer { Interval = DebounceDelay };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            _state.Text = _text;
            _engine.Apply();
        };

        _state.Changed += (_, _) =>
        {
            if (_matchCount != _state.MatchCount)
            {
                _matchCount = _state.MatchCount;
                Raise(nameof(MatchCount));
                Raise(nameof(MatchCountDisplay));
            }
        };

        _matchCount = _state.MatchCount;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Text
    {
        get => _text;
        set
        {
            value ??= string.Empty;
            if (_text == value) return;
            _text = value;
            Raise();
            _debounce.Stop();
            _debounce.Start();
        }
    }

    public int MatchCount => _matchCount;

    public string MatchCountDisplay => _matchCount == 1 ? "1 match" : $"{_matchCount} matches";

    private void Raise([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
