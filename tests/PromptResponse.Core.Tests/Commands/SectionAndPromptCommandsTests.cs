using FluentAssertions;
using PromptResponse.Core.Commands;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Commands;

/// <summary>
/// Behavioural tests for the four mutating-document commands:
/// AddSectionCommand, RemoveSectionCommand, AddPromptCommand, RemovePromptCommand.
/// </summary>
public class SectionAndPromptCommandsTests
{
    private static AprDocument CreateDocument() =>
        new()
        {
            Metadata = new Metadata { Title = "Test" },
            Sections = new List<Section>
            {
                new() { Id = "s1", Title = "First Section",
                    Prompts = new List<Prompt> { new() { Id = "p1", Label = "Existing" } } },
            },
        };

    // ---- AddSectionCommand ----

    [Fact]
    public void AddSectionCommand_Execute_AppendsSectionWhenIndexNegative()
    {
        var doc = CreateDocument();
        var newSection = new Section { Id = "s2", Title = "Appended" };
        var cmd = new AddSectionCommand(doc, newSection);

        cmd.Execute();

        doc.Sections.Should().HaveCount(2);
        doc.Sections[1].Should().BeSameAs(newSection);
    }

    [Fact]
    public void AddSectionCommand_Execute_InsertsAtSpecifiedIndex()
    {
        var doc = CreateDocument();
        var newSection = new Section { Id = "s0", Title = "Inserted First" };
        var cmd = new AddSectionCommand(doc, newSection, index: 0);

        cmd.Execute();

        doc.Sections[0].Should().BeSameAs(newSection);
        doc.Sections.Should().HaveCount(2);
    }

    [Fact]
    public void AddSectionCommand_Execute_IndexBeyondEnd_AppendsAsFallback()
    {
        var doc = CreateDocument();
        var newSection = new Section { Id = "sX", Title = "Far" };
        var cmd = new AddSectionCommand(doc, newSection, index: 999);

        cmd.Execute();

        doc.Sections.Last().Should().BeSameAs(newSection);
    }

    [Fact]
    public void AddSectionCommand_Undo_RemovesAddedSection()
    {
        var doc = CreateDocument();
        var newSection = new Section { Id = "s2", Title = "Temp" };
        var cmd = new AddSectionCommand(doc, newSection);
        cmd.Execute();

        cmd.Undo();

        doc.Sections.Should().HaveCount(1);
        doc.Sections.Should().NotContain(newSection);
    }

    [Fact]
    public void AddSectionCommand_Description_NamesTheSection()
    {
        var doc = CreateDocument();
        var newSection = new Section { Id = "s2", Title = "Customer Details" };
        var cmd = new AddSectionCommand(doc, newSection);

        cmd.Description.Should().Contain("Customer Details");
    }

