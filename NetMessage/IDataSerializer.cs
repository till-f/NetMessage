using System.Text;

namespace NetMessage
{
  public interface IDataSerializer
  {
    /// <summary>
    /// The encoding to be used by the protocol layer. UTF8 is a good default,
    /// but in general it should match the character set that is actually produced
    /// by the serializer. E.g., if the serialized data only contains ASCII
    /// characters, ASCII should be used.
    /// </summary>
    Encoding ProtocolEncoding { get; }

    /// <summary>
    /// The termination sequence to be used by the protocol layer. Using the
    /// EOT (End Of Transmission) character ("\u0004") is a good default.
    /// </summary>
    string ProtocolTerminator { get; }

    /// <summary>
    /// Deserializes the given string into the corresponding object of type T.
    /// </summary>
    T Deserialize<T>(string dataString);

    /// <summary>
    /// Serialized the given object.
    /// </summary>
    string Serialize(object o);
  }
}