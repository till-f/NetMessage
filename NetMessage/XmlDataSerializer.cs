using NetMessage.Base;
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
  public class XmlDataSerializer : IDataSerializer
  {
    private static readonly XmlSerializerNamespaces _emptyNs = new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty });

    public Encoding ProtocolEncoding { get; } = Defaults.Encoding;

    public string ProtocolTerminator { get; } = Defaults.Terminator;

    public T Deserialize<T>(string dataString)
    {
      using (TextReader dataStream = new StringReader(dataString))
      {
        var serializer = new XmlSerializer(typeof(T));
        return (T) serializer.Deserialize(dataStream);
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

        return stream.ToString();
      }
    }
  }
}
