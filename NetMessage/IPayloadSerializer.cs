using System.Text;

namespace NetMessage
{
  public interface IPayloadSerializer
  {
    /// <summary>
    /// The encoding to be used by the protocol layer.
    /// Should depend on the characters produced by the concrete serializer,
    /// e.g., if the serialized data only contains ASCII characters, ASCII
    /// should be returned.
    /// </summary>
    Encoding ProtocolEncoding { get; }

    /// <summary>
    /// The termination sequence to be used by the protocol layer.
    /// </summary>
    string ProtocolTerminator { get; }

    T Deserialize<T>(string payloadString);

    string Serialize(object o);
  }
}