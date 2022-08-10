using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AppToImprove.Models;
using Microsoft.Extensions.Logging;

namespace AppToImprove.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AggregatorController : ControllerBase
    {

        private readonly ILogger<AggregatorController> _logger;
        private readonly HttpClient _httpClient;

        public AggregatorController(ILogger<AggregatorController> logger, IHttpClientFactory httpClientFactory)
        {
            this._logger = logger;
            this._httpClient = httpClientFactory.CreateClient("ForecastProvider");
        }

        [HttpGet(Name = "GetAggregated")]
        public async Task<ActionResult<AggregatedInfo>> Get()
        {
            // For simplicity of test application it was implemented as neighbor controller.
            // But please, consider it as a call to remote independent service.
            var response = await this._httpClient.GetAsync("WeatherForecast");

            if (response.StatusCode == HttpStatusCode.InternalServerError || response.StatusCode == HttpStatusCode.BadRequest)
            {
                this._logger.LogError("Not successful response from server");
                var responseContent = await response.Content.ReadAsStringAsync();
                return this.Problem($"Got an error response from server: {responseContent}");
            }

            var forecastsString = await response.Content.ReadAsStringAsync();
            var forecasts = JsonSerializer.Deserialize<List<WeatherForecast>>(forecastsString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var dates = forecasts.Select(f => f.Date).OrderBy(d => d).ToArray();
            var temperaturesC = forecasts.Select(f => f.TemperatureC).ToArray();
            var temperaturesF = forecasts.Select(f => f.TemperatureF).ToArray();
            var allSummaryWords = forecasts
                .Select(f => f.Summary)
                .GroupBy(s => s)
                .OrderByDescending(g => g.Count())  // Order so most frequent summary goes first.
                .ToArray()[..3].Select(g => g.Key);  // Select 3 most frequent.
            
            this._logger.LogInformation($"Received a bit of forecasts: {response.Content.ReadAsStringAsync()}");

            return new AggregatedInfo
            {
                PeriodStart = dates.First(),
                PeriodEnd = dates.Last(),
                ForecastSamples = forecasts.Count(),
                MinTemperatureC = temperaturesC.Min(),
                MinTemperatureF = temperaturesF.Min(),
                AvgTemperatureC = (int)temperaturesC.Average(),
                AvgTemperatureF = (int)temperaturesF.Average(),
                MaxTemperatureC = temperaturesC.Max(),
                MaxTemperatureF = temperaturesF.Max(),
                SummaryWords = string.Join(" ", allSummaryWords),
            };
        }
    }
}
