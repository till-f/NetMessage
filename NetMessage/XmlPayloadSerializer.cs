using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace NetMessage
{
  /// <summary>
  /// Uses the default XmlSerializer to send (de)serialize objects.
  /// The flag "useBase64" can be used to avoid problems if the serialization may contain the termination sequence.
  /// </summary>
  public class XmlPayloadSerializer : IPayloadSerializer
  {
    private static readonly XmlSerializerNamespaces _emptyNs = new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty });

    private readonly bool _useBase64;

    public XmlPayloadSerializer(bool useBase64 = false)
    {
      _useBase64 = useBase64;

      if (useBase64)
      {
        ProtocolEncoding = Encoding.ASCII;
      }
      else
      {
        ProtocolEncoding = Encoding.UTF8;
      }
    }

    public Encoding ProtocolEncoding { get; }

    public string ProtocolTerminator { get; } = TypedProtocol.DefaultTerminator;

    public T Deserialize<T>(string payloadString)
    {
      if (_useBase64)
      {
        payloadString = Encoding.UTF8.GetString(Convert.FromBase64String(payloadString));
      }

      using (TextReader payloadStream = new StringReader(payloadString))
      {
        var serializer = new XmlSerializer(typeof(T));
        return (T) serializer.Deserialize(payloadStream);
      }
    }

    public string Serialize(object o)
    {
      var settings = new XmlWriterSettings
      {
        Indent = false,
        OmitXmlDeclaration = true
      };

      using (var stream = new StringWriter())
      using (var writer = XmlWriter.Create(stream, settings))
      {
        var serializer = new XmlSerializer(o.GetType());
        serializer.Serialize(writer, o, _emptyNs);

        if (_useBase64)
        {
          return Convert.ToBase64String(Encoding.UTF8.GetBytes(stream.ToString()));
        }

        return stream.ToString();
      }
    }
  }
}
