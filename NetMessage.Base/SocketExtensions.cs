using System;
using System.Net.Sockets;

namespace NetMessage.Base
{
  public static class SocketExtensions
  {
    /// <summary>
    /// Sets the KeepAlive time and interval for a TCP socket.
    /// </summary>
    /// <param name="socket">The socket</param>
    /// <param name="keepAliveTime">Specifies the timeout with no activity until the first keep-alive packet is sent. A value smaller or equal zero disables keep alive.</param>
    /// <param name="keepAliveInterval">Specifies the interval between when successive keep-alive packets are sent if no acknowledgement is received.</param>
    /// 
    public static void SetTcpKeepAlive(this Socket socket, TimeSpan keepAliveTime, TimeSpan keepAliveInterval)
    {
      // The SIO_KEEPALIVE_VALS structure.
      // See https://learn.microsoft.com/en-us/windows/win32/winsock/sio-keepalive-vals for details.
      // struct tcp_keepalive {
      //   ULONG onoff;
      //   ULONG keepalivetime;
      //   ULONG keepaliveinterval;
      // };

      uint keepAliveTimeInms = keepAliveTime.IsInfinite() ? 0 : (uint) keepAliveTime.TotalMilliseconds;
      uint keepAliveIntervalInms = (uint) keepAliveInterval.TotalMilliseconds;

      byte[] keepAliveStruct = new byte[12];
      BitConverter.GetBytes(keepAliveTimeInms).CopyTo(keepAliveStruct, 0); // non-zero value means "on", zero means "off"
      BitConverter.GetBytes(keepAliveTimeInms).CopyTo(keepAliveStruct, 4);
      BitConverter.GetBytes(keepAliveIntervalInms).CopyTo(keepAliveStruct, 8);

      socket.IOControl(IOControlCode.KeepAliveValues, keepAliveStruct, null);
    }

    public static bool IsInfinite(this TimeSpan timeSpan)
    {
      return timeSpan <= TimeSpan.Zero;
    }
  }
}
