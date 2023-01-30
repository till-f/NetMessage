using NetMessage.Base.Message;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetMessage.Base
{
  public abstract class CommunicatorBase<TRequest, TProtocol, TData> : IDisposable
    where TRequest : Request<TRequest, TProtocol, TData>
    where TProtocol : class, IProtocol<TData>
  {
    private readonly ConcurrentDictionary<int, ResponseEvent<TData>> _responseEvents = new ConcurrentDictionary<int, ResponseEvent<TData>>();

    private readonly ManualResetEvent _receiveTaskStoppedEvent = new ManualResetEvent(false);
    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private int _responseIdCounter;

    /// <summary>
    /// The time to wait for the response after sending a request before a TimeoutException is thrown.
    /// Use a TimeSpan that represents -1 millisecond to wait indefinitely.
    /// The default value is 10 seconds.
    /// </summary>
    public virtual TimeSpan ResponseTimeout { get; set; } = Defaults.ResponseTimeout;

    /// <summary>
    /// If true, the environment/application will be terminated if the receive task faulted. This may
    /// only happens if a handler of the OnError event fails. The default value is 'false' which means
    /// that the receive task still dies but the application keeps running.
    /// </summary>
    public virtual bool FailOnFaultedReceiveTask { get; set; }
    
    /// <summary>
    /// Used to retrieve the remote socket.
    /// </summary>
    protected abstract Socket? RemoteSocket { get; }

    /// <summary>
    /// The protocol specific buffer used for sending / receiving.
    /// </summary>
    protected abstract TProtocol? ProtocolBuffer { get; }

    /// <summary>
    /// Called when the connection was closed.
    /// </summary>
    protected abstract void NotifyClosed();

    /// <summary>
    /// Called when an error occured.
    /// If the error breaks the connection, <see cref="NotifyClosed"/> is called first.
    /// </summary>
    protected abstract void NotifyError(string errorMessage, Exception? exception);

    /// <summary>
    /// Called for every message received.
    /// </summary>
    protected abstract void HandleMessage(Message<TData> message);

    /// <summary>
    /// Called for every request received.
    /// </summary>
    protected abstract void HandleRequest(TRequest request);

    /// <summary>
    /// Retrieves the cancellation token. It is set when close is requested.
    /// It can be reset by calling <see cref="ResetCancellationToken"/>.
    /// </summary>
    protected CancellationToken CancellationToken => _cancellationTokenSource.Token;

    /// <summary>
    /// Retrieves if the underlying socket is connected.
    /// </summary>
    public bool IsConnected => RemoteSocket != null && RemoteSocket.Connected;

    /// <summary>
    /// Cancels async operations, closes the socket and disposes it.
    /// </summary>
    public void Close()
    {
      _cancellationTokenSource.Cancel();
      _receiveTaskStoppedEvent.WaitOne(TimeSpan.FromSeconds(1));
      RemoteSocket?.Close();
      RemoteSocket?.Dispose();
      NotifyClosed();
    }

    /// <summary>
    /// Resets the CancellationToken so that communication can be cancelled again.
    /// Can only be called when communication is inactive (socket not connected).
    /// </summary>
    protected void ResetCancellationToken()
    {
      if (IsConnected)
      {
        NotifyError("Cannot reset CancellationToken because socket is connected", null);
        return;
      }

      _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Converts the message to its raw format and sends it to the remote socket.
    /// Exceptions during conversion are thrown synchronously. The asynchronous send task returns
    /// the number of bytes sent if successful, otherwise it completes with an invalid socket error.
    /// 
    /// Protected because concrete implementations may prefer that this method is not exposed.
    /// </summary>
    protected Task<int> SendMessageInternalAsync(TData data)
    {
      var rawData = ProtocolBuffer!.ToRawMessage(data);
      return SendRawDataAsync(rawData);
    }

    /// <summary>
    /// Sends request to the remote socket and awaits the corresponding response.
    /// Protected because concrete implementations may prefer that this method is not exposed.
    /// </summary>
    protected async Task<Response<TData>?> SendRequestInternalAsync(TData data)
    {
      int responseId;
      lock (_responseEvents)
      {
        responseId = _responseIdCounter++;
      }

      byte[] rawData;
      try
      {
        rawData = ProtocolBuffer!.ToRawRequest(data, responseId);
      }
      catch (Exception ex)
      {
        NotifyError($"{ex.GetType().Name} while converting to raw format", ex);
        throw;
      }

      var waitToken = new ManualResetEventSlim(false);
      var responseEvent = new ResponseEvent<TData>(waitToken);
      _responseEvents[responseId] = responseEvent;

      var sendResult = await SendRawDataAsync(rawData);
      if (sendResult <= 0)
      {
        _responseEvents.TryRemove(responseId, out _);

        if (!CancellationToken.IsCancellationRequested)
        {
          throw new InvalidOperationException("Failed sending request to remote socket");
        }
        return null;
      }

      var waitResponseResult = waitToken.Wait(ResponseTimeout, CancellationToken);
      if (!waitResponseResult)
      {
        if (!CancellationToken.IsCancellationRequested)
        {
          throw new TimeoutException($"Timeout while waiting for response id {responseId}");
        }
        return null;
      }

      return responseEvent.Response;
    }

    protected Task<int> SendRawDataAsync(byte[] rawData)
    {
      if (!IsConnected)
      {
        NotifyError("Cannot send when not connected", null);
        return Task.FromResult(-1);
      }

      var sendTask = RemoteSocket!.SendAsync(new ArraySegment<byte>(rawData), SocketFlags.None);
      return sendTask;
    }

    /// <summary>
    /// Starts receiving messages from the remote socket.
    /// </summary>
    protected void StartReceiveAsync()
    {
      _receiveTaskStoppedEvent.Reset();

      var receiveTask = Task.Run(() =>
      {
        while (!CancellationToken.IsCancellationRequested)
        {
          try
          {
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[RemoteSocket!.ReceiveBufferSize]);
            var singleReceiveTask = RemoteSocket.ReceiveAsync(buffer, SocketFlags.None);
            singleReceiveTask.Wait(CancellationToken);

            if (!singleReceiveTask.IsCompleted || singleReceiveTask.IsFaulted)
            {
              // should never occur
              throw new InvalidOperationException("Single receive terminated abnormally");
            }

            int byteCount = singleReceiveTask.Result;
            if (singleReceiveTask.Result == 0)
            {
              continue;
            }

            var rawData = new byte[byteCount];
            Array.Copy(buffer.Array!, rawData, byteCount);
            var receivedMessages = ProtocolBuffer!.FromRaw(rawData);
            foreach (var messageInfo in receivedMessages)
            {
              if (messageInfo is Message<TData> message)
              {
                HandleMessage(message);
              }
              else if (messageInfo is TRequest request)
              {
                request.SetContext(
                  (msg, id) => ProtocolBuffer!.ToRawResponse(msg, id),
                  SendRawDataAsync,
                  NotifyError
                  );
                HandleRequest(request);
              }
              else if (messageInfo is Response<TData> response)
              {
                var responseEvent = _responseEvents[response.ResponseId];
                _responseEvents.TryRemove(response.ResponseId, out _);
                responseEvent.Response = response;
                responseEvent.Set();
              }
            }
          }
          catch (Exception ex)
          {
            if (HandleException(ex)) return;
          }
        }
      }, CancellationToken);

      _receiveTaskStoppedEvent.Set();

      // The receive task is very robust and almost impossible to fail. Any exception is propagated via the OnError event.
      // Only if an exception occurs inside an OnError handler, this exception would stop the receive task more or less silently.
      // By default, this is what happens, but the user may set FailOnFaultedReceiveTask='true' to avoid this. In that case, we
      // fail/crash the environment if the task faulted.
      if (FailOnFaultedReceiveTask)
      {
        receiveTask.ContinueWith(c => Environment.FailFast($"Receive task faulted: {c.Exception?.Message}", c.Exception), TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
      }
    }

    /// <summary>
    /// Handler for exceptions in the async background tasks.
    /// Returns true to indicate that task should be aborted.
    /// </summary>
    protected bool HandleException(Exception ex)
    {
      // CancellationToken was triggered. This is NOT an error (do not notify about it)
      if (ex is OperationCanceledException)
      {
        return true;
      }

      if (ex.InnerException is SocketException se)
      {
        if (se.SocketErrorCode != SocketError.ConnectionReset)
        {
          NotifyError($"Socket Error {se.SocketErrorCode}", se);
        }

        // TODO: depending on kind of error, Close or try to Reconnect
        Close();
        return true;
      }

      NotifyError($"Unexpected {ex.GetType().Name}", ex);

      // It is possible that the connection was closed after the wait completed, but before next wait started.
      // In that case, a NullReferenceException (or similar) might be thrown because the RemoteSocket is not
      // functional. This case is detected by checking IsCancellationRequested again. If it was requested,
      // ignore the error.
      if (CancellationToken.IsCancellationRequested)
      {
        Console.WriteLine($"DEBUG: Exception above was thrown after cancellation was requested!");
        return true;
      }

      return false;
    }

    public void Dispose()
    {
      Close();
    }
  }
}
