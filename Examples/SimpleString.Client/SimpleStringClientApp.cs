using System;
using NetMessage.Base.Packets;

namespace NetMessage.Examples.SimpleString.Client
{
  class SimpleStringClientApp
  {
    public const ushort Port = 2012;

    static void Main()
    {
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

    private static SimpleStringClient InitClient()
    {
      var client = new SimpleStringClient();

      client.OnError += OnError;
      client.Connected += OnConnected;
      client.Disconnected += OnDisconnected;
      client.MessageReceived += OnMessageReceived;
      client.RequestReceived += OnRequestReceived;

      return client;
    }

    private static void OnMessageReceived(SimpleStringClient client, Message<string> message)
    {
      Console.WriteLine($"MESSAGE RECEIVED: {message.Data}");
    }

    private static void OnRequestReceived(SimpleStringClient client, SimpleStringRequest request)
    {
      Console.WriteLine($"REQUEST RECEIVED: {request.Data}");
      request.SendResponseAsync("Yes, you are a nice server.");
    }

    private static void OnDisconnected(SimpleStringClient client)
    {
      Console.WriteLine("DISCONNECTED");
    }

    private static void OnConnected(SimpleStringClient client)
    {
      Console.WriteLine("CONNECTED");
    }

    private static void OnError(SimpleStringClient client, string errorMessage, Exception? ex)
    {
      Console.WriteLine($"ERROR: {errorMessage} {ex?.Message}");
    }

    private static async void RequestAndResponseExample(SimpleStringClient client)
    {
      var response = await client.SendRequestAsync("How is the weather?");
      Console.WriteLine($"RECEIVED RESPONSE: {response?.Data}");
    }
  }
}
