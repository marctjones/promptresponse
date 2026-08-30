using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// Shared fixture for structural editing tests. Individual state invariants live
/// in focused partial files so regressions are easier to locate.
/// </summary>
public partial class EditorMutationTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static IProfileService NewService() => new ProfileService(new FixedProbe(), applyAffordanceDefaults: false);
    private static PromptViewModelFactory NewFactory() => new(NewService());

    private static (SectionViewModel vm, Section model, List<PromptViewModelBase> added, List<PromptViewModelBase> removed) NewBlankSection()
    {
        var added = new List<PromptViewModelBase>();
        var removed = new List<PromptViewModelBase>();
        var section = new Section { Id = "s1", Title = "S", Prompts = new List<Prompt>() };
        var vm = new SectionViewModel(section, NewFactory(), depth: 0,
            onPromptAdded: added.Add, onPromptRemoved: removed.Add);
        return (vm, section, added, removed);
    }

}
