using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetMessage.Integration.Test.TestFramework;

namespace NetMessage.Integration.Test
{
  [TestClass]
  public class IntegrationTests : TestBase
  {
    private NetMessageServer? _server;
    private WaitToken? _sessionOpenedWt;
    private WaitToken? _sessionClosedWt;

    private NetMessageSession? _lastOpenedSession;
    private NetMessageSession? _lastClosedSession;

    [TestInitialize]
    public void TestInitialize()
    {
      _server = new NetMessageServer(1234);
      _server.OnError += OnServerError;
      _server.SessionOpened += OnSessionOpened;
      _server.SessionClosed += OnSessionClosed;
      _server.Start();
    }

    [TestCleanup]
    public void TestCleanup()
    {
      _server!.Stop();
      _server!.Dispose();
    }

    [TestMethod]
    public void TestConnectAndDisconnect()
    {
      var client1 = new NetMessageClient();
      var client2 = new NetMessageClient();

      _sessionOpenedWt = new WaitToken(1);
      var task = client1.ConnectAsync("127.0.0.1", 1234);
      task.WaitAndAssert("Client 1 did not connect");
      Assert.IsTrue(task.Result);
      Assert.IsTrue(client1.IsConnected);
      Assert.IsFalse(client2.IsConnected);
      _sessionOpenedWt.WaitAndAssert("No session was opened after connection of client 1");
      var session1 = _lastOpenedSession;

      _sessionOpenedWt = new WaitToken(1);
      task = client2.ConnectAsync("127.0.0.1", 1234);
      task.WaitAndAssert("Client 2 did not connect");
      Assert.IsTrue(task.Result);
      Assert.IsTrue(client1.IsConnected);
      Assert.IsTrue(client2.IsConnected);
      _sessionOpenedWt.WaitAndAssert("No session was opened after connection of client 2");
      var session2 = _lastOpenedSession;

      _sessionClosedWt = new WaitToken(1);
      client1.Disconnect();
      Assert.IsFalse(client1.IsConnected);
      Assert.IsTrue(client2.IsConnected);
      _sessionClosedWt.WaitAndAssert("Session was not closed after disconnection of client 1");
      Assert.AreEqual(session1, _lastClosedSession);

      _sessionClosedWt = new WaitToken(1);
      client2.Disconnect();
      Assert.IsFalse(client1.IsConnected);
      Assert.IsFalse(client2.IsConnected);
      _sessionClosedWt.WaitAndAssert("Session was not closed after disconnection of client 2");
      Assert.AreEqual(session2, _lastClosedSession);
    }

    [TestMethod]
    public void TestSendMessages()
    {
      int messageCount = 10;
      ConcurrentDictionary<NetMessageSession, int> receivedMessagesServerPerPort = new();
      
      var client1 = new NetMessageClient();
      var client2 = new NetMessageClient();
      var messageReceivedWt1 = new WaitToken(messageCount);
      var messageReceivedWt2 = new WaitToken(messageCount);

      _sessionOpenedWt = new WaitToken(1);
      var connectTask = client1.ConnectAsync("127.0.0.1", 1234);
      connectTask.WaitAndAssert("Client 1 did not connect");
      _sessionOpenedWt.WaitAndAssert("No session was opened after connection of client 1");
      var session1 = _lastOpenedSession;

      _sessionOpenedWt = new WaitToken(1);
      connectTask = client2.ConnectAsync("127.0.0.1", 1234);
      connectTask.WaitAndAssert("Client 2 did not connect");
      _sessionOpenedWt.WaitAndAssert("No session was opened after connection of client 2");
      var session2 = _lastOpenedSession;

      _server!.AddMessageHandler<TestMessage>(MessageHandlerServer);

      var sendTask1 = Task.Run(() => SendMessages(client1, messageCount));
      var sendTask2 = Task.Run(() => SendMessages(client2, messageCount));

      sendTask1.WaitAndAssert("Send task 1 did not finish");
      sendTask2.WaitAndAssert("Send task 2 did not finish");

      messageReceivedWt1.WaitAndAssert("Not all messages from client 1 were received");
      messageReceivedWt2.WaitAndAssert("Not all messages from client 2 were received");

      client1.Disconnect();
      client2.Disconnect();

      void MessageHandlerServer(NetMessageSession session, TestMessage message)
      {
        var messageSplit = message.MessageText!.Split(':');
        var receivedPrefix = messageSplit[0];
        var receivedCount = int.Parse(messageSplit[1]);
        Assert.AreEqual(receivedPrefix, "MyMessage");

        if (session == session1)
        {
          messageReceivedWt1.Signal();
        }
        else if (session == session2)
        {
          messageReceivedWt2.Signal();
        }
        else
        {
          Assert.Fail($"Message received from unexpected session: {session.Guid}");
        }

        if (!receivedMessagesServerPerPort.ContainsKey(session))
        {
          receivedMessagesServerPerPort[session] = 0;
        }

        var expectedCount = receivedMessagesServerPerPort[session]++;

        Assert.AreEqual(expectedCount, receivedCount);
      }
    }

    private void SendMessages(NetMessageClient client, int messageCount)
    {
      var taskList = new List<Task<bool>>();
      for (int i = 0; i < messageCount; i++)
      {
        taskList.Add(client.SendMessageAsync(new TestMessage
        {
          MessageText = $"MyMessage:{i}"
        }));
      }

      foreach (var task in taskList)
      {
        task.WaitAndAssert($"Message from task {taskList.IndexOf(task)} was not sent successfully");
      }
    }

    private void OnServerError(NetMessageServer server, NetMessageSession? session, string errorMessage, Exception? ex)
    {
      if (ex != null)
      {
        throw ex;
      }

      Assert.Fail($"Server error: {errorMessage}");
    }

    private void OnSessionOpened(NetMessageSession session)
    {
      _lastOpenedSession = session;
      _sessionOpenedWt?.Signal();
    }

    private void OnSessionClosed(NetMessageSession session)
    {
      _lastClosedSession = session;
      _sessionClosedWt?.Signal();
    }
  }

  public class TestMessage
  {
    public string? MessageText { get; set; }
  }
}
