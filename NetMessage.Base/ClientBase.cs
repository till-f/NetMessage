using NetMessage.Base.Message;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

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
    public event Action<TClient>? Disconnected;
    public event Action<TClient, string, Exception?>? OnError;
    public event Action<TClient, Message<TData>>? MessageReceived;
    public event Action<TClient, TRequest>? RequestReceived;

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

            ResetCancellationToken();

            _remoteSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _remoteSocket.LingerState = new LingerOption(false, 0);  // don't try to send queued data when disconnect is called (but discard)
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
          HandleException(ex);
          return false;
        }
      });
    }

    public void Disconnect()
    {
      if (!IsConnected)
      {
        return;
      }

      Close();
    }

    protected override void HandleMessage(Message<TData> message)
    {
      MessageReceived?.Invoke((TClient)this, message);
    }

    protected override void HandleRequest(TRequest request)
    {
      RequestReceived?.Invoke((TClient)this, request);
    }

    protected override void NotifyClosed()
    {
      _remoteSocket = null;
      _protocolBuffer = null;
      Disconnected?.Invoke((TClient)this);
    }

    protected override void NotifyError(string errorMessage, Exception? exception)
    {
      OnError?.Invoke((TClient)this, errorMessage, exception);
    }
  }
}
