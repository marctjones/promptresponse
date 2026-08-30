using AwesomeAssertions;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

public partial class EditorMutationTests
{
    [Fact]
    public void AddPrompt_AppendsDefaultPromptAndNotifiesHost()
    {
        var (vm, model, added, _) = NewBlankSection();

        var newVm = vm.AddPrompt();

        vm.PromptViewModels.Should().Contain(newVm);
        model.Prompts.Should().HaveCount(1);
        model.Prompts[0].Hints.ExpectedDataType.Should().Be("text",
            "default prompts are text-typed so the user can immediately rename + retype");
        added.Should().Contain(newVm,
            "the host needs to subscribe to the new VM's Response changes for progress + advisories");
    }

    [Fact]
    public void RemovePrompt_DropsPromptFromModelAndVMTree_AndNotifiesHost()
    {
        var (vm, model, _, removed) = NewBlankSection();
        var p1 = vm.AddPrompt();
        var p2 = vm.AddPrompt();

        vm.RemovePrompt(p1);

        vm.PromptViewModels.Should().NotContain(p1);
        vm.PromptViewModels.Should().Contain(p2);
        model.Prompts.Should().HaveCount(1);
        removed.Should().Contain(p1);
    }

    [Fact]
    public void AddNestedSection_AppendsAndPropagatesCallbacks()
    {
        var (vm, model, added, _) = NewBlankSection();

        var child = vm.AddNestedSection();
        child.AddPrompt();

        vm.NestedSections.Should().Contain(child);
        model.Sections.Should().HaveCount(1);
        added.Should().Contain(child.PromptViewModels.Last(),
            "the prompt added inside the nested child must reach the host's subscription");
    }

    [Fact]
    public void RemoveNestedSection_DetachesAllPromptsRecursively()
    {
        var (vm, _, _, removed) = NewBlankSection();
        var child = vm.AddNestedSection();
        var grandchild = child.AddNestedSection();
        var sibling = vm.AddNestedSection();
        grandchild.AddPrompt();
        grandchild.AddPrompt();

        vm.RemoveNestedSection(child);

        vm.NestedSections.Should().ContainSingle().Which.Should().Be(sibling);
        removed.Should().HaveCountGreaterThanOrEqualTo(2,
            "removing a section subtree must detach every prompt under it (including grandchildren)");
    }

    [Fact]
    public void AddPromptOnNestedSection_WiresCallbackThroughToHost()
    {
        var (vm, _, added, _) = NewBlankSection();
        var child = vm.AddNestedSection();
        var grandchild = child.AddNestedSection();
        _ = vm.AddNestedSection();

        var deep = grandchild.AddPrompt();

        added.Should().Contain(deep,
            "constructor-injected callbacks must propagate to grandchild SectionViewModels");
    }
}
