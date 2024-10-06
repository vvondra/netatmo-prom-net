using Prometheus;
using System.Text.Json.Nodes;

public class NetatmoCollector : BackgroundService
{
    private readonly NetatmoService _netatmoService;
    private readonly ILogger<NetatmoCollector> _logger;
    private static readonly Gauge Temperature = Metrics.CreateGauge("netatmo_temperature_celsius", "Temperature in Celsius", new GaugeConfiguration
    {
        LabelNames = new[] { "home", "room_id" }
    });

    public NetatmoCollector(NetatmoService netatmoService, ILogger<NetatmoCollector> logger)
    {
        _netatmoService = netatmoService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var homes = await _netatmoService.FetchHomes();
                _logger.LogInformation("Fetched homes data: {HomesData}", homes?.ToJsonString());

                if (homes is JsonArray homesArray)
                {
                    foreach (var home in homesArray)
                    {
                        if (home is JsonObject homeObj)
                        {
                            var homeId = homeObj["id"]?.GetValue<string>();
                            var rooms = homeObj["rooms"] as JsonArray;

                            if (rooms != null)
                            {
                                foreach (var room in rooms)
                                {
                                    if (room is JsonObject roomObj)
                                    {
                                        var roomId = roomObj["id"]?.GetValue<string>();

                                        if (!string.IsNullOrEmpty(roomId))
                                        {
                                            var measurements = await _netatmoService.FetchHome(homeId, roomId);
                                            _logger.LogInformation("Fetched room measurements: {RoomMeasurements}", measurements?.ToJsonString());

                                            if (measurements is JsonArray measurementsArray && measurementsArray.Count > 0)
                                            {
                                                var firstMeasurement = measurementsArray[0] as JsonObject;
                                                if (firstMeasurement != null && firstMeasurement["value"] is JsonArray valueArray)
                                                {
                                                    if (valueArray.Count > 0 && valueArray[0] is JsonArray innerArray && innerArray.Count > 0)
                                                    {
                                                        var temperatureNode = innerArray[0];
                                                        if (temperatureNode != null)
                                                        {
                                                            try
                                                            {
                                                                double temperature = temperatureNode.GetValue<double>();
                                                                Temperature.WithLabels(homeId, roomId).Set(temperature);
                                                                _logger.LogInformation("Updated temperature for home {HomeId}, room {RoomId}: {Temperature}°C", homeId, roomId, temperature);
                                                            }
                                                            catch (InvalidOperationException)
                                                            {
                                                                _logger.LogWarning("Unable to parse temperature value for home {HomeId}, room {RoomId}. Value: {Value}", homeId, roomId, temperatureNode.ToJsonString());
                                                            }
                                                        }
                                                        else
                                                        {
                                                            _logger.LogWarning("Temperature node is null for home {HomeId}, room {RoomId}", homeId, roomId);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        _logger.LogWarning("Unexpected value array structure for home {HomeId}, room {RoomId}", homeId, roomId);
                                                    }
                                                }
                                                else
                                                {
                                                    _logger.LogWarning("Unexpected measurement structure for home {HomeId}, room {RoomId}", homeId, roomId);
                                                }
                                            }
                                            else
                                            {
                                                _logger.LogWarning("No measurements found for home {HomeId}, room {RoomId}", homeId, roomId);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Unexpected homes data structure");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting data");
            }

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}