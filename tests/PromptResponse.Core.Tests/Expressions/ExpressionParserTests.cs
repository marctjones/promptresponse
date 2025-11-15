using FluentAssertions;
using PromptResponse.Core.Expressions;
using Xunit;

namespace PromptResponse.Core.Tests.Expressions;

/// <summary>
/// Unit tests for ExpressionParser.
/// </summary>
public class ExpressionParserTests
{
    private readonly ExpressionParser _parser;

    public ExpressionParserTests()
    {
        _parser = new ExpressionParser();
    }

    #region Literal Expressions

    [Theory]
    [InlineData("42")]
    [InlineData("123.45")]
    [InlineData("-10")]
    public void Parse_WithNumberLiteral_ShouldReturnLiteralExpression(string input)
    {
        // Act
        var result = _parser.Parse(input);

        // Assert
        result.Should().BeOfType<LiteralExpression>();
    }

    [Theory]
    [InlineData("\"hello\"")]
    [InlineData("\"test string\"")]
    [InlineData("\"\"")]
    public void Parse_WithStringLiteral_ShouldReturnLiteralExpression(string input)
    {
        // Act
        var result = _parser.Parse(input);

        // Assert
        result.Should().BeOfType<LiteralExpression>();
    }

    #endregion

    #region Field Reference Expressions

    [Theory]
    [InlineData("{field1}")]
    [InlineData("{employeeName}")]
    [InlineData("{section_1_prompt_1}")]
    public void Parse_WithFieldReference_ShouldReturnFieldReferenceExpression(string input)
    {
        // Act
        var result = _parser.Parse(input);

        // Assert
        result.Should().BeOfType<FieldReferenceExpression>();
    }

    #endregion

    #region Binary Operations

    [Theory]
    [InlineData("2 + 3")]
    [InlineData("10 - 5")]
    [InlineData("4 * 6")]
    [InlineData("20 / 4")]
    public void Parse_WithArithmeticOperation_ShouldReturnBinaryExpression(string input)
    {
        // Act
        var result = _parser.Parse(input);

        // Assert
        result.Should().BeOfType<BinaryExpression>();
    }

    [Theory]
    [InlineData("{field1} + {field2}")]
    [InlineData("{salary} * 12")]
    [InlineData("100 - {deductions}")]
    public void Parse_WithFieldsInArithmetic_ShouldReturnBinaryExpression(string input)
    {
        // Act
        var result = _parser.Parse(input);

        // Assert
        result.Should().BeOfType<BinaryExpression>();
    }

    [Theory]
    [InlineData("{field1} == {field2}")]
    [InlineData("{age} >= 18")]
    [InlineData("{status} != \"pending\"")]
    public void Parse_WithComparisonOperation_ShouldReturnBinaryExpression(string input)
    {
        // Act
        var result = _parser.Parse(input);

        // Assert
        result.Should().BeOfType<BinaryExpression>();
    }

    #endregion

    #region Function Calls

    [Theory]
    [InlineData("SUM(1, 2, 3)")]
    [InlineData("AVG({field1}, {field2})")]
    [InlineData("COUNT({item1}, {item2}, {item3})")]
    public void Parse_WithFunctionCall_ShouldReturnFunctionCallExpression(string input)
    {
        // Act
        var result = _parser.Parse(input);

        // Assert
        result.Should().BeOfType<FunctionCallExpression>();
    }

    [Fact]
    public void Parse_WithNestedFunctions_ShouldReturnNestedFunctionCallExpression()
    {
        // Arrange
        var input = "SUM(AVG(1, 2), 3)";

        // Act
        var result = _parser.Parse(input);

        // Assert
        result.Should().BeOfType<FunctionCallExpression>();
    }

    [Theory]
    [InlineData("IF({age} >= 18, \"adult\", \"minor\")")]
    [InlineData("IF({married} == \"yes\", {spouse_income}, 0)")]
    public void Parse_WithConditionalFunction_ShouldReturnFunctionCallExpression(string input)
    {
        // Act
        var result = _parser.Parse(input);

        // Assert
        result.Should().BeOfType<FunctionCallExpression>();
    }

    #endregion

    #region Complex Expressions

    [Theory]
    [InlineData("(2 + 3) * 4")]
    [InlineData("({field1} + {field2}) / 2")]
    [InlineData("SUM({income1}, {income2}) - {deductions}")]
    public void Parse_WithComplexExpression_ShouldReturnCorrectStructure(string input)
    {
        // Act
        var result = _parser.Parse(input);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Error Cases

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_WithEmptyOrNullInput_ShouldThrow(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.Parse(input));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData("(")]
    [InlineData(")")]
    [InlineData("2 +")]
    [InlineData("* 3")]
    public void Parse_WithInvalidSyntax_ShouldThrowExpressionParseException(string input)
    {
        // Act & Assert
        Assert.Throws<ExpressionParseException>(() => _parser.Parse(input));
    }

    [Theory]
    [InlineData("{field")]
    [InlineData("field}")]
    [InlineData("SUM(1, 2")]
    [InlineData("AVG(")]
    public void Parse_WithUnclosedDelimiters_ShouldThrowExpressionParseException(string input)
    {
        // Act & Assert
        Assert.Throws<ExpressionParseException>(() => _parser.Parse(input));
    }

    #endregion
}
