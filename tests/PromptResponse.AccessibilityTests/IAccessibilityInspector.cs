using System.Collections.Generic;
using System.Threading.Tasks;

namespace PromptResponse.AccessibilityTests;

/// <summary>
/// Platform-agnostic interface for inspecting accessibility properties of UI elements.
/// </summary>
/// <remarks>
/// Implementations:
/// - LinuxAccessibilityInspector: Uses AT-SPI2 via D-Bus
/// - WindowsAccessibilityInspector: Uses UI Automation (future)
/// - MacAccessibilityInspector: Uses NSAccessibility (future)
/// </remarks>
public interface IAccessibilityInspector
{
    /// <summary>
    /// Gets the platform this inspector supports.
    /// </summary>
    string Platform { get; }

    /// <summary>
    /// Checks if this inspector is available on the current platform.
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Finds an accessible element by its accessible name.
    /// </summary>
    /// <param name="name">The accessible name to search for.</param>
    /// <param name="role">Optional role filter (e.g., "text", "button", "heading").</param>
    /// <returns>The accessible element, or null if not found.</returns>
    Task<AccessibleElement?> FindElementByNameAsync(string name, string? role = null);

    /// <summary>
    /// Finds all accessible elements matching criteria.
    /// </summary>
    /// <param name="role">Role to filter by (e.g., "text field", "button").</param>
    /// <returns>List of matching elements.</returns>
    Task<IReadOnlyList<AccessibleElement>> FindElementsByRoleAsync(string role);

    /// <summary>
    /// Gets the entire accessibility tree for the application.
    /// </summary>
    /// <param name="applicationName">Name of the application to inspect.</param>
    /// <returns>Root accessible element with children.</returns>
    Task<AccessibleElement?> GetAccessibilityTreeAsync(string applicationName);

    /// <summary>
    /// Validates that an element has proper accessibility properties.
    /// </summary>
    /// <param name="element">Element to validate.</param>
    /// <returns>Validation result with any issues found.</returns>
    Task<AccessibilityValidationResult> ValidateElementAsync(AccessibleElement element);
}

/// <summary>
/// Represents an accessible UI element.
/// </summary>
public class AccessibleElement
{
    /// <summary>
    /// Accessible name (label announced by screen readers).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Accessible role (e.g., "button", "text field", "heading").
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Help text or description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Current value (for text fields, sliders, etc.).
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Current states (focused, selected, expanded, etc.).
    /// </summary>
    public HashSet<string> States { get; set; } = new();

    /// <summary>
    /// Child elements in the accessibility tree.
    /// </summary>
    public List<AccessibleElement> Children { get; set; } = new();

    /// <summary>
    /// Parent element (null for root).
    /// </summary>
    public AccessibleElement? Parent { get; set; }

    /// <summary>
    /// Platform-specific identifier.
    /// </summary>
    public string? PlatformId { get; set; }

    /// <summary>
    /// Whether this element is visible to assistive technologies.
    /// </summary>
    public bool IsAccessible { get; set; } = true;

    /// <summary>
    /// Recursively finds all descendants matching a predicate.
    /// </summary>
    public IEnumerable<AccessibleElement> FindDescendants(Func<AccessibleElement, bool> predicate)
    {
        foreach (var child in Children)
        {
            if (predicate(child))
            {
                yield return child;
            }

            foreach (var descendant in child.FindDescendants(predicate))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Gets a flattened list of all descendants.
    /// </summary>
    public IEnumerable<AccessibleElement> GetAllDescendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var descendant in child.GetAllDescendants())
            {
                yield return descendant;
            }
        }
    }

    public override string ToString()
    {
        return $"{Role}: \"{Name}\"" + (string.IsNullOrEmpty(Value) ? "" : $" = \"{Value}\"");
    }
}

/// <summary>
/// Result of accessibility validation.
/// </summary>
public class AccessibilityValidationResult
{
    public bool IsValid => Issues.Count == 0;
    public List<AccessibilityIssue> Issues { get; set; } = new();

    public void AddIssue(AccessibilityIssueSeverity severity, string message, string? recommendation = null)
    {
        Issues.Add(new AccessibilityIssue
        {
            Severity = severity,
            Message = message,
            Recommendation = recommendation
        });
    }
}

/// <summary>
/// An accessibility issue found during validation.
/// </summary>
public class AccessibilityIssue
{
    public AccessibilityIssueSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Recommendation { get; set; }
}

/// <summary>
/// Severity of accessibility issues.
/// </summary>
public enum AccessibilityIssueSeverity
{
    /// <summary>
    /// Critical - prevents assistive technology users from accessing content.
    /// </summary>
    Critical,

    /// <summary>
    /// High - significantly impairs assistive technology user experience.
    /// </summary>
    High,

    /// <summary>
    /// Medium - causes confusion or extra effort for assistive technology users.
    /// </summary>
    Medium,

    /// <summary>
    /// Low - minor issue that slightly affects assistive technology users.
    /// </summary>
    Low,

    /// <summary>
    /// Info - not an issue but could be improved.
    /// </summary>
    Info
}
