using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetMessage.Integration.Test.TestFramework
{
  [TestClass]
  public class TestBase
  {
    public TestContext? TestContext { get; set; }

    [TestInitialize]
    public void TestBaseInitialize()
    {
    }

    [TestCleanup]

    public void TestBaseCleanup()
    {
    }
  }
}
