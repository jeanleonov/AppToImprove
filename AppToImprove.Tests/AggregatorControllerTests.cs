using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace AppToImprove.Tests
{
    [TestFixture]
    public class AggregatorControllerTests
    {
        private AppToImproveWebApplicationFactory _factory;
        private HttpClient _client;
        private WireMockServer _remoteServerMock;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var remoteServerFixture = new RemoteServerFixture();
            this._remoteServerMock = remoteServerFixture.Server;
            this._factory = new AppToImproveWebApplicationFactory();

            var inMemoryConfig = new Dictionary<string, string>
            {
                ["WeatherForecastUrl"] = $"http://localhost:{remoteServerFixture.Port}",
                ["WeatherForecastCallAttemptsCount"] = "3",
                ["WeatherForecastCallTimeout"] = "1",
            };
            this._client = this._factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(inMemoryConfig));
            }).CreateClient();
        }

        [SetUp]
        public void SetUp()
        {
            this._remoteServerMock.Reset();
        }

        [Test]
        public async Task GetAggregated_RemoteRespondsProperly_AssertCallsAndResults()
        {
            // Arrange
            var forecastResponseBodyString = @"[
                {
                    ""date"": ""2022-08-10T00:00:00Z"",
                    ""temperatureC"": 40,
                    ""temperatureF"": 800,
                    ""summary"": ""Harno""
                },
                {
                    ""date"": ""2022-08-11T00:00:00Z"",
                    ""temperatureC"": -35,
                    ""temperatureF"": 10,
                    ""summary"": ""Rusnia""
                },
                {
                    ""date"": ""2022-08-12T00:00:00Z"",
                    ""temperatureC"": 400,
                    ""temperatureF"": 900,
                    ""summary"": ""Horyt""
                }
            ]";

            var forecastResponse = Response
                .Create()
                .WithStatusCode(200)
                .WithBody(forecastResponseBodyString, encoding: Encoding.UTF8)
                .WithHeader("Content-Type", "application/json");

            this._remoteServerMock
                .Given(Request.Create().UsingGet().WithPath("/WeatherForecast"))
                .RespondWith(forecastResponse);

            // Act
            var response = await this._client.GetAsync("/Aggregator", CancellationToken.None);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.True(response.IsSuccessStatusCode);

            var expected = @"{
                ""periodStart"": ""2022-08-10T00:00:00Z"",
                ""periodEnd"": ""2022-08-12T00:00:00Z"",
                ""forecastSamples"": 3,
                ""minTemperatureC"": -35,
                ""minTemperatureF"": -30,
                ""avgTemperatureC"": 135,
                ""avgTemperatureF"": 274,
                ""maxTemperatureC"": 400,
                ""maxTemperatureF"": 751,
                ""summaryWords"": ""Harno Rusnia Horyt""}".Replace(" ", "").Replace("\n", "").Replace("\r", "");
            Assert.AreEqual(expected, responseContent.Replace(" ", ""));
        }

        [Test]
        public async Task GetAggregated_RemoteRespondsFor2Of3CallsWithInternalServerError_AssertCallsAndResults()
        {
            // Arrange
            var forecastResponseBodyString = @"[
                {
                    ""date"": ""2022-08-10T00:00:00Z"",
                    ""temperatureC"": 40,
                    ""temperatureF"": 800,
                    ""summary"": ""Harno""
                },
                {
                    ""date"": ""2022-08-11T00:00:00Z"",
                    ""temperatureC"": -35,
                    ""temperatureF"": 10,
                    ""summary"": ""Rusnia""
                },
                {
                    ""date"": ""2022-08-12T00:00:00Z"",
                    ""temperatureC"": 400,
                    ""temperatureF"": 900,
                    ""summary"": ""Horyt""
                }
            ]";

            var forecastResponse = Response
                .Create()
                .WithStatusCode(200)
                .WithBody(forecastResponseBodyString, encoding: Encoding.UTF8)
                .WithHeader("Content-Type", "application/json");

            var errorResponse = Response
                .Create()
                .WithStatusCode(HttpStatusCode.InternalServerError);

            this._remoteServerMock
                .Given(Request.Create().UsingGet().WithPath("/WeatherForecast"))
                .InScenario("2of3CallsWithInternalServerError")
                .WillSetStateTo("Second Call")
                .RespondWith(errorResponse);

            this._remoteServerMock
                .Given(Request.Create().UsingGet().WithPath("/WeatherForecast"))
                .InScenario("2of3CallsWithInternalServerError")
                .WhenStateIs("Second Call")
                .WillSetStateTo("Third Call")
                .RespondWith(errorResponse);

            this._remoteServerMock
                .Given(Request.Create().UsingGet().WithPath("/WeatherForecast"))
                .InScenario("2of3CallsWithInternalServerError")
                .WhenStateIs("Third Call")
                .RespondWith(forecastResponse);

            // Act
            var response = await this._client.GetAsync("/Aggregator", CancellationToken.None);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.True(response.IsSuccessStatusCode);

            var expected = @"{
                ""periodStart"": ""2022-08-10T00:00:00Z"",
                ""periodEnd"": ""2022-08-12T00:00:00Z"",
                ""forecastSamples"": 3,
                ""minTemperatureC"": -35,
                ""minTemperatureF"": -30,
                ""avgTemperatureC"": 135,
                ""avgTemperatureF"": 274,
                ""maxTemperatureC"": 400,
                ""maxTemperatureF"": 751,
                ""summaryWords"": ""Harno Rusnia Horyt""}".Replace(" ", "").Replace("\n", "").Replace("\r", "");
            Assert.AreEqual(expected, responseContent.Replace(" ", ""));
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }
    }
}
