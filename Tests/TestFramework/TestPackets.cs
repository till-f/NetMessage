namespace NetMessage.Integration.Test.TestFramework
{
  public class TestMessage
  {
    public string? MessageText { get; set; }

    public int MessageCount { get; set; }
  }

  public class TestRequest : IRequest<TestResponse>
  {
    public string? RequestText { get; set; }

    public int RequestCount { get; set; }
  }

  public class TestResponse
  {
    public string? ResponseText { get; set; }

    public int ResponseCount { get; set; }
  }
}
