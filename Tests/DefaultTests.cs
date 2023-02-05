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
  /// Test class for integration tests of <see cref="NetMessageClient"/> and <see cref="NetMessageServer"/> with a
  /// "default" server and clients.
  /// 
  /// For all tests, it keeps track of received packets and provides a <see cref="WaitToken"/> so that tests can wait for
  /// received packets.
  ///
  /// The test initialize method will setup the WaitTokens and received packet counts and connects all clients to the server.
  /// Note that additional setup is performed by the base class.
  /// </summary>
  [TestClass]
  public class DefaultTests : TestBase
  {
    // the number of messages that should be sent for the "burst" tests
    private const int MessageCount = 1000;

    // memento for the number of received messages/requests; initialized with zero in TestInitialize
    private readonly int[] _receivedMessagesCount = new int[ClientCount];
    private readonly int[] _receivedRequestsCount = new int[ClientCount];

    // wait token for the 'burst' tests; used to wait until all ('MessageCount') messages were received; initialized in TestInitialize
    private readonly WaitToken[] _receivedMessageWaitToken = new WaitToken[ClientCount];
    private readonly WaitToken[] _receivedRequestWaitToken = new WaitToken[ClientCount];

    [TestInitialize]
    public void TestInitialize()
    {
      _server!.Start();

      for (int i = 0; i < ClientCount; i++)
      {
        _receivedMessagesCount[i] = 0;
        _receivedRequestsCount[i] = 0;
        _receivedMessageWaitToken[i] = new WaitToken(MessageCount);
        _receivedRequestWaitToken[i] = new WaitToken(MessageCount);

        ConnectClient(i);
      }
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

    [TestMethod]
    public async Task ServerRespondsTooSlow()
    {
      // Test 1: successful request
      _server!.AddRequestHandler<TestRequest, TestResponse>(OnRequestReceived);

      _receivedRequestWaitToken[0] = new WaitToken(1);
      var result = await _clients[0].SendRequestAsync(new TestRequest { RequestText = TestRequestText, RequestCount = 0 });
      Assert.AreEqual(TestRequestText.ToLowerInvariant(), result.ResponseText, "Unexpected ResponseText received");
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

      // test cleanup will disconnect the client in the next step, but the server might still try to send the response
      // (this is a race condition that's hard to avoid, so just ignore upcoming errors)
      _ignoreServerErrors = true;
    }

    [TestMethod]
    public async Task ClientRespondsTooSlow()
    {
      // Test 1: successful request
      _clients[0].AddRequestHandler<TestRequest, TestResponse>(OnRequestReceived);

      _receivedRequestWaitToken[0] = new WaitToken(1);
      var result = await _sessions[0].SendRequestAsync(new TestRequest { RequestText = TestRequestText, RequestCount = 0 });
      Assert.AreEqual(TestRequestText.ToLowerInvariant(), result.ResponseText, "Unexpected ResponseText received");
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

        var result = requestTask.WaitAndAssert($"Message from task {i} was not sent successfully");
        Assert.AreEqual(TestRequestText.ToLowerInvariant(), result.ResponseText, "Unexpected ResponseText received");
        Assert.AreEqual(i, result.ResponseCount, "Unexpected ResponseCount received");
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

      var expectedCount = _receivedRequestsCount[communicatorIndex]++;
      Assert.AreEqual(expectedCount, tr.Request.RequestCount, "Unexpected request count");
      _receivedRequestWaitToken[communicatorIndex].Signal();
    }

    private void OnMessageReceived(object communicator, TestMessage message)
    {
      Assert.AreEqual(message.MessageText, TestMessageText);

      var communicatorIndex = GetCommunicatorIndex(communicator);

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
  }
}
