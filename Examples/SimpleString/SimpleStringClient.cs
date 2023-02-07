using NetMessage.Base;
using NetMessage.Base.Packets;
using System.Threading.Tasks;

namespace NetMessage.Examples.SimpleString
{
  /// <summary>
  /// Client for the <see cref="SimpleStringProtocol"/>.
  /// Note that the Send methods simply forward to the protected methods of the generic base class.
  /// </summary>
  public class SimpleStringClient : ClientBase<SimpleStringClient, SimpleStringRequest, SimpleStringProtocol, string>
  {
    public Task<int> SendMessageAsync(string message)
    {
      return SendMessageInternalAsync(message);
    }

    public Task<Response<string>?> SendRequestAsync(string request)
    {
      return SendRequestInternalAsync(request);
    }

    protected override SimpleStringProtocol CreateProtocolBuffer()
    {
      return new SimpleStringProtocol();
    }
  }
}
