using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetMessage.Base;
using NetMessage.Integration.Test.TestFramework;
using System;
using System.Threading;

namespace NetMessage.Integration.Test
{
  [TestClass]
  public class HeartbeatTests : TestBase
  {
    WaitToken[] _connectionLostSessionWaitTokens = new WaitToken[ClientCount];
    WaitToken[] _connectionLostClientWaitTokens = new WaitToken[ClientCount];

    [TestInitialize]
    public void TestInitialize()
    {
      _ignoreServerErrors = true;
    }

    [TestMethod]
    public void HeartbeatHappyPath()
    {
      // happy config: heartbeat is sent quick enough
      StartAndConnect(
        heartbeatInterval: TimeSpan.FromMilliseconds(100),
        heartbeatTimeout: TimeSpan.FromMilliseconds(100),
        receiveTimeout: TimeSpan.FromMilliseconds(300),
        true
        );

      Thread.Sleep(1000);

      for (int clientIndex = 0; clientIndex < ClientCount; clientIndex++)
      {
        _connectionLostClientWaitTokens[clientIndex].AssertNotSet($"ConnectionLost was triggered on client {clientIndex}");
        _connectionLostSessionWaitTokens[clientIndex].AssertNotSet($"ConnectionLost was triggered on session {clientIndex}");
        Assert.IsTrue(_clients[clientIndex].IsConnected, $"client {clientIndex} is not connected");
        Assert.IsTrue(_sessions[clientIndex].IsConnected, $"session {clientIndex} is not connected");
      }
    }

    [TestMethod]
    public void HeartbeatReceiveTimeout()
    {
      // heartbeat is sent too slow 
      StartAndConnect(
        heartbeatInterval: TimeSpan.FromMilliseconds(200),
        heartbeatTimeout: TimeSpan.FromMilliseconds(300),
        receiveTimeout: TimeSpan.FromMilliseconds(100),
        true
        );

      // session receive timeout should be triggered
      for (int clientIndex = 0; clientIndex < ClientCount; clientIndex++)
      {
        _connectionLostSessionWaitTokens[clientIndex].WaitAndAssert($"ConnectionLost was not triggered on session {clientIndex}");
      }

      Thread.Sleep(1000);

      // client does not detect a connection loss because it can send the heartbeat (it just is too slow)
      // session then closes the connection which is detected by client
      for (int clientIndex = 0; clientIndex < ClientCount; clientIndex++)
      {
        _connectionLostClientWaitTokens[clientIndex].AssertNotSet($"ConnectionLost was triggered on client {clientIndex}");
        Assert.IsFalse(_clients[clientIndex].IsConnected, $"client {clientIndex} is still connected");
        Assert.IsFalse(_sessions[clientIndex].IsConnected, $"session {clientIndex} is still connected");
      }
    }

    [TestMethod]
    public void HeartbeatDisabledNoReceiveTimeout()
    {
      // heartbeat is sent too slow 
      StartAndConnect(
        heartbeatInterval: Timeout.InfiniteTimeSpan,
        heartbeatTimeout: TimeSpan.FromMilliseconds(100),
        receiveTimeout: Timeout.InfiniteTimeSpan,
        true
        );

      Thread.Sleep(1000);

      for (int clientIndex = 0; clientIndex < ClientCount; clientIndex++)
      {
        _connectionLostClientWaitTokens[clientIndex].AssertNotSet($"ConnectionLost was triggered on client {clientIndex}");
        _connectionLostSessionWaitTokens[clientIndex].AssertNotSet($"ConnectionLost was triggered on session {clientIndex}");
        Assert.IsTrue(_clients[clientIndex].IsConnected, $"client {clientIndex} is not connected");
        Assert.IsTrue(_sessions[clientIndex].IsConnected, $"session {clientIndex} is not connected");
      }
    }

    [TestMethod]
    public void HeartbeatNoTimeoutWhenNoConnection()
    {
      // heartbeat is sent too slow 
      StartAndConnect(
        heartbeatInterval: TimeSpan.FromMilliseconds(1000),
        heartbeatTimeout: TimeSpan.FromMilliseconds(100),
        receiveTimeout: TimeSpan.FromMilliseconds(100),
        false
        );

      Thread.Sleep(1000);

      for (int clientIndex = 0; clientIndex < ClientCount; clientIndex++)
      {
        _connectionLostClientWaitTokens[clientIndex].AssertNotSet($"ConnectionLost was triggered on client {clientIndex}");
        _connectionLostSessionWaitTokens[clientIndex].AssertNotSet($"ConnectionLost was triggered on session {clientIndex}");
        Assert.IsFalse(_clients[clientIndex].IsConnected, $"client {clientIndex} was connected");
        Assert.IsNull(_sessions[clientIndex], $"a session existed for index {clientIndex}");
      }
    }

    private void StartAndConnect(TimeSpan heartbeatInterval, TimeSpan heartbeatTimeout, TimeSpan receiveTimeout, bool connectClients)
    {
      _server!.ReceiveTimeout = receiveTimeout;
      _server.Start();

      for (int i = 0; i < ClientCount; i++)
      {
        _clients[i].HeartbeatInterval = heartbeatInterval;
        _clients[i].HeartbeatTimeout = heartbeatTimeout;
        _connectionLostClientWaitTokens[i] = new WaitToken(1);
        _connectionLostSessionWaitTokens[i] = new WaitToken(1);
        if (connectClients)
        {
          ConnectClient(i);
        }
      }
    }

    protected override void OnCommunicatorError(object communicator, int clientIndex, Exception? ex)
    {
      if (!(ex is ConnectionLostException))
      {
        return;
      }

      if (communicator is NetMessageClient)
      {
        _connectionLostClientWaitTokens[clientIndex].Signal();
      }
      else if (communicator is NetMessageSession)
      {
        _connectionLostSessionWaitTokens[clientIndex].Signal();
      }
    }
  }
}
