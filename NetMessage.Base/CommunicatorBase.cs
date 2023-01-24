using NetMessage.Base.Message;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetMessage.Base
{
  public abstract class CommunicatorBase<TRequest, TProtocol, TPld>
    where TRequest : Request<TRequest, TProtocol, TPld>
    where TProtocol : class, IProtocol<TPld>
  {
    private readonly ConcurrentDictionary<int, ResponseEvent<TPld>> _responseEvents = new ConcurrentDictionary<int, ResponseEvent<TPld>>();
    private readonly QueuedLockProvider _sendLockProvider = new QueuedLockProvider();

    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private int _responseIdCounter;

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
    protected abstract void HandleMessage(Message<TPld> message);

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
    /// Sends request to the remote socket and awaits the corresponding response.
    /// Protected because concrete implementations may prefer that this method is not exposed.
    /// </summary>
    protected async Task<Response<TPld>?> SendRequestInternalAsync(TPld requestPayload)
    {
      int responseId;
      lock (_responseEvents)
      {
        responseId = _responseIdCounter++;
      }

      byte[] rawData;
      try
      {
        rawData = ProtocolBuffer!.ToRawRequest(requestPayload, responseId);
      }
      catch (Exception ex)
      {
        NotifyError($"{ex.GetType().Name} while converting to raw format", ex);
        throw;
      }

      var waitToken = new ManualResetEventSlim(false);
      var responseEvent = new ResponseEvent<TPld>(waitToken);
      _responseEvents[responseId] = responseEvent;

      var sendResult = await SendRawDataAsync(rawData);
      if (!sendResult)
      {
        _responseEvents.TryRemove(responseId, out _);

        if (!CancellationToken.IsCancellationRequested)
        {
          throw new InvalidOperationException("Failed sending request to remote socket");
        }
        return null;
      }

      var waitResponseResult = waitToken.Wait(TimeSpan.FromSeconds(10), CancellationToken);
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

    /// <summary>
    /// Sends message to the remote socket.
    /// Protected because concrete implementations may prefer that this method is not exposed.
    /// </summary>
    protected Task<bool> SendMessageInternalAsync(TPld messagePayload)
    {
      try
      {
        var rawData = ProtocolBuffer!.ToRawMessage(messagePayload);
        return SendRawDataAsync(rawData);
      }
      catch (Exception ex)
      {
        NotifyError($"{ex.GetType().Name} while converting to raw format", ex);
        return Task.FromResult(false);
      }
    }

    /// <summary>
    /// Starts receiving messages from the remote socket.
    /// </summary>
    protected Task ReceiveAsync()
    {
      var receiveTask = Task.Run(() =>
      {
        while (true)
        {
          try
          {
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[RemoteSocket!.ReceiveBufferSize]);
            var receiveTask = RemoteSocket.ReceiveAsync(buffer, SocketFlags.None);
            receiveTask.Wait(CancellationToken);

            if (!receiveTask.IsCompleted)
            {
              // should never occur
              throw new InvalidOperationException("ReceiveTask terminated abnormally");
            }

            int byteCount = receiveTask.Result;
            if (receiveTask.Result == 0)
            {
              continue;
            }

            var rawData = new byte[byteCount];
            Array.Copy(buffer.Array!, rawData, byteCount);
            var receivedMessages = ProtocolBuffer!.FromRaw(rawData);
            foreach (var messageInfo in receivedMessages)
            {
              if (messageInfo is Message<TPld> message)
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
              else if (messageInfo is Response<TPld> response)
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

      // The receive task is very robust and almost impossible to fail. Any exception is propagated via the OnError event.
      // Only if an exception occurs inside an OnError handler, this exception would stop the receive thread more or less silently.
      // To avoid this, we fail/crash the environment if the receive task faulted.
      receiveTask.ContinueWith(c => Environment.FailFast($"Receive task faulted: {c.Exception?.Message}", c.Exception), TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

      return receiveTask;
    }

    protected Task<bool> SendRawDataAsync(byte[] rawData)
    {
      return Task.Run(() =>
      {
        // only one send request per communicator at once
        using (_sendLockProvider.GetLock())
        {
          try
          {
            if (!IsConnected)
            {
              NotifyError("Cannot send to socket because it is not connected", null);
              return false;
            }

            var sendTask = RemoteSocket!.SendAsync(new ArraySegment<byte>(rawData), SocketFlags.None);
            sendTask.Wait(CancellationToken);

            if (!sendTask.IsCompleted)
            {
              // should never occur
              throw new InvalidOperationException("SendTask terminated abnormally");
            }

            return true;
          }
          catch (Exception ex)
          {
            HandleException(ex);
            return false;
          }
        }
      }, CancellationToken);
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
  }
}
