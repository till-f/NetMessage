using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetMessage.Base;
using System;

namespace NetMessage.Integration.Test.TestFramework
{
  /// <summary>
  /// Base class for integration tests.
  /// 
  /// For all tests, it maintains one server and N clients + respective sessions and keeps track of last
  /// opened/closed session.
  /// 
  /// The test initialize method will construct server and client, add the basic handlers. It will not
  /// start the server and does not connect the clients so that server and/or client config can still be
  /// changed.
  /// 
  /// The test cleanup method will disconnect all clients, stop the server and disposes them.
  /// 
  /// The server and all client have the <see cref="CommunicatorBase{TRequest,TProtocol,TData}.FailOnFaultedReceiveTask"/>
  /// property set to true and a throwing handler is registered for the OnError event. This means that the the test
  /// framework is killed on every receive error. The original exception (if any) is contained as inner exception to
  /// support debugging (Note that Visual Studio does not show the exception or stack trace in the Test Explorer if the 
  /// framework fails, so it may be necessary to check the Output window for Tests). For similar reasons, tests will also
  /// fail on every send error. To avoid spurious failing tests when test cleanup is called, <see cref="_ignoreServerErrors"/>
  /// may be used in edge cases.
  /// </summary>
  [TestClass]
  public class TestBase
  {
    // host, port and some dummy data for testing
    protected const string ServerHost = "127.0.0.1";
    protected const int ServerPort = 1234;
    protected const int ResponseTimeoutMs = 200;
    protected const int ResponseTimeoutMaxDiscr = 20;
    protected const string TestMessageText = "TestMessage";
    protected const string TestRequestText = "TestRequest";
    protected const int ClientCount = 2;

    // the server and all clients are constructed and connected in TestInitialize
    protected NetMessageServer? _server;
    protected readonly NetMessageClient[] _clients = new NetMessageClient[ClientCount];
    protected readonly NetMessageSession[] _sessions = new NetMessageSession[ClientCount];

    // always contains the last opened/closed session
    protected NetMessageSession? _lastOpenedSession;
    protected NetMessageSession? _lastClosedSession;
    protected SessionClosedArgs? _lastSessionClosedArgs;

    // always contains the last disconnected client
    protected NetMessageClient? _lastDisconnectedClient;
    protected SessionClosedArgs? _lastClientDisconnectedArgs;

    // can be used to wait for a session being opened / closed, but a new wait token must be constructed by the test
    protected WaitToken? _sessionOpenedWt;
    protected WaitToken? _sessionClosedWt;
    protected WaitToken? _clientDisconnectedWt;

    // may be used to avoid failing tests in edge cases (see class comment)
    protected bool _ignoreServerErrors;

    public TestContext? TestContext { get; set; }

    [TestInitialize]
    public void TestBaseInitialize()
    {
      _ignoreServerErrors = false;
      _lastOpenedSession = null;
      _lastClosedSession = null;
      _lastDisconnectedClient = null;
      _lastClientDisconnectedArgs = null;
      _lastSessionClosedArgs = null;

      _server = new NetMessageServer(ServerPort);
      _server.ResponseTimeout = TimeSpan.FromMilliseconds(ResponseTimeoutMs);
      _server.FailOnFaultedReceiveTask = true;
      _server.OnError += OnServerError;
      _server.SessionOpened += OnSessionOpened;
      _server.SessionClosed += OnSessionClosed;

      for (int i = 0; i < ClientCount; i++)
      {
        _clients[i] = new NetMessageClient();
        _clients[i].ResponseTimeout = TimeSpan.FromMilliseconds(ResponseTimeoutMs);
        _clients[i].FailOnFaultedReceiveTask = true;
        _clients[i].OnError += OnError;
        _clients[i].Disconnected += OnDisconnected;
      }
    }

    [TestCleanup]
    public void TestBaseCleanup()
    {
      for (int i = 0; i < ClientCount; i++)
      {
        _clients[i].Disconnect();
        _clients[i].Dispose();
      }

      _server!.Stop();
      _server!.Dispose();
    }

    protected void ConnectClient(int clientIndex)
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

    protected virtual void OnCommunicatorError(object communicator, int clientIndex, Exception? ex)
    {
    }

    private void OnServerError(NetMessageServer server, NetMessageSession? session, string errorMessage, Exception? ex)
    {
      if (session == null)
      {
        OnError(server, errorMessage, ex);
      }
      else
      {
        OnError(session, errorMessage, ex);
      }
    }

    private void OnError(object errorSource, string errorMessage, Exception? ex)
    {
      if (errorSource is NetMessageSession session)
      {
        OnCommunicatorError(session, Array.IndexOf(_sessions, session), ex);
      }
      else if (errorSource is NetMessageClient client)
      {
        OnCommunicatorError(client, Array.IndexOf(_clients, client), ex);
      }

      if (_ignoreServerErrors)
      {
        return;
      }

      errorMessage = $"{errorSource.GetType().Name} error: {errorMessage}";
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

    protected virtual void OnSessionClosed(NetMessageSession session, SessionClosedArgs args)
    {
      _lastClosedSession = session;
      _lastSessionClosedArgs = args;
      _sessionClosedWt?.Signal();
    }

    protected virtual void OnDisconnected(NetMessageClient client, SessionClosedArgs args)
    {
      _lastDisconnectedClient = client;
      _lastClientDisconnectedArgs = args;
      _clientDisconnectedWt?.Signal();
    }
  }
}
