using System.Net.Sockets;

namespace NetMessage.Base
{
  public enum ECloseReason
  {
    /// <summary>
    /// The connection was closed after disconnect handshake completed successfully.
    /// The disconnection may have been initiated by either endpoint.
    /// </summary>
    GracefulShutdown,

    /// <summary>
    /// The connection was closed because a disconnect request was not acknowledged in 
    /// time by the remote endpoint.
    /// </summary>
    DisconnectTimeout,

    /// <summary>
    /// The connection was closed because the heartbeat timeout exceeded
    /// (failed to send heartbeat message, or it was not received in time).
    /// </summary>
    ConnectionLost,

    /// <summary>
    /// The connection was closed because of a SocketException.
    /// Further details are provided by <see cref="SessionClosedArgs.SocketException"/>, see <see cref="SocketException.SocketErrorCode"/>.
    /// </summary>
    SocketException,

    /// <summary>
    /// The connection was closed because Dispose() was called on the communicator object.
    /// </summary>
    ObjectDisposed,

    /// <summary>
    /// The connection was closed because an exception occured while accepting the connection.
    /// This can only occur on the server; the client would see a SocketException.
    /// </summary>
    AcceptError
  }

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
}
