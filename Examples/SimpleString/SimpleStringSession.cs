using NetMessage.Base;
using NetMessage.Base.Message;
using System.Threading.Tasks;

namespace NetMessage.Examples.SimpleString
{
  /// <summary>
  /// Session for the <see cref="SimpleStringProtocol"/>.
  /// Note that the Send methods simply forward to the protected methods of the generic base class.
  /// </summary>
  public class SimpleStringSession : SessionBase<SimpleStringServer, SimpleStringSession, SimpleStringRequest, SimpleStringProtocol, string>
  {
    public Task<bool> SendMessageAsync(string message)
    {
      return SendMessageInternalAsync(message);
    }

    public Task<Response<string>?> SendRequestAsync(string request)
    {
      return SendRequestInternalAsync(request);
    }
  }
}
