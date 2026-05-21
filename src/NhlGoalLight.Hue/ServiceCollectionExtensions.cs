using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NhlGoalLight.Abstractions;

namespace NhlGoalLight.Hue;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHueGoalLight(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<HueOptions>()
            .Bind(config.GetSection("Hue"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Hue local API. No resilience handler — bridge is on LAN, fast, and we
        // don't want retries to stack up during a quick flash sequence.
        services.AddHttpClient<HueClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<HueOptions>>().Value;
            client.BaseAddress = new Uri($"http://{opts.BridgeIp}/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddSingleton<IGoalLight, HueGoalLight>();
        return services;
    }
}
