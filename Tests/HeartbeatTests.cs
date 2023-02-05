using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetMessage.Base;
using NetMessage.Integration.Test.TestFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.Integration.Test
{
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
    public void ConnectAndDisconnect()
    {
      // happy config: heartbeat is sent quick enough
      StartAndConnect(
        heartbeatInterval: TimeSpan.FromMilliseconds(100),
        heartbeatTimeout: TimeSpan.FromMilliseconds(100),
        receiveTimeout: TimeSpan.FromMilliseconds(200)
        );

      Task.Delay(TimeSpan.FromSeconds(1));

      // TODO: none of the wait tokens should have been triggered
    }

    private void StartAndConnect(TimeSpan heartbeatInterval, TimeSpan heartbeatTimeout, TimeSpan receiveTimeout)
    {
      _server!.ReceiveTimeout = receiveTimeout;
      _server.Start();

      for (int i = 0; i < ClientCount; i++)
      {
        _clients[i].HeartbeatInterval = heartbeatInterval;
        _clients[i].HeartbeatTimeout = heartbeatTimeout;
        _connectionLostClientWaitTokens[i] = new WaitToken(1);
        _connectionLostSessionWaitTokens[i] = new WaitToken(1);
        ConnectClient(i);
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
