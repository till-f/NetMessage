using System;

namespace NetMessage.Base
{
  public static class Defaults
  {
    public static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(10);

    public static readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds(3);

    public const uint KeepAliveTime = 5000;

    public const uint KeepAliveInterval = 1000;
  }
}
