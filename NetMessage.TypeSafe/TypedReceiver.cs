using NetMessage.Base;
using NetMessage.Base.Message;
using System;
using System.Collections.Generic;

namespace NetMessage.TypeSafe
{
  /// <summary>
  /// Internal helper that supports the registration of message handlers and request handlers in a client or a server.
  /// The <see cref="NotifyMessageReceived"/> and <see cref="NotifyRequestReceived"/> methods must be called when a 
  /// message or request is received from the protocol layer. The deserialized object will then be forwarded to the
  /// appropriate handler.
  /// </summary>
  public class TypedReceiver<TCommunicator, TRequest, TProtocol>
    where TCommunicator : CommunicatorBase<TRequest, TProtocol, TypedPayload>
    where TRequest : Request<TRequest, TProtocol, TypedPayload>
    where TProtocol : class, IProtocol<TypedPayload>
  {
    private readonly Dictionary<string, List<TypedMessageHandler<TCommunicator, TRequest, TProtocol>>> _messageHandlers = new Dictionary<string, List<TypedMessageHandler<TCommunicator, TRequest, TProtocol>>>();
    private readonly Dictionary<string, List<TypedRequestHandler<TCommunicator, TRequest, TProtocol>>> _requestHandlers = new Dictionary<string, List<TypedRequestHandler<TCommunicator, TRequest, TProtocol>>>();
    private readonly IPayloadSerializer _payloadConverter;

    public TypedReceiver(IPayloadSerializer payloadConverter)
    {
      _payloadConverter = payloadConverter;
    }

    public void AddMessageHandler<TTPld>(Action<TCommunicator, TTPld> messageHandler)
    {
      var internalMessageHandler = new TypedMessageHandler<TCommunicator, TRequest, TProtocol, TTPld>(_payloadConverter, messageHandler);
      AddHandler(_messageHandlers, typeof(TTPld), internalMessageHandler);
    }

    public void RemoveMessageHandler<TTPld>(Action<TCommunicator, TTPld> messageHandler)
    {
      RemoveHandler(_messageHandlers, typeof(TTPld), messageHandler);
    }

    public void AddRequestHandler<TTPld, TTRsp>(Action<TCommunicator, TypedRequest<TTPld, TTRsp>> requestHandler)
      where TTPld : IRequest<TTRsp>
    {
      var internalRequestHandler = new TypedRequestHandler<TCommunicator, TRequest, TProtocol, TTPld, TTRsp>(_payloadConverter, requestHandler);
      AddHandler(_requestHandlers, typeof(TTPld), internalRequestHandler);
    }

    public void RemoveRequestHandler<TTPld, TTRsp>(Action<TCommunicator, TypedRequest<TTPld, TTRsp>> requestHandler)
      where TTPld : IRequest<TTRsp>
    {
      RemoveHandler(_requestHandlers, typeof(TTPld), requestHandler);
    }

    internal void NotifyMessageReceived(TCommunicator communicator, Message<TypedPayload> message)
    {
      var typeId = message.Payload.TypeId;

      if (_messageHandlers.ContainsKey(typeId))
      {
        _messageHandlers[typeId].ForEach(lst => lst.InvokeMessageHandler(communicator, message));
      }
    }

    internal void NotifyRequestReceived(TCommunicator communicator, TypedRequestInternal request)
    {
      var typeId = request.Payload.TypeId;

      if (_requestHandlers.ContainsKey(typeId))
      {
        _requestHandlers[typeId].ForEach(lst => lst.InvokeRequestHandler(communicator, request));
      }
    }

    private void AddHandler<TUser>(Dictionary<string, List<TUser>> handlers, Type type, TUser handler)
    {
      var typeId = type.FullName;
      if (typeId == null)
      {
        // should be impossible
        throw new InvalidOperationException($"No type id for {type}");
      }

      lock (handlers)
      {
        List<TUser> list;
        if (handlers.ContainsKey(typeId))
        {
          list = handlers[typeId];
        }
        else
        {
          list = new List<TUser>();
          handlers.Add(typeId, list);
        }

        list.Add(handler);
      }
    }

    private void RemoveHandler<TInternal, TUser>(Dictionary<string, List<TInternal>> handlers, Type type, TUser handler)
      where TInternal : TypedReceiveHandler
    {
      var typeId = type.FullName;
      if (typeId == null)
      {
        // should be impossible
        throw new InvalidOperationException($"No type id for {type}");
      }

      lock (handlers)
      {
        if (!handlers.ContainsKey(typeId))
        {
          return;
        }

        List<TInternal> list = handlers[typeId];
        list.RemoveAll(h => h.UserHandler.Equals(handler));
      }
    }
  }
}
