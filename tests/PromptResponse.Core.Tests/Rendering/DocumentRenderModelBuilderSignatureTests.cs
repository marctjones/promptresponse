using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using Xunit;

namespace PromptResponse.Core.Tests.Rendering;

/// <summary>
/// Verifies signature rendering and the public builder argument boundary.
/// </summary>
public class DocumentRenderModelBuilderSignatureTests
{
    private readonly DocumentRenderModelBuilder _builder = DocumentRenderModelBuilderTestFactory.CreateBuilder();

    [Fact]
    public void Build_SignedDocument_AppendsSignatureBlock()
    {
        var doc = DocumentRenderModelBuilderTestFactory.CreateDocument(new Section { Id = "s", Title = "S", Prompts = [new Prompt { Id = "a", Label = "A" }] });
        doc.Metadata.TemplateId = "t";
        using var cert = PromptResponse.Core.Signing.SignatureCertificates.CreateSelfSigned(
            "Publisher", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        doc.Signatures = [PromptResponse.Core.Signing.AprSigner.SignTemplate(doc, cert, DateTime.UtcNow)];

        var block = _builder.Build(doc, RenderOptions.Default).Blocks.OfType<SignatureBlock>().Single();

        block.Signatures.Should().ContainSingle();
        block.Signatures[0].Role.Should().Be("Publisher");
        block.Signatures[0].Signer.Should().Be("Publisher");
        block.Signatures[0].ContentValid.Should().BeTrue();
    }

    [Fact]
    public void Build_UnsignedDocument_HasNoSignatureBlock()
    {
        var doc = DocumentRenderModelBuilderTestFactory.CreateDocument(new Section { Id = "s", Title = "S", Prompts = [new Prompt { Id = "a", Label = "A" }] });
        _builder.Build(doc, RenderOptions.Default).Blocks.OfType<SignatureBlock>().Should().BeEmpty();
    }

    [Fact]
    public void Build_NullDocument_Throws()
    {
        var act = () => _builder.Build(null!, RenderOptions.Default);
        act.Should().Throw<ArgumentNullException>();
    }
}
