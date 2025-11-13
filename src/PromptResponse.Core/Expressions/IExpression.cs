namespace PromptResponse.Core.Expressions;

/// <summary>
/// Interface for expressions that can be evaluated.
/// </summary>
public interface IExpression
{
    /// <summary>
    /// Evaluates the expression with the given context.
    /// </summary>
    /// <param name="context">The evaluation context containing field values.</param>
    /// <returns>The result of the evaluation.</returns>
    object Evaluate(IEvaluationContext context);
}
