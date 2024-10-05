using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

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
        // ... (keep the existing RefreshToken logic)
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