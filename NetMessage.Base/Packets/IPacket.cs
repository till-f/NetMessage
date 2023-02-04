namespace NetMessage.Base.Packets
{
  public interface IPacket<TData>
  {
    TData Data { get; }
  }
}
