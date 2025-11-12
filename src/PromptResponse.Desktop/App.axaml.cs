using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.Views;

namespace PromptResponse.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = serviceProvider.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<IAprSerializer, AprJsonSerializer>();
        services.AddSingleton<DocumentValidator>();
        services.AddSingleton<DataTypeValidator>();

        // Desktop services
        services.AddSingleton<IFileService, FileService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<FormFillingViewModel>();
    }
}
