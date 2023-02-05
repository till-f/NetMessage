using NetMessage.Base.Packets;
using System.Collections.Generic;

namespace NetMessage.Base
{
  public interface IProtocol<TData>
  {
    /// <summary>
    /// Processes the received raw data and returns all received messages,
    /// requests and responses together with the corresponding request and
    /// response id, if applicable.
    /// 
    /// If the protocol does not support the request/response mechanism,
    /// the client and server implementation must not expose this method:
    /// <see cref="CommunicatorBase{TRequest, TProtocol, TData}.SendRequestInternalAsync(TData)"/>
    /// 
    /// The implementation must use an internal buffer to store the data
    /// that cannot be processed yet (e.g. an incompletely received packets)
    /// so that is ready to be consued when the next chunk of data arrives.
    /// </summary>
    IList<IPacket<TData>> FromRaw(byte[] rawData);

    /// <summary>
    /// Converts a message object to the raw message format.
    /// </summary>
    byte[] ToRawMessage(TData data);

    /// <summary>
    /// Converts a request object to the raw request format.
    /// 
    /// If the protocol does not support the request/response mechanism,
    /// the client and server implementation must not expose this method:
    /// <see cref="CommunicatorBase{TRequest, TProtocol, TData}.SendRequestInternalAsync(TData)"/>
    /// </summary>
    byte[] ToRawRequest(TData data, int requestId);

    /// <summary>
    /// Converts a response object to the raw response format.
    /// 
    /// If the protocol does not support the request/response mechanism,
    /// the client and server implementation must not expose this method:
    /// <see cref="CommunicatorBase{TRequest, TProtocol, TData}.SendRequestInternalAsync(TData)"/>
    /// </summary>
    byte[] ToRawResponse(TData data, int responseId);

    /// <summary>
    /// Returns the heartbeat packet. If the protocol does not support
    /// heartbeats, this method should throw and the client should set
    /// <see cref="ClientBase{TClient, TRequest, TProtocol, TData}.HeartbeatInterval"/> to infinite.
    /// </summary>
    byte[] HeartbeatPacket { get; }
  }
}
