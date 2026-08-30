using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PromptResponse.Desktop.Services.Dialogs;

/// <summary>
/// Builds the interactive content for the small, application-owned dialogs.
/// </summary>
internal static class InteractiveDialogContentBuilder
{
    public static Control BuildConfirmation(string message, Action confirm, Action cancel)
    {
        var yesButton = new Button
        {
            Content = "Yes",
            MinWidth = 80,
            Margin = new Thickness(0, 0, 10, 0),
        };
        yesButton.SetValue(AutomationProperties.NameProperty, "Confirm action");
        yesButton.SetValue(AutomationProperties.HelpTextProperty, "Click to confirm and proceed");
        yesButton.Click += (_, _) => confirm();

        var noButton = new Button { Content = "No", MinWidth = 80 };
        noButton.SetValue(AutomationProperties.NameProperty, "Cancel action");
        noButton.SetValue(AutomationProperties.HelpTextProperty, "Click to cancel and go back");
        noButton.Click += (_, _) => cancel();

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 10),
        };
        messageText.SetValue(AutomationProperties.NameProperty, "Confirmation message");
        messageText.SetValue(AutomationProperties.HelpTextProperty, message);

        return new StackPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                messageText,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0),
                    Children = { yesButton, noButton },
                },
            },
        };
    }

    public static Control BuildInput(
        string title,
        string message,
        string defaultValue,
        bool isPassword,
        Action<string> submit,
        Action cancel)
    {
        var label = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        };
        var input = new TextBox
        {
            Text = defaultValue,
            PasswordChar = isPassword ? '•' : '\0',
            MinWidth = 300,
        };
        input.SetValue(AutomationProperties.NameProperty, title);
        input.SetValue(AutomationProperties.HelpTextProperty, message);

        var okButton = new Button { Content = "OK", MinWidth = 80, Margin = new Thickness(0, 0, 10, 0), IsDefault = true };
        okButton.SetValue(AutomationProperties.NameProperty, "Confirm input");
        okButton.Click += (_, _) => submit(input.Text ?? string.Empty);

        var cancelButton = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
        cancelButton.SetValue(AutomationProperties.NameProperty, "Cancel input");
        cancelButton.Click += (_, _) => cancel();

        return new StackPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                label,
                input,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 12, 0, 0),
                    Children = { okButton, cancelButton },
                },
            },
        };
    }

    public static Control BuildChoice(string message, IReadOnlyList<string> choices, Action<int> select, Action cancel)
    {
        var options = new StackPanel { Spacing = 6 };
        for (var index = 0; index < choices.Count; index++)
        {
            var captured = index;
            var option = new RadioButton
            {
                Content = choices[index],
                GroupName = "dialog-choice",
                IsChecked = index == 0,
            };
            option.SetValue(AutomationProperties.NameProperty, choices[index]);
            option.SetValue(AutomationProperties.HelpTextProperty, $"Submission destination {index + 1} of {choices.Count}");
            option.IsCheckedChanged += (_, _) =>
            {
                if (option.IsChecked == true) select(captured);
            };
            options.Children.Add(option);
        }

        var selectButton = new Button { Content = "Continue", MinWidth = 96, IsDefault = true };
        selectButton.SetValue(AutomationProperties.NameProperty, "Continue with selected destination");
        selectButton.Click += (_, _) => select(-1);

        var cancelButton = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true, Margin = new Thickness(10, 0, 0, 0) };
        cancelButton.SetValue(AutomationProperties.NameProperty, "Cancel submission");
        cancelButton.Click += (_, _) => cancel();

        var content = new StackPanel { Margin = new Thickness(20), Spacing = 8 };
        content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new ScrollViewer { Content = options, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        content.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { selectButton, cancelButton },
        });
        return content;
    }
}
