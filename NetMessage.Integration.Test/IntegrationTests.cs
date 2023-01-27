using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetMessage.Integration.Test.TestFramework;

namespace NetMessage.Integration.Test
{
  [TestClass]
  public class IntegrationTests : TestBase
  {
    private const string ServerHost = "127.0.0.1";
    private const int ServerPort = 1234;
    private const int MessageCount = 2000;
    private const int ClientCount = 2;

    private NetMessageServer? _server;
    private readonly NetMessageClient[] _clients = new NetMessageClient[ClientCount];
    private readonly NetMessageSession[] _sessions = new NetMessageSession[ClientCount];
    private readonly int[] _receivedMessagesCount = new int[ClientCount];
    private readonly WaitToken[] _messageReceivedWt = new WaitToken[ClientCount];

    private WaitToken? _sessionOpenedWt;
    private WaitToken? _sessionClosedWt;
    private NetMessageSession? _lastOpenedSession;
    private NetMessageSession? _lastClosedSession;

    [TestInitialize]
    public void TestInitialize()
    {
      _server = new NetMessageServer(ServerPort);
      _server.OnError += OnServerError;
      _server.SessionOpened += OnSessionOpened;
      _server.SessionClosed += OnSessionClosed;
      _server.Start();

      for (int i = 0; i < ClientCount; i++)
      {
        _clients[i] = new NetMessageClient();
        _receivedMessagesCount[i] = 0;
        _messageReceivedWt[i] = new WaitToken(MessageCount);

        ConnectClient(i);
      }
    }

    [TestCleanup]
    public void TestCleanup()
    {
      for (int i = 0; i < ClientCount; i++)
      {
        _clients[i].Disconnect();
      }

      _server!.Stop();
      _server!.Dispose();
    }

    [TestMethod]
    public void ConnectOfConnectedClient()
    {
      var task = _clients[0].ConnectAsync(ServerHost, ServerPort);
      task.WaitAndAssert("Connect task did not succeed");
      Assert.IsFalse(task.Result, "Connecting of already connected client did not return false");
    }

    [TestMethod]
    public void ConnectAndDisconnect()
    {
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
    public void SendMessagesToServer()
    {
      _server!.AddMessageHandler<TestMessage>(OnMessageReceived);

      var sendTask0 = Task.Run(() => SendMessages(_clients[0]));
      var sendTask1 = Task.Run(() => SendMessages(_clients[1]));

      sendTask0.WaitAndAssert("Send task 0 did not finish");
      sendTask1.WaitAndAssert("Send task 1 did not finish");

      _messageReceivedWt[0].WaitAndAssert("Not all messages from client 0 were received");
      _messageReceivedWt[1].WaitAndAssert("Not all messages from client 1 were received");
    }

    [TestMethod]
    public void SendMessagesToClients()
    {
      _clients[0].AddMessageHandler<TestMessage>(OnMessageReceived);
      _clients[1].AddMessageHandler<TestMessage>(OnMessageReceived);

      var sendTask0 = Task.Run(() => SendMessages(_sessions[0]));
      var sendTask1 = Task.Run(() => SendMessages(_sessions[1]));

      sendTask0.WaitAndAssert("Send task 0 did not finish");
      sendTask1.WaitAndAssert("Send task 1 did not finish");

      _messageReceivedWt[0].WaitAndAssert("Not all messages from client 0 were received");
      _messageReceivedWt[1].WaitAndAssert("Not all messages from client 1 were received");
    }

    private void ConnectClient(int clientIndex)
    {
      _sessionOpenedWt = new WaitToken(1);
      var task = _clients[clientIndex].ConnectAsync(ServerHost, ServerPort);
      task.WaitAndAssert($"Client {clientIndex} did not connect");
      Assert.IsTrue(task.Result);
      Assert.IsTrue(_clients[clientIndex].IsConnected);
      _sessionOpenedWt.WaitAndAssert($"No session was opened after connection of client {clientIndex}");

      if (_lastOpenedSession == null)
      {
        throw new AssertFailedException("Last opened session memento was null after connection");
      }

      _sessions[clientIndex] = _lastOpenedSession;
    }

    private void SendMessages(object communicator)
    {
      var taskList = new List<Task<int>>();
      for (int i = 0; i < MessageCount; i++)
      {
        Task<int> sendTask;
        if (communicator is NetMessageClient client)
        {
          sendTask = client.SendMessageAsync(new TestMessage
          {
            MessageText = "MyMessage",
            MessageCount = i
          });
        }
        else if (communicator is NetMessageSession session)
        {
          sendTask = session.SendMessageAsync(new TestMessage
          {
            MessageText = "MyMessage",
            MessageCount = i
          });
        }
        else
        {
          throw new AssertFailedException($"Send request from unexpected communicator: {communicator}");
        }

        taskList.Add(sendTask);
      }

      foreach (var task in taskList)
      {
        task.WaitAndAssert($"Message from task {taskList.IndexOf(task)} was not sent successfully");
      }
    }

    private void OnMessageReceived(object communicator, TestMessage message)
    {
      Assert.AreEqual(message.MessageText, "MyMessage");

      var communicatorIndex = -1;
      if (communicator is NetMessageClient client)
      {
        communicatorIndex = Array.IndexOf(_clients, client);
      }
      else if (communicator is NetMessageSession session)
      {
        communicatorIndex = Array.IndexOf(_sessions, session);
      }

      if (communicatorIndex < 0)
      {
        throw new AssertFailedException($"Message received from unexpected communicator: {communicator}");
      }

      TestContext!.WriteLine($"Received Message on {communicator.GetType().Name} {communicatorIndex}: {message.MessageCount}");

      var expectedCount = _receivedMessagesCount[communicatorIndex]++;
      Assert.AreEqual(expectedCount, message.MessageCount);
      _messageReceivedWt[communicatorIndex].Signal();
    }
    
    private void OnServerError(NetMessageServer server, NetMessageSession? session, string errorMessage, Exception? ex)
    {
      TestContext!.WriteLine($"Server error: {errorMessage}");
      if (ex != null)
      {
        TestContext.WriteLine(ex.Message);
        TestContext.WriteLine(ex.StackTrace);
      }
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

    public int MessageCount { get; set; }
  }
}
