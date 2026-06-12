using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Services.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GTA5Optimizer.Services.Extensions;

/// <summary>
/// Расширения для DI контейнера
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddGTA5OptimizerServices(this IHostApplicationBuilder builder)
    {
        // Core services
        builder.Services.AddSingleton<ISystemOptimizer, SystemOptimizer>();
        builder.Services.AddSingleton<IGameDetector, GameDetector>();
        builder.Services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
        builder.Services.AddSingleton<IRegistryManager, RegistryManager>();
        builder.Services.AddSingleton<IMemoryManager, MemoryManager>();
        builder.Services.AddSingleton<IProcessManager, ProcessManager>();
        builder.Services.AddSingleton<ILoggerService, LoggerService>();
        builder.Services.AddSingleton<IProfileManager, ProfileManager>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IMajesticAnalyzer, MajesticAnalyzer>();
        builder.Services.AddSingleton<SystemInfoDetector>();

        // Background services
        builder.Services.AddHostedService<AutoOptimizationService>();

        return builder;
    }

    public static IServiceCollection AddGTA5OptimizerServices(this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<ISystemOptimizer, SystemOptimizer>();
        services.AddSingleton<IGameDetector, GameDetector>();
        services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
        services.AddSingleton<IRegistryManager, RegistryManager>();
        services.AddSingleton<IMemoryManager, MemoryManager>();
        services.AddSingleton<IProcessManager, ProcessManager>();
        services.AddSingleton<ILoggerService, LoggerService>();
        services.AddSingleton<IProfileManager, ProfileManager>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IMajesticAnalyzer, MajesticAnalyzer>();
        services.AddSingleton<SystemInfoDetector>();

        // Background services - use IHostedService registration compatible with IServiceCollection
        services.AddSingleton<IHostedService, AutoOptimizationService>();

        return services;
    }
}
