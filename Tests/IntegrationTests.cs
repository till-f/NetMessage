using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetMessage.Base;
using NetMessage.Integration.Test.TestFramework;

namespace NetMessage.Integration.Test
{
  /// <summary>
  /// Test class for integration tests of <see cref="NetMessageClient"/> and <see cref="NetMessageServer"/>.
  /// For all tests, it maintains one server and N clients + respective sessions and keeps track of received messages.
  /// Additionally, it keeps track of the last opened/closed sessions and provides a <see cref="WaitToken"/> so that
  /// tests can wait for such events or received messages.
  ///
  /// The <see cref="TestInitialize"/> method will create all instances (including default WaitTokens) and connects
  /// all clients to the server.
  ///
  /// The server and all client have the <see cref="CommunicatorBase{TRequest,TProtocol,TData}.FailOnFaultedReceiveTask"/>
  /// property set to true and a throwing handler is registered for the OnError event. This means that the the test
  /// framework is killed on every receive error. The original exception (if any) is contained as inner exception to support
  /// debugging (Note that Visual Studio does not show the exception or stack trace in the Test Explorer if the framework fails,
  /// so it may be necessary to check the Output window for Tests). For similar reasons, tests will also fail on every send error.
  /// To avoid spurious failing tests when test cleanup is called, <see cref="_ignoreServerErrors"/> may be used in edge cases.
  /// </summary>
  [TestClass]
  public class IntegrationTests : TestBase
  {
    // host, port and some dummy data for testing
    private const string ServerHost = "127.0.0.1";
    private const int ServerPort = 1234;
    private const int ResponseTimeoutMs = 200;
    private const int ResponseTimeoutMaxDiscr = 20;
    private const string TestMessageText = "TestMessage";
    private const string TestRequestText = "TestRequest";

    // the number of messages that should be sent for the "burst" tests
    private const int MessageCount = 2000;

    // the number of clients for all tests (some test may only use one of them)
    private const int ClientCount = 2;

    // the server and all clients are constructed and connected when test methods are entered
    private NetMessageServer? _server;
    private readonly NetMessageClient[] _clients = new NetMessageClient[ClientCount];
    private readonly NetMessageSession[] _sessions = new NetMessageSession[ClientCount];

    // all elements are zero when test methods are entered
    private readonly int[] _receivedMessagesCount = new int[ClientCount];
    private readonly int[] _receivedRequestsCount = new int[ClientCount];

    // all wait tokens are must be set 'ClientCount' times before they fire
    private readonly WaitToken[] _receivedMessageWaitToken = new WaitToken[ClientCount];
    private readonly WaitToken[] _receivedRequestWaitToken = new WaitToken[ClientCount];

    // always contains the last opened/closed session
    private NetMessageSession? _lastOpenedSession;
    private NetMessageSession? _lastClosedSession;

    // may be used to avoid failing tests in edge cases (see class comment)
    private bool _ignoreServerErrors;

    // already set when test methods are entered; if used by tests, a new WaitToken should be constructed
    private WaitToken? _sessionOpenedWt;
    private WaitToken? _sessionClosedWt;

