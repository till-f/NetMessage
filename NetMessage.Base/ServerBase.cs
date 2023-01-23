using NetMessage.Base.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetMessage.Base
{
  public abstract class ServerBase<TServer, TSession, TRequest, TProtocol, TPld> : IDisposable
    where TServer : ServerBase<TServer, TSession, TRequest, TProtocol, TPld>
    where TSession : SessionBase<TServer, TSession, TRequest, TProtocol, TPld>, new()
    where TRequest : Request<TRequest, TProtocol, TPld>
    where TProtocol : class, IProtocol<TPld>
  {
    private readonly Dictionary<Guid, TSession> _sessions = new Dictionary<Guid, TSession>();
    private readonly IPEndPoint _endPoint;

    private Socket? _socket;
    private CancellationTokenSource? _cancellationTokenSource;

    public event Action<TSession>? SessionOpened;
    public event Action<TSession>? SessionClosed;
    public event Action<TServer, string>? OnError;
    public event Action<TSession, Message<TPld>>? MessageReceived;
    public event Action<TSession, TRequest>? RequestReceived;

    protected ServerBase(int listeningPort)
    {
      _endPoint = new IPEndPoint(IPAddress.Any, listeningPort);
    }

    /// <summary>
    /// Called to create a protocol buffer that is used exclusively for one session.
    /// The returned instance must not be used for other sessions.
    /// </summary>
    protected abstract TProtocol CreateProtocolBuffer();

    /// <summary>
    /// Called when a new session was created to allow the concrete server to perform
    /// additional initialization.
    /// </summary>
    protected abstract void InitSession(TSession session);

    public void Start()
    {
      if (_socket != null)
      {
        return;
      }

      _cancellationTokenSource = new CancellationTokenSource();
      _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
      _socket.Bind(_endPoint);
      try
      {
        _socket.Listen(10);
      }
      catch (Exception ex)
      {
        OnError?.Invoke((TServer)this, $"Could not start listening on socket: {ex.Message}");
        _socket.Dispose();
        _socket = null;
      }
      AcceptConnectionsAsync();
    }

    public void Stop()
    {
      if (_socket == null)
      {
        return;
      }

      lock (_sessions)
      {
        _cancellationTokenSource!.Cancel();
        _socket?.Close();
        _socket?.Dispose();
        _socket = null;

        foreach (var kvp in _sessions.ToArray())
        {
          kvp.Value.Close();
          _sessions.Remove(kvp.Key);
        }
      }
    }

    public TSession? TryGetSession(Guid sessionId)
    {
      lock (_sessions)
      {
        if (_sessions.ContainsKey(sessionId))
        {
          var session = _sessions[sessionId];
          if (session.IsConnected)
          {
            return _sessions[sessionId];
          }
        }
      }

      return null;
    }

    internal void NotifySessionError(TSession session, string message)
    {
      OnError?.Invoke((TServer)this, $"Error in session {session.Guid} (Port {session.RemoteEndPoint?.Port}): {message}");
    }

    internal void NotifySessionClosed(TSession session)
    {
      lock (_sessions)
      {
        if (_sessions.ContainsKey(session.Guid))
        {
          _sessions.Remove(session.Guid);
        }
      }
      SessionClosed?.Invoke(session);
    }

    internal void NotifyMessagesReceived(TSession session, Message<TPld> message)
    {
      MessageReceived?.Invoke(session, message);
    }

    internal void NotifyRequestReceived(TSession session, TRequest request)
    {
      RequestReceived?.Invoke(session, request);
    }

    private void AcceptConnectionsAsync()
    {
      Task.Run(() =>
      {
        while (true)
        {
          TSession? session = null;
          try
          {
            if (_socket == null)
            {
              return;
            }

            var acceptTask = _socket.AcceptAsync();
            acceptTask.Wait(_cancellationTokenSource!.Token);

            if (!acceptTask.IsCompleted)
            {
              // should never occur
              throw new InvalidOperationException("AcceptTask terminated abnormally");
            }

            if (acceptTask.Result == null)
            {
              // should never occur
              throw new InvalidOperationException("AcceptTask completed but did not return the remote socket");
            }

            var remoteSocket = acceptTask.Result;

            lock (_sessions)
            {
              if (_cancellationTokenSource.IsCancellationRequested)
              {
                remoteSocket.Close();
                remoteSocket.Dispose();
                return;
              }

              session = new TSession();
              InitSession(session);
              session.InitAndStart((TServer)this, remoteSocket, CreateProtocolBuffer());
              _sessions.Add(session.Guid, session);
            }

            SessionOpened?.Invoke(session);
          }
          catch (Exception ex)
          {
            if (session != null)
            {
              session.Close();
            }

            // CancellationToken was triggered. This is NOT an error (do not notify about it)
            if (ex is OperationCanceledException)
            {
              return;
            }

            if (ex.InnerException is SocketException se)
            {
              OnError?.Invoke((TServer)this, $"{se.SocketErrorCode} - {se.Message}");
              return;
            }

            OnError?.Invoke((TServer)this, $"Unexpected {ex.GetType().Name}: {ex.Message}");
          }
        }
      });
    }

    public void Dispose()
    {
      Stop();
    }
  }
}
