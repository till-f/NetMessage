using NetMessage.Base.Message;
using System.Threading.Tasks;

namespace NetMessage
{
  /// <summary>
  /// Holds the final deserialized request that was received on the <see cref="TypedProtocol"/>.
  /// It also takes care of deserializing the response and sending it over the network.
  /// Internally it still holds the the original request object that was received from the protocol
  /// layer (<see cref="TypedRequestInternal"/>).
  /// </summary>
  public class TypedRequest<TTPld, TTRsp>
    where TTPld : IRequest<TTRsp>
  {
    private readonly IPayloadSerializer _payloadConverter;
    private readonly TypedRequestInternal _requestInternal;

    public TypedRequest(IPayloadSerializer payloadConverter, TypedRequestInternal requestInternal, TTPld request)
    {
      _payloadConverter = payloadConverter;
      _requestInternal = requestInternal;
      Request = request;
    }

    public TTPld Request { get; }

    public Task<bool> SendResponseAsync(TTRsp response)
    {
      string typeId = response.GetType().FullName;
      string serialized = _payloadConverter.Serialize(response);
      return _requestInternal.SendResponseAsync(new TypedPayload(typeId, serialized));
    }
  }


  /// <summary>
  /// Request for the <see cref="TypedProtocol"/>. Only used internally.
  /// See <see cref="TypedRequest{T}"/> for the corresponding class that is exposed to the user.
  /// </summary>
  public class TypedRequestInternal : Request<TypedRequestInternal, TypedProtocol, TypedPayload>
  {
    internal TypedRequestInternal(TypedPayload payload, int requestId) : base(payload, requestId)
    {
    }

    internal Task<bool> SendResponseAsync(TypedPayload response)
    {
      return SendResponseInternalAsync(response);
    }
  }
}
