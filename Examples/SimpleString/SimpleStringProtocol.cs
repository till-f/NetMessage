using NetMessage.Base;
using NetMessage.Base.Message;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetMessage.Examples.SimpleString
{
  /// <summary>
  /// This is a simple protocol to transer plain strings between client and server.
  /// 
  /// The protocol supports the request/response mechanism.
  /// 
  /// The end of a message is determined by a termination sequence ( <see cref="Terminator"/>)
  /// which must not be contained in the transferred data (no escaping is applied by the protocol).
  /// </summary>
  //
  // Raw message string format:
  // Message   :DataString
  // Request   >RequestId:DataString
  // Response  <ResonseId:DataString
  public class SimpleStringProtocol : IProtocol<string>
  {
    private const char _separatorToken = ':';
    private const char _requestToken = '>';
    private const char _responseToken = '<';

    private string _buffer = string.Empty;

    /// <summary>
    /// The used encoding (default is UTF8)
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// The termination sequence (default is the EOT character, ASCII code 0x4)
    /// </summary>
    public string Terminator { get; set; } = "\u0004";

    public IList<IPacket<string>> FromRaw(byte[] rawData)
    {
      var messages = new List<IPacket<string>>();

      var text = Encoding.GetString(rawData);

      var offset = 0;
      while (offset < text.Length)
      {
        var eotPos = text.IndexOf(Terminator, offset);

        if (eotPos == -1)
        {
          _buffer = _buffer + text.Substring(offset);
          break;
        }
        else
        {
          var rawString = _buffer + text.Substring(offset, eotPos);
          messages.Add(ParseMessage(rawString));
          _buffer = string.Empty;
          offset = eotPos + Terminator.Length;
        }
      }

      return messages;
    }

    private IPacket<string> ParseMessage(string rawString)
    {
      EMessageKind messageKind;
      switch (rawString[0])
      {
        case _requestToken:
          messageKind = EMessageKind.Request;
          break;
        case _responseToken:
          messageKind = EMessageKind.Response;
          break;
        default:
          messageKind = EMessageKind.Message;
          break;
      }

      var sepIdx = rawString.IndexOf(_separatorToken);
      if (sepIdx < 0)
      {
        throw new InvalidOperationException($"Unsupported message format: separator not as expected");
      }

      var idField = -1;
      if (messageKind != EMessageKind.Message)
      {
        idField = int.Parse(rawString.Substring(1, sepIdx - 1));
      }

      var dataString = rawString.Substring(sepIdx + 1);

      switch (messageKind)
      {
        case EMessageKind.Request:
          return new SimpleStringRequest(dataString, idField);
        case EMessageKind.Response:
          return new Response<string>(dataString, idField);
        default:
          return new Message<string>(dataString);
      }
    }

    public byte[] ToRawMessage(string dataString)
    {
      return ToRaw(EMessageKind.Message, dataString, -1);
    }

    public byte[] ToRawRequest(string dataString, int requestId)
    {
      return ToRaw(EMessageKind.Request, dataString, requestId);
    }

    public byte[] ToRawResponse(string dataString, int responseId)
    {
      return ToRaw(EMessageKind.Response, dataString, responseId);
    }

    private byte[] ToRaw(EMessageKind messageKind, string dataString, int id)
    {
      string messageKindToken;
      switch (messageKind)
      {
        case EMessageKind.Message:
          messageKindToken = string.Empty;
          break;
        case EMessageKind.Request:
          messageKindToken = _requestToken.ToString();
          break;
        case EMessageKind.Response:
          messageKindToken = _responseToken.ToString();
          break;
        default:
          throw new InvalidOperationException();
      }

      var responseIdString = messageKind == EMessageKind.Message ? "" : id.ToString();

      var sb = new StringBuilder();
      sb.Append(messageKindToken);
      sb.Append(responseIdString);
      sb.Append(_separatorToken);
      sb.Append(dataString);
      sb.Append(Terminator);

      return Encoding.GetBytes(sb.ToString());
    }
  }
}