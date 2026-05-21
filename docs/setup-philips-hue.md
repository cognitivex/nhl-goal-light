# Setup: Philips Hue Bridge (Local CLIP API v1)

This is the one-time setup for the **legacy Hue Local CLIP API** — the unversioned `/api/<username>` endpoint that has shipped on every Hue Bridge since 2012. It still works on the current Bridge V2 and on the (now EOL'd) Bridge V1.

> Not to be confused with **Hue API v2** (`/clip/v2/...`, Bridge V2 only, requires HTTPS + application key header). This worker uses v1.

The Hue local API uses a 40-char "username" you have to mint by pressing the bridge's physical button. One-time only.

## 1. Find the bridge IP

```bash
# Cloud discovery returns bridges on your NAT — easiest path.
curl https://discovery.meethue.com
# Or check your router's DHCP table for the Philips device.
```

## 2. Pair (button press)

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

## 3. List your lights to find their IDs

```bash
curl http://<BRIDGE_IP>/api/<HUE_API_USER>/lights | jq 'keys'
# => ["1", "2"]
```

The numeric IDs go in `HUE_LIGHT_1`, `HUE_LIGHT_2`, etc.

## 4. Sanity-check by setting a color manually

```bash
curl -X PUT http://<BRIDGE_IP>/api/<HUE_API_USER>/lights/1/state \
  -H 'Content-Type: application/json' \
  -d '{"on":true,"bri":254,"xy":[0.675,0.322]}'
```

The bulb should flash red. If it does, Hue setup is done.