using System;
using System.Threading.Tasks;

namespace NetMessage.Base.Message
{
  public abstract class Request<TRequest, TProtocol, TPld> : IMessage<TPld>
    where TRequest : Request<TRequest, TProtocol, TPld>
    where TProtocol : class, IProtocol<TPld>
  {
    private Func<TPld, int, byte[]>? _toRawResponse;
    private Func<byte[], Task<bool>>? _sendRawData;
    private Action<string, Exception?>? _notifyError;

    /// <summary>
    /// Container for a response (in contrast to message and request)
    /// </summary>
    protected Request(TPld payload, int requestId)
    {
      Payload = payload;
      RequestId = requestId;
    }

    public TPld Payload { get; }

    public int RequestId { get; }

    /// <summary>
    /// Sends response to the remote socket.
    /// Protected because concrete implementations may prefer that this method is not exposed.
    /// </summary>
    protected Task<bool> SendResponseInternalAsync(TPld response)
    {
      try
      {
        var rawData = _toRawResponse!(response, RequestId);
        return _sendRawData!(rawData);
      }
      catch (Exception ex)
      {
        _notifyError!($"{ex.GetType().Name} while converting to raw format", ex);
        return Task.FromResult(false);
      }
    }

    public void SetContext(Func<TPld, int, byte[]> toRawResponse, Func<byte[], Task<bool>> sendRawData, Action<string, Exception?> notifyError)
    {
      _toRawResponse = toRawResponse;
      _sendRawData = sendRawData;
      _notifyError = notifyError;
    }
  }
}
