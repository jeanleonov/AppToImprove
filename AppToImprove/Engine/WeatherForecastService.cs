using AppToImprove.Configuration;
using AppToImprove.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;

namespace AppToImprove.Engine
{
    sealed class WeatherForecastService : IWeatherForecastService
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WeatherForecastService> _logger;

        public WeatherForecastService(ILogger<WeatherForecastService> logger!!, IHttpClientFactory httpClientFactory!!)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async IAsyncEnumerable<WeatherForecast> GetWeatherForecast(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var httpClient = _httpClientFactory.CreateClient(ConfigurationConstants.HttpClients.ForecastProvider);

            HttpResponseMessage? response = null;
            Stream? stream = null;
            try
            {
                int? statusCode = null;
                try
                {
                    // For simplicity of test application it was implemented as neighbor controller.
                    // But please, consider it as a call to remote independent service.
                    response = await httpClient.GetAsync(
                        "WeatherForecast",
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);

                    statusCode = (int)response.StatusCode;
                    response.EnsureSuccessStatusCode();

                    stream = await response.Content.ReadAsStreamAsync();

                    _logger.LogInformation($"Weather forecasts requested successfully.");
                }
                catch (Exception ex)
                {
                    var statusCodeMsg = statusCode is null ? "" : $" StatusCode = {statusCode}.";
                    _logger.LogError(ex, $"Error occurred while requesting weather forecasts.{statusCodeMsg}");
                    throw;
                }

                await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<WeatherForecast>(
                    stream,
                    _jsonSerializerOptions,
                    cancellationToken))
                {
                    if (item != null)
                        yield return item;
                }
            }
            finally
            {
                stream?.Dispose();
                response?.Dispose();
            }
        }
    }
}
