namespace DiaEditApp.Services;

using Microsoft.Extensions.DependencyInjection;

using DiaEditApp.ViewModels;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiaEditAppServices(this IServiceCollection services)
    {
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        return services;
    }
}