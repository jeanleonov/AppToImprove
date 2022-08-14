using AppToImprove.Models;
using System.Collections.Generic;
using System.Threading;

namespace AppToImprove.Engine
{
    public interface IWeatherForecastService
    {
        IAsyncEnumerable<WeatherForecast> GetWeatherForecast(CancellationToken cancellationToken);
    }
}
