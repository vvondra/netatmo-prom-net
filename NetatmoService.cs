using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

public class NetatmoService
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NetatmoService> _logger;
    private string _authToken;
    private string _refreshToken;
    private DateTime _tokenExpirationTime;

    public NetatmoService(IHttpClientFactory clientFactory, IConfiguration configuration, ILogger<NetatmoService> logger)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
        _logger = logger;
        _authToken = _configuration["NETATMO_AUTH_TOKEN"];
        _refreshToken = _configuration["NETATMO_REFRESH_TOKEN"];
        _tokenExpirationTime = DateTime.UtcNow.AddHours(2); // Assume initial token is valid for 2 hours
    }

    private async Task EnsureValidToken()
    {
        if (DateTime.UtcNow >= _tokenExpirationTime)
        {
            await RefreshToken();
        }
    }
    private async Task RefreshToken()
    {
        try
        {
            var client = _clientFactory.CreateClient();
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", _refreshToken),
                new KeyValuePair<string, string>("client_id", _configuration["NETATMO_CLIENT_ID"]),
                new KeyValuePair<string, string>("client_secret", _configuration["NETATMO_CLIENT_SECRET"])
            });

            var response = await client.PostAsync("https://api.netatmo.com/oauth2/token", content);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<JsonNode>(responseContent);

            _authToken = tokenData["access_token"].GetValue<string>();
            _refreshToken = tokenData["refresh_token"].GetValue<string>();
            _tokenExpirationTime = DateTime.UtcNow.AddSeconds(tokenData["expires_in"].GetValue<int>());

            _logger.LogInformation("Token refreshed successfully. New expiration: {ExpirationTime}", _tokenExpirationTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh token");
            throw;
        }
    }

    public async Task<JsonNode> FetchHomes()
    {
        await EnsureValidToken();
        var client = _clientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);

        var response = await client.GetAsync("https://api.netatmo.com/api/homesdata");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("Raw homes data: {RawData}", content);
        var json = JsonNode.Parse(content);
        return json["body"]["homes"];
    }

    public async Task<JsonNode> FetchHome(string homeId, string roomId)
    {
        await EnsureValidToken();
        var client = _clientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);

        var response = await client.GetAsync($"https://api.netatmo.com/api/getroommeasure?home_id={homeId}&room_id={roomId}&scale=30min&type=temperature&limit=1");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("Raw room data: {RawData}", content);
        var json = JsonNode.Parse(content);
        return json["body"];
    }
}