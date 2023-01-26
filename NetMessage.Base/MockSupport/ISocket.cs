using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetMessage.Base.MockSupport
{
  public interface ISocket
  {
    public int ReceiveBufferSize { get; }

    public bool Connected { get; }

    public IPEndPoint? RemoteEndPoint { get; }

    public void Bind(IPEndPoint ep);

    public void Listen(int backlog);

    public Task<ISocket> AcceptAsync();

    public Task ConnectAsync(string host, int port);

    public Task<int> ReceiveAsync(ArraySegment<byte> buffer, SocketFlags flags);

    public Task<int> SendAsync(ArraySegment<byte> buffer, SocketFlags flags);

    public void Close();

    public void Dispose();
  }
}
