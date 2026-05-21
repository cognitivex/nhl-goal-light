using NhlGoalLight.Abstractions;
using NhlGoalLight.Hue;
using NhlGoalLight.Nhl;
using NhlGoalLight.Worker.Services;
using NhlGoalLight.Worker.State;

// Preview mode: run a single sequence and exit. Useful for tweaking
// colors/timing without needing MTL to actually score.
//   Usage: dotnet run -- --preview            (your-team goal sequence)
//          dotnet run -- --preview opponent   (opponent goal sequence)
var previewIdx = Array.IndexOf(args, "--preview");
if (previewIdx >= 0)
{
    var which = previewIdx + 1 < args.Length ? args[previewIdx + 1] : "ours";
    var previewBuilder = Host.CreateApplicationBuilder(args);
    ConfigureServices(previewBuilder);
    using var previewHost = previewBuilder.Build();
    var goalLight = previewHost.Services.GetRequiredService<IGoalLight>();
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
    if (which.Equals("opponent", StringComparison.OrdinalIgnoreCase))
        await goalLight.PlayOpponentGoalAsync(cts.Token);
    else
        await goalLight.PlayOurGoalAsync(cts.Token);
    return;
}

var builder = Host.CreateApplicationBuilder(args);
ConfigureServices(builder);
var host = builder.Build();
await host.RunAsync();
return;

static void ConfigureServices(HostApplicationBuilder builder)
{
    // Team-preset library: dictionary of celebrations keyed by team abbreviation.
    // Bundled in the image; users can mount their own at /app/celebrations.json
    // to override. reloadOnChange picks up edits without restarting.
    builder.Configuration.AddJsonFile("celebrations.json", optional: false, reloadOnChange: true);

    // Environment variables override appsettings — keeps secrets out of files.
    builder.Configuration.AddEnvironmentVariables();

    builder.Services
        .AddOptions<AppOptions>()
        .Bind(builder.Configuration.GetSection("App"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // Resolve the active celebration: Celebrations:{TeamAbbrev}, falling back
    // to Celebrations:Default if the team has no preset.
    var teamAbbrev = builder.Configuration["Nhl:TeamAbbrev"] ?? "MTL";
    var celebrationSection = builder.Configuration.GetSection($"Celebrations:{teamAbbrev}");
    if (!celebrationSection.Exists())
        celebrationSection = builder.Configuration.GetSection("Celebrations:Default");

    builder.Services
        .AddOptions<CelebrationOptions>()
        .Bind(celebrationSection)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddNhlClient(builder.Configuration);
    builder.Services.AddHueGoalLight(builder.Configuration);

    builder.Services.AddSingleton<PlayStateStore>();
    builder.Services.AddHostedService<GoalLightService>();
}
