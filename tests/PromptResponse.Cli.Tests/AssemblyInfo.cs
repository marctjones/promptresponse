using Xunit;

// Console is process-global, and these tests capture it: the CLI commands write to
// Console directly, so asserting on their output means Console.SetOut for the duration
// of a call. Two classes here do that - CliRobustnessTests and ImportCommandTests - and
// with classes running in parallel one test's output lands in the other's capture.
//
// That is not hypothetical: the import quality report test failed with "Validating: ..."
// in its captured output, which is a robustness test's line, not its own. The race was
// latent as soon as the second class started capturing; adding a third made it fire.
//
// Serialising this assembly costs about a second, and buys assertions that mean what
// they say. A flaky test is worse than a slow one - it teaches people to re-run rather
// than to read.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
