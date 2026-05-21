using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NhlGoalLight.Hue;

public sealed class HueClient(
    HttpClient http,
    IOptions<HueOptions> options,
    ILogger<HueClient> logger)
{
    private readonly HueOptions _opts = options.Value;

    private static readonly JsonSerializerOptions HueJson = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task SetStateAsync(int lightId, HueLightState state, CancellationToken ct)
    {
        var path = $"api/{_opts.ApiUser}/lights/{lightId}/state";
        try
        {
            using var response = await http.PutAsJsonAsync(path, state, HueJson, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                logger.LogWarning("Hue PUT /lights/{Id}/state failed: {Status} {Body}",
                    lightId, response.StatusCode, body);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Hue PUT /lights/{Id}/state network error.", lightId);
        }
    }

    public Task SetAllAsync(HueLightState state, CancellationToken ct)
        => Task.WhenAll(_opts.LightIds.Select(id => SetStateAsync(id, state, ct)));
}
