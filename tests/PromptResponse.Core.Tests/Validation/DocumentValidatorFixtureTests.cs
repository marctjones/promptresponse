using AwesomeAssertions;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

/// <summary>Regression validation for representative APR document fixtures.</summary>
public class DocumentValidatorFixtureTests : DocumentValidatorTestBase
{
    [Theory]
    [InlineData("irs-form-w4-2024.aprt")]
    [InlineData("gsa-sf86-sections.aprt")]
    [InlineData("irs-form-1040-simplified.aprt")]
    public void Validate_GovernmentTemplate_ShouldPassValidation(string filename)
    {
        var document = new AprJsonSerializer().Deserialize(File.ReadAllText(DocumentValidatorFixtureSupport.GetPath(filename)));

        var result = Validator.Validate(document);

        result.IsValid.Should().BeTrue($"{filename} should pass validation");
        result.Errors.Should().BeEmpty($"{filename} should have no validation errors");
    }

    [Fact]
    public void Validate_AllGovernmentForms_ShouldPassValidation()
    {
        foreach (var filename in new[] { "irs-form-w4-2024.aprt", "gsa-sf86-sections.aprt", "irs-form-1040-simplified.aprt" })
        {
            var document = new AprJsonSerializer().Deserialize(File.ReadAllText(DocumentValidatorFixtureSupport.GetPath(filename)));
            var result = Validator.Validate(document);

            result.IsValid.Should().BeTrue($"{filename} should pass validation");
            result.Errors.Should().BeEmpty($"{filename} should have no validation errors");
        }
    }
}
