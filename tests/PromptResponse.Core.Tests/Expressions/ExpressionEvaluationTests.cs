using FluentAssertions;
using PromptResponse.Core.Expressions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Expressions;

/// <summary>
/// Integration tests for expression evaluation using DocumentEvaluationContext.
/// </summary>
public class ExpressionEvaluationTests
{
    private readonly ExpressionParser _parser;
    private readonly AprDocument _document;
    private readonly IEvaluationContext _context;

    public ExpressionEvaluationTests()
    {
        _parser = new ExpressionParser();
        _document = CreateTestDocument();
        _context = new DocumentEvaluationContext(_document);
    }

    #region Arithmetic Operations

    [Theory]
    [InlineData("2 + 3", 5.0)]
    [InlineData("10 - 4", 6.0)]
    [InlineData("5 * 6", 30.0)]
    [InlineData("20 / 4", 5.0)]
    public void Evaluate_WithSimpleArithmetic_ShouldReturnCorrectResult(string expression, double expected)
    {
        // Arrange
        var expr = _parser.Parse(expression);

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        Convert.ToDouble(result).Should().BeApproximately(expected, 0.001);
    }

    [Fact]
    public void Evaluate_WithFieldAddition_ShouldSumFieldValues()
    {
        // Arrange
        var expr = _parser.Parse("{salary} + {bonus}");

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        Convert.ToDouble(result).Should().BeApproximately(70000.0, 0.001);
    }

    [Fact]
    public void Evaluate_WithFieldMultiplication_ShouldMultiplyCorrectly()
    {
        // Arrange
        var expr = _parser.Parse("{hourly_rate} * {hours_worked}");

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        Convert.ToDouble(result).Should().BeApproximately(800.0, 0.001);
    }

    #endregion

    #region Comparison Operations

    [Theory]
    [InlineData("{age} >= 18", true)]
    [InlineData("{age} < 18", false)]
    [InlineData("{age} == 25", true)]
    [InlineData("{age} != 30", true)]
    public void Evaluate_WithNumericComparison_ShouldReturnBoolean(string expression, bool expected)
    {
        // Arrange
        var expr = _parser.Parse(expression);

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        Convert.ToBoolean(result).Should().Be(expected);
    }

    [Theory]
    [InlineData("{name} == \"John Doe\"", true)]
    [InlineData("{name} != \"Jane Smith\"", true)]
    public void Evaluate_WithStringComparison_ShouldReturnBoolean(string expression, bool expected)
    {
        // Arrange
        var expr = _parser.Parse(expression);

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        Convert.ToBoolean(result).Should().Be(expected);
    }

    #endregion

    #region Function Evaluations

    [Fact]
    public void Evaluate_WithSumFunction_ShouldReturnSum()
    {
        // Arrange
        var expr = _parser.Parse("SUM({salary}, {bonus}, 1000)");

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        Convert.ToDouble(result).Should().BeApproximately(71000.0, 0.001);
    }

    [Fact]
    public void Evaluate_WithAvgFunction_ShouldReturnAverage()
    {
        // Arrange
        var expr = _parser.Parse("AVG({salary}, {bonus})");

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        Convert.ToDouble(result).Should().BeApproximately(35000.0, 0.001);
    }

    [Fact]
    public void Evaluate_WithIfFunction_WhenTrue_ShouldReturnTrueBranch()
    {
        // Arrange
        var expr = _parser.Parse("IF({age} >= 18, \"adult\", \"minor\")");

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        result.ToString().Should().Be("adult");
    }

    [Fact]
    public void Evaluate_WithIfFunction_WhenFalse_ShouldReturnFalseBranch()
    {
        // Arrange
        var expr = _parser.Parse("IF({age} < 18, \"minor\", \"adult\")");

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        result.ToString().Should().Be("adult");
    }

    [Fact]
    public void Evaluate_WithRoundFunction_ShouldRoundCorrectly()
    {
        // Arrange
        var expr = _parser.Parse("ROUND({hourly_rate} / 3, 2)");

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        Convert.ToDouble(result).Should().BeApproximately(6.67, 0.01);
    }

    [Fact]
    public void Evaluate_WithAbsFunction_ShouldReturnAbsoluteValue()
    {
        // Arrange
        var expr = _parser.Parse("ABS(-100)");

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        Convert.ToDouble(result).Should().Be(100.0);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Evaluate_WithNonExistentField_ShouldReturnEmptyString()
    {
        // Arrange
        var expr = _parser.Parse("{nonexistent_field}");

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        result.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_WithDivisionByZero_ShouldHandleGracefully()
    {
        // Arrange
        var expr = _parser.Parse("10 / 0");

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        // Should return Infinity or handle gracefully
        result.Should().NotBeNull();
    }

    [Fact]
    public void Evaluate_WithEmptyFieldValue_ShouldTreatAsZero()
    {
        // Arrange
        var expr = _parser.Parse("{empty_field} + 100");

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        Convert.ToDouble(result).Should().Be(100.0);
    }

    #endregion

    #region Complex Expressions

    [Fact]
    public void Evaluate_WithNestedFunctions_ShouldEvaluateCorrectly()
    {
        // Arrange
        var expr = _parser.Parse("SUM(AVG({salary}, {bonus}), 1000)");

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        Convert.ToDouble(result).Should().BeApproximately(36000.0, 0.001);
    }

    [Fact]
    public void Evaluate_WithMixedOperations_ShouldRespectOperatorPrecedence()
    {
        // Arrange
        var expr = _parser.Parse("2 + 3 * 4");

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        Convert.ToDouble(result).Should().Be(14.0);
    }

    [Fact]
    public void Evaluate_WithParentheses_ShouldRespectGrouping()
    {
        // Arrange
        var expr = _parser.Parse("(2 + 3) * 4");

        // Act
        var result = expr.Evaluate(_context);

        // Assert
        Convert.ToDouble(result).Should().Be(20.0);
    }

    #endregion

    private static AprDocument CreateTestDocument()
    {
        return new AprDocument
        {
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section_1",
                    Title = "Test Section",
                    Prompts = new List<Prompt>
                    {
                        new() { Id = "name", Label = "Name", Response = "John Doe" },
                        new() { Id = "age", Label = "Age", Response = "25" },
                        new() { Id = "salary", Label = "Salary", Response = "60000" },
                        new() { Id = "bonus", Label = "Bonus", Response = "10000" },
                        new() { Id = "hourly_rate", Label = "Hourly Rate", Response = "20" },
                        new() { Id = "hours_worked", Label = "Hours Worked", Response = "40" },
                        new() { Id = "empty_field", Label = "Empty Field", Response = "" }
                    }
                }
            }
        };
    }
}
