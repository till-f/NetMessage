using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetMessage.Base.MockSupport
{
  public class RealSocket : ISocket, IDisposable
  {
    private readonly Socket _socket;

    public RealSocket(Socket socket)
    {
      _socket = socket;
    }

    public bool Connected => _socket.Connected;

    public int ReceiveBufferSize => _socket.ReceiveBufferSize;

    public IPEndPoint? RemoteEndPoint => _socket.RemoteEndPoint as IPEndPoint;

    public void Bind(IPEndPoint ep)
    {
      _socket.Bind(ep);
    }

    public void Listen(int backlog)
    {
      _socket.Listen(backlog);
    }

    public Task<ISocket> AcceptAsync()
    {
      return Task.Run(() => (ISocket) new RealSocket(_socket.Accept()));
    }

    public Task ConnectAsync(string host, int port)
    {
      return _socket.ConnectAsync(host, port);
    }

    public Task<int> ReceiveAsync(ArraySegment<byte> buffer, SocketFlags flags)
    {
      return _socket.ReceiveAsync(buffer, flags);
    }

    public Task<int> SendAsync(ArraySegment<byte> buffer, SocketFlags flags)
    {
      return _socket.SendAsync(buffer, flags);
    }

    public void Close()
    {
      _socket.Close();
    }

    public void Dispose()
    {
      _socket.Dispose();
    }
  }
}
