namespace NetMessage.Base.Message
{
  public enum EMessageKind { Message, Request, Response }

  public class Response<TData> : IPacket<TData>
  {
    /// <summary>
    /// Container for a response (in contrast to message and request)
    /// </summary>
    public Response(TData data, int responseId)
    {
      Data = data;
      ResponseId = responseId;
    }

    public TData Data { get; }

    public int ResponseId { get; }
  }
}
