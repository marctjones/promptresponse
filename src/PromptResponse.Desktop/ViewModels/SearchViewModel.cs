using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// Find-and-jump within a form. Matches prompts by label, id, or response text
/// (case-insensitive). Empty query returns no matches (not everything) so the user
/// isn't drowned in noise.
/// </summary>
public sealed partial class SearchViewModel : ObservableObject
{
    private AprDocument? _document;
    private readonly ObservableCollection<Prompt> _matches = new();

    public IReadOnlyList<Prompt> Matches => _matches;

    public IEnumerable<string> SectionTitlesForCurrentMatch
    {
        get
        {
            if (CurrentMatch == null || _document == null) yield break;
            foreach (var path in PathTo(_document, CurrentMatch))
            {
                yield return path;
            }
        }
    }

    [ObservableProperty]
    private int matchCount;

    [ObservableProperty]
    private int currentMatchIndex;

    [ObservableProperty]
    private Prompt? currentMatch;

    private string _query = string.Empty;
    public string Query
    {
        get => _query;
        set
        {
            if (_query == value) return;
            SetProperty(ref _query, value);
            Recompute();
        }
    }

    public void SetDocument(AprDocument? document)
    {
        _document = document;
        Recompute();
    }

    public void NextMatch()
    {
        if (_matches.Count == 0) return;
        CurrentMatchIndex = (CurrentMatchIndex + 1) % _matches.Count;
        CurrentMatch = _matches[CurrentMatchIndex];
    }

    public void PreviousMatch()
    {
        if (_matches.Count == 0) return;
        CurrentMatchIndex = (CurrentMatchIndex - 1 + _matches.Count) % _matches.Count;
        CurrentMatch = _matches[CurrentMatchIndex];
    }

    public void Clear()
    {
        Query = string.Empty;
    }

    private void Recompute()
    {
        _matches.Clear();
        if (_document == null || string.IsNullOrEmpty(_query))
        {
            MatchCount = 0;
            CurrentMatchIndex = 0;
            CurrentMatch = null;
            return;
        }

        var query = _query.Trim();
        if (query.Length == 0)
        {
            MatchCount = 0;
            CurrentMatchIndex = 0;
            CurrentMatch = null;
            return;
        }

        foreach (var section in _document.Sections)
        {
            CollectMatches(section, query);
        }

        MatchCount = _matches.Count;
        CurrentMatchIndex = _matches.Count > 0 ? 0 : 0;
        CurrentMatch = _matches.Count > 0 ? _matches[0] : null;
    }

    private void CollectMatches(Section section, string query)
    {
        foreach (var prompt in section.Prompts)
        {
            if (PromptMatches(prompt, query))
            {
                _matches.Add(prompt);
            }
        }
        foreach (var nested in section.Sections)
        {
            CollectMatches(nested, query);
        }
    }

    private static bool PromptMatches(Prompt prompt, string query)
    {
        return Contains(prompt.Label, query)
            || Contains(prompt.Id, query)
            || Contains(prompt.Response, query);
    }

    private static bool Contains(string? haystack, string needle) =>
        haystack != null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> PathTo(AprDocument document, Prompt target)
    {
        var path = new List<string>();
        if (Find(document.Sections, target, path))
        {
            return path;
        }
        return Array.Empty<string>();
    }

    private static bool Find(IReadOnlyList<Section> sections, Prompt target, List<string> path)
    {
        foreach (var section in sections)
        {
            path.Add(section.Title);
            if (section.Prompts.Contains(target)) return true;
            if (Find(section.Sections, target, path)) return true;
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }
}
