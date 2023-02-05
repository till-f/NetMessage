using NetMessage.Base;
using NetMessage.Base.Packets;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetMessage
{
  /// <summary>
  /// This is a simple protocol to transer strings with an additional type information
  /// between client and server.
  /// 
  /// The protocol supports the request/response mechanism.
  /// 
  /// The end of a message is determined by a termination sequence ( <see cref="Terminator"/>)
  /// which must not be contained in the transferred data (no escaping is applied by the protocol).
  /// </summary>
  //
  // Raw message string format:
  // Message   TypeId::DataString
  // Request   >TypeId:RequestId:DataString
  // Response  <TypeId:ResonseId:DataString
  public class TypedProtocol : IProtocol<TypedDataString>
  {
    private const char SeparatorToken = ':';
    private const char RequestToken = '>';
    private const char ResponseToken = '<';

    private string _buffer = string.Empty;

    /// <summary>
    /// The used encoding (default is UTF8)
    /// </summary>
    public Encoding Encoding { get; set; } = Defaults.Encoding;

    /// <summary>
    /// The termination sequence (default is the EOT character, ASCII code 0x4)
    /// </summary>
    public string Terminator { get; set; } = Defaults.Terminator;

    public byte[] HeartbeatPacket => Encoding.GetBytes(Terminator);

    public IList<IPacket<TypedDataString>> FromRaw(byte[] rawData)
    {
      var messages = new List<IPacket<TypedDataString>>();

      var text = Encoding.GetString(rawData);

      var offset = 0;
      while (offset < text.Length)
      {
        var terminatorPos = text.IndexOf(Terminator, offset, StringComparison.Ordinal);

        if (terminatorPos == -1)
        {
          _buffer = _buffer + text.Substring(offset);
          break;
        }
        else if (terminatorPos == offset)
        {
          // empty message / heartbeat (only terminator was transferred)
          offset = terminatorPos + Terminator.Length;
        }
        else
        {
          var rawString = _buffer + text.Substring(offset, terminatorPos-offset);
          messages.Add(ParseMessage(rawString));
          _buffer = string.Empty;
          offset = terminatorPos + Terminator.Length;
        }
      }

      return messages;
    }

    private IPacket<TypedDataString> ParseMessage(string rawString)
    {
      EMessageKind messageKind;
      switch (rawString[0])
      {
        case RequestToken:
          messageKind = EMessageKind.Request;
          break;
        case ResponseToken:
          messageKind = EMessageKind.Response;
          break;
        default:
          messageKind = EMessageKind.Message;
          break;
      }

      var startIdx = messageKind == EMessageKind.Message ? 0 : 1;

      var sepIdx1 = rawString.IndexOf(SeparatorToken);
      if (sepIdx1 < 0)
      {
        throw new InvalidOperationException($"Unsupported message format: 1st separator not found");
      }

      var sepIdx2 = rawString.IndexOf(SeparatorToken, sepIdx1 + 1);
      if (sepIdx2 < 0)
      {
        throw new InvalidOperationException($"Unsupported message format: 2nd separator not found");
      }
      
      var typeField = rawString.Substring(startIdx, sepIdx1 - startIdx);

      var idField = -1;
      if (messageKind != EMessageKind.Message)
      {
        idField = int.Parse(rawString.Substring(sepIdx1 + 1, sepIdx2 - sepIdx1 - 1));
      }

      string dataString;
      if (sepIdx2 < rawString.Length - 1)
      {
        dataString = rawString.Substring(sepIdx2 + 1);
      }
      else
      {
        dataString = string.Empty;
      }

      switch (messageKind)
      {
        case EMessageKind.Request:
          return new TypedRequestInternal(new TypedDataString(typeField, dataString), idField);
        case EMessageKind.Response:
          return new Response<TypedDataString>(new TypedDataString(typeField, dataString), idField);
        default:
          return new Message<TypedDataString>(new TypedDataString(typeField, dataString));
      }
    }

    public byte[] ToRawMessage(TypedDataString typesDataString)
    {
      return ToRaw(EMessageKind.Message, typesDataString, -1);
    }

    public byte[] ToRawRequest(TypedDataString typesDataString, int requestId)
    {
      return ToRaw(EMessageKind.Request, typesDataString, requestId);
    }

    public byte[] ToRawResponse(TypedDataString typesDataString, int responseId)
    {
      return ToRaw(EMessageKind.Response, typesDataString, responseId);
    }

    private byte[] ToRaw(EMessageKind messageKind, TypedDataString typesDataString, int id)
    {
      string messageKindToken;
      switch (messageKind)
      {
        case EMessageKind.Message:
          messageKindToken = string.Empty;
          break;
        case EMessageKind.Request:
          messageKindToken = RequestToken.ToString();
          break;
        case EMessageKind.Response:
          messageKindToken = ResponseToken.ToString();
          break;
        default:
          throw new InvalidOperationException();
      }

      var responseIdString = messageKind == EMessageKind.Message ? "" : id.ToString();

      var sb = new StringBuilder();
      sb.Append(messageKindToken);
      sb.Append(typesDataString.TypeId);
      sb.Append(SeparatorToken);
      sb.Append(responseIdString);
      sb.Append(SeparatorToken);
      sb.Append(typesDataString.DataString);
      sb.Append(Terminator);

      return Encoding.GetBytes(sb.ToString());
    }
  }
}
