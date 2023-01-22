using NetMessage.Base;
using System.Threading.Tasks;

namespace NetMessage
{
  public class NetMessageSession : SessionBase<NetMessageServer, NetMessageSession, TypedRequestInternal, TypedProtocol, TypedPayload>
  {
    private IPayloadSerializer _payloadConverter;

    public Task<bool> SendMessageAsync(object message)
    {
      string typeId = message.GetType().FullName;
      string serialized = _payloadConverter.Serialize(message);
      return SendMessageInternalAsync(new TypedPayload(typeId, serialized));
    }

    public async Task<TRsp> SendRequestAsync<TRsp>(IRequest<TRsp> request)
    {
      string typeId = request.GetType().FullName;
      string serialized = _payloadConverter.Serialize(request);
      var result = await SendRequestInternalAsync(new TypedPayload(typeId, serialized));

      // result is null if task was cancelled normally (abnormal cancellations or failures will throw)
      if (result == null)
      {
        return default(TRsp);
      }

      return _payloadConverter.Deserialize<TRsp>(result.Payload.ActualPayload);
    }

    internal void Init(IPayloadSerializer payloadConverter)
    {
      _payloadConverter = payloadConverter;
    }
  }
}
