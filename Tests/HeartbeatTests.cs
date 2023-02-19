using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetMessage.Base;
using NetMessage.Integration.Test.TestFramework;
using System;
using System.Threading;

namespace NetMessage.Integration.Test
{
  /// <summary>
  /// Test class for heartbeat specific behavior (ConnectionLoss when heartbeat is not received).
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
        heartbeatIntervalServer: TimeSpan.FromMilliseconds(100),
        heartbeatIntervalClient: TimeSpan.FromMilliseconds(100),
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
    public void HeartbeatReceiveTimeoutOnServer()
    {
      // heartbeat is sent too slow 
      StartAndConnect(
        heartbeatIntervalServer: TimeSpan.FromMilliseconds(100),
        heartbeatIntervalClient: TimeSpan.FromMilliseconds(500),
        receiveTimeout: TimeSpan.FromMilliseconds(300),
        connectClients: true
        );

      // session receive timeout should be triggered
      for (int clientIndex = 0; clientIndex < ClientCount; clientIndex++)
      {
        _connectionLostSessionErrorWaitTokens[clientIndex].WaitAndAssert($"ConnectionLost Error was not triggered on session {clientIndex}");
        _sessionClosedWaitTokens[clientIndex].WaitAndAssert($"SessionClosed was not triggered on session {clientIndex}");
        Assert.AreEqual(ECloseReason.ConnectionLost, _lastSessionClosedArgs?.Reason, $"SessionClosed was triggered with unexpected reason");
      }
    }

    [TestMethod]
    public void HeartbeatReceiveTimeoutOnClient()
    {
      // heartbeat is sent too slow 
      StartAndConnect(
        heartbeatIntervalServer: TimeSpan.FromMilliseconds(500),
        heartbeatIntervalClient: TimeSpan.FromMilliseconds(100),
        receiveTimeout: TimeSpan.FromMilliseconds(300),
        connectClients: true
        );

      // session receive timeout should be triggered
      for (int clientIndex = 0; clientIndex < ClientCount; clientIndex++)
      {
        _connectionLostClientErrorWaitTokens[clientIndex].WaitAndAssert($"ConnectionLost Error was not triggered on client {clientIndex}");
        _clientDisconnectedWaitTokens[clientIndex].WaitAndAssert($"Disconnected was not triggered on client {clientIndex}");
        Assert.AreEqual(ECloseReason.ConnectionLost, _lastClientDisconnectedArgs?.Reason, $"SessionClosed was triggered with unexpected reason");
      }
    }

    [TestMethod]
    public void HeartbeatDisabledNoReceiveTimeout()
    {
      // heartbeat is disabled
      StartAndConnect(
        heartbeatIntervalServer: Timeout.InfiniteTimeSpan,
        heartbeatIntervalClient: Timeout.InfiniteTimeSpan,
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
        heartbeatIntervalServer: TimeSpan.FromMilliseconds(1000),
        heartbeatIntervalClient: TimeSpan.FromMilliseconds(1000),
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

    private void StartAndConnect(TimeSpan heartbeatIntervalServer, TimeSpan heartbeatIntervalClient, TimeSpan receiveTimeout, bool connectClients)
    {
      _server!.HeartbeatInterval = heartbeatIntervalServer;
      _server.ReceiveTimeout = receiveTimeout;
      _server.Start();

      for (int i = 0; i < ClientCount; i++)
      {
        _clients[i].HeartbeatInterval = heartbeatIntervalClient;
        _clients[i].ReceiveTimeout = receiveTimeout;
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
