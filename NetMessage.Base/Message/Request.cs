using System;
using System.Threading.Tasks;

namespace NetMessage.Base.Message
{
  public abstract class Request<TRequest, TProtocol, TPld> : IPacket<TPld>
    where TRequest : Request<TRequest, TProtocol, TPld>
    where TProtocol : class, IProtocol<TPld>
  {
    private Func<TPld, int, byte[]>? _toRawResponse;
    private Func<byte[], Task<int>>? _sendRawData;
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
    /// Converts the response to its raw format and sends it to the remote socket.
    /// Exceptions during conversion are thrown synchronously. The asynchronous send task returns
    /// the number of bytes sent if successful, otherwise it completes with an invalid socket error.
    /// 
    /// Protected because concrete implementations may prefer that this method is not exposed.
    /// </summary>
    protected Task<int> SendResponseInternalAsync(TPld response)
    {
      try
      {
        var rawData = _toRawResponse!(response, RequestId);
        return _sendRawData!(rawData);
      }
      catch (Exception ex)
      {
        _notifyError!($"{ex.GetType().Name} while converting to raw format", ex);
        return Task.FromResult(-1);
      }
    }

    public void SetContext(Func<TPld, int, byte[]> toRawResponse, Func<byte[], Task<int>> sendRawData, Action<string, Exception?> notifyError)
    {
      _toRawResponse = toRawResponse;
      _sendRawData = sendRawData;
      _notifyError = notifyError;
    }
  }
}
