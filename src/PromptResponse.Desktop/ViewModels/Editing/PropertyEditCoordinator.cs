namespace PromptResponse.Desktop.ViewModels.Editing;

/// <summary>
/// Applies a scalar view-model edit directly or records it in an
/// <see cref="EditHistory"/> while preserving property-change notifications.
/// </summary>
/// <remarks>
/// The target is explicit because <see cref="PropertyEditCommand{T}"/> uses it
/// to merge consecutive edits to the same property. Keeping this policy here
/// makes editor view models responsible only for their model-specific getters,
/// setters, and any additional derived-state notifications.
/// </remarks>
public static class PropertyEditCoordinator
{
    public static void Apply<T>(
        object target,
        string propertyName,
        EditHistory? history,
        Func<T> getter,
        Action<T> applySetter,
        T newValue,
        Action<string> notify)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(applySetter);
        ArgumentNullException.ThrowIfNull(notify);

        var oldValue = getter();
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue)) return;

        if (history?.IsApplying == true)
        {
            applySetter(newValue);
            notify(propertyName);
            return;
        }

        if (history is null)
        {
            applySetter(newValue);
            notify(propertyName);
            return;
        }

        history.Execute(new PropertyEditCommand<T>(
            target,
            propertyName,
            value =>
            {
                applySetter(value);
                notify(propertyName);
            },
            oldValue,
            newValue));
    }
}
