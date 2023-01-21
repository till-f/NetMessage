using NetMessage.Base.Message;
using System.Collections.Generic;

namespace NetMessage.Base
{
  public interface IProtocol<TPld>
  {
    /// <summary>
    /// Process the received raw data and return all received messages
    /// together with the corresponding meta data.
    /// 
    /// If the protocol does not support the request/response mechanism,
    /// the corresponding Client and Server implementations must not use
    /// this method:
    /// <see cref="CommunicatorBase{TRequest, TProtocol, TPld}.SendRequestInternalAsync(TPld)"/>
    /// 
    /// The implementation should use an internal buffer to store the
    /// data that cannot be processed yet (e.g. incompletely received
    /// messages) and use it when the method is called the next time.
    /// </summary>
    IList<IMessage<TPld>> FromRaw(byte[] rawData);

    /// <summary>
    /// Convert the message to its raw format.
    /// </summary>
    byte[] ToRawMessage(TPld payload);

    /// <summary>
    /// Convert the message to the raw request format.
    /// If the protocol does not support the request/response mechanism,
    /// the corresponding Client and Server implementations must not use
    /// this method:
    /// <see cref="CommunicatorBase{TRequest, TProtocol, TPld}.SendRequestInternalAsync(TPld)"/>
    /// </summary>
    byte[] ToRawRequest(TPld payload, int responseId);

    /// <summary>
    /// Convert the message to the raw response format.
    /// If the protocol does not support the request/response mechanism,
    /// the corresponding Client and Server implementations must not use
    /// this method:
    /// <see cref="CommunicatorBase{TRequest, TProtocol, TPld}.SendRequestInternalAsync(TPld)"/>
    /// </summary>
    byte[] ToRawResponse(TPld payload, int responseId);
  }
}
