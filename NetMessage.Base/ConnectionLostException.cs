using System;


namespace NetMessage.Base
{
  public class ConnectionLostException : Exception
  {
    public ConnectionLostException(int timeout) : base($"No heartbeat was received after {timeout} ms") 
    {
    }
  }
}
