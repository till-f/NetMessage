using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetMessage.Integration.Test.TestFramework
{
  public static class TaskExtensions
  {
    public static void WaitAndAssert(this Task t, string message)
    {
      var result = t.Wait(WaitToken.Timeout);
      Assert.IsTrue(result, $"{message}; Task timed out");
      Assert.IsNull(t.Exception, $"{message}; Task threw {t.Exception?.GetType().Name}: {t.Exception?.Message}");
      Assert.IsFalse(t.IsFaulted, $"{message}; Task faulted");
    }
  }
}
