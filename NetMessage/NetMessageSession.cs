using NetMessage.Base;
using System.Threading.Tasks;

namespace NetMessage
{
  public class NetMessageSession : SessionBase<NetMessageServer, NetMessageSession, TypedRequestInternal, TypedProtocol, TypedPayload>
  {
    private IPayloadSerializer? _payloadConverter;

    /// <summary>
    /// Converts the message to its raw format and sends it to the remote socket.
    /// Exceptions during conversion are thrown synchronously. The asynchronous send task returns
    /// the number of bytes sent if successful, otherwise it completes with an invalid socket error.
    /// </summary>
    public Task<int> SendMessageAsync(object message)
    {
      string typeId = message.GetType().FullName;
      string serialized = _payloadConverter!.Serialize(message);
      return SendMessageInternalAsync(new TypedPayload(typeId, serialized));
    }

    // TODO: return Task<TRsp?> here (requires language version 9.0+)
    public async Task<TTRsp> SendRequestAsync<TTRsp>(IRequest<TTRsp> request)
    {
      string typeId = request.GetType().FullName;
      string serialized = _payloadConverter!.Serialize(request);
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

    internal void Init(IPayloadSerializer payloadConverter)
    {
      _payloadConverter = payloadConverter;
    }
  }
}
