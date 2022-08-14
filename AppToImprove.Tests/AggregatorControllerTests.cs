using AppToImprove.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace AppToImprove.Tests
{
    [TestFixture]
    public class AggregatorControllerTests
    {
        private AppToImproveWebApplicationFactory _factory;
        private HttpClient _client;
        private RemoteServerFixture _remoteServer;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _remoteServer = new RemoteServerFixture();
            _factory = new AppToImproveWebApplicationFactory();

            var inMemoryConfig = new Dictionary<string, string>
            {
                [ConfigurationConstants.Properties.WeatherForecastUrl] = $"http://localhost:{_remoteServer.Port}",
            };
            _client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(inMemoryConfig));
            }).CreateClient();
        }

        [SetUp]
        public void SetUp()
        {
            _remoteServer.Reset();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
            _remoteServer?.Dispose();
        }

        private void SetupRemote(string content)
        {
            _remoteServer.RegisterJsonResponse(HttpMethods.Get, "/WeatherForecast", content);
        }

        private void SetupRemote(Stream content)
        {
            _remoteServer.RegisterJsonResponse(HttpMethods.Get, "/WeatherForecast", content);
        }

        [Test]
        public async Task GetAggregated_RemoteRespondsProperly_AssertCallsAndResults()
        {
            // Arrange
            SetupRemote("""
                [
                  {
                    "date": "2022-08-10T00:00:00Z",
                    "temperatureC": 40,
                    "temperatureF": 800,
                    "summary": "Harno"
                  },
                  {
                    "date": "2022-08-11T00:00:00Z",
                    "temperatureC": -35,
                    "temperatureF": 10,
                    "summary": "Rusnia"
                  },
                  {
                    "date": "2022-08-12T00:00:00Z",
                    "temperatureC": 400,
                    "temperatureF": 900,
                    "summary": "Horyt"
                  }
                ]
                """);

            // Act
            var response = await _client.GetAsync("/Aggregator", CancellationToken.None);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.True(response.IsSuccessStatusCode);

            var expected = """
                {
                  "periodStart": "2022-08-10T00:00:00Z",
                  "periodEnd": "2022-08-12T00:00:00Z",
                  "forecastSamples": 3,
                  "minTemperatureC": -35,
                  "minTemperatureF": -30,
                  "avgTemperatureC": 135,
                  "avgTemperatureF": 274,
                  "maxTemperatureC": 400,
                  "maxTemperatureF": 751,
                  "summaryWords": "Harno Rusnia Horyt"
                }
                """.Replace(" ", "").Replace("\n", "").Replace("\r", "");
            Assert.AreEqual(expected, responseContent.Replace(" ", ""));
        }

        [Test]
        public async Task GetAggregated_EmptyRemote()
        {
            SetupRemote("[]");
            var response = await _client.GetAsync("/Aggregator", CancellationToken.None);
            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Test]
        public async Task GetAggregated_InvalidRemote_BadStructure1()
        {
            SetupRemote("zxcvzxvsd");
            var response = await _client.GetAsync("/Aggregator", CancellationToken.None);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        [Test]
        public async Task GetAggregated_InvalidRemote_BadStructure2()
        {
            SetupRemote("[][]");
            var response = await _client.GetAsync("/Aggregator", CancellationToken.None);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        [Test]
        public async Task GetAggregated_InvalidRemote_BadStructure3()
        {
            SetupRemote("[[]]");
            var response = await _client.GetAsync("/Aggregator", CancellationToken.None);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        [Test]
        public async Task GetAggregated_InvalidRemote_MalformedNonFirst()
        {
            SetupRemote("""
                [
                  {
                    "date": "2022-08-10T00:00:00Z",
                    "temperatureC": 40,
                    "temperatureF": 800,
                    "summary": "Harno"
                  },
                  dsfdsvczvcv
                ]
                """);
            var response = await _client.GetAsync("/Aggregator", CancellationToken.None);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        [Test]
        public async Task GetAggregated_InvalidRemote_InvalidDate()
        {
            SetupRemote("""
                [
                  {
                    "date": "dsafdsbzfhstrhx",
                    "temperatureC": 40,
                    "temperatureF": 800,
                    "summary": "Harno"
                  }
                ]
                """);
            var response = await _client.GetAsync("/Aggregator", CancellationToken.None);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        [Test]
        public async Task GetAggregated_InvalidRemote_SummaryOnly()
        {
            SetupRemote("""
                [
                  {
                    "summary": "Harno"
                  }
                ]
                """);
            var response = await _client.GetAsync("/Aggregator", CancellationToken.None);
            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Test]
        public async Task GetAggregated_InvalidRemote_TempOnly()
        {
            SetupRemote("""
                [
                  {
                    "temperatureC": 40
                  }
                ]
                """);
            var response = await _client.GetAsync("/Aggregator", CancellationToken.None);
            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Test]
        public async Task GetAggregated_InvalidRemote_DateOnly()
        {
            SetupRemote("""
                [
                  {
                    "date": "2022-08-10T00:00:00Z"
                  }
                ]
                """);
            var response = await _client.GetAsync("/Aggregator", CancellationToken.None);
            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Test]
        public async Task GetAggregated_InvalidRemote_NoTokens()
        {
            SetupRemote("");
            var response = await _client.GetAsync("/Aggregator", CancellationToken.None);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        [Test]
        public async Task GetAggregated_InvalidRemote_BadCode()
        {
            var response = await _client.GetAsync("/Aggregator", CancellationToken.None);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        [Test]
        public async Task GetAggregated_SmallRemote()
        {
            // Arrange
            SetupRemote("""
                [
                  {
                    "date": "2022-08-10T00:00:00Z",
                    "temperatureC": 40,
                    "temperatureF": 800,
                    "summary": "Harno"
                  }
                ]
                """);

            // Act
            var response = await _client.GetAsync("/Aggregator", CancellationToken.None);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.True(response.IsSuccessStatusCode);

            var expected = """
                {
                  "periodStart": "2022-08-10T00:00:00Z",
                  "periodEnd": "2022-08-10T00:00:00Z",
                  "forecastSamples": 1,
                  "minTemperatureC": 40,
                  "minTemperatureF": 103,
                  "avgTemperatureC": 40,
                  "avgTemperatureF": 103,
                  "maxTemperatureC": 40,
                  "maxTemperatureF": 103,
                  "summaryWords": "Harno"
                }
                """.Replace(" ", "").Replace("\n", "").Replace("\r", "");
            Assert.AreEqual(expected, responseContent.Replace(" ", ""));
        }

        [Test]
        public async Task GetAggregated_HugeRemote()
        {
            // Arrange
            const int entriesCount = 1000000;

            using var stream = new ForecastsStream();
            SetupRemote(stream);

            var emitterTask = Task.Factory.StartNew(async () =>
            {
                await stream.Add("[");

                for (int i = 0; i < entriesCount; i++)
                {
                    const int bufferMaxCount = 4096;
                    while (stream.Count >= bufferMaxCount)
                        SpinWait.SpinUntil(() => stream.Count < bufferMaxCount);

                    if (i != 0)
                        await stream.Add(",");

                    await stream.Add("""
                        {
                            "date": "2022-08-10T00:00:00Z",
                            "temperatureC": 40,
                            "temperatureF": 800,
                            "summary": "Harno"
                        }
                        """);
                }

                await stream.Add("]");

                stream.Complete();
            });

            // Act
            var response = await _client.GetAsync("/Aggregator", CancellationToken.None);
            var responseContent = await response.Content.ReadAsStringAsync();

            await emitterTask;

            // Assert
            Assert.True(response.IsSuccessStatusCode);

            var expected = $$"""
                {
                  "periodStart": "2022-08-10T00:00:00Z",
                  "periodEnd": "2022-08-10T00:00:00Z",
                  "forecastSamples": {{entriesCount}},
                  "minTemperatureC": 40,
                  "minTemperatureF": 103,
                  "avgTemperatureC": 40,
                  "avgTemperatureF": 103,
                  "maxTemperatureC": 40,
                  "maxTemperatureF": 103,
                  "summaryWords": "Harno"
                }
                """.Replace(" ", "").Replace("\n", "").Replace("\r", "");
            Assert.AreEqual(expected, responseContent.Replace(" ", ""));
        }

        sealed class ForecastsStream : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            private readonly BufferBlock<string> _content = new();

            public void Complete() => _content.Complete();

            public Task Add(string content) => _content.SendAsync(content);

            public int Count => _content.Count;

            public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            {
                var writer = new StreamWriter(destination, leaveOpen: true);

                while (await _content.OutputAvailableAsync(cancellationToken))
                {
                    var msg = await _content.ReceiveAsync(cancellationToken);
                    await writer.WriteAsync(msg.AsMemory(), cancellationToken);
                }

                await writer.FlushAsync();
            }
        }
    }
}
