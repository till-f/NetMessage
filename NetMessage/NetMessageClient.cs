using NetMessage.Base;
using System;
using System.Threading.Tasks;

namespace NetMessage
{
  public class NetMessageClient : ClientBase<NetMessageClient, TypedRequestInternal, TypedProtocol, TypedDataString>
  {
    private readonly TypedReceiver<NetMessageClient, TypedRequestInternal, TypedProtocol> _receiver;
    private readonly IDataSerializer _dataSerializer;

    public NetMessageClient() : this(new XmlDataSerializer())
    {
    }

    public NetMessageClient(IDataSerializer dataSerializer)
    {
      _dataSerializer = dataSerializer;
      _receiver = new TypedReceiver<NetMessageClient, TypedRequestInternal, TypedProtocol>(_dataSerializer);

      MessageReceived += _receiver.NotifyMessageReceived;
      RequestReceived += _receiver.NotifyRequestReceived;
    }

    public void AddMessageHandler<TTData>(Action<NetMessageClient, TTData> messageHandler)
    {
      _receiver.AddMessageHandler(messageHandler);
    }

    public void RemoveMessageHandler<TTData>(Action<NetMessageClient, TTData> messageHandler)
    {
      _receiver.RemoveMessageHandler(messageHandler);
    }

    public void AddRequestHandler<TTData, TTRsp>(Action<NetMessageClient, TypedRequest<TTData, TTRsp>> requestHandler)
      where TTData : IRequest<TTRsp>
    {
      _receiver.AddRequestHandler(requestHandler);
    }

    public void RemoveRequestHandler<TTData, TTRsp>(Action<NetMessageClient, TypedRequest<TTData, TTRsp>> requestHandler)
      where TTData : IRequest<TTRsp>
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
      string serialized = _dataSerializer.Serialize(message);
      return SendMessageInternalAsync(new TypedDataString(typeId, serialized));
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
      string serialized = _dataSerializer.Serialize(request);
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

    protected override TypedProtocol CreateProtocolBuffer()
    {
      return new TypedProtocol
      {
        Encoding = _dataSerializer.ProtocolEncoding,
        Terminator = _dataSerializer.ProtocolTerminator
      };
    }
  }
}
