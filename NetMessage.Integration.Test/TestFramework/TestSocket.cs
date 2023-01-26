using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetMessage.Base.MockSupport;

namespace NetMessage.Integration.Test.TestFramework
{
  public static class TestConnections
  {
    public static ConcurrentDictionary<int, TestSocket> ListeningSockets = new ();
  }

  public class TestSocket : ISocket
  {
    private readonly AutoResetEvent _acceptEvent = new (false);
    private readonly AutoResetEvent _connectEvent = new (false);
    private readonly AutoResetEvent _receiveEvent = new (false);
    private readonly AutoResetEvent _sendEvent = new (false);
    private readonly ConcurrentQueue<TestSocket> _incomingConnectionRequests = new ();
    private readonly ConcurrentQueue<byte[]> _incomingDataBuffers = new ();

    private bool _isClosed;
    private int? _listeningPort;
    private TestSocket? _otherSocket;

    private int _returnPortCount = 1000;

    public TestSocket(SocketType st, ProtocolType pt)
    {
      Assert.AreEqual(SocketType.Stream, st);
      Assert.AreEqual(ProtocolType.Tcp, pt);
    }

    private TestSocket(TestSocket otherSocket)
    {
      _otherSocket = otherSocket;
      otherSocket._otherSocket = this;
      Connected = true;
      otherSocket.Connected = true;
      otherSocket._connectEvent.Set();
    }

    public int ReceiveBufferSize => 48000;

    public bool Connected { get; private set; }

    public IPEndPoint? RemoteEndPoint { get; private set; }

    public void Bind(IPEndPoint ep)
    {
      Assert.IsFalse(_isClosed);
      _listeningPort = ep.Port;
    }

    public void Listen(int backlog)
    {
      Assert.IsFalse(_isClosed);
      Assert.IsNotNull(_listeningPort);
      TestConnections.ListeningSockets[_listeningPort.Value] = this;
    }

    public Task<ISocket> AcceptAsync()
    {
      Assert.IsFalse(_isClosed);

      return Task.Run(() =>
      {
        _acceptEvent.WaitOne();
        _incomingConnectionRequests.TryDequeue(out var otherSocket);

        Debug.Assert(otherSocket != null, nameof(otherSocket) + " != null");

        return (ISocket) new TestSocket(otherSocket)
        {
          RemoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), _returnPortCount++)
        };
      });
    }

    public Task ConnectAsync(string host, int port)
    {
      Assert.IsFalse(_isClosed);

      if (!TestConnections.ListeningSockets.TryGetValue(port, out var listeningSocket))
      {
        throw new SocketException((int)SocketError.ConnectionRefused);
      }

      return Task.Run(() =>
      {
        listeningSocket.AcceptConnectionRequest(this);
        _connectEvent.WaitOne();
        RemoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);
      });
    }

    public Task<int> ReceiveAsync(ArraySegment<byte> buffer, SocketFlags flags)
    {
      Assert.IsFalse(_isClosed);
      Assert.IsNotNull(_otherSocket);

      return Task.Run(() =>
      {
        _receiveEvent.WaitOne();
        _incomingDataBuffers.TryDequeue(out var receivedData);
        receivedData!.CopyTo(buffer.Array!, 0);
        _otherSocket._sendEvent.Set();
        return receivedData.Length;
      });
    }

    public Task<int> SendAsync(ArraySegment<byte> buffer, SocketFlags flags)
    {
      Assert.IsFalse(_isClosed);
      Assert.IsNotNull(_otherSocket);

      return Task.Run(() =>
      {
        var bufferToSend = new byte[buffer.Count];
        buffer.CopyTo(bufferToSend);
        _otherSocket._incomingDataBuffers.Enqueue(bufferToSend);
        _otherSocket._receiveEvent.Set();
        _sendEvent.WaitOne();
        return buffer.Count;
      });
    }

    public void Close()
    {
      _isClosed = true;
    }

    public void Dispose()
    {
      _isClosed = true;
    }

    private void AcceptConnectionRequest(TestSocket otherSocket)
    {
      Assert.IsFalse(_isClosed);

      _incomingConnectionRequests.Enqueue(otherSocket);
      _acceptEvent.Set();
    }
  }
}