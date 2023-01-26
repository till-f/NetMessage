using System.Net.Sockets;

namespace NetMessage.Base.MockSupport
{
  public abstract class SocketFactory
  {
    public static SocketFactory? Instance;

    public static ISocket CreateSocket(SocketType st, ProtocolType pt)
    {
      if (Instance == null)
      {
        Instance = new RealSocketFactory();
      }

      return Instance.Create(st, pt);
    }

    protected abstract ISocket Create(SocketType st, ProtocolType pt);
  }

  public class RealSocketFactory : SocketFactory
  {
    protected override ISocket Create(SocketType st, ProtocolType pt)
    {
      return new RealSocket(new Socket(st, pt));
    }
  }
}
