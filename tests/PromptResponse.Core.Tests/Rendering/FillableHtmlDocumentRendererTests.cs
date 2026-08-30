using System.Text;
using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using Xunit;

namespace PromptResponse.Core.Tests.Rendering;

/// <summary>
/// Verifies the fillable HTML renderer: a self-contained web form with live
/// inputs, an embedded copy of the document for the round-trip, and a vanilla-JS
/// download shim. Like the static renderer, all dynamic text is HTML-encoded
/// (an XSS boundary), and the embedded JSON is unicode-escaped so it cannot
/// break out of its script container.
/// </summary>
public class FillableHtmlDocumentRendererTests
{
    private readonly FillableHtmlDocumentRenderer _renderer = new();

    private static AprDocument SampleDoc() => new()
    {
        Metadata = new Metadata { Title = "Contact Form", Description = "Please complete" },
        Sections =
        [
            new Section
            {
                Id = "s1", Title = "Personal",
                Prompts =
                [
                    new Prompt { Id = "name", Label = "Full Name", Response = "Ada", Hints = new PromptHints { HelpText = "As on ID" } },
                    new Prompt { Id = "email", Label = "Email", Response = "", Hints = new PromptHints { ExpectedDataType = "email" } },
                    new Prompt { Id = "subscribe", Label = "Subscribe", Response = "true", Hints = new PromptHints { ExpectedDataType = "boolean" } },
                    new Prompt { Id = "color", Label = "Color", Response = "Blue", Hints = new PromptHints { SuggestedValues = ["Red", "Blue"] } },
                    new Prompt { Id = "notes", Label = "Notes", Response = "", Hints = new PromptHints { ExpectedDataType = "multiline" } },
                ],
            },
        ],
    };

    private static string Render(FillableHtmlDocumentRenderer r, AprDocument doc, RenderOptions? o = null) =>
        Encoding.UTF8.GetString(r.RenderToBytes(doc, o));

    [Fact]
    public void FormatMetadata_IsHtmlForm()
    {
        _renderer.FormatId.Should().Be("html-form");
        _renderer.FileExtension.Should().Be(".html");
    }

    [Fact]
    public void Render_ProducesFormWithDownloadButtonAndEmbeddedDocument()
    {
        var html = Render(_renderer, SampleDoc());

        html.Should().StartWith("<!DOCTYPE html>");
        html.Should().Contain("<html lang=\"en\">");
        html.Should().Contain("<form id=\"apr-form\"");
        html.Should().Contain("id=\"apr-download\"");
        html.Should().Contain("Download filled form");
        html.Should().Contain("<script type=\"application/json\" id=\"apr-document\">");
        html.Should().Contain(".aprf");
    }

    [Fact]
    public void Render_MapsFieldsToTypedInputs()
    {
        var html = Render(_renderer, SampleDoc());

        // Each prompt is keyed by its stable id for the round-trip.
        html.Should().Contain("data-prompt-id=\"name\"");
        html.Should().Contain("type=\"email\"");                 // email data type
        html.Should().Contain("type=\"checkbox\"");              // boolean -> checkbox
        html.Should().Contain("<select");                        // suggested values -> dropdown
        html.Should().Contain("<textarea");                      // multiline -> textarea
    }

    [Fact]
    public void Render_PrefillsExistingResponses()
    {
        var html = Render(_renderer, SampleDoc());

        html.Should().Contain("value=\"Ada\"");                  // text default
        html.Should().Contain("checked");                        // boolean "true"
        html.Should().Contain("<option value=\"Blue\" selected>"); // dropdown selection
    }

    [Fact]
    public void Render_UsesTheRightEmptyChoicePrompt_ForFieldsAndTableCells()
    {
        var doc = new AprDocument
        {
            Metadata = new Metadata { Title = "Choices" },
            Sections =
            [
                new Section
                {
                    Id = "s", Title = "Fields",
                    Prompts = [new Prompt { Id = "field", Label = "Field", Hints = new PromptHints { SuggestedValues = ["A"] } }],
                },
                new Section
                {
                    Id = "t", Title = "Table", Kind = "table",
                    Sections = [new Section { Id = "row", Title = "Row", Prompts =
                    [new Prompt { Id = "cell", Label = "Cell", Hints = new PromptHints { SuggestedValues = ["A"] } }] }],
                },
            ],
        };

        var html = Render(_renderer, doc);

        html.Should().Contain("<option value=\"\">— choose —</option>");
        html.Should().Contain("<option value=\"\">—</option>");
    }

