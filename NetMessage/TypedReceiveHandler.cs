using NetMessage.Base;
using NetMessage.Base.Packets;
using System;

namespace NetMessage
{
  public abstract class TypedReceiveHandler
  {
    protected TypedReceiveHandler(IDataSerializer dataSerializer)
    {
      DataSerializer = dataSerializer;
    }

    protected IDataSerializer DataSerializer { get; }

    public abstract object UserHandler { get; }
  }

  /// <summary>
  /// Internal helper to deserialize received messages from the protocol layer and
  /// forwards them to the properly typed message handler.
  /// </summary>
  public abstract class TypedMessageHandler<TCommunicator, TRequest, TProtocol> : TypedReceiveHandler
    where TCommunicator : CommunicatorBase<TRequest, TProtocol, TypedDataString>
    where TRequest : Request<TRequest, TProtocol, TypedDataString>
    where TProtocol : class, IProtocol<TypedDataString>
  {
    protected TypedMessageHandler(IDataSerializer dataSerializer) : base(dataSerializer) { }

    public abstract void InvokeMessageHandler(TCommunicator communicator, Message<TypedDataString> message);
  }

  /// <summary>
  /// Internal helper to deserialize received messages from the protocol layer and
  /// forwards them to the properly typed message handler.
  /// </summary>
  public class TypedMessageHandler<TCommunicator, TRequest, TProtocol, TTData> : TypedMessageHandler<TCommunicator, TRequest, TProtocol>
    where TCommunicator : CommunicatorBase<TRequest, TProtocol, TypedDataString>
    where TRequest : Request<TRequest, TProtocol, TypedDataString>
    where TProtocol : class, IProtocol<TypedDataString>
  {
    public TypedMessageHandler(IDataSerializer dataSerializer, Action<TCommunicator, TTData> messageHandler) : base (dataSerializer)
    {
      MessageHandler = messageHandler;
    }

    public Action<TCommunicator, TTData> MessageHandler { get; }

    public override object UserHandler => MessageHandler;

    public override void InvokeMessageHandler(TCommunicator communicator, Message<TypedDataString> message)
    {
      var instance = DataSerializer.Deserialize<TTData>(message.Data.DataString);
      if (instance == null)
      {
        throw new InvalidOperationException($"Could not create instance of type {typeof(TTData)}");
      }
      MessageHandler.Invoke(communicator, instance);
    }
  }

  /// <summary>
  /// Internal helper to deserialize received requests from the protocol layer and
  /// forwards them to the properly typed request handler.
  /// </summary>
  public abstract class TypedRequestHandler<TCommunicator, TRequest, TProtocol> : TypedReceiveHandler
    where TCommunicator : CommunicatorBase<TRequest, TProtocol, TypedDataString>
    where TRequest : Request<TRequest, TProtocol, TypedDataString>
    where TProtocol : class, IProtocol<TypedDataString>
  {
    protected TypedRequestHandler(IDataSerializer dataSerializer) : base (dataSerializer) { }

    public abstract void InvokeRequestHandler(TCommunicator communicator, TypedRequestInternal request);
  }

  /// <summary>
  /// Internal helper to deserialize received requests from the protocol layer and
  /// forwards them to the properly typed request handler.
  /// </summary>
  public class TypedRequestHandler<TCommunicator, TRequest, TProtocol, TTData, TTRsp> : TypedRequestHandler<TCommunicator, TRequest, TProtocol>
    where TCommunicator : CommunicatorBase<TRequest, TProtocol, TypedDataString>
    where TRequest : Request<TRequest, TProtocol, TypedDataString>
    where TProtocol : class, IProtocol<TypedDataString>
    where TTData : IRequest<TTRsp>
  {
    public TypedRequestHandler(IDataSerializer dataSerializer, Action<TCommunicator, TypedRequest<TTData, TTRsp>> requestHandler) : base(dataSerializer)
    {
      RequestHandler = requestHandler;
    }

    public Action<TCommunicator, TypedRequest<TTData, TTRsp>> RequestHandler { get; }

    public override object UserHandler => RequestHandler;

    public override void InvokeRequestHandler(TCommunicator communicator, TypedRequestInternal request)
    {
      var instance = DataSerializer.Deserialize<TTData>(request.Data.DataString);
      if (instance == null)
      {
        throw new InvalidOperationException($"Could not create instance of type {typeof(TTData)}");
      }
      var deserializedRequest = new TypedRequest<TTData, TTRsp>(DataSerializer, request, instance);
      RequestHandler.Invoke(communicator, deserializedRequest);
    }
  }
}
