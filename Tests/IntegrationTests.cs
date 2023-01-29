using System;
using System.Collections.Generic;
using System.Threading;
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

    private const int ResponseTimeoutMs = 200;
    private const int ResponseTimeoutMaxDiscr = 20;
    private const string TestMessageText = "TestMessage";
    private const string TestRequestText = "TestRequest";

    private NetMessageServer? _server;
    private readonly NetMessageClient[] _clients = new NetMessageClient[ClientCount];
    private readonly NetMessageSession[] _sessions = new NetMessageSession[ClientCount];
    private readonly int[] _receivedMessagesCount = new int[ClientCount];
    private readonly int[] _receivedRequestsCount = new int[ClientCount];
    private readonly WaitToken[] _receivedMessageWaitToken = new WaitToken[ClientCount];
    private readonly WaitToken[] _receivedRequestWaitToken = new WaitToken[ClientCount];

    private WaitToken? _sessionOpenedWt;
    private WaitToken? _sessionClosedWt;
    private NetMessageSession? _lastOpenedSession;
    private NetMessageSession? _lastClosedSession;

    public bool _ignoreServerErrors;

    [TestInitialize]
    public void TestInitialize()
    {
      _server = new NetMessageServer(ServerPort);
      _server.ResponseTimeout = TimeSpan.FromMilliseconds(ResponseTimeoutMs);
      _server.OnError += OnServerError;
      _server.SessionOpened += OnSessionOpened;
      _server.SessionClosed += OnSessionClosed;
      _server.Start();

      for (int i = 0; i < ClientCount; i++)
      {
        _clients[i] = new NetMessageClient();
        _clients[i].ResponseTimeout = TimeSpan.FromMilliseconds(ResponseTimeoutMs);
        _receivedMessagesCount[i] = 0;
        _receivedRequestsCount[i] = 0;
        _receivedMessageWaitToken[i] = new WaitToken(MessageCount);
        _receivedRequestWaitToken[i] = new WaitToken(MessageCount);

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
    public async Task ServerRespondsTooSlow()
    {
      // Test 1: successful request
      _server!.AddRequestHandler<TestRequest, TestResponse>(OnRequestReceived);

      _receivedRequestWaitToken[0] = new WaitToken(1);
      var result = await _clients[0].SendRequestAsync(new TestRequest { RequestText = TestRequestText, RequestCount = 0 });
      Assert.AreEqual(TestRequestText.ToLowerInvariant(), result.ResponseText, "Unexpected response received");
      _receivedRequestWaitToken[0].WaitAndAssert("Request was not received by server");

      // Test 2: unsuccessful request - server is not listening
      _server!.RemoveRequestHandler<TestRequest, TestResponse>(OnRequestReceived);
      await SendRequestAndExpectTimeout(_clients[0], 1); // the request will never reach the server, i.e., the request count is irrelevant

      // Test 3: unsuccessful request - server is too slow
      _server!.AddRequestHandler<TestRequest, TestResponse>((session, tr) =>
      {
        Thread.Sleep(ResponseTimeoutMs + 10);
        OnRequestReceived(session, tr);
      });

      _receivedRequestWaitToken[0] = new WaitToken(1);
      await SendRequestAndExpectTimeout(_clients[0], 1); // same requestCount as in previous message, because previous one was not received
      _receivedRequestWaitToken[0].WaitAndAssert("Request was not received by server");

      // TestCleanup() will disconnect the client in the next step, but the server might still try to send the response (our timeout/threshold is pretty tight)
      _ignoreServerErrors = true;
    }

    [TestMethod]
    public async Task ClientRespondsTooSlow()
    {
      // Test 1: successful request
      _clients[0].AddRequestHandler<TestRequest, TestResponse>(OnRequestReceived);

      _receivedRequestWaitToken[0] = new WaitToken(1);
      var result = await _sessions[0].SendRequestAsync(new TestRequest { RequestText = TestRequestText, RequestCount = 0 });
      Assert.AreEqual(TestRequestText.ToLowerInvariant(), result.ResponseText, "Unexpected response received");
      _receivedRequestWaitToken[0].WaitAndAssert("Request was not received by client");

      // Test 2: unsuccessful request - client is not listening
      _clients[0].RemoveRequestHandler<TestRequest, TestResponse>(OnRequestReceived);
      await SendRequestAndExpectTimeout(_sessions[0], 1); // the request will never reach the cleint, i.e., the request count is irrelevant

      // Test 3: unsuccessful request - client is too slow
      _clients[0].AddRequestHandler<TestRequest, TestResponse>((client, tr) =>
      {
        Thread.Sleep(ResponseTimeoutMs + 10);
        OnRequestReceived(client, tr);
      });

      _receivedRequestWaitToken[0] = new WaitToken(1);
      await SendRequestAndExpectTimeout(_sessions[0], 1); // same requestCount as in previous message, because previous one was not received
      _receivedRequestWaitToken[0].WaitAndAssert("Request was not received by client");

      // TestCleanup() will disconnect the client in the next step, but it might still try to send the response (our timeout/threshold is pretty tight)
      _ignoreServerErrors = true;
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

      _receivedMessageWaitToken[0].WaitAndAssert("Not all messages from client 0 were received");
      _receivedMessageWaitToken[1].WaitAndAssert("Not all messages from client 1 were received");
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

      _receivedMessageWaitToken[0].WaitAndAssert("Not all messages from client 0 were received");
      _receivedMessageWaitToken[1].WaitAndAssert("Not all messages from client 1 were received");
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

    private async Task SendRequestAndExpectTimeout(object communicator, int requestCount)
    {
      var startTime = DateTime.Now;
      try
      {
        if (communicator is NetMessageClient client)
        {
          var result = await client.SendRequestAsync(new TestRequest { RequestText = TestRequestText, RequestCount = requestCount });
        }
        else if (communicator is NetMessageSession session)
        {
          var result = await session.SendRequestAsync(new TestRequest { RequestText = TestRequestText, RequestCount = requestCount });
        }
        else
        {
          throw new AssertFailedException($"Send request from unexpected communicator: {communicator}");
        }
      }
      catch (TimeoutException)
      {
        var delta = (DateTime.Now - startTime).TotalMilliseconds;
        var minDelta = ResponseTimeoutMs - ResponseTimeoutMaxDiscr;
        var maxDelta = ResponseTimeoutMs + ResponseTimeoutMaxDiscr;
        Assert.IsTrue(delta > minDelta && delta < maxDelta, $"Timeout did not occur in expected time, it occured after {delta} ms");
        return;
      }

      Assert.Fail("Exception was not thrown");
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
            MessageText = TestMessageText,
            MessageCount = i
          });
        }
        else if (communicator is NetMessageSession session)
        {
          sendTask = session.SendMessageAsync(new TestMessage
          {
            MessageText = TestMessageText,
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

    private void OnRequestReceived(object communicator, TypedRequest<TestRequest, TestResponse> tr)
    {
      tr.SendResponseAsync(new TestResponse { ResponseText = tr.Request.RequestText?.ToLowerInvariant() });

      var communicatorIndex = GetCommunicatorIndex(communicator);

      TestContext!.WriteLine($"Received Request on {communicator.GetType().Name} {communicatorIndex}: {tr.Request.RequestCount}");

      var expectedCount = _receivedRequestsCount[communicatorIndex]++;
      Assert.AreEqual(expectedCount, tr.Request.RequestCount, "Unexpected request count");
      _receivedRequestWaitToken[communicatorIndex].Signal();
    }

    private void OnMessageReceived(object communicator, TestMessage message)
    {
      Assert.AreEqual(message.MessageText, TestMessageText);

      var communicatorIndex = GetCommunicatorIndex(communicator);

      TestContext!.WriteLine($"Received Message on {communicator.GetType().Name} {communicatorIndex}: {message.MessageCount}");

      var expectedCount = _receivedMessagesCount[communicatorIndex]++;
      Assert.AreEqual(expectedCount, message.MessageCount, "Unexpected message count");
      _receivedMessageWaitToken[communicatorIndex].Signal();
    }

    private int GetCommunicatorIndex(object communicator)
    {
      if (communicator is NetMessageClient client)
      {
        return Array.IndexOf(_clients, client);
      }
      else if (communicator is NetMessageSession session)
      {
        return Array.IndexOf(_sessions, session);
      }

      throw new AssertFailedException($"Unexpected communicator object: {communicator}");
    }

    private void OnServerError(NetMessageServer server, NetMessageSession? session, string errorMessage, Exception? ex)
    {
      if (_ignoreServerErrors)
      {
        return;
      }

      if (ex != null)
      {
        errorMessage += ": " + ex.Message + "\n" + ex.StackTrace;        
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

    public int MessageCount { get; set; }
  }

  public class TestRequest : IRequest<TestResponse>
  {
    public string? RequestText { get; set; }

    public int RequestCount { get; set; }
  }

  public class TestResponse
  {
    public string? ResponseText { get; set; }
  }
}
