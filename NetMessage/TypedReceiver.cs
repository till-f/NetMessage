using NetMessage.Base;
using NetMessage.Base.Packets;
using System;
using System.Collections.Generic;

namespace NetMessage
{
  /// <summary>
  /// Internal helper that supports the registration of message handlers and request handlers in a client or a server.
  /// The <see cref="NotifyMessageReceived"/> and <see cref="NotifyRequestReceived"/> methods must be called when a 
  /// message or request is received from the protocol layer. The deserialized object will then be forwarded to the
  /// appropriate handler.
  /// </summary>
  public class TypedReceiver<TCommunicator, TRequest, TProtocol>
    where TCommunicator : CommunicatorBase<TRequest, TProtocol, TypedDataString>
    where TRequest : Request<TRequest, TProtocol, TypedDataString>
    where TProtocol : class, IProtocol<TypedDataString>
  {
    private readonly Dictionary<string, List<TypedMessageHandler<TCommunicator, TRequest, TProtocol>>> _messageHandlers = new Dictionary<string, List<TypedMessageHandler<TCommunicator, TRequest, TProtocol>>>();
    private readonly Dictionary<string, List<TypedRequestHandler<TCommunicator, TRequest, TProtocol>>> _requestHandlers = new Dictionary<string, List<TypedRequestHandler<TCommunicator, TRequest, TProtocol>>>();
    private readonly IDataSerializer _dataSerializer;

    public TypedReceiver(IDataSerializer dataSerializer)
    {
      _dataSerializer = dataSerializer;
    }

    public void AddMessageHandler<TTData>(Action<TCommunicator, TTData> messageHandler)
    {
      var internalMessageHandler = new TypedMessageHandler<TCommunicator, TRequest, TProtocol, TTData>(_dataSerializer, messageHandler);
      AddHandler(_messageHandlers, typeof(TTData), internalMessageHandler);
    }

    public void RemoveMessageHandler<TTData>(Action<TCommunicator, TTData> messageHandler)
    {
      RemoveHandler(_messageHandlers, typeof(TTData), messageHandler);
    }

    public void AddRequestHandler<TTData, TTRsp>(Action<TCommunicator, TypedRequest<TTData, TTRsp>> requestHandler)
      where TTData : IRequest<TTRsp>
    {
      var internalRequestHandler = new TypedRequestHandler<TCommunicator, TRequest, TProtocol, TTData, TTRsp>(_dataSerializer, requestHandler);
      AddHandler(_requestHandlers, typeof(TTData), internalRequestHandler);
    }

    public void RemoveRequestHandler<TTData, TTRsp>(Action<TCommunicator, TypedRequest<TTData, TTRsp>> requestHandler)
      where TTData : IRequest<TTRsp>
    {
      RemoveHandler(_requestHandlers, typeof(TTData), requestHandler);
    }

    internal void NotifyMessageReceived(TCommunicator communicator, Message<TypedDataString> message)
    {
      var typeId = message.Data.TypeId;

      if (_messageHandlers.ContainsKey(typeId))
      {
        _messageHandlers[typeId].ForEach(lst => lst.InvokeMessageHandler(communicator, message));
      }
    }

    internal void NotifyRequestReceived(TCommunicator communicator, TypedRequestInternal request)
    {
      var typeId = request.Data.TypeId;

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
