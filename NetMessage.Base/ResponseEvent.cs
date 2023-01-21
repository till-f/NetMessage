using NetMessage.Base.Message;
using System.Threading;

namespace NetMessage.Base
{
  public class ResponseEvent<TPld>
  {
    private readonly ManualResetEventSlim _resetEvent;

    public ResponseEvent(ManualResetEventSlim resetEvent)
    {
      _resetEvent = resetEvent;
    }

    public Response<TPld> Response { get; set; }

    public void Set()
    {
      _resetEvent.Set();
    }
  }
}