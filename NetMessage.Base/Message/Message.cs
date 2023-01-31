namespace NetMessage.Base.Message
{
  public class Message<TData> : IPacket<TData>
  {
    /// <summary>
    /// Container for a message (in contrast to response and request)
    /// </summary>
    public Message(TData data)
    {
      Data = data;
    }

    public TData Data { get; }
  }
}
