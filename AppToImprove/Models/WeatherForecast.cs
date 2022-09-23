using System;
using AppToImprove.Util;

namespace AppToImprove.Models
{
    public class WeatherForecast
    {
        public DateTime Date { get; set; }

        public int TemperatureC { get; set; }

        public int TemperatureF => TemperatureC.ConvertFromCelsiusToFahrenheit();

        public string Summary { get; set; }
    }
}
