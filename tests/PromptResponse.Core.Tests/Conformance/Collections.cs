namespace PromptResponse.Core.Tests.Conformance;

/// <summary>Names of the xUnit collections that serialize tests which must not run alongside each other.</summary>
internal static class Collections
{
    /// <summary>The suites that feed hostile input to a reader under a wall-clock budget.</summary>
    public const string HostileInput = "Hostile-input fuzzing";
}
