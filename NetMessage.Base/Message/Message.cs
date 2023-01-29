﻿namespace NetMessage.Base.Message
{
  public class Message<TPld> : IPacket<TPld>
  {
    /// <summary>
    /// Container for a message (in contrast to response and request)
    /// </summary>
    public Message(TPld payload)
    {
      Payload = payload;
    }

    public TPld Payload { get; }
  }
}
