using System.Net.Sockets;

namespace NetMessage.Base
{
  public class SessionClosedArgs
  {
    public SessionClosedArgs(ECloseReason reason, SocketException? socketException = null)
    {
      Reason = reason;
      SocketException = socketException;
    }

    public ECloseReason Reason { get; }

    public SocketException? SocketException { get; }
  }

  public enum ECloseReason
  {
    /// <summary>
    /// Connection was closed after disconnect handshake was performed. Disconnect was either initiated by this or the remote endpoint.
    /// </summary>
    GracefulShutdown,

    /// <summary>
    /// Connection was closed because disconnect handshake was not acknowledged in time by the remote endpoint.
    /// </summary>
    DisconnectTimeout,

    /// <summary>
    /// Connection was closed because heartbeat timeout exceeded (failed to send heartbeat message, or it was not received in time).
    /// </summary>
    ConnectionLost,

    /// <summary>
    /// Connection was closed because of a SocketException. Further details are provided by <see cref="SessionClosedArgs.SocketException"/>, see <see cref="SocketException.SocketErrorCode"/>.
    /// </summary>
    SocketException,

    /// <summary>
    /// Connection was closed because Dispose() was called on the communicator object.
    /// </summary>
    ObjectDisposed,

    /// <summary>
    /// Connection was closed because an exception occured while accepting the connection.
    /// </summary>
    AcceptError
  }
}
