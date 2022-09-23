using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AppToImprove.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using AppToImprove.Util;

namespace AppToImprove.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AggregatorController : ControllerBase
    {
        private readonly int _callAttemptsCount;
        private readonly int _timeoutBetweenCalls;

        private readonly ILogger<AggregatorController> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public AggregatorController(ILogger<AggregatorController> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            this._logger = logger;
            this._httpClient = httpClientFactory.CreateClient("ForecastProvider");
            this._configuration = configuration;

            this._callAttemptsCount = _configuration.GetValue<int?>("WeatherForecastCallAttemptsCount") ?? 3;
            this._timeoutBetweenCalls = _configuration.GetValue<int?>("WeatherForecastCallTimeout") ?? 1000;
        }

        [HttpGet(Name = "GetAggregated")]
        public async Task<ActionResult<AggregatedInfo>> Get()
        {
            // For simplicity of test application it was implemented as neighbor controller.
            // But please, consider it as a call to remote independent service.
            var response = await this._httpClient.GetAsync("WeatherForecast");

            if (response.StatusCode >= HttpStatusCode.InternalServerError)
            {
                this._logger.LogWarning($"Call 1 of {_callAttemptsCount} to remote service failed");

                for (var callNumber = 2; callNumber <= _callAttemptsCount; callNumber++)
                {
                    await Task.Delay(_timeoutBetweenCalls);

                    this._logger.LogDebug($"Trying to make one more call after {_timeoutBetweenCalls} ms delay");
                    response = await this._httpClient.GetAsync("WeatherForecast");

                    if (response.StatusCode < HttpStatusCode.InternalServerError)
                    {
                        break;
                    }

                    this._logger.LogWarning($"Call {callNumber} of {_callAttemptsCount} to remote service failed");
                }

                if (_callAttemptsCount < 2 || response.StatusCode >= HttpStatusCode.InternalServerError)
                {
                    this._logger.LogError($"Remote service currently is unavailable");
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return this.Problem($"Got an error response from server: {responseContent}");
                }
            }

            if (response.StatusCode >= HttpStatusCode.BadRequest)
            {
                this._logger.LogError("Not successful response from server");
                var responseContent = await response.Content.ReadAsStringAsync();
                return this.Problem($"Got an error response from server: {responseContent}");
            }

            var forecastsString = await response.Content.ReadAsStringAsync();
            var forecasts = JsonSerializer.Deserialize<List<WeatherForecast>>(forecastsString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var dates = forecasts.Select(f => f.Date);
            var temperaturesC = forecasts.Select(f => f.TemperatureC).ToArray();
            var minTemperatureC = temperaturesC.Min();
            var avgTemperatureC = (int)temperaturesC.Average();
            var maxTemperatureC = temperaturesC.Max();

            var allSummaryWords = forecasts
                .GroupBy(f => f.Summary)
                .OrderByDescending(g => g.Count())  // Order so most frequent summary goes first.
                .Take(3)
                .Select(g => g.Key);  // Select 3 most frequent.

            this._logger.LogInformation($"Received a bit of forecasts: {await response.Content.ReadAsStringAsync()}");

            return new AggregatedInfo
            {
                PeriodStart = dates.Min(),
                PeriodEnd = dates.Max(),
                ForecastSamples = forecasts.Count(),
                MinTemperatureC = minTemperatureC,
                MinTemperatureF = minTemperatureC.ConvertFromCelsiusToFahrenheit(),
                AvgTemperatureC = avgTemperatureC,
                AvgTemperatureF = avgTemperatureC.ConvertFromCelsiusToFahrenheit(),
                MaxTemperatureC = maxTemperatureC,
                MaxTemperatureF = maxTemperatureC.ConvertFromCelsiusToFahrenheit(),
                SummaryWords = string.Join(" ", allSummaryWords),
            };
        }
    }
}
