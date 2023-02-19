using NetMessage.Base.Packets;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Timers;

namespace NetMessage.Base
{
  public abstract class ClientBase<TClient, TRequest, TProtocol, TData> : CommunicatorBase<TRequest, TProtocol, TData>
    where TClient : ClientBase<TClient, TRequest, TProtocol, TData>
    where TRequest : Request<TRequest, TProtocol, TData>
    where TProtocol : class, IProtocol<TData>
  {
    private Socket? _remoteSocket;
    private TProtocol? _protocolBuffer;

    public event Action<TClient>? Connected;
    public event Action<TClient, SessionClosedArgs>? Disconnected;
    public event Action<TClient, string, Exception?>? OnError;
    public event Action<TClient, Message<TData>>? MessageReceived;
    public event Action<TClient, TRequest>? RequestReceived;

    /// <summary>
    /// Only applicable if heartbeat is disabled.
    /// Specifies the timeout with no activity until the first keep-alive packet is sent. A value smaller or equal zero turns off keep alive.
    /// </summary>
    public TimeSpan KeepAliveTime { get; set; } = Defaults.KeepAliveTime;

    /// <summary>
    /// Only applicable if heartbeat is disabled.
    /// Specifies the interval between when successive keep-alive packets are sent if no acknowledgement was received.
    /// </summary>
    public TimeSpan KeepAliveInterval { get; set; } = Defaults.KeepAliveInterval;

    /// <summary>
    /// Called to create a protocol buffer that is used exclusively for one session.
    /// The returned instance must not be shared.
    /// </summary>
    protected abstract TProtocol CreateProtocolBuffer();

    protected override Socket? RemoteSocket => _remoteSocket;

    protected override TProtocol? ProtocolBuffer => _protocolBuffer;

    public Task<bool> ConnectAsync(string remoteHost, int remotePort)
    {
      return Task.Run(() =>
      {
        try
        {
          // only one connection attempt at once
          lock (this)
          {
            if (IsConnected)
            {
              return false;
            }

            ResetConnectionState();

            _remoteSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _remoteSocket.LingerState = new LingerOption(true, 0);  // discard queued data when socket is shut down and reset the connection
            if (HeartbeatInterval.IsInfinite())
            {
              _remoteSocket.SetTcpKeepAlive(KeepAliveTime, KeepAliveInterval);
            }
            else
            {
              _remoteSocket.SetTcpKeepAlive(TimeSpan.Zero, TimeSpan.Zero);
            }

            _protocolBuffer = CreateProtocolBuffer();
            var connectTask = _remoteSocket.ConnectAsync(remoteHost, remotePort);
            connectTask.Wait();

            if (!connectTask.IsCompleted || connectTask.IsFaulted)
            {
              // should never occur
              throw new InvalidOperationException("Connect task terminated abnormally");
            }

            StartReceiveAsync();

            Connected?.Invoke((TClient)this);

            return true;
          }
        }
        catch (Exception ex)
        {
          // CancellationToken was triggered. This is not an error (do not notify about it)
          if (ex is OperationCanceledException)
          {
            return false;
          }

          NotifyError($"Unexpected {ex.GetType().Name} when connecting", ex);
          return false;
        }
      });
    }

    protected override void HandleMessage(Message<TData> message)
    {
      MessageReceived?.Invoke((TClient)this, message);
    }

    protected override void HandleRequest(TRequest request)
    {
      RequestReceived?.Invoke((TClient)this, request);
    }

    protected override void NotifyClosed(SessionClosedArgs args)
    {
      _remoteSocket = null;
      _protocolBuffer = null;
      Disconnected?.Invoke((TClient)this, args);
    }

    protected override void NotifyError(string errorMessage, Exception? exception)
    {
      OnError?.Invoke((TClient)this, errorMessage, exception);
    }
  }
}
