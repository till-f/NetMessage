using System.Net.Sockets;
using NetMessage.Base.MockSupport;

namespace NetMessage.Integration.Test.TestFramework
{
  class TestSocketFactory : SocketFactory
  {
    protected override ISocket Create(SocketType st, ProtocolType pt)
    {
      return new TestSocket(st, pt);
    }
  }
}
