namespace NetMessage
{
  public interface IPayloadSerializer
  {
    T Deserialize<T>(string payloadString);

    string Serialize(object o);
  }
}