using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetMessage.Integration.Test.TestFramework;
using System.Threading;

namespace NetMessage.Integration.Test
{
  /// <summary>
  /// Test class of specific behavior when connection is established.
  /// 
  /// The test initialize method will start the server, but clients are not connected.
  /// </summary>
  [TestClass]
  public class ConnectionTests : TestBase
  {
    [TestInitialize]
    public void TestInitialize()
    {
      _server!.Start();
    }

    [TestMethod]
    public void ConnectAndDisconnect()
    {
      Assert.IsFalse(_clients[0].IsConnected);
      Assert.IsFalse(_clients[1].IsConnected);

      ConnectClient(0);
      Assert.IsTrue(_clients[0].IsConnected);
      Assert.IsFalse(_clients[1].IsConnected);

      ConnectClient(1);
      Assert.IsTrue(_clients[0].IsConnected);
      Assert.IsTrue(_clients[1].IsConnected);

      _sessionClosedWt = new WaitToken(1);
      _clients[0].Disconnect();
      Assert.IsFalse(_clients[0].IsConnected);
      Assert.IsTrue(_clients[1].IsConnected);
      _sessionClosedWt.WaitAndAssert("Session was not closed after disconnection of client 0");
      Assert.AreEqual(_sessions[0], _lastClosedSession);

      _sessionClosedWt = new WaitToken(1);
      _clients[1].Disconnect();
      Assert.IsFalse(_clients[0].IsConnected);
      Assert.IsFalse(_clients[1].IsConnected);
      _sessionClosedWt.WaitAndAssert("Session was not closed after disconnection of client 1");
      Assert.AreEqual(_sessions[1], _lastClosedSession);
    }

    [TestMethod]
    public void ConnectOfConnectedClient()
    {
      ConnectClient(0);

      var task = _clients[0].ConnectAsync(ServerHost, ServerPort);
      task.WaitAndAssert("Connect task did not succeed");
      Assert.IsFalse(task.Result, "Connecting of already connected client did not return false");
    }

    [TestMethod]
    public void ConnectionCanBeRefused()
    {
      _server!.RemoteSocketVerifier = (socket) => 
      {
        return false;
      };

      // Connection of client will not be closed gracefully; ignore errors from here 
      _ignoreServerErrors = true;

      _sessionOpenedWt = new WaitToken(1);
      var task = _clients[0].ConnectAsync(ServerHost, ServerPort);
      task.WaitAndAssert("Connect task did not succeed");

      // With .NET we cannot easily reject the connection before it is established because it is hard to set the callback of WSAAceppt from C#
      // see https://learn.microsoft.com/de-de/windows/win32/api/winsock2/nf-winsock2-wsaaccept
      // see https://stackoverflow.com/questions/5395984/tcplistener-reject-an-incoming-connection-request-before-connection
      // Thus, NetMessage will first accept the connection before immediately closing it. Use the windows firewall to reject connections
      // when malicious clients should not be able to detect this endpoint.
      Assert.IsTrue(task.Result, "Connection request did not return true");

      Thread.Sleep(1000);

      _sessionOpenedWt.AssertNotSet("A session was opened even thought connection should have been rejected");
      Assert.IsFalse(_clients[0].IsConnected, "Client was connected even thought connection should have been rejected");
    }

  }
}
