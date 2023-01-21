using NetMessage.Base;
using System;

namespace NetMessage.TypeSafe
{
  public class NetMessageServer : ServerBase<NetMessageServer, NetMessageSession, TypedRequestInternal, TypedProtocol, TypedPayload>
  {
    private readonly TypedReceiver<NetMessageSession, TypedRequestInternal, TypedProtocol> _receiver;
    private readonly IPayloadSerializer _payloadConverter;

    public NetMessageServer(int listeningPort) : this(listeningPort, new XmlPayloadSerializer())
    {
    }

    public NetMessageServer(int listeningPort, IPayloadSerializer payloadConverter) : base(listeningPort)
    {
      _payloadConverter = payloadConverter;
      _receiver = new TypedReceiver<NetMessageSession, TypedRequestInternal, TypedProtocol>(_payloadConverter);

      MessageReceived += _receiver.NotifyMessageReceived;
      RequestReceived += _receiver.NotifyRequestReceived;
    }

    public void AddMessageHandler<TTPld>(Action<NetMessageSession, TTPld> messageHandler)
    {
      _receiver.AddMessageHandler(messageHandler);
    }

    public void RemoveMessageHandler<TTPld>(Action<NetMessageSession, TTPld> messageHandler)
    {
      _receiver.RemoveMessageHandler(messageHandler);
    }

    public void AddRequestHandler<TTPld, TTRsp>(Action<NetMessageSession, TypedRequest<TTPld, TTRsp>> requestHandler)
      where TTPld : IRequest<TTRsp>
    {
      _receiver.AddRequestHandler(requestHandler);
    }

    public void RemoveRequestHandler<TTPld, TTRsp>(Action<NetMessageSession, TypedRequest<TTPld, TTRsp>> requestHandler)
      where TTPld : IRequest<TTRsp>
    {
      _receiver.RemoveRequestHandler(requestHandler);
    }

    protected override TypedProtocol CreateProtocolBuffer()
    {
      return new TypedProtocol();
    }

    protected override void InitSession(NetMessageSession session)
    {
      session.Init(_payloadConverter);
    }
  }
}
