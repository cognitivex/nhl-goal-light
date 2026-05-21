# NHL Goal Light

Goal light for your smart bulbs. Pick any NHL team, configure the celebration.

A self-hosted .NET 9 worker that watches the NHL data feed and runs a lighting sequence on your smart bulbs when your team scores. Runs in Docker on anything that runs Docker — Raspberry Pi, NAS, old laptop, whatever.

## Supported integrations

| Component | Status |
|---|---|
| NHL data (api-web.nhle.com) | ✅ |
| Philips Hue (local CLIP API) | ✅ |
| LIFX | Planned |
| WLED / ESPHome | Planned |
| Govee | Planned |

Any NHL team works today via config. Set `Nhl__TeamAbbrev` to your team's abbreviation and the worker loads the matching preset from `celebrations.json` (colors, team name, chant). Several teams are bundled out of the box; add or edit your own without recompiling. See [Customizing the celebration](#customizing-the-celebration).

## How it works

1. Once a day (or after a restart), queries the NHL schedule for your team's next game.
2. Sleeps until ~5 minutes before puck-drop.
3. Polls `/gamecenter/{gameId}/landing` (the lightweight one) every ~10 seconds with conditional `If-None-Match` — most polls return a ~200-byte `304 Not Modified` instead of a 16 KB body.
4. When `homeTeam.score` or `awayTeam.score` changes, fires the matching sequence on your bulbs. Your team → celebration; opposing team → a deliberately uninteresting beige flash (if enabled).
5. Score baseline persists to `./data/play-state.json` so a container restart mid-game doesn't replay or miss goals.

Goal reversals (on-ice review overturning a goal) are handled — if the score goes back down, we silently rebaseline instead of firing.

## Prerequisites

- Docker + docker-compose
- A smart-light system on your LAN. Today that means **Philips Hue** with a bridge (any generation, including V1) and at least one color bulb.
- A static / DHCP-reserved IP for the Hue bridge so it doesn't change after a router reboot.

## One-time Hue setup

Pair with the bridge and mint a local API user — see [docs/setup-philips-hue.md](docs/setup-philips-hue.md) for the **Philips Hue Bridge (Local CLIP API v1)** walkthrough.

## Configure & run

```bash
cp .env.example .env
# Edit .env: bridge IP, API user, light IDs, your contact in NHL_USER_AGENT.

# Set your team in docker-compose.yml under environment:
#   - Nhl__TeamAbbrev=MTL   (TOR, BOS, EDM, NYR, etc.)

docker compose up -d
docker compose logs -f
```

Healthy startup:

```
GoalLightService started. Watching MTL.
MTL game 2025020123 at 2026-01-15 00:00:00Z. Sleeping 03:42:11.
```

When your team scores:

```
Goal: MTL 1→2.
🚨 GOAAAAAL! Canadiens scored — flashing signal, then team colors.
```

The container restarts automatically on system reboot (`restart: unless-stopped` in `docker-compose.yml`) as long as Docker itself auto-starts.

## Configuration reference

All values can be set via `appsettings.json` or env vars (`Section__Key=value`).

| Setting | Default | Notes |
|---|---|---|
| `Nhl:TeamAbbrev` | `MTL` | NHL team abbreviation (3 letters max). |
| `Nhl:PollIntervalMs` | `10000` | Time between landing polls. Below ~5s is rude and pointless — CDN cache is 15-20s. |
| `Nhl:PreGameLeadMinutes` | `5` | How many minutes before puck-drop to start polling. |
| `Nhl:UserAgent` | `NhlGoalLight/1.0` | Sent on every NHL request. **Set this to identify yourself** so the NHL ops team can contact you if your polling causes problems. |
| `Hue:BridgeIp` | _required_ | LAN IP of the bridge. |
| `Hue:ApiUser` | _required_ | The 40-char username from pairing. |
| `Hue:LightIds` | _required_ | Array of light IDs to control. |
| `Hue:FlashIntervalMs` | `400` | Per-color hold during the flash phases. |
| `Hue:HoldFinalColorMs` | `15000` | How long to hold the final solid color before restoring. |
| `App:EnableOpponentGoalDimRed` | `false` | Flash an uninteresting beige when the opponent scores. Name is misleading — beige, not red. |
| `App:RestoreAfterSequence` | `null` | Bulb state to restore to after a sequence. Null = turn off. |
| `App:DataDirectory` | `./data` | Where `play-state.json` is written. |

