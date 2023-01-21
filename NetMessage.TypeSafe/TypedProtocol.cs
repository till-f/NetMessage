using NetMessage.Base;
using NetMessage.Base.Message;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetMessage.TypeSafe
{
  /// <summary>
  /// This is a simple protocol to transer strings with an additional type information
  /// between client and server.
  /// 
  /// The protocol supports the request/response mechanism.
  /// 
  /// The end of a message is determined by a termination sequence ( <see cref="Terminator"/>)
  /// which must not be contained in the payload (no escaping is applied by the protocol).
  /// </summary>
  //
  // Raw message string format:
  // Message   TypeId::Payload
  // Request   >TypeId:ResonseId:Payload
  // Response  <TypeId:ResonseId:Payload
  public class TypedProtocol : IProtocol<TypedPayload>
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

    public IList<IMessage<TypedPayload>> FromRaw(byte[] rawData)
    {
      var messages = new List<IMessage<TypedPayload>>();

      var text = Encoding.GetString(rawData);

      var offset = 0;
      while (offset < text.Length)
      {
        var eotPos = text.IndexOf(Terminator, offset, StringComparison.Ordinal);

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

    private IMessage<TypedPayload> ParseMessage(string rawString)
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

      var startIdx = messageKind == EMessageKind.Message ? 0 : 1;

      var sepIdx1 = rawString.IndexOf(_separatorToken);
      if (sepIdx1 < 0)
      {
        throw new InvalidOperationException($"Unsupported message format: 1st separator not found");
      }

      var sepIdx2 = rawString.IndexOf(_separatorToken, sepIdx1 + 1);
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

      string payload;
      if (sepIdx2 < rawString.Length - 1)
      {
        payload = rawString.Substring(sepIdx2 + 1);
      }
      else
      {
        payload = string.Empty;
      }

      switch (messageKind)
      {
        case EMessageKind.Request:
          return new TypedRequestInternal(new TypedPayload(typeField, payload), idField);
        case EMessageKind.Response:
          return new Response<TypedPayload>(new TypedPayload(typeField, payload), idField);
        default:
          return new Message<TypedPayload>(new TypedPayload(typeField, payload));
      }
    }

    public byte[] ToRawMessage(TypedPayload payload)
    {
      return ToRaw(EMessageKind.Message, payload, -1);
    }

    public byte[] ToRawRequest(TypedPayload payload, int responseId)
    {
      return ToRaw(EMessageKind.Request, payload, responseId);
    }

    public byte[] ToRawResponse(TypedPayload payload, int responseId)
    {
      return ToRaw(EMessageKind.Response, payload, responseId);
    }

    private byte[] ToRaw(EMessageKind messageKind, TypedPayload payload, int responseId)
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

      var responseIdString = messageKind == EMessageKind.Message ? "" : responseId.ToString();

      var sb = new StringBuilder();
      sb.Append(messageKindToken);
      sb.Append(payload.TypeId);
      sb.Append(_separatorToken);
      sb.Append(responseIdString);
      sb.Append(_separatorToken);
      sb.Append(payload.ActualPayload);
      sb.Append(Terminator);

      return Encoding.GetBytes(sb.ToString());
    }
  }
}
