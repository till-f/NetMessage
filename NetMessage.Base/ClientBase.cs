﻿using NetMessage.Base.Message;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetMessage.Base
{
  public abstract class ClientBase<TClient, TRequest, TProtocol, TPld> : CommunicatorBase<TRequest, TProtocol, TPld>, IDisposable
    where TClient : ClientBase<TClient, TRequest, TProtocol, TPld>
    where TRequest : Request<TRequest, TProtocol, TPld>
    where TProtocol : class, IProtocol<TPld>
  {
    private Socket? _remoteSocket;
    private TProtocol? _protocolBuffer;

    public event Action<TClient>? Connected;
    public event Action<TClient>? Disconnected;
    public event Action<TClient, string>? OnError;
    public event Action<TClient, Message<TPld>>? MessageReceived;
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
            _protocolBuffer = CreateProtocolBuffer();
            var connectTask = _remoteSocket.ConnectAsync(remoteHost, remotePort);
            connectTask.Wait();

            if (!connectTask.IsCompleted)
            {
              // should never occur
              throw new InvalidOperationException("ConnectTask terminated abnormally");
            }

            ReceiveAsync();

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

    protected override void HandleMessage(Message<TPld> message)
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

    protected override void NotifyError(string errorMessage)
    {
      OnError?.Invoke((TClient)this, errorMessage);
    }

    public void Dispose()
    {
      Close();
    }
  }
}
