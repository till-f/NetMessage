using System;
using System.Text;

namespace NetMessage.Base
{
  public static class Defaults
  {
    public const string Terminator = "\u0004";

    public static readonly Encoding Encoding = Encoding.UTF8;

    public static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(10);

    public static readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds(3);

    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(2);

    public static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(2);

    public static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan KeepAliveTime = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(3);
  }
}
