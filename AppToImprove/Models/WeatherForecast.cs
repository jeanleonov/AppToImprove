using AppToImprove.Engine;
using System;

namespace AppToImprove.Models
{
    public class WeatherForecast
    {
        public DateTime? Date { get; set; }

        public int? TemperatureC { get; set; }

        public int? TemperatureF => TemperatureC is null
            ? null :
            (int)UnitConverter.TemperatureCelciusToFarenheit(TemperatureC.Value);

        public string? Summary { get; set; }
    }
}
