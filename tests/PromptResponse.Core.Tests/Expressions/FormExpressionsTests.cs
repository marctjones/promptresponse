using AwesomeAssertions;
using PromptResponse.Core.Expressions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Expressions;

/// <summary>
/// Expressions are CEL, with expectedDataType supplying the type environment
/// (specification section 8).
/// </summary>
public class FormExpressionsTests
{
    private static Prompt P(string id, string? type = null, string response = "", Action<PromptHints>? hints = null)
    {
        var h = new PromptHints { ExpectedDataType = type };
        hints?.Invoke(h);
        return new Prompt { Id = id, Label = id, Response = response, Hints = h };
    }

    private static AprDocument Doc(params Prompt[] prompts) => new()
    {
        Metadata = new Metadata { Title = "T" },
        Sections = [new Section { Id = "s", Title = "S", Prompts = [.. prompts] }],
    };

    // ── The type environment ──

    [Theory]
    [InlineData("number", "double")]
    [InlineData("currency", "double")]
    [InlineData("boolean", "bool")]
    [InlineData("date", "google.protobuf.Timestamp")]
    [InlineData("text", "string")]
    [InlineData(null, "string")]
    public void Hint_DeclaresTheCelType(string? hint, string expectedContains)
    {
        var context = FormExpressions.BuildContext(Doc(P("f", hint)));

        context.DeclaredTypeOf("f").ToLowerInvariant()
            .Should().Contain(expectedContains.Split('.').Last().ToLowerInvariant());
    }

    [Fact]
    public void TypedFields_AllowNaturalArithmetic_WithoutConversionWrappers()
    {
        // The whole point of the type environment: an author writes what they mean.
        var doc = Doc(
            P("qty", "number", "3"),
            P("price", "currency", "12.50"),
            P("total", "currency", hints: h => h.ExprValue = "qty * price"));

        var context = FormExpressions.BuildContext(doc);

        FormExpressions.ComputeValue(doc.Sections[0].Prompts[2], context).Should().Be("37.5");
    }

    // ── Unbindable values degrade; they never default ──

    [Fact]
    public void FreeTextInATypedField_DoesNotBind_AndTheExpressionDegrades()
    {
        // "about twelve" is a perfectly valid response. It is stored verbatim and simply
        // does not participate in the calculation.
        var doc = Doc(
            P("qty", "number", "about twelve"),
            P("price", "currency", "10"),
            P("total", "currency", hints: h => h.ExprValue = "qty * price"));

        var context = FormExpressions.BuildContext(doc);

        context.IsBound("qty").Should().BeFalse("free text does not bind to double");
        FormExpressions.ComputeValue(doc.Sections[0].Prompts[2], context)
            .Should().BeNull("an unevaluable expression degrades to the stored response");
        doc.Sections[0].Prompts[0].Response.Should().Be("about twelve", "the answer is untouched");
    }

    [Fact]
    public void EmptyTypedField_IsAnError_NotZero()
    {
        // Binding an empty number as 0 would make a blank field silently total as zero:
        // a wrong answer rather than no answer.
        var doc = Doc(
            P("qty", "number", ""),
            P("price", "currency", "10"),
            P("total", "currency", hints: h => h.ExprValue = "qty * price"));

        var context = FormExpressions.BuildContext(doc);

        context.IsBound("qty").Should().BeFalse();
        FormExpressions.ComputeValue(doc.Sections[0].Prompts[2], context).Should().BeNull();
    }

    // ── Advisory behaviour ──

    [Fact]
    public void ExprHidden_RequiresABool_AndAnUnevaluableHintSimplyDoesNotApply()
    {
        var doc = Doc(
            P("rush", "boolean", "true"),
            P("reason", "text", hints: h => h.ExprHidden = "rush != true"),
            P("broken", "text", hints: h => h.ExprHidden = "nosuchfield"));

        var context = FormExpressions.BuildContext(doc);

        FormExpressions.IsHidden(doc.Sections[0].Prompts[1], context).Should().BeFalse("rush is true");
        FormExpressions.IsHidden(doc.Sections[0].Prompts[2], context)
            .Should().BeFalse("a hint that cannot be evaluated does not apply");
    }

    [Fact]
    public void Boolean_AcceptsTheGenerousReadSet()
    {
        foreach (var yes in new[] { "true", "yes", "Y", "1", "on", "x", "checked" })
        {
            var doc = Doc(P("b", "boolean", yes), P("t", "text", hints: h => h.ExprHidden = "b"));
            var context = FormExpressions.BuildContext(doc);
            FormExpressions.IsHidden(doc.Sections[0].Prompts[1], context)
                .Should().BeTrue($"'{yes}' reads as true (specification section 4.9)");
        }
    }

    [Fact]
    public void ExprValidation_ReturnsAMessage_AndEmptyMeansValid()
    {
        var doc = Doc(
            P("start", "number", "5"),
            P("end", "number", "3", h => h.ExprValidation = "_this > start ? '' : 'Must be after start'"));

        var context = FormExpressions.BuildContext(doc);

        FormExpressions.Validate(doc.Sections[0].Prompts[1], context).Should().Be("Must be after start");

        doc.Sections[0].Prompts[1].Response = "9";
        FormExpressions.Validate(doc.Sections[0].Prompts[1], FormExpressions.BuildContext(doc)).Should().BeNull();
    }

