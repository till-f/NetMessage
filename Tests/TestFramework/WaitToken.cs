using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetMessage.Integration.Test.TestFramework
{
  public class WaitToken
  {
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly CountdownEvent _countDownEvent;

    public WaitToken(int initialCount)
    {
      _countDownEvent = new CountdownEvent(initialCount);
    }

    public void Signal()
    {
      _countDownEvent.Signal();
    }

    public void WaitAndAssert(string message)
    {
      var result = _countDownEvent.Wait(Timeout);
      Assert.IsTrue(result, $"Timeout: {message}");
    }
  }
}
