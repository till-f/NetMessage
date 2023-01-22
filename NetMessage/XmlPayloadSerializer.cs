using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace NetMessage
{
  public class XmlPayloadSerializer : IPayloadSerializer
  {
    private static readonly XmlSerializerNamespaces _emptyNs = new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty });

    public T Deserialize<T>(string payloadString)
    {
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
        return stream.ToString();
      }
    }
  }
}
