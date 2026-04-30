using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.Views.Prompts;

public partial class BooleanPromptView : UserControl
{
    public BooleanPromptView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        var yes = this.FindControl<RadioButton>("YesRadio");
        var no = this.FindControl<RadioButton>("NoRadio");
        if (yes is null || no is null) return;

        if (DataContext is BooleanPromptViewModel vm)
        {
            // Sync radio state with VM (initial + on profile/response changes).
            void Sync()
            {
                yes.IsChecked = vm.IsTrue == true;
                no.IsChecked = vm.IsTrue == false;
            }
            Sync();
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(BooleanPromptViewModel.IsTrue) or nameof(BooleanPromptViewModel.Response))
                {
                    Sync();
                }
            };
            yes.IsCheckedChanged += (_, _) =>
            {
                if (yes.IsChecked == true) vm.IsTrue = true;
            };
            no.IsCheckedChanged += (_, _) =>
            {
                if (no.IsChecked == true) vm.IsTrue = false;
            };
        }
    }
}
