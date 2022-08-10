using System;

namespace AppToImprove.Models
{
    public class AggregatedInfo
    {
        public DateTime PeriodStart { get; set; }

        public DateTime PeriodEnd { get; set; }

        public int ForecastSamples { get; set; }

        public int MinTemperatureC { get; set; }

        public int MinTemperatureF { get; set; }

        public int AvgTemperatureC { get; set; }

        public int AvgTemperatureF { get; set; }

        public int MaxTemperatureC { get; set; }

        public int MaxTemperatureF { get; set; }

        public string SummaryWords { get; set; }
    }
}