    [TestInitialize]
    public void TestInitialize()
    {
      _server = new NetMessageServer(ServerPort);
      _server.ResponseTimeout = TimeSpan.FromMilliseconds(ResponseTimeoutMs);
      _server.FailOnFaultedReceiveTask = true;
      _server.OnError += OnServerError;
      _server.SessionOpened += OnSessionOpened;
      _server.SessionClosed += OnSessionClosed;
      _server.Start();

      for (int i = 0; i < ClientCount; i++)
      {
        _clients[i] = new NetMessageClient();
        _clients[i].ResponseTimeout = TimeSpan.FromMilliseconds(ResponseTimeoutMs);
        _clients[i].FailOnFaultedReceiveTask = true;
        _clients[i].OnError += OnCommunicatorError;
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
    public void ConnectOfConnectedClient()
    {
      var task = _clients[0].ConnectAsync(ServerHost, ServerPort);
      task.WaitAndAssert("Connect task did not succeed");
      Assert.IsFalse(task.Result, "Connecting of already connected client did not return false");
    }

    //[TestMethod]
    //public async Task ServerRespondsTooSlow()
    //{
    //  // Test 1: successful request
    //  _server!.AddRequestHandler<TestRequest, TestResponse>(OnRequestReceived);

    //  _receivedRequestWaitToken[0] = new WaitToken(1);
    //  var result = await _clients[0].SendRequestAsync(new TestRequest { RequestText = TestRequestText, RequestCount = 0 });
    //  Assert.AreEqual(TestRequestText.ToLowerInvariant(), result.ResponseText, "Unexpected ResponseText received");
    //  _receivedRequestWaitToken[0].WaitAndAssert("Request was not received by server");

    //  // Test 2: unsuccessful request - server is not listening
    //  _server!.RemoveRequestHandler<TestRequest, TestResponse>(OnRequestReceived);
    //  await SendRequestAndExpectTimeout(_clients[0], 1); // the request will never reach the server, i.e., the request count is irrelevant

    //  // Test 3: unsuccessful request - server is too slow
    //  _server!.AddRequestHandler<TestRequest, TestResponse>((session, tr) =>
    //  {
    //    Thread.Sleep(ResponseTimeoutMs + 10);
    //    OnRequestReceived(session, tr);
    //  });

    //  _receivedRequestWaitToken[0] = new WaitToken(1);
    //  await SendRequestAndExpectTimeout(_clients[0], 1); // same requestCount as in previous message, because previous one was not received
    //  _receivedRequestWaitToken[0].WaitAndAssert("Request was not received by server");

    //  // TestCleanup() will disconnect the client in the next step, but the server might still try to send the response (our timeout/threshold is pretty tight)
    //  _ignoreServerErrors = true;
    //}

    //[TestMethod]
    //public async Task ClientRespondsTooSlow()
    //{
    //  // Test 1: successful request
    //  _clients[0].AddRequestHandler<TestRequest, TestResponse>(OnRequestReceived);

    //  _receivedRequestWaitToken[0] = new WaitToken(1);
    //  var result = await _sessions[0].SendRequestAsync(new TestRequest { RequestText = TestRequestText, RequestCount = 0 });
    //  Assert.AreEqual(TestRequestText.ToLowerInvariant(), result.ResponseText, "Unexpected ResponseText received");
    //  _receivedRequestWaitToken[0].WaitAndAssert("Request was not received by client");

    //  // Test 2: unsuccessful request - client is not listening
    //  _clients[0].RemoveRequestHandler<TestRequest, TestResponse>(OnRequestReceived);
    //  await SendRequestAndExpectTimeout(_sessions[0], 1); // the request will never reach the cleint, i.e., the request count is irrelevant

    //  // Test 3: unsuccessful request - client is too slow
    //  _clients[0].AddRequestHandler<TestRequest, TestResponse>((client, tr) =>
    //  {
    //    Thread.Sleep(ResponseTimeoutMs + 10);
    //    OnRequestReceived(client, tr);
    //  });

    //  _receivedRequestWaitToken[0] = new WaitToken(1);
    //  await SendRequestAndExpectTimeout(_sessions[0], 1); // same requestCount as in previous message, because previous one was not received
    //  _receivedRequestWaitToken[0].WaitAndAssert("Request was not received by client");

    //  // TestCleanup() will disconnect the client in the next step, but it might still try to send the response (our timeout/threshold is pretty tight)
    //  _ignoreServerErrors = true;
    //}

    [TestMethod]
    public void BurstMessagesToServer()
    {
      _server!.AddMessageHandler<TestMessage>(OnMessageReceived);

      var sendTask0 = Task.Run(() => SendMessageBurst(_clients[0]));
      var sendTask1 = Task.Run(() => SendMessageBurst(_clients[1]));

      sendTask0.WaitAndAssert("Send task 0 did not finish");
      sendTask1.WaitAndAssert("Send task 1 did not finish");

      _receivedMessageWaitToken[0].WaitAndAssert("Not all messages from client 0 were received");
      _receivedMessageWaitToken[1].WaitAndAssert("Not all messages from client 1 were received");
    }

    [TestMethod]
    public void BurstMessagesToClients()
    {
      _clients[0].AddMessageHandler<TestMessage>(OnMessageReceived);
      _clients[1].AddMessageHandler<TestMessage>(OnMessageReceived);

      var sendTask0 = Task.Run(() => SendMessageBurst(_sessions[0]));
      var sendTask1 = Task.Run(() => SendMessageBurst(_sessions[1]));

      sendTask0.WaitAndAssert("Send task 0 did not finish");
      sendTask1.WaitAndAssert("Send task 1 did not finish");

      _receivedMessageWaitToken[0].WaitAndAssert("Not all messages from session 0 were received");
      _receivedMessageWaitToken[1].WaitAndAssert("Not all messages from session 1 were received");
    }

    [TestMethod]
    public void BurstRequestsToServer()
    {
      _server!.AddRequestHandler<TestRequest, TestResponse>(OnRequestReceived);

      var sendTask0 = Task.Run(() => SendRequestBurst(_clients[0]));
      var sendTask1 = Task.Run(() => SendRequestBurst(_clients[1]));

      sendTask0.WaitAndAssert("Send task 0 did not finish");
      sendTask1.WaitAndAssert("Send task 1 did not finish");

      _receivedRequestWaitToken[0].WaitAndAssert("Not all messages from client 0 were received");
      _receivedRequestWaitToken[1].WaitAndAssert("Not all messages from client 1 were received");
    }

    [TestMethod]
    public void BurstRequestsToClients()
    {
      _clients[0].AddRequestHandler<TestRequest, TestResponse>(OnRequestReceived);
      _clients[1].AddRequestHandler<TestRequest, TestResponse>(OnRequestReceived);

      var sendTask0 = Task.Run(() => SendRequestBurst(_sessions[0]));
      var sendTask1 = Task.Run(() => SendRequestBurst(_sessions[1]));

      sendTask0.WaitAndAssert("Send task 0 did not finish");
      sendTask1.WaitAndAssert("Send task 1 did not finish");

      _receivedRequestWaitToken[0].WaitAndAssert("Not all messages from session 0 were received");
      _receivedRequestWaitToken[1].WaitAndAssert("Not all messages from session 1 were received");
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
          await client.SendRequestAsync(new TestRequest { RequestText = TestRequestText, RequestCount = requestCount });
        }
        else if (communicator is NetMessageSession session)
        {
          await session.SendRequestAsync(new TestRequest { RequestText = TestRequestText, RequestCount = requestCount });
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
        Assert.IsTrue(delta > minDelta && delta < maxDelta, $"Timeout did not occur after expected time of {ResponseTimeoutMs}, it occurred after {delta} ms");
        return;
      }

      Assert.Fail("Expected TimeoutException was not thrown");
    }

    private void SendMessageBurst(object communicator)
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

    private void SendRequestBurst(object communicator)
    {
      var taskList = new List<Task<TestResponse>>();
      for (int i = 0; i < MessageCount; i++)
      {
        Task<TestResponse> requestTask;
        if (communicator is NetMessageClient client)
        {
          requestTask = client.SendRequestAsync(new TestRequest { RequestText = TestRequestText, RequestCount = i });
        }
        else if (communicator is NetMessageSession session)
        {
          requestTask = session.SendRequestAsync(new TestRequest { RequestText = TestRequestText, RequestCount = i });
        }
        else
        {
          throw new AssertFailedException($"Send request from unexpected communicator: {communicator}");
        }

        taskList.Add(requestTask);
      }

      foreach (var task in taskList)
      {
        var taskIndex = taskList.IndexOf(task);
        var result = task.WaitAndAssert($"Message from task {taskIndex} was not sent successfully");
        Assert.AreEqual(TestRequestText.ToLowerInvariant(), result.ResponseText, "Unexpected ResponseText received");
        Assert.AreEqual(taskIndex, result.ResponseCount, "Unexpected ResponseCount received");
      }
    }

    private void OnRequestReceived(object communicator, TypedRequest<TestRequest, TestResponse> tr)
    {
      tr.SendResponseAsync(new TestResponse
      {
        ResponseText = tr.Request.RequestText?.ToLowerInvariant(), 
        ResponseCount = tr.Request.RequestCount
      });

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
      OnCommunicatorError(server, errorMessage, ex);
    }

    private void OnCommunicatorError(object communicator, string errorMessage, Exception? ex)
    {
      if (_ignoreServerErrors)
      {
        return;
      }

      errorMessage = $"{communicator.GetType().Name} error: {errorMessage}";
      if (ex != null)
      {
        errorMessage += $" - {ex.GetType().Name}: {ex.Message}";
      }

      throw new AssertFailedException(errorMessage, ex);
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
    
    public int ResponseCount { get; set; }
  }
}
