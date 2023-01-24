using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetMessage.Integration.Test.TestFramework
{
  [TestClass]
  public class TestBase
  {
    public static TestContext? TestContext { get; private set; }

    [ClassInitialize]
    public static void TestBaseClassInitialize(TestContext testContext)
    {
      TestContext = testContext;
    }

    [TestInitialize]
    public void TestBaseInitialize()
    {
      AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
      TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    [TestCleanup]

    public void TestBaseCleanup()
    {
      TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
      AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
      if (e.ExceptionObject is Exception ex)
      {
        Assert.Fail($"Unhandled exception: {ex.Message}");
      }

      Assert.Fail($"Unhandled exception: {e.ExceptionObject}");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
      Assert.Fail($"Unhandled exception: {e.Exception.Message}");
    }
  }
}
