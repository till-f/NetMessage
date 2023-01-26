﻿using NetMessage.Base;
using System;
using System.Threading.Tasks;

namespace NetMessage
{
  public class NetMessageClient : ClientBase<NetMessageClient, TypedRequestInternal, TypedProtocol, TypedPayload>
  {
    private readonly TypedReceiver<NetMessageClient, TypedRequestInternal, TypedProtocol> _receiver;
    private readonly IPayloadSerializer _payloadConverter;

    public NetMessageClient() : this(new XmlPayloadSerializer())
    {
    }

    public NetMessageClient(IPayloadSerializer payloadConverter)
    {
      _payloadConverter = payloadConverter;
      _receiver = new TypedReceiver<NetMessageClient, TypedRequestInternal, TypedProtocol>(_payloadConverter);

      MessageReceived += _receiver.NotifyMessageReceived;
      RequestReceived += _receiver.NotifyRequestReceived;
    }

    public void AddMessageHandler<TTPld>(Action<NetMessageClient, TTPld> messageHandler)
    {
      _receiver.AddMessageHandler(messageHandler);
    }

    public void RemoveMessageHandler<TTPld>(Action<NetMessageClient, TTPld> messageHandler)
    {
      _receiver.RemoveMessageHandler(messageHandler);
    }

    public void AddRequestHandler<TTPld, TTRsp>(Action<NetMessageClient, TypedRequest<TTPld, TTRsp>> requestHandler)
      where TTPld : IRequest<TTRsp>
    {
      _receiver.AddRequestHandler(requestHandler);
    }

    public void RemoveRequestHandler<TTPld, TTRsp>(Action<NetMessageClient, TypedRequest<TTPld, TTRsp>> requestHandler)
      where TTPld : IRequest<TTRsp>
    {
      _receiver.RemoveRequestHandler(requestHandler);
    }

    /// <summary>
    /// Converts the message to its raw format and sends it to the remote socket.
    /// Exceptions during conversion are thrown synchronously. The asynchronous send task returns
    /// the number of bytes sent if successful, otherwise it completes with an invalid socket error.
    /// </summary>
    public Task<int> SendMessageAsync(object message)
    {
      string typeId = message.GetType().FullName;
      string serialized = _payloadConverter.Serialize(message);
      return SendMessageInternalAsync(new TypedPayload(typeId, serialized));
    }

    /// <summary>
    /// Converts the request to its raw format and awaits the corresponding response which is returned asynchronously.
    /// The asynchronous task ends faulted if sending was not successful or a timeout occurs when waiting for the response.
    /// Exceptions during conversion are thrown synchronously.
    /// </summary>
    // TODO: return Task<TRsp?> here (requires language version 9.0+)
    public async Task<TTRsp> SendRequestAsync<TTRsp>(IRequest<TTRsp> request)
    {
      string typeId = request.GetType().FullName;
      string serialized = _payloadConverter.Serialize(request);
      var result = await SendRequestInternalAsync(new TypedPayload(typeId, serialized));

      // result is null if task was cancelled normally (abnormal cancellations or failures will throw)
      if (result == null)
      {
#pragma warning disable CS8603
        return default;
#pragma warning restore CS8603
      }

      return _payloadConverter.Deserialize<TTRsp>(result.Payload.ActualPayload);
    }

    protected override TypedProtocol CreateProtocolBuffer()
    {
      return new TypedProtocol
      {
        Encoding = _payloadConverter.ProtocolEncoding,
        Terminator = _payloadConverter.ProtocolTerminator
      };
    }
  }
}
