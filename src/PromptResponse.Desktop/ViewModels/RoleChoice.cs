namespace PromptResponse.Desktop.ViewModels;

/// <summary>One option in "which role are you filling?".</summary>
/// <param name="Id">The role identifier, or null for "everyone".</param>
/// <param name="Name">What to show in the picker.</param>
/// <param name="Description">The author's sentence about who this is, when they wrote one.</param>
public sealed record RoleChoice(string? Id, string Name, string? Description)
{
    /// <summary>Name and description together, for the picker and for assistive technology.</summary>
    public string Display => string.IsNullOrWhiteSpace(Description) ? Name : $"{Name} — {Description}";
}
