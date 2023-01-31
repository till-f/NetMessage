using NetMessage.Base;
using System.Threading.Tasks;

namespace NetMessage
{
  public class NetMessageSession : SessionBase<NetMessageServer, NetMessageSession, TypedRequestInternal, TypedProtocol, TypedDataString>
  {
    private IDataSerializer? _dataSerializer;

    /// <summary>
    /// Converts the message to its raw format and sends it to the remote socket.
    /// Exceptions during conversion are thrown synchronously. The asynchronous send task returns
    /// the number of bytes sent if successful, otherwise it completes with an invalid socket error.
    /// </summary>
    public Task<int> SendMessageAsync(object message)
    {
      string typeId = message.GetType().FullName;
      string serialized = _dataSerializer!.Serialize(message);
      return SendMessageInternalAsync(new TypedDataString(typeId, serialized));
    }

    // TODO: return Task<TRsp?> here (requires language version 9.0+)
    public async Task<TTRsp> SendRequestAsync<TTRsp>(IRequest<TTRsp> request)
    {
      string typeId = request.GetType().FullName;
      string serialized = _dataSerializer!.Serialize(request);
      var result = await SendRequestInternalAsync(new TypedDataString(typeId, serialized));

      // result is null if task was cancelled normally (abnormal cancellations or failures will throw)
      if (result == null)
      {
#pragma warning disable CS8603
        return default;
#pragma warning restore CS8603
      }

      return _dataSerializer.Deserialize<TTRsp>(result.Data.DataString);
    }

    public void Disconnect()
    {
      Close(true);
    }

    internal void Init(IDataSerializer dataSerializer)
    {
      _dataSerializer = dataSerializer;
    }
  }
}
