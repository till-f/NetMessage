namespace NetMessage.Base.Message
{
  public interface IPacket<TPld>
  {
    TPld Payload { get; }
  }
}
