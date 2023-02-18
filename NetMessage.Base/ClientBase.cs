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
    private readonly Timer _heartbeatTimer = new Timer();

    private Socket? _remoteSocket;
    private TProtocol? _protocolBuffer;

    public event Action<TClient>? Connected;
    public event Action<TClient, SessionClosedArgs>? Disconnected;
    public event Action<TClient, string, Exception?>? OnError;
    public event Action<TClient, Message<TData>>? MessageReceived;
    public event Action<TClient, TRequest>? RequestReceived;

    /// <summary>
    /// Specifies the interval between when heartbeat packets are sent. A heartbeat packet is always sent, even if regular packets
    /// were sent in the specified time frame to ensure that a connection loss is detected.
    /// A value smaller or equal zero disables the heartbeat. In that case, the server/session receive timeout should be disabled, too,
    /// so that TCP's native keep alive mechanism is used. Note that a connection loss might not be detected quickly then.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = Defaults.HeartbeatInterval;

    /// <summary>
    /// Specifies the time to wait for a heartbeat packet to be sent before assuming a connection loss.
    /// </summary>
    public TimeSpan HeartbeatTimeout { get; set; } = Defaults.HeartbeatTimeout;

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

            StartHeartbeatTimerIfEnabled();
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

          NotifyError($"{ex.GetType().Name} when connecting: {ex.Message}", ex);
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
      _heartbeatTimer.Stop();
      _remoteSocket = null;
      _protocolBuffer = null;
      Disconnected?.Invoke((TClient)this, args);
    }

    protected override void NotifyError(string errorMessage, Exception? exception)
    {
      OnError?.Invoke((TClient)this, errorMessage, exception);
    }

    private void StartHeartbeatTimerIfEnabled()
    {
      if (HeartbeatInterval.IsInfinite())
      {
        _heartbeatTimer.Stop();
        return;
      }

      _heartbeatTimer.AutoReset = false;
      _heartbeatTimer.Interval = HeartbeatInterval.TotalMilliseconds;
      _heartbeatTimer.Elapsed -= OnHeartbeatTimerElapsed;
      _heartbeatTimer.Elapsed += OnHeartbeatTimerElapsed;
      _heartbeatTimer.Start();
    }

    private void OnHeartbeatTimerElapsed(object sender, ElapsedEventArgs e)
    {
      try
      {
        if (CancellationToken.IsCancellationRequested)
        {
          return;
        }

        var task = SendRawDataAsync(_protocolBuffer!.HeartbeatPacket);
        int heartbeatTimeoutInms = HeartbeatTimeout.IsInfinite() ? -1 : (int)HeartbeatTimeout.TotalMilliseconds;
        var completedInTime = task.Wait(heartbeatTimeoutInms, CancellationToken);

        if (!completedInTime)
        {
          throw new ConnectionLostException($"Heartbeat could not be sent after {heartbeatTimeoutInms} ms");
        }
      }
      catch (Exception ex)
      {
        if (HandleReceiveOrHeartbeatException(ex)) return;
      }

      _heartbeatTimer.Start();
    }
  }
}
