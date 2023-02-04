using System;
using System.Net.Sockets;

namespace NetMessage.Base
{
  public static class SocketExtensions
  {
    /// <summary>
    /// Sets the KeepAlive time and interval for a TCP socket.
    /// </summary>
    /// <param name="socket">the socket</param>
    /// <param name="keepAliveTimeInms">specifies the timeout, in milliseconds, with no activity until the first keep-alive packet is sent. If zero, keep alive is disabled.</param>
    /// <param name="keepAliveIntervalInms">specifies the interval, in milliseconds, between when successive keep-alive packets are sent if no acknowledgement is received.</param>
    public static void SetTcpKeepAlive(this Socket socket, uint keepAliveTimeInms, uint keepAliveIntervalInms)
    {
      // The SIO_KEEPALIVE_VALS structure.
      // See https://learn.microsoft.com/en-us/windows/win32/winsock/sio-keepalive-vals for details.
      // struct tcp_keepalive {
      //   ULONG onoff;
      //   ULONG keepalivetime;
      //   ULONG keepaliveinterval;
      // };

      byte[] keepAliveStruct = new byte[12];
      BitConverter.GetBytes(keepAliveTimeInms).CopyTo(keepAliveStruct, 0); // non-zero value means active
      BitConverter.GetBytes(keepAliveTimeInms).CopyTo(keepAliveStruct, 4);
      BitConverter.GetBytes(keepAliveIntervalInms).CopyTo(keepAliveStruct, 8);

      // write SIO_VALS to Socket IOControl
      socket.IOControl(IOControlCode.KeepAliveValues, keepAliveStruct, null);
    }
  }
}
