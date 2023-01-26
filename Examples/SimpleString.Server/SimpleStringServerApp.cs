using System;
using NetMessage.Base.Message;

namespace NetMessage.Examples.SimpleString.Server
{
  public class SimpleStringServerApp
  {
    public const ushort Port = 2012;

    private static SimpleStringSession? _openSession;

    static void Main()
    {
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

    private static SimpleStringServer InitServer()
    {
      var server = new SimpleStringServer(Port);

      server.OnError += OnError;
      server.SessionOpened += OnSessionOpened;
      server.SessionClosed += OnSessionClosed;
      server.MessageReceived += OnMessageReceived;
      server.RequestReceived += OnRequestReceived;

      server.Start();

      return server;
    }

    private static void OnMessageReceived(SimpleStringSession session, Message<string> message)
    {
      Console.WriteLine($"MESSAGE RECEIVED: {message.Payload}");
    }

    private static void OnRequestReceived(SimpleStringSession session, SimpleStringRequest request)
    {
      Console.WriteLine($"REQUEST RECEIVED: {request.Payload}");
      request.SendResponseAsync("Verry sunny!");
    }

    private static void OnSessionClosed(SimpleStringSession session)
    {
      Console.WriteLine($"SESSION CLOSED: {session.Guid} (Port {session.RemoteEndPoint?.Port})");
      _openSession = null;
    }

    private static void OnSessionOpened(SimpleStringSession session)
    {
      Console.WriteLine($"SESSION OPENED: {session.Guid} (Port {session.RemoteEndPoint?.Port})");
      _openSession = session;
    }

    private static void OnError(SimpleStringServer server, SimpleStringSession? session, string errorMessage, Exception? ex)
    {
      Console.WriteLine($"ERROR: {errorMessage} {ex?.Message}");
    }

    private static async void RequestAndResponseExample()
    {
      var response = await _openSession!.SendRequestAsync("Do you like me?");
      Console.WriteLine($"RECEIVED RESPONSE: {response?.Payload}");
    }
  }
}
