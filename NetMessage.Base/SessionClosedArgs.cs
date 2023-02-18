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
    /// Connection was closed because of successful disconnect handshake.
    /// </summary>
    GracefulShutdown,

    /// <summary>
    /// Connection was closed because disconnect was initiated but not acknowledged in time by remote endpoint.
    /// </summary>
    DisconnectTimeout,

    /// <summary>
    /// Connection was closed because heartbeat timeout exceeded.
    /// </summary>
    ConnectionLost,

    /// <summary>
    /// Connection was closed because of a SocketException. For details, see <see cref="SocketException.SocketErrorCode"/>.
    /// </summary>
    SocketException,

    /// <summary>
    /// Connection was closed because the Dispose() method was called on the communicator object.
    /// </summary>
    ObjectDisposed,

    /// <summary>
    /// Connection was closed because an exception occured while accepting the connection.
    /// </summary>
    AcceptError
  }
}
