namespace AppToImprove.Util
{
    public static class TemperatureConverter
    {
        public static int ConvertFromCelsiusToFahrenheit(this int value) => 32 + (int)(value / 0.5556);
    }
}
