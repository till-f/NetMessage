using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetMessage.Integration.Test.TestFramework
{
  public static class TaskExtensions
  {
    public static void WaitAndAssert(this Task t, string message)
    {
      var result = t.Wait(WaitToken.Timeout);
      Assert.IsTrue(result, $"Timeout: {message}");
      Assert.IsNull(t.Exception, $"{t.Exception?.Message}");
    }
  }
}
