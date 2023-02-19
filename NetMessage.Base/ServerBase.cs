using NetMessage.Base.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetMessage.Base
{
  public abstract class ServerBase<TServer, TSession, TRequest, TProtocol, TData> : IDisposable
    where TServer : ServerBase<TServer, TSession, TRequest, TProtocol, TData>
    where TSession : SessionBase<TServer, TSession, TRequest, TProtocol, TData>, new()
    where TRequest : Request<TRequest, TProtocol, TData>
    where TProtocol : class, IProtocol<TData>
  {
    private readonly Dictionary<Guid, TSession> _sessions = new Dictionary<Guid, TSession>();
    private readonly IPEndPoint _endPoint;

    private Socket? _listenSocket;
    private CancellationTokenSource? _cancellationTokenSource;

    public event Action<TSession>? SessionOpened;
    public event Action<TSession, SessionClosedArgs>? SessionClosed;
    public event Action<TServer, TSession?, string, Exception?>? OnError;
    public event Action<TSession, Message<TData>>? MessageReceived;
    public event Action<TSession, TRequest>? RequestReceived;

    protected ServerBase(int listeningPort)
    {
      _endPoint = new IPEndPoint(IPAddress.Any, listeningPort);
    }

    /// <summary>
    /// Specifies the HeartbeatInterval for all sessions, see <see cref="CommunicatorBase{TRequest,TProtocol,TData}.HeartbeatInterval"/>.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = Defaults.HeartbeatInterval;

    /// <summary>
    /// Specifies the ReceiveTimeout for all sessions, see <see cref="CommunicatorBase{TRequest,TProtocol,TData}.ReceiveTimeout"/>.
    /// </summary>
    public TimeSpan ReceiveTimeout { get; set; } = Defaults.ReceiveTimeout;

    /// <summary>
    /// Only applicable if heartbeat is disabled.
    /// Specifies the timeout with no activity until the first keep-alive packet is sent. A value smaller or equal zero turns off keep alive.
    /// </summary>
    public TimeSpan KeepAliveTime { get; set; } = Defaults.KeepAliveTime;

    /// <summary>
    /// Only applicable if receive timeout is disabled.
    /// Specifies the interval between when successive keep-alive packets are sent if no acknowledgement was received.
    /// </summary>
    public TimeSpan KeepAliveInterval { get; set; } = Defaults.KeepAliveInterval;

    /// <summary>
    /// The ResponseTimeout used for all sessions.
    /// See <see cref="CommunicatorBase{TRequest, TProtocol, TData}.ResponseTimeout"/>.
    /// </summary>
    public TimeSpan ResponseTimeout { get; set; } = Defaults.ResponseTimeout;

    /// <summary>
    /// The FailOnFaultedReceiveTask for all sessions.
    /// See <see cref="CommunicatorBase{TRequest,TProtocol,TData}.FailOnFaultedReceiveTask"/>.
    /// </summary>
    public virtual bool FailOnFaultedReceiveTask { get; set; }

    /// <summary>
    /// This method is called when a connection request was received and the remote socket was opened.
    /// It allows verification of the remote endpoint (e.g. IP Address) before a session is created. 
    /// The connection is rejected when the verifier returns false.
    /// </summary>
    public Func<Socket, bool>? RemoteSocketVerifier { get; set; }

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
      if (_listenSocket != null)
      {
        return;
      }

      _cancellationTokenSource = new CancellationTokenSource();
      _listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
      _listenSocket.Bind(_endPoint);
      try
      {
        _listenSocket.Listen(10);
      }
      catch (Exception ex)
      {
        OnError?.Invoke((TServer)this, null, "Could not start listening on socket", ex);
        _listenSocket.Dispose();
        _listenSocket = null;
      }
      AcceptConnectionsAsync();
    }

    public void Stop()
    {
      if (_listenSocket == null)
      {
        return;
      }

      _cancellationTokenSource!.Cancel();
      _listenSocket.Close();
      _listenSocket.Dispose();
      _listenSocket = null;

      lock (_sessions)
      {
        foreach (var kvp in _sessions.ToArray())
        {
          kvp.Value.Disconnect();
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

    internal void NotifySessionError(TSession session, string message, Exception? exception)
    {
      OnError?.Invoke((TServer)this, session, message, exception);
    }

    internal void NotifySessionClosed(TSession session, SessionClosedArgs args)
    {
      lock (_sessions)
      {
        if (_sessions.ContainsKey(session.Guid))
        {
          _sessions.Remove(session.Guid);
        }
      }
      SessionClosed?.Invoke(session, args);
    }

    internal void NotifyMessagesReceived(TSession session, Message<TData> message)
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
        while (!_cancellationTokenSource!.IsCancellationRequested)
        {
          TSession? session = null;
          try
          {
            if (_listenSocket == null)
            {
              return;
            }

            var acceptTask = _listenSocket.AcceptAsync();
            acceptTask.Wait(_cancellationTokenSource.Token);

            if (!acceptTask.IsCompleted || acceptTask.IsFaulted)
            {
              // should never occur
              throw new InvalidOperationException("Accept task terminated abnormally");
            }

            if (acceptTask.Result == null)
            {
              // should never occur
              throw new InvalidOperationException("Accept task completed but did not return the remote socket");
            }

            var remoteSocket = acceptTask.Result;

            if ((RemoteSocketVerifier != null && !RemoteSocketVerifier.Invoke(remoteSocket)) || _cancellationTokenSource.IsCancellationRequested)
            {
              remoteSocket.Close();
              remoteSocket.Dispose();
              return;
            }

            lock (_sessions)
            {
              session = new TSession();
              InitSession(session);
              session.InitAndStart((TServer)this, remoteSocket, CreateProtocolBuffer());
              _sessions.Add(session.Guid, session);
            }

            SessionOpened?.Invoke(session);
          }
          catch (Exception ex)
          {
            // CancellationToken was triggered. This is NOT an error (do not notify about it).
            // All opened sessions will be shut down gracefully. When this exception is thrown, no accepted session is pending (session is null)
            if (ex is OperationCanceledException)
            {
              return;
            }

            if (session != null)
            {
              session.Close(new SessionClosedArgs(ECloseReason.AcceptError));
            }

            if (ex.InnerException is SocketException se)
            {
              OnError?.Invoke((TServer)this, null, $"Unexpected SocketException (ErrorCode {se.SocketErrorCode}) in listening Socket", se);
              return;
            }

            OnError?.Invoke((TServer)this, null, $"Unexpected {ex.GetType().Name} in listening Socket", ex);
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
