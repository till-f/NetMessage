namespace NetMessage.Base.Message
{
  public class Message<TData> : IPacket<TData>
  {
    /// <summary>
    /// Container for a message (in contrast to response and request)
    /// </summary>
    public Message(TData payload)
    {
      Payload = payload;
    }

    public TData Payload { get; }
  }
}
