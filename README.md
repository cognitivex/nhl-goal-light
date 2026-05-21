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

Any NHL team works today via config — but the celebration colors are currently the Montréal Canadiens' bleu-blanc-rouge. Re-skinning for your team is a small code edit; see [Customizing the celebration](#customizing-the-celebration).

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

The Hue local API uses a 40-char "username" you have to mint by pressing the bridge's physical button. One-time only.

### 1. Find the bridge IP

```bash
# Cloud discovery returns bridges on your NAT — easiest path.
curl https://discovery.meethue.com
# Or check your router's DHCP table for the Philips device.
```

### 2. Pair (button press)

Walk to the bridge, press the round link button on top. You have ~30 seconds. Then:

```bash
curl -X POST http://<BRIDGE_IP>/api \
  -H 'Content-Type: application/json' \
  -d '{"devicetype":"nhl-goal-light#worker"}'
```

Response on success:

```json
[{"success":{"username":"abc123...40chars"}}]
```

Save that string — it's your `HUE_API_USER`.

### 3. List your lights to find their IDs

```bash
curl http://<BRIDGE_IP>/api/<HUE_API_USER>/lights | jq 'keys'
# => ["1", "2"]
```

The numeric IDs go in `HUE_LIGHT_1`, `HUE_LIGHT_2`, etc.

### 4. Sanity-check by setting a color manually

```bash
curl -X PUT http://<BRIDGE_IP>/api/<HUE_API_USER>/lights/1/state \
  -H 'Content-Type: application/json' \
  -d '{"on":true,"bri":254,"xy":[0.675,0.322]}'
```

The bulb should flash red. If it does, Hue setup is done.

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
🚨 ET LE BUT! Flashing green, then rotating bleu-blanc-rouge.
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

To restore to warm white at moderate brightness after a celebration:

```json
"App": {
  "RestoreAfterSequence": { "On": true, "Brightness": 200, "ColorTemp": 366 }
}
```

## Customizing the celebration

The current goal sequence is:

1. **5 seconds** flashing green (signals the score change before colors start).
2. **5 seconds** rotating red → white → blue (Habs colors).
3. **`HoldFinalColorMs`** holding solid red.
4. Fade to your `RestoreAfterSequence` state (or off).

To change colors for your team, edit `Services/HueClient.cs` — the `HabsRed`, `HabsWhite`, `HabsBlue` static states are CIE xy color coordinates. A few options:

- Use the Hue Color Picker (e.g., [https://hueapi.colorhexa.com](https://hueapi.colorhexa.com)) to get xy values for your hex codes.
- Or switch to `Hue`/`Sat` instead of `Xy` to let the bulb do gamut mapping — the existing `GoalGreen` is an example (`Hue=25500, Sat=254`).

The phase durations live in `Services/LightSequencer.cs`. The cadence inside each phase respects `Hue:FlashIntervalMs`.

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
NhlGoalLight.sln                     Solution file
docker-compose.yml
.env.example
data/                                Runtime state (volume mount target)
src/
└── NhlGoalLight.Worker/
    ├── Program.cs                   Host + DI wiring, --preview entry point
    ├── Configuration/Options.cs     NhlOptions, HueOptions, AppOptions
    ├── Models/NhlModels.cs          Landing + Schedule API DTOs
    ├── Services/
    │   ├── NhlClient.cs             Schedule + landing HTTP w/ ETag conditionals
    │   ├── HueClient.cs             Hue local CLIP API + team colors
    │   ├── LightSequencer.cs        Goal celebration choreography
    │   └── GoalLightService.cs      BackgroundService orchestrator
    ├── State/PlayStateStore.cs      Score baseline persistence
    ├── appsettings.json
    └── Dockerfile                   .NET runtime alpine + tzdata
```

## License

TBD.