    [Fact]
    public void Render_AssociatesLabelsAndHelpForAccessibility()
    {
        var html = Render(_renderer, SampleDoc());

        // Label points at the input id, and help is wired via aria-describedby.
        html.Should().MatchRegex("<label for=\"f\\d+\">Full Name</label>");
        html.Should().Contain("aria-describedby=\"");
        html.Should().Contain("As on ID");
    }

    [Fact]
    public void Render_EscapesResponses_AndEmbeddedJson_NoScriptBreakout()
    {
        var doc = new AprDocument
        {
            Metadata = new Metadata { Title = "T" },
            Sections = [new Section { Id = "s", Title = "S", Prompts = [
                new Prompt { Id = "x", Label = "Comment", Response = "</script><script>alert(1)</script>" }] }],
        };

        var html = Render(_renderer, doc);

        // The malicious response must not appear as a live script anywhere —
        // not in the input value, not inside the embedded JSON block.
        html.Should().NotContain("<script>alert(1)</script>");
        // The embedded JSON unicode-escapes '<' (System.Text.Json emits <; our
        // own pass covers anything it leaves raw) so it can't terminate the script.
        html.ToLowerInvariant().Should().Contain("\\u003c");
    }

    [Fact]
    public void Render_AlwaysIncludesEmptyFields_EvenWhenCallerExcludesThem()
    {
        // A form needs its blanks regardless of the caller's empty-field preference.
        var html = Render(_renderer, SampleDoc(), new RenderOptions { IncludeEmptyFields = false });

        html.Should().Contain("data-prompt-id=\"email\"");
        html.Should().Contain("data-prompt-id=\"notes\"");
    }

    [Fact]
    public void Render_FixedTableCells_BecomeLiveInputsKeyedByCellId()
    {
        var doc = new AprDocument
        {
            Metadata = new Metadata { Title = "Quarterly" },
            Sections =
            [
                new Section
                {
                    Id = "q", Title = "By quarter",
                    Kind = "table",
                    Sections =
                    [
                        new Section { Id = "q1", Title = "Q1", Prompts =
                        [
                            new Prompt { Id = "q1.revenue", Label = "Revenue", Response = "5000" },
                            new Prompt { Id = "q1.audited", Label = "Audited", Response = "true", Hints = new PromptHints { ExpectedDataType = "boolean" } },
                            new Prompt { Id = "q1.status", Label = "Status", Response = "Final", Hints = new PromptHints { SuggestedValues = ["Draft", "Final"] } },
                        ]},
                    ],
                },
            ],
        };

        var html = Render(_renderer, doc);

        // Each cell is a live input keyed by its "{rowId}.{columnId}" id.
        html.Should().Contain("<th scope=\"row\">Q1</th>");
        html.Should().Contain("data-prompt-id=\"q1.revenue\"").And.Contain("value=\"5000\"");
        html.Should().Contain("data-prompt-id=\"q1.audited\"");
        html.Should().MatchRegex("type=\"checkbox\"[^>]*data-prompt-id=\"q1.audited\"[^>]*checked"); // boolean column -> checked checkbox
        html.Should().Contain("data-prompt-id=\"q1.status\"");
        html.Should().Contain("<option value=\"Final\" selected>");                                   // suggested-values column -> dropdown
        html.Should().Contain("aria-label=\"Q1 Revenue\"");                                           // accessible name from row + column
    }

    [Fact]
    public void Render_GoesThroughTheSharedBuilder()
    {
        var builder = Substitute.For<IDocumentRenderModelBuilder>();
        builder.Build(Arg.Any<AprDocument>(), Arg.Any<RenderOptions>())
            .Returns(new RenderModel("T", null, DocumentType.Template, [new FieldBlock("L", "V", true, null, null, "fid")]));
        var renderer = new FillableHtmlDocumentRenderer(builder);

        var html = Render(renderer, SampleDoc());

        builder.Received(1).Build(Arg.Any<AprDocument>(), Arg.Any<RenderOptions>());
        html.Should().Contain("data-prompt-id=\"fid\"").And.Contain("value=\"V\"");
    }
}