    // ── Marshalling back, via the canonical write forms of section 4.9 ──

    [Fact]
    public void ComputedResults_UseTheCanonicalWriteForms()
    {
        var doc = Doc(
            P("n", "number", "2"),
            P("flag", "boolean", hints: h => h.ExprValue = "n > 1"),
            P("sum", "number", hints: h => h.ExprValue = "n + 1.5"));

        var context = FormExpressions.BuildContext(doc);

        FormExpressions.ComputeValue(doc.Sections[0].Prompts[1], context).Should().Be("true");
        FormExpressions.ComputeValue(doc.Sections[0].Prompts[2], context).Should().Be("3.5");
    }

    [Fact]
    public void RecomputeComputedValues_LeavesAnAnswerAloneWhenTheExpressionFails()
    {
        var doc = Doc(
            P("qty", "number", "not a number"),
            P("total", "currency", "hand entered", h => h.ExprValue = "qty * 2.0"));

        FormExpressions.RecomputeComputedValues(doc);

        doc.Sections[0].Prompts[1].Response.Should().Be("hand entered",
            "a filler never loses an answer to a broken formula");
    }

    [Fact]
    public void ComputedField_IsNotReadOnly_BecauseAnyStringIsAValidResponse()
    {
        // Read-only is a presentation hint, never a lock. A computed total that is wrong
        // must be correctable, or the format has stopped accepting text.
        var doc = Doc(P("n", "number", "2"), P("total", "number", hints: h => h.ExprValue = "n * 2.0"));
        var context = FormExpressions.BuildContext(doc);
        var total = doc.Sections[0].Prompts[1];

        FormExpressions.IsComputed(total).Should().BeTrue();
        FormExpressions.IsReadOnly(total, context).Should().BeFalse(
            "being computed does not lock a field; only exprReadOnly asks for that presentation");
    }

    [Fact]
    public void RecomputeComputedValues_DoesNotRevertAnAnswerSomeoneTypedOver()
    {
        var doc = Doc(P("n", "number", "2"), P("total", "number", hints: h => h.ExprValue = "n * 2.0"));

        FormExpressions.RecomputeComputedValues(doc);
        var total = doc.Sections[0].Prompts[1];
        total.Response.Should().Be("4", "computed on the first pass");
        total.ResponseMetadata.Source.Should().Be(FormExpressions.ComputedSource);

        // A person corrects it. Setting Response clears the provenance.
        total.Response = "4 (agreed with vendor)";
        total.ResponseMetadata.Source.Should().BeNull("an authored answer is not a computed one");

        doc.Sections[0].Prompts[0].Response = "10";
        FormExpressions.RecomputeComputedValues(doc);

        total.Response.Should().Be("4 (agreed with vendor)",
            "a correction survives; reverting it would lose an answer the format promises to keep");
    }

    [Fact]
    public void RecomputeComputedValues_StillUpdatesAValueItProducedItself()
    {
        var doc = Doc(P("n", "number", "2"), P("total", "number", hints: h => h.ExprValue = "n * 2.0"));

        FormExpressions.RecomputeComputedValues(doc);
        doc.Sections[0].Prompts[0].Response = "5";
        FormExpressions.RecomputeComputedValues(doc);

        doc.Sections[0].Prompts[1].Response.Should().Be("10",
            "an untouched computed field keeps tracking its inputs");
    }

    [Fact]
    public void RecomputeComputedValues_SettlesChainedComputations()
    {
        var doc = Doc(
            P("a", "number", "2"),
            P("b", "number", hints: h => h.ExprValue = "a * 3.0"),
            P("c", "number", hints: h => h.ExprValue = "b + 1.0"));

        FormExpressions.RecomputeComputedValues(doc);

        doc.Sections[0].Prompts[1].Response.Should().Be("6");
        doc.Sections[0].Prompts[2].Response.Should().Be("7");
    }

    // ── Authoring-time checking: strictness lands on the author, never the filler ──

    [Fact]
    public void Check_ReportsTypeErrorsAtAuthoringTime()
    {
        var context = FormExpressions.BuildContext(Doc(P("note", "text"), P("qty", "number")));

        context.Check("qty * 2.0").Should().BeEmpty("a sound expression has no diagnostics");

        context.Check("note ? 'a' : 'b'").Should().NotBeEmpty(
            "CEL requires bool in a condition; a string is a type error the author should see");
        context.Check("nosuchfield * 2.0").Should().NotBeEmpty("an undeclared reference is caught");
    }

    [Fact]
    public void GetAllPrompts_WalksNestedSectionsInOrder()
    {
        var doc = new AprDocument
        {
            Metadata = new Metadata { Title = "T" },
            Sections =
            [
                new Section
                {
                    Id = "outer", Title = "Outer",
                    Prompts = [P("first")],
                    Sections = [new Section { Id = "inner", Title = "Inner", Prompts = [P("second")] }],
                },
            ],
        };

        FormExpressions.GetAllPrompts(doc).Select(p => p.Id).Should().Equal("first", "second");
    }
}
