using System;
using System.Threading;

namespace NetMessage.Base
{
  /// <summary>
  /// Lock object that guarantees entering the locked section in the
  /// same order in which the lock was acquired.
  /// see https://stackoverflow.com/questions/961869/is-there-a-synchronization-class-that-guarantee-fifo-order-in-c/961904#961904
  /// </summary>
  public sealed class QueuedLockProvider
  {
    private readonly object _innerLock;
    private volatile int _ticketsCount = 0;
    private volatile int _ticketToRide = 1;

    public QueuedLockProvider()
    {
      _innerLock = new object();
    }

    public Lock GetLock()
    {
      return new Lock(this);
    }

    private void Enter()
    {
      int myTicket = Interlocked.Increment(ref _ticketsCount);
      Monitor.Enter(_innerLock);
      while (true)
      {

        if (myTicket == _ticketToRide)
        {
          return;
        }
        else
        {
          Monitor.Wait(_innerLock);
        }
      }
    }

    private void Exit()
    {
      Interlocked.Increment(ref _ticketToRide);
      Monitor.PulseAll(_innerLock);
      Monitor.Exit(_innerLock);
    }

    public class Lock : IDisposable
    {
      private readonly QueuedLockProvider _lockProvider;

      internal Lock(QueuedLockProvider lockProvider)
      {
        _lockProvider = lockProvider;
        _lockProvider.Enter();
      }

      public void Dispose()
      {
        _lockProvider.Exit();
      }
    }
  }
}
