using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels;

namespace PromptResponse.Desktop.Views;

public partial class DisplayPreferencesView : UserControl
{
    public DisplayPreferencesView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnResetClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DisplayPreferencesViewModel vm) vm.Reset();
    }

    private void OnPresetExcellentClicked(object? sender, RoutedEventArgs e)   => Apply(ProfilePresets.Preset.ExcellentVision);
    private void OnPresetBlindClicked(object? sender, RoutedEventArgs e)       => Apply(ProfilePresets.Preset.BlindScreenReader);
    private void OnPresetLowVisionClicked(object? sender, RoutedEventArgs e)   => Apply(ProfilePresets.Preset.LowVisionHighContrast);
    private void OnPresetCognitiveClicked(object? sender, RoutedEventArgs e)   => Apply(ProfilePresets.Preset.CognitiveDyslexia);
    private void OnPresetMotorClicked(object? sender, RoutedEventArgs e)       => Apply(ProfilePresets.Preset.MotorMobility);

    private void Apply(ProfilePresets.Preset preset)
    {
        if (DataContext is DisplayPreferencesViewModel vm) vm.ApplyPreset(preset);
    }
}
