namespace PromptResponse.Desktop.ViewModels.Prompts.Presentation;

/// <summary>Pure display and accessibility rules for role-assigned fields.</summary>
internal static class PromptRolePresentation
{
    internal static bool IsMine(string? role, string? activeRole) =>
        string.IsNullOrWhiteSpace(activeRole)
        || string.IsNullOrWhiteSpace(role)
        || string.Equals(role, activeRole, StringComparison.Ordinal);

    internal static string? Badge(bool isMine, string? roleDisplayName) =>
        !isMine && !string.IsNullOrWhiteSpace(roleDisplayName)
            ? $"For {roleDisplayName}"
            : null;

    internal static string? Announcement(bool isMine, string? roleDisplayName) =>
        string.IsNullOrWhiteSpace(roleDisplayName)
            ? null
            : isMine
                ? $"For {roleDisplayName}."
                : $"For {roleDisplayName}. You can still answer it if you need to.";
}
