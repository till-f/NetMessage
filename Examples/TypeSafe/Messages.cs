using NetMessage.TypeSafe;
using System;

namespace NetMessage.Examples.TypeSafe
{
  public enum EWeather
  {
    Sunny, Rainy, Snow, Thunderstorm
  }

  public class WeatherRequest : IRequest<WeatherResponse>
  {
    public string City { get; set; }

    public DateTime Date { get; set; }
  }

  public class WeatherResponse
  {
    public EWeather Forecast { get; set; }
  }

  public class CalculationRequest : IRequest<CalculationResponse>
  {
    public double ValueA { get; set; }

    public double ValueB { get; set; }
  }

  public class CalculationResponse
  {
    public double Result { get; set; }
  }
}
