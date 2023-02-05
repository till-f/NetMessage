using System;


namespace NetMessage.Base
{
  public class ConnectionLostException : Exception
  {
    public ConnectionLostException(string message) : base(message)
    {
    }
  }
}
