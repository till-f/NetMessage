﻿namespace NetMessage.Base.Message
{
  public interface IPacket<TData>
  {
    TData Data { get; }
  }
}