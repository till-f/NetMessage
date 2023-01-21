using NetMessage.Base;

namespace NetMessage.Examples.SimpleString
{
  /// <summary>
  /// Server for the <see cref="SimpleStringProtocol"/>.
  /// </summary>
  public class SimpleStringServer : ServerBase<SimpleStringServer, SimpleStringSession, SimpleStringRequest, SimpleStringProtocol, string>
  {
    public SimpleStringServer(int listeningPort) : base(listeningPort)
    {
    }

    protected override SimpleStringProtocol CreateProtocolBuffer()
    {
      return new SimpleStringProtocol();
    }

    protected override void InitSession(SimpleStringSession session)
    {
      // nothing to init
    }
  }
}
