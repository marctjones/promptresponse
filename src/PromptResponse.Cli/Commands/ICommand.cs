namespace PromptResponse.Cli.Commands;

/// <summary>
/// Interface for CLI commands.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="args">Command arguments.</param>
    /// <returns>Exit code (0 for success).</returns>
    Task<int> ExecuteAsync(string[] args);
}
