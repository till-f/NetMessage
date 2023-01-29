namespace NetMessage.Base.Message
{
  public enum EMessageKind { Message, Request, Response }

  public class Response<TData> : IPacket<TData>
  {
    /// <summary>
    /// Container for a response (in contrast to message and request)
    /// </summary>
    public Response(TData payload, int responseId)
    {
      Payload = payload;
      ResponseId = responseId;
    }

    public TData Payload { get; }

    public int ResponseId { get; }
  }
}
