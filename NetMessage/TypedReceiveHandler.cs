using NetMessage.Base;
using NetMessage.Base.Message;
using System;

namespace NetMessage
{
  public abstract class TypedReceiveHandler
  {
    protected TypedReceiveHandler(IPayloadSerializer payloadConverter)
    {
      PayloadConverter = payloadConverter;
    }

    protected IPayloadSerializer PayloadConverter { get; }

    public abstract object UserHandler { get; }
  }

  /// <summary>
  /// Internal helper to deserialize received messages from the protocol layer and
  /// forwards them to the properly typed message handler.
  /// </summary>
  public abstract class TypedMessageHandler<TCommunicator, TRequest, TProtocol> : TypedReceiveHandler
    where TCommunicator : CommunicatorBase<TRequest, TProtocol, TypedPayload>
    where TRequest : Request<TRequest, TProtocol, TypedPayload>
    where TProtocol : class, IProtocol<TypedPayload>
  {
    protected TypedMessageHandler(IPayloadSerializer payloadConverter) : base(payloadConverter) { }

    public abstract void InvokeMessageHandler(TCommunicator communicator, Message<TypedPayload> message);
  }

  /// <summary>
  /// Internal helper to deserialize received messages from the protocol layer and
  /// forwards them to the properly typed message handler.
  /// </summary>
  public class TypedMessageHandler<TCommunicator, TRequest, TProtocol, TTPld> : TypedMessageHandler<TCommunicator, TRequest, TProtocol>
    where TCommunicator : CommunicatorBase<TRequest, TProtocol, TypedPayload>
    where TRequest : Request<TRequest, TProtocol, TypedPayload>
    where TProtocol : class, IProtocol<TypedPayload>
  {
    public TypedMessageHandler(IPayloadSerializer payloadConverter, Action<TCommunicator, TTPld> messageHandler) : base (payloadConverter)
    {
      MessageHandler = messageHandler;
    }

    public Action<TCommunicator, TTPld> MessageHandler { get; }

    public override object UserHandler => MessageHandler;

    public override void InvokeMessageHandler(TCommunicator communicator, Message<TypedPayload> message)
    {
      var instance = PayloadConverter.Deserialize<TTPld>(message.Payload.ActualPayload);
      if (instance == null)
      {
        throw new InvalidOperationException($"Could not create instance of type {typeof(TTPld)}");
      }
      MessageHandler.Invoke(communicator, instance);
    }
  }

  /// <summary>
  /// Internal helper to deserialize received requests from the protocol layer and
  /// forwards them to the properly typed request handler.
  /// </summary>
  public abstract class TypedRequestHandler<TCommunicator, TRequest, TProtocol> : TypedReceiveHandler
    where TCommunicator : CommunicatorBase<TRequest, TProtocol, TypedPayload>
    where TRequest : Request<TRequest, TProtocol, TypedPayload>
    where TProtocol : class, IProtocol<TypedPayload>
  {
    protected TypedRequestHandler(IPayloadSerializer payloadConverter) : base (payloadConverter) { }

    public abstract void InvokeRequestHandler(TCommunicator communicator, TypedRequestInternal request);
  }

  /// <summary>
  /// Internal helper to deserialize received requests from the protocol layer and
  /// forwards them to the properly typed request handler.
  /// </summary>
  public class TypedRequestHandler<TCommunicator, TRequest, TProtocol, TTPld, TTRsp> : TypedRequestHandler<TCommunicator, TRequest, TProtocol>
    where TCommunicator : CommunicatorBase<TRequest, TProtocol, TypedPayload>
    where TRequest : Request<TRequest, TProtocol, TypedPayload>
    where TProtocol : class, IProtocol<TypedPayload>
    where TTPld : IRequest<TTRsp>
  {
    public TypedRequestHandler(IPayloadSerializer payloadConverter, Action<TCommunicator, TypedRequest<TTPld, TTRsp>> requestHandler) : base(payloadConverter)
    {
      RequestHandler = requestHandler;
    }

    public Action<TCommunicator, TypedRequest<TTPld, TTRsp>> RequestHandler { get; }

    public override object UserHandler => RequestHandler;

    public override void InvokeRequestHandler(TCommunicator communicator, TypedRequestInternal request)
    {
      var instance = PayloadConverter.Deserialize<TTPld>(request.Payload.ActualPayload);
      if (instance == null)
      {
        throw new InvalidOperationException($"Could not create instance of type {typeof(TTPld)}");
      }
      var deserializedRequest = new TypedRequest<TTPld, TTRsp>(PayloadConverter, request, instance);
      RequestHandler.Invoke(communicator, deserializedRequest);
    }
  }
}
