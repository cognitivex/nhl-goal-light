using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace NhlGoalLight.Nhl;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNhlClient(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<NhlOptions>()
            .Bind(config.GetSection("Nhl"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // NHL HTTP client with retry/circuit-breaker via Polly v8.
        // Decompression enabled so the ~200KB play-by-play comes over the wire as
        // ~20KB gzip — much friendlier on poll cadence + cellular setups.
        services.AddHttpClient<NhlClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<NhlOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(opts.UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        })
        .AddStandardResilienceHandler();

        return services;
    }
}
