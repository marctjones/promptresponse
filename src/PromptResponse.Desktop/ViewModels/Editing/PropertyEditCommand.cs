namespace PromptResponse.Desktop.ViewModels.Editing;

/// <summary>
/// Generic edit command for a single property mutation on a single target.
/// Captures the old + new values so Undo can restore the original. Merges
/// with consecutive same-property/same-target edits within a short window so
/// keystroke-by-keystroke typing collapses into one undo step.
/// </summary>
/// <typeparam name="T">The property's value type.</typeparam>
public sealed class PropertyEditCommand<T> : IEditCommand
{
    private static readonly TimeSpan MergeWindow = TimeSpan.FromMilliseconds(500);

    private readonly object _target;
    private readonly string _propertyName;
    private readonly Action<T> _apply;
    private readonly T _oldValue;
    private T _newValue;
    private DateTime _lastApplied;

    public PropertyEditCommand(
        object target,
        string propertyName,
        Action<T> apply,
        T oldValue,
        T newValue)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _oldValue = oldValue;
        _newValue = newValue;
        _lastApplied = DateTime.UtcNow;
    }

    public string Description => $"Edit {_propertyName}";

    public void Execute() { _apply(_newValue); _lastApplied = DateTime.UtcNow; }
    public void Undo() => _apply(_oldValue);

    public bool CanMergeWith(IEditCommand next)
        => next is PropertyEditCommand<T> p
           && ReferenceEquals(p._target, _target)
           && p._propertyName == _propertyName
           && DateTime.UtcNow - _lastApplied < MergeWindow;

    public void MergeWith(IEditCommand next)
    {
        if (next is PropertyEditCommand<T> p)
        {
            // Keep our oldValue (the user's original starting state); take the
            // newest newValue as the resulting state. The next command was
            // already executed by the history before merge — don't re-apply.
            _newValue = p._newValue;
            _lastApplied = DateTime.UtcNow;
        }
    }
}
