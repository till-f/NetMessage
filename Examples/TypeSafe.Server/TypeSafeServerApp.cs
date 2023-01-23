using System;
using System.Globalization;

namespace NetMessage.Examples.TypeSafe.Server
{
  public class TypeSafeServerApp
  {
    public const ushort Port = 2012;

    private static NetMessageSession? _openSession;

    static void Main()
    {
      CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

      var server = InitServer();

      Console.WriteLine("SERVER started. Press (s)end, (q)uit...");

      char consoleKey;
      while ((consoleKey = Console.ReadKey().KeyChar) != 'q')
      {
        Console.WriteLine();
        switch (consoleKey)
        {
          case 's':
            if (_openSession == null)
            {
              Console.WriteLine("Client not connected.");
              break;
            }
            _openSession.SendMessageAsync("Blah, blah, blah...");
            break;
          case 'r':
            if (_openSession == null)
            {
              Console.WriteLine("Client not connected.");
              break;
            }
            RequestAndResponseExample();
            break;
        }
      }
      
      Console.WriteLine("Stopping server...");
      server.Stop();
    }

    private static NetMessageServer InitServer()
    {
      var server = new NetMessageServer(Port);

      server.OnError += OnError;
      server.SessionOpened += OnSessionOpened;
      server.SessionClosed += OnSessionClosed;

      server.AddMessageHandler<string>(StringMessageHandler);
      server.AddRequestHandler<WeatherRequest, WeatherResponse>(WeatherRequestHandler);

      server.Start();

      return server;
    }

    private static void OnSessionClosed(NetMessageSession session)
    {
      Console.WriteLine($"SESSION CLOSED: {session.Guid} (Port {session.RemoteEndPoint?.Port})");
      _openSession = null;
    }

    private static void OnSessionOpened(NetMessageSession session)
    {
      Console.WriteLine($"SESSION OPENED: {session.Guid} (Port {session.RemoteEndPoint?.Port})");
      _openSession = session;
    }

    private static void OnError(NetMessageServer server, string errorMessage)
    {
      Console.WriteLine($"ERROR: {errorMessage}");
    }

    private static void StringMessageHandler(NetMessageSession sesion, string stringMessage)
    {
      Console.WriteLine($"RECEIVED STRING MESSAGE: {stringMessage}");
    }

    private static void WeatherRequestHandler(NetMessageSession session, TypedRequest<WeatherRequest, WeatherResponse> weatherRequest)
    {
      var request = weatherRequest.Request;

      Console.WriteLine($"RECEIVED WEATHER REQUEST: What is the wheater in {request.City} on {request.Date}");

      var response = new WeatherResponse { Forecast = EWeather.Sunny };
      weatherRequest.SendResponseAsync(response);
    }

    private static async void RequestAndResponseExample()
    {
      var response = await _openSession!.SendRequestAsync(new CalculationRequest { ValueA = 42.0, ValueB = 1.5 });
      Console.WriteLine($"RECEIVED RESPONSE: {response.Result}");
    }
  }
}
