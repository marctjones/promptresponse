using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views;
using PromptResponse.Desktop.Views.Prompts;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Tests that replicate the LIVE app's DI + view-construction flow without shortcuts.
/// Existing E2E tests built fixtures that shared a single <see cref="IProfileService"/>
/// instance from the start of each test — that masked any DI-scoping or view-construction
/// timing bug, because by the time the test ran, the prompt VM and Display Preferences
/// VM already pointed at the same object.
///
/// These tests instead:
///   1. Build a real <see cref="IServiceProvider"/> the way <c>App.axaml.cs</c> does
///   2. Resolve <see cref="IProfileService"/> from DI in two different consumer paths
///   3. Verify they share state (the singleton-ness of the service)
///   4. Drive the Display Preferences VM separately from the form filling VM
///   5. Confirm flag mutations propagate to the form view's input behavior
/// </summary>
public class LiveAppFlowTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static IServiceProvider BuildContainer()
    {
        // Mirror App.axaml.cs's DI configuration so we test what production wires.
        var services = new ServiceCollection();
        services.AddSingleton<IOsAccessibilityProbe, FixedProbe>();
        // Test-mode constructor — no affordance defaults so tests assume "no flags
        // active" until they explicitly toggle one. Production wires the default
        // sighted-user defaults via the parameterless constructor.
        services.AddSingleton<IProfileService>(sp =>
            new ProfileService(sp.GetRequiredService<IOsAccessibilityProbe>(), applyAffordanceDefaults: false));
        services.AddTransient<DisplayPreferencesViewModel>();
        return services.BuildServiceProvider();
    }

    [AvaloniaFact]
    public void DI_ProfileService_IsSingleton_AcrossConsumers()
    {
        var sp = BuildContainer();
        var a = sp.GetRequiredService<IProfileService>();
        var b = sp.GetRequiredService<IProfileService>();
        ReferenceEquals(a, b).Should().BeTrue(
            "IProfileService is registered as singleton; flag mutations on one consumer must reach all others");
    }

    [AvaloniaFact]
    public void TogglingPhoneMaskFlagInDisplayPreferences_ReachesFormPhoneFieldAcrossDIScope()
    {
        // Simulates the live flow: open the form (one DI resolution), THEN open Display
        // Preferences as a separate dialog (another DI resolution), toggle a flag there,
        // and verify the form's phone field starts reshaping mid-typing.
        var sp = BuildContainer();
        var sharedService = sp.GetRequiredService<IProfileService>();

        // ─── form side ───
        var phoneVm = new PhonePromptViewModel(
            new Prompt { Id = "p1", Label = "Phone", Hints = new PromptHints { ExpectedDataType = "phone" } },
            sharedService);
        var phoneView = new PhonePromptView { DataContext = phoneVm };
        var phoneWindow = phoneView.ShowInWindow(width: 600, height: 200);
        var phoneTextBox = phoneView.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        phoneTextBox.Focus();
        GuiTestExtensions.PumpDispatcher();

        // BEFORE: typing should pass digits through raw — universal core.
        phoneWindow.TypeText("555");
        phoneTextBox.Text.Should().Be("555",
            "without the PhoneInputMask flag the form must pass raw digits through");

        // ─── separate Display Preferences VM (different transient instance, same singleton) ───
        var prefsVm = sp.GetRequiredService<DisplayPreferencesViewModel>();
        prefsVm.PhoneInputMask = true;

        // AFTER: continue typing — the mask should now reshape from this point.
        phoneTextBox.Text = string.Empty; // clear
        phoneTextBox.Focus();
        GuiTestExtensions.PumpDispatcher();
        phoneWindow.TypeText("5551234567");

        phoneTextBox.Text.Should().Be("(555) 123-4567",
            "toggling the flag in Display Preferences must propagate via the singleton " +
            "service to the form's PhoneInputMaskBehavior — if this fails, that's the live bug");
    }

    [AvaloniaFact]
    public void PhoneMask_ThroughSectionViewItemsControl_StillReshapesOnTyping()
    {
        // The live app does NOT instantiate PhonePromptView directly the way the
        // test fixtures do — it uses an ItemsControl + PromptDataTemplateSelector that
        // builds the view dynamically and sets DataContext via the binding system.
        // This test mirrors that exactly: build a SectionView whose ItemsControl
        // renders a PhonePromptViewModel through the selector, then type into the
        // resulting TextBox and verify reshape. If the test fixture's "new view +
        // initializer DataContext" shortcut was masking the real bug, this fails.
        var sp = BuildContainer();
        var service = sp.GetRequiredService<IProfileService>();
        var prefs = sp.GetRequiredService<DisplayPreferencesViewModel>();
        prefs.PhoneInputMask = true;

        var factory = new PromptViewModelFactory(service);
        var prompt = new Prompt
        {
            Id = "p1",
            Label = "Phone",
            Hints = new PromptHints { ExpectedDataType = "phone" },
        };
        var section = new SectionViewModel(
            new Section { Id = "s1", Title = "Contact", Prompts = new List<Prompt> { prompt } },
            factory,
            depth: 0);

        // First — directly verify the selector works on its own.
        var selector = new PromptDataTemplateSelector();
        var phoneVm = section.PromptViewModels[0];
        ((Avalonia.Controls.Templates.IDataTemplate)selector).Match(phoneVm).Should().BeTrue(
            "selector must accept PhonePromptViewModel");
        var built = ((Avalonia.Controls.Templates.IDataTemplate)selector).Build(phoneVm);
        built.Should().BeOfType<PhonePromptView>(
            "selector must build PhonePromptView for PhonePromptViewModel");

        // Now render through the live SectionView XAML.
        var sectionView = new SectionView { DataContext = section };
        var window = sectionView.ShowInWindow(width: 700, height: 600);

        for (var i = 0; i < 5; i++)
        {
            window.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        // Enumerate every descendant — types AND any TextBoxes by name.
        var allDescendants = sectionView.GetVisualDescendants().ToList();
        var allTypes = string.Join(", ", allDescendants.Select(v => v.GetType().Name).Distinct().Take(30));
        var allTextBoxes = string.Join(", ", allDescendants.OfType<TextBox>().Select(t => t.Name ?? "(unnamed)"));
        var phoneViews = allDescendants.OfType<PhonePromptView>().Count();
        var itemsControls = allDescendants.OfType<ItemsControl>().Count();
        var contentPresenters = allDescendants.OfType<Avalonia.Controls.Presenters.ContentPresenter>().Count();

        // Inspect each ItemsControl's bindings — find the prompts one and check ItemTemplate.
        var promptsItemsControl = allDescendants.OfType<ItemsControl>()
            .FirstOrDefault(ic => ic.ItemsSource is IEnumerable<PromptViewModelBase>);
        var itemsSourceCount = (promptsItemsControl?.ItemsSource as IEnumerable<PromptViewModelBase>)?.Count() ?? -1;
        var itemTemplateType = promptsItemsControl?.ItemTemplate?.GetType().FullName ?? "(null)";

        var textBox = sectionView.TryFindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        textBox.Should().NotBeNull(
            $"PhonePromptViews: {phoneViews}, ItemsControls: {itemsControls}, " +
            $"ContentPresenters: {contentPresenters}, TextBoxes: [{allTextBoxes}], Types: {allTypes}, " +
            $"prompts ItemsControl found: {promptsItemsControl != null}, " +
            $"items count: {itemsSourceCount}, ItemTemplate: {itemTemplateType}");

        textBox!.Focus();
        GuiTestExtensions.PumpDispatcher();
        window.TypeText("5551234567");

        textBox.Text.Should().Be("(555) 123-4567",
            "the live ItemsControl + PromptDataTemplateSelector path must wire the InputMask " +
            "behavior through DataContextChanged exactly like the simpler test fixtures do");
    }

    [AvaloniaFact]
    public void Probe_BareItemsControl_WithSelector_MaterializesPhoneView()
    {
        // Strip away SectionView completely — just an ItemsControl with the selector and
        // one PhonePromptViewModel. If this fails, the selector + ItemsControl pairing
        // itself is broken.
        var sp = BuildContainer();
        var service = sp.GetRequiredService<IProfileService>();

        var phoneVm = new PhonePromptViewModel(
            new Prompt { Id = "p1", Label = "Phone", Hints = new PromptHints { ExpectedDataType = "phone" } },
            service);

        var ic = new ItemsControl
        {
            ItemsSource = new[] { phoneVm },
            ItemTemplate = new PromptDataTemplateSelector(),
        };
        var window = ic.ShowInWindow(width: 700, height: 400);
        for (var i = 0; i < 5; i++) { window.UpdateLayout(); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }

        var phoneView = ic.GetVisualDescendants().OfType<PhonePromptView>().FirstOrDefault();
        var allTypes = string.Join(", ", ic.GetVisualDescendants().Select(v => v.GetType().Name).Distinct());
        phoneView.Should().NotBeNull(
            $"bare ItemsControl + PromptDataTemplateSelector must materialize PhonePromptView. " +
            $"Descendants: {allTypes}");
    }

    [AvaloniaFact]
    public void CalendarPickerVisibility_RespondsToDisplayPreferencesToggle_AcrossDIScope()
    {
        var sp = BuildContainer();
        var sharedService = sp.GetRequiredService<IProfileService>();

        var dateVm = new DatePromptViewModel(
            new Prompt { Id = "d1", Label = "DOB", Hints = new PromptHints { ExpectedDataType = "date" } },
            sharedService);
        var dateView = new DatePromptView { DataContext = dateVm };
        dateView.ShowInWindow(width: 600, height: 200);
        var picker = dateView.FindDescendant<CalendarDatePicker>(p => p.Name == "DatePicker");

        picker.IsVisible.Should().BeFalse("universal core hides the picker by default");

        var prefsVm = sp.GetRequiredService<DisplayPreferencesViewModel>();
        prefsVm.CalendarPicker = true;
        GuiTestExtensions.PumpDispatcher();

        picker.IsVisible.Should().BeTrue(
            "flipping the flag via DI-resolved DisplayPreferences VM must propagate to the form");
    }
}
