using Microsoft.Extensions.Options;
using NhlGoalLight.Worker.Configuration;
using NhlGoalLight.Worker.Services;
using NhlGoalLight.Worker.State;

// Preview mode: run a single sequence and exit. Useful for tweaking
// colors/timing without needing MTL to actually score.
//   Usage: dotnet run -- --preview            (Habs goal sequence)
//          dotnet run -- --preview opponent   (opponent goal sequence)
var previewIdx = Array.IndexOf(args, "--preview");
if (previewIdx >= 0)
{
    var which = previewIdx + 1 < args.Length ? args[previewIdx + 1] : "habs";
    var previewBuilder = Host.CreateApplicationBuilder(args);
    ConfigureServices(previewBuilder);
    using var previewHost = previewBuilder.Build();
    var sequencer = previewHost.Services.GetRequiredService<LightSequencer>();
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
    if (which.Equals("opponent", StringComparison.OrdinalIgnoreCase))
        await sequencer.PlayOpponentGoalAsync(cts.Token);
    else
        await sequencer.PlayHabsGoalAsync(cts.Token);
    return;
}

var builder = Host.CreateApplicationBuilder(args);
ConfigureServices(builder);
var host = builder.Build();
await host.RunAsync();
return;

static void ConfigureServices(HostApplicationBuilder builder)
{

// Environment variables override appsettings — keeps secrets out of files.
builder.Configuration.AddEnvironmentVariables();

builder.Services
    .AddOptions<NhlOptions>()
    .Bind(builder.Configuration.GetSection("Nhl"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<HueOptions>()
    .Bind(builder.Configuration.GetSection("Hue"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection("App"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// NHL HTTP client with retry/circuit-breaker via Polly v8.
// Decompression enabled so the ~200KB play-by-play comes over the wire as
// ~20KB gzip — much friendlier on poll cadence + cellular setups.
builder.Services.AddHttpClient<NhlClient>((sp, client) =>
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

// Hue local API. No resilience handler — bridge is on LAN, fast, and we
// don't want retries to stack up during a quick flash sequence.
builder.Services.AddHttpClient<HueClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<HueOptions>>().Value;
    client.BaseAddress = new Uri($"http://{opts.BridgeIp}/");
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddSingleton<PlayStateStore>();
builder.Services.AddSingleton<LightSequencer>();
builder.Services.AddHostedService<GoalLightService>();
}
