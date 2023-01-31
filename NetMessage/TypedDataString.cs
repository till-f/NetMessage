using System;

namespace NetMessage
{
  /// <summary>
  /// The TData type for the <see cref="TypedProtocol"/>.
  /// </summary>
  public class TypedDataString
  {
    public TypedDataString(string typeId, string dataString)
    {
      TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
      DataString = dataString ?? throw new ArgumentNullException(nameof(dataString));
    }

    public string TypeId { get; }

    public string DataString { get; }
  }
}