    [Fact]
    public void AddSectionCommand_Constructor_RejectsNullArguments()
    {
        var doc = CreateDocument();
        var section = new Section { Id = "s2" };

        Action nullDoc = () => new AddSectionCommand(null!, section);
        Action nullSection = () => new AddSectionCommand(doc, null!);

        nullDoc.Should().Throw<ArgumentNullException>();
        nullSection.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddSectionCommand_NeverMerges()
    {
        var doc = CreateDocument();
        var s = new Section { Id = "s2" };
        var cmd = new AddSectionCommand(doc, s);
        var other = new AddSectionCommand(doc, new Section { Id = "s3" });

        cmd.CanMergeWith(other).Should().BeFalse();
        // MergeWith is a no-op; calling it shouldn't blow up.
        cmd.MergeWith(other);
    }

    // ---- RemoveSectionCommand ----

    [Fact]
    public void RemoveSectionCommand_Execute_RemovesSectionAndRecordsIndex()
    {
        var doc = CreateDocument();
        var second = new Section { Id = "s2", Title = "Second" };
        doc.Sections.Add(second);
        var cmd = new RemoveSectionCommand(doc, second);

        cmd.Execute();

        doc.Sections.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveSectionCommand_Undo_RestoresSectionAtOriginalIndex()
    {
        var doc = CreateDocument();
        var middle = new Section { Id = "sMid", Title = "Middle" };
        var last = new Section { Id = "sLast", Title = "Last" };
        doc.Sections.Add(middle);
        doc.Sections.Add(last);

        var cmd = new RemoveSectionCommand(doc, middle);
        cmd.Execute();
        doc.Sections.Should().Equal(doc.Sections.First(), last);

        cmd.Undo();

        doc.Sections.Should().HaveCount(3);
        doc.Sections[1].Should().BeSameAs(middle, "section restored at original index");
    }

    [Fact]
    public void RemoveSectionCommand_Execute_OnUnknownSection_Throws()
    {
        var doc = CreateDocument();
        var phantom = new Section { Id = "ghost", Title = "Not in doc" };
        var cmd = new RemoveSectionCommand(doc, phantom);

        Action act = () => cmd.Execute();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveSectionCommand_Description_NamesTheSection()
    {
        var doc = CreateDocument();
        var s = new Section { Id = "s9", Title = "Payment Info" };
        doc.Sections.Add(s);
        var cmd = new RemoveSectionCommand(doc, s);

        cmd.Description.Should().Contain("Payment Info");
    }

    [Fact]
    public void RemoveSectionCommand_Constructor_RejectsNullArguments()
    {
        var doc = CreateDocument();
        var s = new Section();

        Action nullDoc = () => new RemoveSectionCommand(null!, s);
        Action nullSection = () => new RemoveSectionCommand(doc, null!);

        nullDoc.Should().Throw<ArgumentNullException>();
        nullSection.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RemoveSectionCommand_NeverMerges()
    {
        var doc = CreateDocument();
        var s = new Section();
        doc.Sections.Add(s);
        var cmd = new RemoveSectionCommand(doc, s);

        cmd.CanMergeWith(cmd).Should().BeFalse();
        cmd.MergeWith(cmd);
    }

    // ---- AddPromptCommand ----

    [Fact]
    public void AddPromptCommand_Execute_AppendsPromptToSection()
    {
        var doc = CreateDocument();
        var newPrompt = new Prompt { Id = "pNew", Label = "Added" };
        var cmd = new AddPromptCommand(doc.Sections[0], newPrompt);

        cmd.Execute();

        doc.Sections[0].Prompts.Should().HaveCount(2);
        doc.Sections[0].Prompts.Last().Should().BeSameAs(newPrompt);
    }

    [Fact]
    public void AddPromptCommand_Execute_InsertsAtSpecifiedIndex()
    {
        var doc = CreateDocument();
        var p = new Prompt { Id = "pFirst", Label = "First" };
        var cmd = new AddPromptCommand(doc.Sections[0], p, index: 0);

        cmd.Execute();

        doc.Sections[0].Prompts[0].Should().BeSameAs(p);
    }

    [Fact]
    public void AddPromptCommand_Execute_IndexBeyondEnd_AppendsAsFallback()
    {
        var doc = CreateDocument();
        var p = new Prompt { Id = "pX", Label = "Far" };
        var cmd = new AddPromptCommand(doc.Sections[0], p, index: 999);

        cmd.Execute();

        doc.Sections[0].Prompts.Last().Should().BeSameAs(p);
    }

    [Fact]
    public void AddPromptCommand_Undo_RemovesAddedPrompt()
    {
        var doc = CreateDocument();
        var p = new Prompt { Id = "pTemp", Label = "Temp" };
        var cmd = new AddPromptCommand(doc.Sections[0], p);
        cmd.Execute();

        cmd.Undo();

        doc.Sections[0].Prompts.Should().HaveCount(1);
        doc.Sections[0].Prompts.Should().NotContain(p);
    }

    [Fact]
    public void AddPromptCommand_Description_NamesPromptAndSection()
    {
        var doc = CreateDocument();
        var p = new Prompt { Id = "pX", Label = "Email" };
        var cmd = new AddPromptCommand(doc.Sections[0], p);

        var description = cmd.Description;

        description.Should().Contain("Email");
        description.Should().Contain("First Section");
    }

    [Fact]
    public void AddPromptCommand_Constructor_RejectsNullArguments()
    {
        var doc = CreateDocument();
        var p = new Prompt { Id = "p" };

        Action nullSection = () => new AddPromptCommand(null!, p);
        Action nullPrompt = () => new AddPromptCommand(doc.Sections[0], null!);

        nullSection.Should().Throw<ArgumentNullException>();
        nullPrompt.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddPromptCommand_NeverMerges()
    {
        var doc = CreateDocument();
        var p = new Prompt { Id = "p2" };
        var cmd = new AddPromptCommand(doc.Sections[0], p);
        var other = new AddPromptCommand(doc.Sections[0], new Prompt { Id = "p3" });

        cmd.CanMergeWith(other).Should().BeFalse();
        cmd.MergeWith(other);
    }

    // ---- RemovePromptCommand ----

    [Fact]
    public void RemovePromptCommand_Execute_RemovesPromptFromSection()
    {
        var doc = CreateDocument();
        var existing = doc.Sections[0].Prompts[0];
        var cmd = new RemovePromptCommand(doc.Sections[0], existing);

        cmd.Execute();

        doc.Sections[0].Prompts.Should().BeEmpty();
    }

    [Fact]
    public void RemovePromptCommand_Undo_RestoresPromptAtOriginalIndex()
    {
        var doc = CreateDocument();
        var first = doc.Sections[0].Prompts[0];
        var second = new Prompt { Id = "p2", Label = "Second" };
        var third = new Prompt { Id = "p3", Label = "Third" };
        doc.Sections[0].Prompts.Add(second);
        doc.Sections[0].Prompts.Add(third);

        var cmd = new RemovePromptCommand(doc.Sections[0], second);
        cmd.Execute();
        doc.Sections[0].Prompts.Should().Equal(first, third);

        cmd.Undo();

        doc.Sections[0].Prompts[1].Should().BeSameAs(second, "prompt restored at original index");
    }

    [Fact]
    public void RemovePromptCommand_Execute_OnUnknownPrompt_Throws()
    {
        var doc = CreateDocument();
        var phantom = new Prompt { Id = "phantom" };
        var cmd = new RemovePromptCommand(doc.Sections[0], phantom);

        Action act = () => cmd.Execute();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemovePromptCommand_Description_NamesPromptAndSection()
    {
        var doc = CreateDocument();
        var p = doc.Sections[0].Prompts[0];
        var cmd = new RemovePromptCommand(doc.Sections[0], p);

        var description = cmd.Description;

        description.Should().Contain("Existing");
        description.Should().Contain("First Section");
    }

    [Fact]
    public void RemovePromptCommand_Constructor_RejectsNullArguments()
    {
        var doc = CreateDocument();
        var p = doc.Sections[0].Prompts[0];

        Action nullSection = () => new RemovePromptCommand(null!, p);
        Action nullPrompt = () => new RemovePromptCommand(doc.Sections[0], null!);

        nullSection.Should().Throw<ArgumentNullException>();
        nullPrompt.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RemovePromptCommand_NeverMerges()
    {
        var doc = CreateDocument();
        var p = doc.Sections[0].Prompts[0];
        var cmd = new RemovePromptCommand(doc.Sections[0], p);

        cmd.CanMergeWith(cmd).Should().BeFalse();
        cmd.MergeWith(cmd);
    }
}
