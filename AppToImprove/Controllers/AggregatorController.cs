using AppToImprove.Configuration;
using AppToImprove.Engine;
using AppToImprove.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AppToImprove.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AggregatorController : ControllerBase
    {
        private static readonly object _remoteApiCacheKey = $"{nameof(AggregatorController)}::{nameof(QueryRemoteService)}";
        private const int _cacheTimeoutSeconds = 3;
        private static readonly TimeSpan _cacheTimeout = TimeSpan.FromMilliseconds(_cacheTimeoutSeconds);

        private readonly ILogger<AggregatorController> _logger;
        private readonly IWeatherForecastService _weatherForecastService;
        private readonly IMemoryCache _memoryCache;
        private readonly bool _isDevelopment;

        public AggregatorController(
            ILogger<AggregatorController> logger!!,
            IWeatherForecastService weatherForecastService!!,
            IMemoryCache memoryCache!!,
            IWebHostEnvironment env!!)
        {
            _logger = logger;
            _weatherForecastService = weatherForecastService;
            _memoryCache = memoryCache;
            _isDevelopment = env.IsDevelopment();
        }

        [HttpGet(Name = ConfigurationConstants.Routes.AggregatorIndex)]
        [ResponseCache(Duration = _cacheTimeoutSeconds, Location = ResponseCacheLocation.Client)]
        public async Task<ActionResult<AggregatedInfo>> Get(CancellationToken cancellationToken)
        {
            var (problem, result) = await QueryRemoteService(
                _remoteApiCacheKey,
                async () =>
                {
                    AggregatedInfo? result;

                    using (_logger.BeginScope("Fetching weather forecasts."))
                    {
                        var sw = Stopwatch.StartNew();

                        var forecasts = _weatherForecastService.GetWeatherForecast(cancellationToken);
                        result = await Aggregate(forecasts, cancellationToken);

                        var count = result?.ForecastSamples.ToString() ?? "no";
                        _logger.LogInformation($"Fetched {count} samples from the weather forecast service in {sw.ElapsedMilliseconds} ms.");
                    }

                    return result;
                });

            if (problem is not null)
                return problem;

            if (result == null)
                return Problem(
                    "No data available.",
                    statusCode: StatusCodes.Status204NoContent);

            return result;
        }

        private static async Task<AggregatedInfo?> Aggregate(
            IAsyncEnumerable<WeatherForecast> forecasts,
            CancellationToken cancellationToken)
        {
            int count = 0;
            DateTime dateMin = DateTime.MaxValue, dateMax = DateTime.MinValue;
            int tempMin = int.MaxValue, tempMax = int.MinValue;
            double tempAvg = 0;
            Dictionary<string, int> summaryCounter = new(StringComparer.InvariantCulture);

            await foreach (var forecast in forecasts.WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var date = forecast.Date;
                if (date == null)
                    continue;
                if (forecast.Date < dateMin)
                    dateMin = date.Value;
                if (forecast.Date > dateMax)
                    dateMax = date.Value;

                var temp = forecast.TemperatureC;
                if (temp == null)
                    continue;
                if (forecast.TemperatureC < tempMin)
                    tempMin = temp.Value;
                if (forecast.TemperatureC > tempMax)
                    tempMax = temp.Value;

                tempAvg = ((tempAvg * count) + temp.Value) / (count + 1);

                if (!string.IsNullOrEmpty(forecast.Summary))
                {
                    if (!summaryCounter.TryGetValue(forecast.Summary, out var summaryValue))
                        summaryValue = 0;
                    summaryCounter[forecast.Summary] = summaryValue + 1;
                }

                count++;
            }

            if (count == 0)
                return null;

            var summaryResultEnumerable = summaryCounter
                .OrderByDescending(x => x.Value)
                .Select((v, i) => (v, i))
                .TakeWhile(x => x.i < 3)
                .Select(x => x.v.Key);
            var summaryResult = string.Join(' ', summaryResultEnumerable);

            return new AggregatedInfo
            {
                PeriodStart = dateMin,
                PeriodEnd = dateMax,
                ForecastSamples = count,
                MinTemperatureC = tempMin,
                MinTemperatureF = (int)UnitConverter.TemperatureCelciusToFarenheit(tempMin),
                AvgTemperatureC = (int)tempAvg,
                AvgTemperatureF = (int)UnitConverter.TemperatureCelciusToFarenheit(tempAvg),
                MaxTemperatureC = tempMax,
                MaxTemperatureF = (int)UnitConverter.TemperatureCelciusToFarenheit(tempMax),
                SummaryWords = summaryResult,
            };
        }

        private async Task<(ObjectResult? Problem, T? Result)> QueryRemoteService<T>(object cacheKey, Func<Task<T>> func)
        {
            try
            {
                if (_isDevelopment)
                    return (null, await func());

                return (null,
                    await _memoryCache.GetOrCreateAsync(
                        cacheKey,
                        entry =>
                        {
                            entry.AbsoluteExpirationRelativeToNow = _cacheTimeout;
                            return func();
                        }));
            }
            catch (Exception ex)
            {
                var detail = ex switch
                {
                    BrokenCircuitException => "The data source server is unavailable currently.",
                    HttpRequestException => "Bad data source server response code.",
                    JsonException => "Bad data source server response.",
                    IOException => "Cannot read data source server response.",
                    _ => null
                };

                if (detail is null)
                    throw;

                _logger.LogError(ex, detail);

                return (Problem(
                    detail,
                    statusCode: StatusCodes.Status503ServiceUnavailable), default);
            }
        }
    }
}
