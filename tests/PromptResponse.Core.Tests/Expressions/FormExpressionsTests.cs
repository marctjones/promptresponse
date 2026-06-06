using AwesomeAssertions;
using PromptResponse.Core.Expressions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Core.Tests.Expressions;

/// <summary>
/// Verifies the document-level expression service: conditional visibility,
/// computed (read-only) values with fixpoint recompute, conditional-expected,
/// and cross-field validation — plus that the new expr* hints round-trip.
/// </summary>
public class FormExpressionsTests
{
    private static AprDocument Doc(params Prompt[] prompts) => new()
    {
        Metadata = new Metadata { Title = "T" },
        Sections = [new Section { Id = "s", Title = "S", Prompts = prompts.ToList() }],
    };

    [Fact]
    public void IsHidden_RespondsToOtherFields()
    {
        var doc = Doc(
            new Prompt { Id = "emp_status", Response = "Retired" },
            new Prompt { Id = "employer", Hints = new PromptHints { ExprHidden = "emp_status == 'Retired' || emp_status == 'Student'" } });
        var employer = FormExpressions.GetAllPrompts(doc)[1];

        FormExpressions.IsHidden(employer, FormExpressions.BuildFields(doc)).Should().BeTrue();

        doc.Sections[0].Prompts[0].Response = "Employed";
        FormExpressions.IsHidden(employer, FormExpressions.BuildFields(doc)).Should().BeFalse();
    }

    [Fact]
    public void ComputeValue_DerivesFromOtherFields()
    {
        var doc = Doc(
            new Prompt { Id = "qty", Response = "3" },
            new Prompt { Id = "price", Response = "4.5" },
            new Prompt { Id = "total", Hints = new PromptHints { ExprValue = "double(qty) * double(price)" } });

        var total = FormExpressions.GetAllPrompts(doc)[2];
        FormExpressions.ComputeValue(total, FormExpressions.BuildFields(doc)).Should().Be("13.5");
        FormExpressions.IsReadOnly(total, FormExpressions.BuildFields(doc)).Should().BeTrue("computed fields are read-only");
    }

    [Fact]
    public void RecomputeComputedValues_SettlesChainedTotals()
    {
        // grand = sub1 + sub2; sub1 = a + b; sub2 = c  → fixpoint over chained deps.
        var doc = Doc(
            new Prompt { Id = "a", Response = "1" },
            new Prompt { Id = "b", Response = "2" },
            new Prompt { Id = "c", Response = "10" },
            new Prompt { Id = "sub1", Hints = new PromptHints { ExprValue = "double(a) + double(b)" } },
            new Prompt { Id = "sub2", Hints = new PromptHints { ExprValue = "double(c)" } },
            new Prompt { Id = "grand", Hints = new PromptHints { ExprValue = "double(sub1) + double(sub2)" } });

        var changed = FormExpressions.RecomputeComputedValues(doc);

        changed.Should().BeTrue();
        var p = FormExpressions.GetAllPrompts(doc);
        p.Single(x => x.Id == "sub1").Response.Should().Be("3");
        p.Single(x => x.Id == "sub2").Response.Should().Be("10");
        p.Single(x => x.Id == "grand").Response.Should().Be("13");
    }

    [Fact]
    public void RecomputeComputedValues_CircularReference_TerminatesSafely()
    {
        var doc = Doc(
            new Prompt { Id = "x", Hints = new PromptHints { ExprValue = "double(y) + 1" } },
            new Prompt { Id = "y", Hints = new PromptHints { ExprValue = "double(x) + 1" } });

        var act = () => FormExpressions.RecomputeComputedValues(doc);

        act.Should().NotThrow("a circular computed reference must terminate, not hang");
    }

    [Fact]
    public void IsExpected_IsConditional()
    {
        var doc = Doc(
            new Prompt { Id = "emp_status", Response = "Employed" },
            new Prompt { Id = "employer", Hints = new PromptHints { ExprExpected = "emp_status == 'Employed'" } });
        var employer = FormExpressions.GetAllPrompts(doc)[1];

        FormExpressions.IsExpected(employer, FormExpressions.BuildFields(doc)).Should().BeTrue();
    }

    [Fact]
    public void Validate_ReturnsMessageOrNull()
    {
        var doc = Doc(
            new Prompt { Id = "start", Response = "2021-01-01" },
            new Prompt
            {
                Id = "end", Response = "2020-01-01",
                Hints = new PromptHints { ExprValidation = "_this == '' || start == '' ? '' : (timestamp(_this) > timestamp(start) ? '' : 'End must be after start')" },
            });
        var end = FormExpressions.GetAllPrompts(doc)[1];

        FormExpressions.Validate(end, FormExpressions.BuildFields(doc)).Should().Be("End must be after start");

        end.Response = "2022-01-01";
        FormExpressions.Validate(end, FormExpressions.BuildFields(doc)).Should().BeNull("valid → no message");
    }

    [Fact]
    public void BrokenExpression_DegradesGracefully()
    {
        var doc = Doc(new Prompt { Id = "p", Hints = new PromptHints { ExprHidden = "this is ) not valid" } });
        var p = FormExpressions.GetAllPrompts(doc)[0];

        FormExpressions.IsHidden(p, FormExpressions.BuildFields(doc)).Should().BeFalse("a broken hint must not hide/block the field");
    }

    [Fact]
    public void DemoTemplate_ComputesTotal_AndHidesConditionalFields()
    {
        // The bundled order-form demo must actually behave as advertised.
        var path = Path.Combine(
            Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "examples", "order-form.aprt");
        File.Exists(path).Should().BeTrue("the order-form demo template must exist");

        var doc = new AprJsonSerializer().Deserialize(File.ReadAllText(path));
        var byId = FormExpressions.GetAllPrompts(doc).ToDictionary(p => p.Id);

        byId["quantity"].Response = "3";
        byId["unit_price"].Response = "4.5";
        FormExpressions.RecomputeComputedValues(doc);
        byId["line_total"].Response.Should().Be("13.5", "computed line total = qty x price");

        // Conditional gift message: hidden until is_gift is true.
        FormExpressions.IsHidden(byId["gift_message"], FormExpressions.BuildFields(doc)).Should().BeTrue();
        byId["is_gift"].Response = "true";
        FormExpressions.IsHidden(byId["gift_message"], FormExpressions.BuildFields(doc)).Should().BeFalse();

        // Rush reason becomes expected only when rush is requested.
        byId["rush"].Response = "true";
        FormExpressions.IsExpected(byId["rush_reason"], FormExpressions.BuildFields(doc)).Should().BeTrue();
    }

    [Fact]
    public void ExprHints_RoundTripThroughJson()
    {
        var serializer = new AprJsonSerializer();
        var doc = Doc(new Prompt
        {
            Id = "total",
            Hints = new PromptHints
            {
                ExprHidden = "a == 'x'",
                ExprValue = "double(a) + 1",
                ExprExpected = "a != ''",
                ExprValidation = "a == '' ? 'required' : ''",
                ExprReadOnly = "true",
            },
        });

        var restored = serializer.Deserialize(serializer.Serialize(doc));
        var hints = FormExpressions.GetAllPrompts(restored)[0].Hints;

        hints.ExprHidden.Should().Be("a == 'x'");
        hints.ExprValue.Should().Be("double(a) + 1");
        hints.ExprExpected.Should().Be("a != ''");
        hints.ExprValidation.Should().Be("a == '' ? 'required' : ''");
        hints.ExprReadOnly.Should().Be("true");
    }
}
