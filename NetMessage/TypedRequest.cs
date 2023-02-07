using System;
using NetMessage.Base.Packets;
using System.Threading.Tasks;

namespace NetMessage
{
  /// <summary>
  /// Holds the final deserialized request that was received on the <see cref="TypedProtocol"/>.
  /// It also takes care of deserializing the response and sending it over the network.
  /// Internally it still holds the the original request object that was received from the protocol
  /// layer (<see cref="TypedRequestInternal"/>).
  /// </summary>
  public class TypedRequest<TTData, TTRsp>
    where TTData : IRequest<TTRsp>
  {
    private readonly IDataSerializer _dataSerializer;
    private readonly TypedRequestInternal _requestInternal;

    public TypedRequest(IDataSerializer dataSerializer, TypedRequestInternal requestInternal, TTData request)
    {
      _dataSerializer = dataSerializer;
      _requestInternal = requestInternal;
      Request = request;
    }

    public TTData Request { get; }

    /// <summary>
    /// Converts the response to its raw format and sends it to the remote socket.
    /// Exceptions during conversion are thrown synchronously. The asynchronous send task returns
    /// the number of bytes sent if successful, otherwise it completes with an invalid socket error.
    /// </summary>
    public Task<int> SendResponseAsync(TTRsp response)
    {
      if (response == null) throw new ArgumentNullException(nameof(response));
      
      var typeId = response.GetType().FullName;
      var serialized = _dataSerializer.Serialize(response);
      return _requestInternal.SendResponseAsync(new TypedDataString(typeId, serialized));
    }
  }


  /// <summary>
  /// Request for the <see cref="TypedProtocol"/>. Only used internally.
  /// See <see cref="TypedRequest{TTData, TTRsp}"/> for the corresponding class that is exposed to the user.
  /// </summary>
  public class TypedRequestInternal : Request<TypedRequestInternal, TypedProtocol, TypedDataString>
  {
    internal TypedRequestInternal(TypedDataString typedDataString, int requestId) : base(typedDataString, requestId)
    {
    }

    internal Task<int> SendResponseAsync(TypedDataString typedDataString)
    {
      return SendResponseInternalAsync(typedDataString);
    }
  }
}
