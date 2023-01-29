using NetMessage.Base;
using System;
using System.Text;

namespace NetMessage
{
  public class NetMessageServer : ServerBase<NetMessageServer, NetMessageSession, TypedRequestInternal, TypedProtocol, TypedDataString>
  {
    private readonly TypedReceiver<NetMessageSession, TypedRequestInternal, TypedProtocol> _receiver;
    private readonly IDataSerializer _dataSerializer;

    public NetMessageServer(int listeningPort) : this(listeningPort, new XmlDataSerializer())
    {
    }

    public NetMessageServer(int listeningPort, IDataSerializer dataSerializer) : base(listeningPort)
    {
      _dataSerializer = dataSerializer;
      _receiver = new TypedReceiver<NetMessageSession, TypedRequestInternal, TypedProtocol>(_dataSerializer);

      MessageReceived += _receiver.NotifyMessageReceived;
      RequestReceived += _receiver.NotifyRequestReceived;
    }

    public void AddMessageHandler<TTData>(Action<NetMessageSession, TTData> messageHandler)
    {
      _receiver.AddMessageHandler(messageHandler);
    }

    public void RemoveMessageHandler<TTData>(Action<NetMessageSession, TTData> messageHandler)
    {
      _receiver.RemoveMessageHandler(messageHandler);
    }

    public void AddRequestHandler<TTData, TTRsp>(Action<NetMessageSession, TypedRequest<TTData, TTRsp>> requestHandler)
      where TTData : IRequest<TTRsp>
    {
      _receiver.AddRequestHandler(requestHandler);
    }

    public void RemoveRequestHandler<TTData, TTRsp>(Action<NetMessageSession, TypedRequest<TTData, TTRsp>> requestHandler)
      where TTData : IRequest<TTRsp>
    {
      _receiver.RemoveRequestHandler(requestHandler);
    }

    protected override TypedProtocol CreateProtocolBuffer()
    {
      return new TypedProtocol
      {
        Encoding = _dataSerializer.ProtocolEncoding,
        Terminator = _dataSerializer.ProtocolTerminator
      };
    }

    protected override void InitSession(NetMessageSession session)
    {
      session.Init(_dataSerializer);
    }
  }
}
