using System.Collections.ObjectModel;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.ViewModels.Workflows;

/// <summary>
/// Resolves document roles and applies the current role selection to prompt view models.
/// The shell retains its public bindings while this workflow owns the role-specific state
/// and the invariant that every tracked prompt receives the same active role.
/// </summary>
internal sealed class RoleSelectionWorkflow
{
    private readonly Func<IEnumerable<PromptViewModelBase>> _prompts;
    private readonly ObservableCollection<RoleChoice> _availableRoles = new();
    private RoleChoice? _activeRoleChoice;

    public RoleSelectionWorkflow(Func<IEnumerable<PromptViewModelBase>> prompts)
    {
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
    }

    public event Action? StateChanged;

    public ObservableCollection<RoleChoice> AvailableRoles => _availableRoles;

    public bool HasRoles => _availableRoles.Count > 1;

    public RoleChoice? ActiveRoleChoice
    {
        get => _activeRoleChoice;
        set
        {
            if (_activeRoleChoice == value) return;
            _activeRoleChoice = value;
            ApplyActiveRoleToPrompts();
            StateChanged?.Invoke();
        }
    }

    public string? ActiveRoleDescription => _activeRoleChoice?.Description;

    public string ActiveRoleSummary
    {
        get
        {
            if (!HasRoles) return string.Empty;
            if (_activeRoleChoice?.Id is null) return "Showing every part of this form.";

            var prompts = _prompts().ToArray();
            var mine = prompts.Count(prompt => prompt.IsMine);
            return $"{mine} of {prompts.Length} fields are for " +
                   $"{_activeRoleChoice.Name}. The rest are marked, and still answerable.";
        }
    }

    public void Apply(AprDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        // A duplicate prompt id makes a document invalid, but it must still open so its
        // author can correct it. Last/first choice is immaterial for an invalid document;
        // keeping the first prevents an exception while leaving it editable.
        var resolved = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (prompt, role) in FormRoles.Resolve(document))
        {
            resolved.TryAdd(prompt.Id, role);
        }

        foreach (var prompt in _prompts())
        {
            var role = resolved.GetValueOrDefault(prompt.Model.Id);
            prompt.Role = role;
            prompt.RoleDisplayName = FormRoles.DisplayName(document, role);
        }

        _availableRoles.Clear();
        var used = FormRoles.Used(document);
        if (used.Count > 0)
        {
            _availableRoles.Add(new RoleChoice(null, "Everyone", "Show every part of this form"));
            foreach (var id in used)
            {
                var definition = FormRoles.Definition(document, id);
                _availableRoles.Add(new RoleChoice(id, definition?.DisplayName ?? id, definition?.Description));
            }
        }

        _activeRoleChoice = _availableRoles.FirstOrDefault();
        ApplyActiveRoleToPrompts();
        StateChanged?.Invoke();
    }

    private void ApplyActiveRoleToPrompts()
    {
        foreach (var prompt in _prompts())
        {
            prompt.ActiveRole = _activeRoleChoice?.Id;
        }
    }
}
