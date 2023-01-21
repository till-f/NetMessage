namespace NetMessage.TypeSafe
{
  /// <summary>
  /// The payload type for the <see cref="TypedProtocol"/>.
  /// </summary>
  public class TypedPayload
  {
    public TypedPayload(string typeId, string payload)
    {
      TypeId = typeId;
      ActualPayload = payload;
    }

    public string TypeId { get; }

    public string ActualPayload { get; }
  }
}
