using NetMessage.Base.Message;
using System;
using System.Net;
using System.Net.Sockets;

namespace NetMessage.Base
{
  public abstract class SessionBase<TServer, TSession, TRequest, TProtocol, TPld> : CommunicatorBase<TRequest, TProtocol, TPld>
    where TServer : ServerBase<TServer, TSession, TRequest, TProtocol, TPld>
    where TSession : SessionBase<TServer, TSession, TRequest, TProtocol, TPld>, new()
    where TRequest : Request<TRequest, TProtocol, TPld>
    where TProtocol : class, IProtocol<TPld>
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
      ReceiveAsync();
    }

    public Guid Guid { get; }

    public TServer? Server { get; private set; }

    public IPEndPoint? RemoteEndPoint { get; private set; }

    protected override Socket? RemoteSocket => _remoteSocket;

    protected override TProtocol? ProtocolBuffer => _protocolBuffer;

    protected override void HandleMessage(Message<TPld> message)
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

    protected override void NotifyError(string errorMessage)
    {
      Server!.NotifySessionError((TSession)this, errorMessage);
    }
  }
}
