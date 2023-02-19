using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetMessage.Base;
using NetMessage.Integration.Test.TestFramework;
using System;
using System.Threading;

namespace NetMessage.Integration.Test
{
  /// <summary>
  /// Test class for heartbeat specific behavior (connection loss exception when heartbeat is not received).
  /// 
  /// The test initialized method will not start the server or connect clients.
  /// </summary>
  [TestClass]
  public class HeartbeatTests : TestBase
  {
    WaitToken[] _clientDisconnectedWaitTokens = new WaitToken[ClientCount];
    WaitToken[] _sessionClosedWaitTokens = new WaitToken[ClientCount];

    WaitToken[] _connectionLostClientErrorWaitTokens = new WaitToken[ClientCount];
    WaitToken[] _connectionLostSessionErrorWaitTokens = new WaitToken[ClientCount];

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
        heartbeatSendTimeout: TimeSpan.FromMilliseconds(100),
        receiveTimeout: TimeSpan.FromMilliseconds(300),
        connectClients: true
        );

      Thread.Sleep(1000);

      for (int clientIndex = 0; clientIndex < ClientCount; clientIndex++)
      {
        _connectionLostClientErrorWaitTokens[clientIndex].AssertNotSet($"ConnectionLost Error was triggered on client {clientIndex}");
        _connectionLostSessionErrorWaitTokens[clientIndex].AssertNotSet($"ConnectionLost Error was triggered on session {clientIndex}");
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
        heartbeatSendTimeout: TimeSpan.FromMilliseconds(300),
        receiveTimeout: TimeSpan.FromMilliseconds(100),
        connectClients: true
        );

      // session receive timeout should be triggered
      for (int clientIndex = 0; clientIndex < ClientCount; clientIndex++)
      {
        _connectionLostSessionErrorWaitTokens[clientIndex].WaitAndAssert($"ConnectionLost Error was not triggered on session {clientIndex}");
        _sessionClosedWaitTokens[clientIndex].WaitAndAssert($"SessionClosed was not triggered on session {clientIndex}");
        Assert.AreEqual(ECloseReason.ConnectionLost, _lastSessionClosedArgs?.Reason, $"SessionClosed was triggered with unexpected reason");
      }

      Thread.Sleep(1000);

      // client does not detect a connection loss because it can still send the heartbeat
      // session then closes the connection which is detected by client
      for (int clientIndex = 0; clientIndex < ClientCount; clientIndex++)
      {
        _connectionLostClientErrorWaitTokens[clientIndex].AssertNotSet($"ConnectionLost Error was triggered on client {clientIndex}");
        _clientDisconnectedWaitTokens[clientIndex].WaitAndAssert($"Disconnected was not triggered on client {clientIndex}");
        Assert.AreEqual(ECloseReason.SocketException, _lastClientDisconnectedArgs?.Reason, $"Disconnected was triggered with unexpected reason");
        Assert.IsFalse(_clients[clientIndex].IsConnected, $"client {clientIndex} is still connected");
        Assert.IsFalse(_sessions[clientIndex].IsConnected, $"session {clientIndex} is still connected");
      }
    }

    [TestMethod]
    public void HeartbeatDisabledNoReceiveTimeout()
    {
      // heartbeat is disabled
      StartAndConnect(
        heartbeatInterval: Timeout.InfiniteTimeSpan,
        heartbeatSendTimeout: TimeSpan.FromMilliseconds(100),
        receiveTimeout: Timeout.InfiniteTimeSpan,
        connectClients: true
        );

      Thread.Sleep(1000);

      for (int clientIndex = 0; clientIndex < ClientCount; clientIndex++)
      {
        _connectionLostClientErrorWaitTokens[clientIndex].AssertNotSet($"ConnectionLost Error was triggered on client {clientIndex}");
        _connectionLostSessionErrorWaitTokens[clientIndex].AssertNotSet($"ConnectionLost Error was triggered on session {clientIndex}");
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
        heartbeatSendTimeout: TimeSpan.FromMilliseconds(100),
        receiveTimeout: TimeSpan.FromMilliseconds(100),
        connectClients: false
        );

      Thread.Sleep(1000);

      for (int clientIndex = 0; clientIndex < ClientCount; clientIndex++)
      {
        _connectionLostClientErrorWaitTokens[clientIndex].AssertNotSet($"ConnectionLost Error was triggered on client {clientIndex}");
        _connectionLostSessionErrorWaitTokens[clientIndex].AssertNotSet($"ConnectionLost Error was triggered on session {clientIndex}");
        Assert.IsFalse(_clients[clientIndex].IsConnected, $"client {clientIndex} was connected");
        Assert.IsNull(_sessions[clientIndex], $"a session existed for index {clientIndex}");
      }
    }

    private void StartAndConnect(TimeSpan heartbeatInterval, TimeSpan heartbeatSendTimeout, TimeSpan receiveTimeout, bool connectClients)
    {
      _server!.ReceiveTimeout = receiveTimeout;
      _server.Start();

      for (int i = 0; i < ClientCount; i++)
      {
        _clients[i].HeartbeatInterval = heartbeatInterval;
        _clients[i].HeartbeatSendTimeout = heartbeatSendTimeout;
        _connectionLostClientErrorWaitTokens[i] = new WaitToken(1);
        _connectionLostSessionErrorWaitTokens[i] = new WaitToken(1);
        _clientDisconnectedWaitTokens[i] = new WaitToken(1);
        _sessionClosedWaitTokens[i] = new WaitToken(1);
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
        _connectionLostClientErrorWaitTokens[clientIndex].Signal();
      }
      else if (communicator is NetMessageSession)
      {
        _connectionLostSessionErrorWaitTokens[clientIndex].Signal();
      }
    }

    protected override void OnDisconnected(NetMessageClient client, SessionClosedArgs args)
    {
      base.OnDisconnected(client, args);

      _clientDisconnectedWaitTokens[Array.IndexOf(_clients, client)].Signal();
    }

    protected override void OnSessionClosed(NetMessageSession session, SessionClosedArgs args)
    {
      base.OnSessionClosed(session, args);

      _sessionClosedWaitTokens[Array.IndexOf(_sessions, session)].Signal();
    }
  }
}