Per-team celebration colors and chants live in `celebrations.json` — see [Customizing the celebration](#customizing-the-celebration) below.

To restore to warm white at moderate brightness after a celebration:

```json
"App": {
  "RestoreAfterSequence": { "On": true, "Brightness": 200, "ColorTemp": 366 }
}
```

## Customizing the celebration

The goal sequence is:

1. **5 seconds** flashing `GoalSignalColor` (default green) — signals the score change before team colors start.
2. **5 seconds** rotating through `Colors` — the array can be 1+ entries; the worker just cycles.
3. **`Hue:HoldFinalColorMs`** holding `Colors[0]` solid.
4. Fade to your `App:RestoreAfterSequence` state (or off).

### celebrations.json

Per-team presets live in `src/NhlGoalLight.Worker/celebrations.json`, bundled into the image. Keys are NHL team abbreviations; entries are picked by matching `Nhl:TeamAbbrev`. **All 32 NHL teams are included.** If the configured team has no entry (or the abbrev is wrong), `Default` is used.

```json
{
  "Celebrations": {
    "TOR": {
      "TeamName": "Maple Leafs",
      "Colors": ["#00205B", "#FFFFFF"],
      "GoalSignalColor": "#00FF00",
      "GoalChant": "🚨 GO LEAFS GO!"
    }
  }
}
```

### Adding your team

Two options:

- **Edit `celebrations.json` in the repo** and rebuild the image.
- **Mount your own file** at `/app/celebrations.json` in `docker-compose.yml` (commented mount is provided). Hot-reloaded on file change — no container restart needed.

### How colors render

Colors are converted to Hue's hue/saturation internally (not CIE xy) so the bulb does its own gamut mapping — looks correct on gamut-B, gamut-C, and white-ambiance bulbs without per-hardware tweaking. Pure whites and grays use the D65 xy white point automatically.

> ⚠️ **Avoid `#000000`** in `Colors` — pure black has no hue, so the converter falls back to white (the same code path as `#FFFFFF`). Bundled teams whose brand pairs a primary color with black (Bruins, Sens, Penguins, etc.) just drop the black and rotate the remaining colors.

The phase durations are still in code (`src/NhlGoalLight.Hue/HueGoalLight.cs`); the per-flash cadence respects `Hue:FlashIntervalMs`.

## Preview the lights

Test sequences without waiting for a goal:

```bash
dotnet run --project src/NhlGoalLight.Worker -- --preview              # your-team goal sequence
dotnet run --project src/NhlGoalLight.Worker -- --preview opponent     # opponent sequence (no-op unless EnableOpponentGoalDimRed=true)
```

This runs once and exits — useful for tweaking colors, timing, and `RestoreAfterSequence` interactively. Still needs valid `Hue:*` config since it talks to your real bridge.

## Caveats

- **Detection latency**: the NHL CDN caches the landing response for 15-20 seconds, so worst-case time from puck-crosses-line to bulb-flash is ~25-35s including TV streaming delay. The TV broadcast itself is 5-15s behind real-time on most distribution paths, so you'll often see the light flash *with* or *before* the broadcast celebration. Sub-2-second reactions like the Budweiser Red Light require a paid push feed; this is the public-API ceiling.
- **Hue Bridge V1** receives no updates from Signify (EOL'd April 2020). Local API still works, but you're building on abandoned hardware. Bulbs migrate to a V2 bridge cleanly if it dies.
- **Mid-game starts**: if the worker starts while a game is in progress, it baselines at the *current* score — it won't retroactively celebrate goals that happened before it was watching.
- **`InvariantGlobalization=true`** is set in the csproj — saves ~30 MB on the runtime image but means we can't use ICU-based features. Time zone lookups still work because .NET reads `/usr/share/zoneinfo` directly (provided by the `tzdata` package in the Dockerfile).

## Project layout

```
NhlGoalLight.sln
docker-compose.yml
.dockerignore
.env.example
data/                                  Runtime state (volume mount target)
docs/setup-philips-hue.md
src/NhlGoalLight.Worker/celebrations.json  Per-team color/chant presets (mountable)
src/
├── NhlGoalLight.Abstractions/         IGoalLight contract + AppOptions + CelebrationOptions
├── NhlGoalLight.Nhl/                  Schedule + landing HTTP, ETag conditionals, NhlOptions
├── NhlGoalLight.Hue/                  Philips Hue (Local CLIP API v1) integration
│   ├── HueClient.cs                   Bridge HTTP
│   ├── HueLightState.cs               Color payload (Beige + Off constants)
│   ├── HueColor.cs                    Hex → HueLightState conversion
│   ├── HueGoalLight.cs                IGoalLight implementation (celebration choreography)
│   └── HueOptions.cs
└── NhlGoalLight.Worker/               Composition root
    ├── Program.cs                     Host + DI wiring, --preview entry point
    ├── Services/GoalLightService.cs   BackgroundService orchestrator (depends on IGoalLight)
    ├── State/PlayStateStore.cs        Score baseline persistence
    ├── appsettings.json
    └── Dockerfile                     .NET runtime alpine + tzdata
```

Adding a new bulb vendor = new `NhlGoalLight.<Vendor>` project that references `NhlGoalLight.Abstractions` and registers an `IGoalLight`.

## License

TBD.
