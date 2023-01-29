﻿using NetMessage.Base.Message;
using System;
using System.Net;
using System.Net.Sockets;

namespace NetMessage.Base
{
  public abstract class SessionBase<TServer, TSession, TRequest, TProtocol, TData> : CommunicatorBase<TRequest, TProtocol, TData>
    where TServer : ServerBase<TServer, TSession, TRequest, TProtocol, TData>
    where TSession : SessionBase<TServer, TSession, TRequest, TProtocol, TData>, new()
    where TRequest : Request<TRequest, TProtocol, TData>
    where TProtocol : class, IProtocol<TData>
  {
    private Socket? _remoteSocket;
    private TProtocol? _protocolBuffer;

    protected SessionBase()
    {
      Guid = Guid.NewGuid();
    }

    internal void InitAndStart(TServer server, Socket remoteSocket, TProtocol protocolBuffer)
    {
      Server = server;
      _remoteSocket = remoteSocket;
      _protocolBuffer = protocolBuffer;
      RemoteEndPoint = (IPEndPoint)_remoteSocket.RemoteEndPoint;
      StartReceiveAsync();
    }

    public Guid Guid { get; }

    public TServer? Server { get; private set; }

    public IPEndPoint? RemoteEndPoint { get; private set; }

    public override TimeSpan ResponseTimeout
    {
      get => Server?.ResponseTimeout ?? base.ResponseTimeout;
      set
      {
        if (Server != null)
        {
          Server.ResponseTimeout = value;
        }
        else
        {
          base.ResponseTimeout = value;
        }
      }
    }

    protected override Socket? RemoteSocket => _remoteSocket;

    protected override TProtocol? ProtocolBuffer => _protocolBuffer;

    protected override void HandleMessage(Message<TData> message)
    {
      Server!.NotifyMessagesReceived((TSession)this, message);
    }

    protected override void HandleRequest(TRequest request)
    {
      Server!.NotifyRequestReceived((TSession)this, request);
    }

    protected override void NotifyClosed()
    {
      _remoteSocket = null;
      _protocolBuffer = null;
      Server!.NotifySessionClosed((TSession)this);
    }

    protected override void NotifyError(string errorMessage, Exception? exception)
    {
      Server!.NotifySessionError((TSession)this, errorMessage, exception);
    }
  }
}
