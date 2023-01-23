using System;

namespace NetMessage
{
  /// <summary>
  /// The payload type for the <see cref="TypedProtocol"/>.
  /// </summary>
  public class TypedPayload
  {
    public TypedPayload(string typeId, string payload)
    {
      TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
      ActualPayload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    public string TypeId { get; }

    public string ActualPayload { get; }
  }
}
