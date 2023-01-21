namespace NetMessage.Base.Message
{
  public interface IMessage<TPld>
  {
    TPld Payload { get; }
  }
}
