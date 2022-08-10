using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AppToImprove.Models;

namespace AppToImprove.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private static readonly Random Random = new Random();

        private readonly ILogger _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            this._logger = logger;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public ActionResult<IEnumerable<WeatherForecast>> Get()
        {
            var randomNumber = Random.Next(0, 1000);

            return randomNumber switch
            {
                0 => this.Problem(statusCode: 504),
                1 => this.Problem(statusCode: 400),
                _ => Enumerable.Range(0, Random.Next(5, 50000))
                    .Select(index => new WeatherForecast
                    {
                        Date = DateTime.Now.AddDays(index),
                        TemperatureC = Random.Next(-20, 55),
                        Summary = Summaries[Random.Next(Summaries.Length)]
                    })
                    .ToArray()
            };
        }
    }
}
