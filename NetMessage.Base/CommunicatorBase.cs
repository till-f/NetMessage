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
    private readonly ManualResetEvent _disconnectionFinishedEvent = new ManualResetEvent(true);

    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _isClosing;
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
    /// It can be reset by calling <see cref="ResetConnectionState"/>.
    /// </summary>
    protected CancellationToken CancellationToken => _cancellationTokenSource.Token;

    /// <summary>
    /// Retrieves if the underlying socket is connected.
    /// </summary>
    public bool IsConnected => RemoteSocket != null && RemoteSocket.Connected;

    /// <summary>
    /// Gracefully closes the connection.
    /// This call is blocking and the default timeout is <see cref="Defaults.DisconnectTimeout"/>.
    /// It is ensured that <see cref="Close"/> was called when this method returns.
    /// </summary>
    public void Disconnect(TimeSpan? timeout = null)
    {
      if (IsConnected)
      {
        _disconnectionFinishedEvent.Reset();
        RemoteSocket?.Shutdown(SocketShutdown.Send);
        var result = _disconnectionFinishedEvent.WaitOne(timeout ?? Defaults.DisconnectTimeout);
        if (!result)
        {
          NotifyError("Timeout while waiting for the acknowledgement of disconnect", null);
          Close();
        }
      }
    }

    /// <summary>
    /// Cancels async operations, closes the socket and disposes it.
    /// It does not wait for async operations to signal completion.
    /// It does not care about the other endpoint.
    /// Use <see cref="Disconnect"/> to gracefully close the connection.
    /// </summary>
    public void Close()
    {
      if (_isClosing)
      {
        return;
      }
      _isClosing = true;

      _cancellationTokenSource.Cancel();
      RemoteSocket?.Close();
      RemoteSocket?.Dispose();

      _disconnectionFinishedEvent.Set();
      NotifyClosed();
    }

    /// <summary>
    /// Resets the state so that connection can be closed again.
    /// This resets _isClosing and _cancellationTokenSource.
    /// Must not be called when communication is active (socket already connected).
    /// </summary>
    protected void ResetConnectionState()
    {
      if (IsConnected)
      {
        NotifyError("Cannot reset connection because socket is connected", null);
        return;
      }

      _isClosing = false;
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
      var receiveTask = Task.Run(() =>
      {
        while (!CancellationToken.IsCancellationRequested)
        {
          try
          {
            var buffer = new ArraySegment<byte>(new byte[RemoteSocket!.ReceiveBufferSize]);
            var singleReceiveTask = RemoteSocket.ReceiveAsync(buffer, SocketFlags.None);
            singleReceiveTask.Wait(CancellationToken);

            if (!singleReceiveTask.IsCompleted || singleReceiveTask.IsFaulted)
            {
              // should never occur
              throw new InvalidOperationException("Single receive terminated abnormally");
            }

            var byteCount = singleReceiveTask.Result;

            if (byteCount == 0)
            {
              // successful completion of a zero-byte receive operation indicates graceful closure of remote socket
              RemoteSocket.Shutdown(SocketShutdown.Both);
              RemoteSocket.Disconnect(false);
              Close();
              return;
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
            if (HandleReceiveException(ex)) return;
          }
        }
      }, CancellationToken);

      // The receive task is very robust and almost impossible to fail. Any exception is propagated via the OnError event.
      // Only if an exception occurs inside an OnError handler, this exception would stop the receive task more or less silently.
      // By default, this is what happens, but the user may set FailOnFaultedReceiveTask='true' to avoid this. In that case, we
      // fail/crash the environment if the task faulted.
      receiveTask.ContinueWith(c =>
      {
        if (FailOnFaultedReceiveTask)
        {
          Environment.FailFast($"Receive task faulted: {c.Exception?.Message}", c.Exception);
        }
      }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
    }

    /// <summary>
    /// Handler for exceptions in the async receive tasks.
    /// Returns true to indicate that the receive should be aborted.
    /// </summary>
    protected bool HandleReceiveException(Exception ex)
    {
      // CancellationToken was triggered. This is not an error (do not notify about it)
      if (ex is OperationCanceledException)
      {
        return true;
      }

      // Socket exception occurred. In this case, we consider the connection unhealthy and we close it.
      // FUTURE: depending on kind of error, try automatic reconnect / restoring the connection
      if (ex.InnerException is SocketException se)
      {
        NotifyError($"Unexpected socket error in receive task: {se.SocketErrorCode}", se);
        Close();
        return true;
      }

      // Another exception occurred. In this case, the connection should still be usable.
      NotifyError($"Unexpected {ex.GetType().Name} in receive task: {ex.Message}", ex);
      return false;
    }

    public void Dispose()
    {
      Close();
    }
  }
}
