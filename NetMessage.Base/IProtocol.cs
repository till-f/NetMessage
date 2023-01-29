using NetMessage.Base.Message;
using System.Collections.Generic;

namespace NetMessage.Base
{
  public interface IProtocol<TPld>
  {
    /// <summary>
    /// Processes the received raw data and returns all received messages,
    /// requests and responses together with the corresponding request and
    /// response id, if applicable.
    /// 
    /// If the protocol does not support the request/response mechanism,
    /// the client and server implementation must not expose this method:
    /// <see cref="CommunicatorBase{TRequest, TProtocol, TPld}.SendRequestInternalAsync(TPld)"/>
    /// 
    /// The implementation must use an internal buffer to store the data
    /// that cannot be processed yet (e.g. an incompletely received packets)
    /// so that is ready to be consued when the next chunk of data arrives.
    /// </summary>
    IList<IPacket<TPld>> FromRaw(byte[] rawData);

    /// <summary>
    /// Converts a message object to the raw message format.
    /// </summary>
    byte[] ToRawMessage(TPld payload);

    /// <summary>
    /// Converts a request object to the raw request format.
    /// 
    /// If the protocol does not support the request/response mechanism,
    /// the client and server implementation must not expose this method:
    /// <see cref="CommunicatorBase{TRequest, TProtocol, TPld}.SendRequestInternalAsync(TPld)"/>
    /// </summary>
    byte[] ToRawRequest(TPld payload, int requestId);

    /// <summary>
    /// Converts a response object to the raw response format.
    /// 
    /// If the protocol does not support the request/response mechanism,
    /// the client and server implementation must not expose this method:
    /// <see cref="CommunicatorBase{TRequest, TProtocol, TPld}.SendRequestInternalAsync(TPld)"/>
    /// </summary>
    byte[] ToRawResponse(TPld payload, int responseId);
  }
}
