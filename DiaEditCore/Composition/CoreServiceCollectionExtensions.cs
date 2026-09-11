namespace DiaEditCore.Composition;

using Microsoft.Extensions.DependencyInjection;

using DiaEditCore.Commands;
using DiaEditCore.Session;

/// <summary>
/// DiaEditCore側のDIコンテナ登録。
/// </summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddDiaEditCore(this IServiceCollection services)
    {
        services.AddSingleton<CommandInvoker>();
        services.AddSingleton<ProjectSession>(); 

        return services;
    }
}
