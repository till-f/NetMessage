using System;
using System.Globalization;
using NetMessage.Examples.TypeSafe;
using NetMessage.TypeSafe;

namespace NetMessage.Examples.SimpleString.Client
{
  class TypeSafeClientApp
  {
    public const ushort Port = 2012;

    static void Main()
    {
      CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

      var client = InitClient();

      Console.WriteLine("CLIENT started. Press (c)onnect, (s)end, (r)equest, (d)isconnect, (q)uit...");

      char consoleKey;
      while ((consoleKey = Console.ReadKey().KeyChar) != 'q')
      {
        Console.WriteLine();
        switch (consoleKey)
        {
          case 'c':
            if (client.IsConnected)
            {
              Console.WriteLine("Already connected.");
              break;
            }
            Console.WriteLine("Connecting...");
            client.ConnectAsync("127.0.0.1", Port);
            break;
          case 's':
            if (!client.IsConnected)
            {
              Console.WriteLine("Cannot send, not connected.");
              break;
            }
            client.SendMessageAsync("Hello World!");
            break;
          case 'r':
            if (!client.IsConnected)
            {
              Console.WriteLine("Cannot send, not connected.");
              break;
            }
            RequestAndResponseExample(client);
            break;
          case 'd':
            if (!client.IsConnected)
            {
              Console.WriteLine("Already disconnected.");
              break;
            }
            Console.WriteLine("Closing...");
            client.Disconnect();
            break;
        }
      }
    }

    private static NetMessageClient InitClient()
    {
      var client = new NetMessageClient();

      client.OnError += OnError;
      client.Connected += OnConnected;
      client.Disconnected += OnDisconnected;

      client.AddMessageHandler<string>(StringMessageHandler);
      client.AddRequestHandler<CalculationRequest, CalculationResponse>(CalculationRequestHandler);

      return client;
    }

    private static void OnDisconnected(NetMessageClient client)
    {
      Console.WriteLine("DISCONNECTED");
    }

    private static void OnConnected(NetMessageClient client)
    {
      Console.WriteLine("CONNECTED");
    }

    private static void OnError(NetMessageClient client, string errorMessage)
    {
      Console.WriteLine($"ERROR: {errorMessage}");
    }

    private static void StringMessageHandler(NetMessageClient arg1, string stringMessage)
    {
      Console.WriteLine($"RECEIVED STRING MESSAGE: {stringMessage}");
    }

    private static void CalculationRequestHandler(NetMessageClient client, TypedRequest<CalculationRequest, CalculationResponse> calculationRequest)
    {
      var request = calculationRequest.Request;

      Console.WriteLine($"RECEIVED CALCULATION REQUEST: What is {request.ValueA} * {request.ValueB}");

      var response = new CalculationResponse { Result = request.ValueA * request.ValueB };
      calculationRequest.SendResponseAsync(response);
    }

    private static async void RequestAndResponseExample(NetMessageClient client)
    {
      var response = await client.SendRequestAsync(new WeatherRequest { City = "Bonn", Date = DateTime.Now });
      Console.WriteLine($"RECEIVED RESPONSE: {response.Forecast}");
    }
  }
}
