namespace NetMessage.Base.Message
{
  public enum EMessageKind { Message, Request, Response }

  public class Response<TPld> : IPacket<TPld>
  {
    /// <summary>
    /// Container for a response (in contrast to message and request)
    /// </summary>
    public Response(TPld payload, int responseId)
    {
      Payload = payload;
      ResponseId = responseId;
    }

    public TPld Payload { get; }

    public int ResponseId { get; }
  }
}
