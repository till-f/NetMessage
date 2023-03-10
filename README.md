# NetMessage<br>
[![NuGet Download](https://img.shields.io/nuget/v/NetMessage.svg?style=flat)](https://www.nuget.org/packages/NetMessage/)
[![Build Status](https://img.shields.io/azure-devops/build/tsharpsoftware/netmessage/1/main)](https://tsharpsoftware.visualstudio.com/NetMessage/_build/latest?definitionId=1&branchName=main&view=codecoverage-tab)
[![Coverage](https://img.shields.io/azure-devops/coverage/tsharpsoftware/netmessage/1/main)](https://tsharpsoftware.visualstudio.com/NetMessage/_build/latest?definitionId=1&branchName=main&view=codecoverage-tab)

**Typesafe and lightweight RPC for .NET**

*NetMessage* is a small and easy to use RPC and messaging library. The `NetMessageClient` and `NetMessageServer` classes
provide typesafe communication for any kind of .NET application. All message types are defined by plain C# classes. No
configuration files, no external tools and no additional dependencies.

*NetMessage* can be an alternative to [gRPC](https://grpc.io/), if endpoints are implemented in .NET.


## Quickstart

### 1. Download
Get *NetMessage* from [NuGet](https://www.nuget.org/packages/NetMessage/ "NetMessage on NuGet.org").


### 2. Define packet types
Define classes for your packets, e.g.:

```cs
public class WeatherRequest : IRequest<WeatherResponse>
{
  public string City { get; set; }
  public DateTime Date { get; set; }
}

public class WeatherResponse
{
  public string Forecast { get; set; }
} 
```

Note that not only the packet types are defined by this, but also a communication contract between client and server, which is
enforced by the compiler: in this example, `WeatherRequest` is tied to the response type `WeatherResponse`. When an endpoint
receives a `WeatherRequest` packet, it *must* reply with an instance of `WeatherResponse`. On the caller side, no typecast
is needed to consume the response of the specifc type.

Besides request and response packets, endpoints can also send simple messages. See [Working Principle](#working-principle)
for more details.

All instances are (de)serialized by the selected `IDataSerializer`. By default, the `XmlSerializer` from .NET is used, which
imposes some [restrictions](https://learn.microsoft.com/en-us/dotnet/standard/serialization/introducing-xml-serialization#items-that-can-be-serialized).


### 3. Initialize and start server
Create a server instance, which will listen on the specified port, and add handlers for relevant events, messages
and requests. Then, start the server:

```cs
var server = new NetMessageServer(1234);

server.OnError += OnError;
server.SessionOpened += OnSessionOpened;
server.SessionClosed += OnSessionClosed;

server.AddMessageHandler<string>(StringMessageHandler);
server.AddRequestHandler<WeatherRequest, WeatherResponse>(WeatherRequestHandler);

server.Start();
```

Don't forget to implement the corresponding handlers, for example:

```cs
private static void WeatherRequestHandler(NetMessageSession session, 
                    TypedRequest<WeatherRequest, WeatherResponse> tr)
{
  Console.WriteLine($"REQUEST: How is the wheater in {tr.Request.City}?");
  var response = new WeatherResponse { Forecast = "Sunny" };
  tr.SendResponseAsync(response);
}
```

### 4. Initialize client and connect to server
Create the client instance and add handlers for relevant events, messages and requests.
Then, connect to the server using its host name and port number:

```cs
var client = new NetMessageClient();

client.OnError += OnError;
client.Connected += OnConnected;
client.Disconnected += OnDisconnected;

// of course, our client could also listen for communication initiated by the server
//client.AddMessageHandler<string>(StringMessageHandler);
//client.AddRequestHandler<Request, RequestResponse>(RequestHandler);

client.ConnectAsync("127.0.0.1", 1234);
```

### 5. Start communication
When the connection has been established, you are ready to send messages or requests:

```cs
// following line sends a simple string message
client.SendMessageAsync("Hello World!");

// following line sends a request and waits for the corresponding response
var response = await client.SendRequestAsync(new WeatherRequest { City = "Bonn" });
Console.WriteLine($"RESPONSE: {response.Forecast}");
```

## Example
Check out the *TypeSafe* example in the [Examples](https://github.com/till-f/NetMessage/tree/main/Examples) folder.
It is a small console application that can send a few pre-defined messages on a key press from the client to the
server and vice versa. Or just inspect the relevant files from here:

* [Packets](Examples/TypeSafe/Packets.cs)
* [ClientApp](Examples/TypeSafe.Client/NetMessageClientApp.cs)
* [ServerApp](Examples/TypeSafe.Server/NetMessageServerApp.cs)


## Closing Connections

To gracefully shut down a connection, call `Disconnect()` on the client or the corresponding session of the server. Both endpoints
are notified about the closed connection: on clients, the `Disconnected` event is triggered, on the server the `SessionClosed` event.

These events are also triggered in case of connection errors, for example, when the connection is reset by the operating system,
or when the connection was lost (see [heartbeats](#heartbeats-and-keepalive) below). To understand the reason
for disconnection, details are provided in the `SessionClosedArgs`. Possible reasons are defined by `ECloseReason`, see
[here](NetMessage.Base/SessionClosedArgs.cs).


## Error Handling
Every error or exception in the communication layer triggers the `OnError` event of the server or client. If the connection
is closed due to an error, the respective `Disconnected` or `SessionClosed` event is triggered first.


## Heartbeats and Keepalive
By default, heartbeat signals are send by both endpoints in certain intervals (`HeartbeatInterval`). If one endpoint does not
receive a packet or heartbeat signal for a certain time (`ReceiveTimeout`), it will assume that the connection was lost and closes
the session with a `ConnectionLost` reason. The default timing values can be found [here](NetMessage.Base/Defaults.cs) (among others).

If heartbeats are disabled, the TCP "keep alive" mechanism provided by the operating system is used. To disable heartbeats, set
`HeartbeatInterval` to a value smaller or equal to zero. In that case, the OS should send the first keep alive message when no data
was transmitted for `KeepAliveTime` and should then retry after `KeepAliveInterval` if no acknowledgement was received. The number of
retries depends on the OS settings (it should be a fixed value of 10 for recent versions of Microsoft Windows). However, the keep alive
timing values are not properly considered in all cases and it may take *much* longer before a connection loss is detected. If the
detection of a connection loss is important, it is highly recommended to use heartbeats instead of keep alive.


## Working Principle
Three basic packet kinds can be distinguished:

* **Message**: an arbitrary message
* **Request**: a message that expects a response of a specific type
* **Response**: the response for a request

The client or server application can register on the `NetMessageClient` or `NetMessageServer` class to be notified when
a message or request *with a specific type* is received:

* `void AddMessageHandler<TTData>(Action<NetMessageClient, TTData> messageHandler)`
  * Where `TTData` is the expected message type without constraints.
* `void AddRequestHandler<TTData, TTRsp>(Action<NetMessageClient, TypedRequest<TTData, TTRsp>> requestHandler)`
  * Where `TTData` is the expected request type and must derive `IRequest<TTRsp>`. `TTRsp` is the expected response type without 
    constraints. The `IRequest` interface is otherwise empty.

Once connected, both the client and the server application can initiate sending of messages or requests on the
`NetMessageClient` or `NetMessageSession` class (one session is created on the server for every connected client):

* `Task<bool> SendMessageAsync(object message)`
  * Note that a message can be of any type, but the receiver must have added a handler for the appropriate type to receive it.
* `Task<TTRsp> SendRequestAsync<TTRsp>(IRequest<TTRsp> request)`
  * The task provides access to the received response with the appropriate type. If no response was received before a given
    timeout, a TimeoutException is thrown. The timeout value can be configured on the client or the server object.
  * Note that like for messages, the receiver must have added a handler for the appropriate type to receive the request.

A response is sent by calling the following method diretly on the received `TypedRequest<TTData, TTRsp>` object, which wraps
the original user request object of type `TTData`:

* `Task<bool> SendResponseAsync(TTRsp response)`
  * The type of the response is constrained by the received request. The sender of the request is notified transparently,
    it is not possible to add an explicit handler.


## Customization
If you just want to change the way how C# objects are (de)serialized, e.g., if you want to use Json instead of XML, all you have to do
is implementing the `IDataSerializer` interface ([IDataSerializer.cs](NetMessage/IDataSerializer.cs)) and pass the corresponding instance
to the `NetMessageServer` and `NetMessageClient` constructor respectively. Take a look at the existing implementation for XML in
[XmlDataSerializer.cs](NetMessage/XmlDataSerializer.cs) if you need an example.

If you want to implement a truly custom protocol, you can still take advantage of the event based notifications provided by *NetMessage*,
but the higher layer for transparent (de)serialization of C# objects is not available. If this is OK, you can use the *NetMessage.Base*
[NuGet package](https://www.nuget.org/packages/NetMessage.Base "NetMessage.Base on NuGet.org") directly. You have to implement the `IProtocol`
interface (see [IProtocol.cs](NetMessage.Base/IProtocol.cs)) and must add the corresponding concrete classes for the server, client, session
and request objects. See the *SimpleString* Example under [Examples](https://github.com/till-f/NetMessage/tree/main/Examples)
for details.


## Tests
There is small but strong collection of integration tests, see [here](https://github.com/till-f/NetMessage/tree/main/Tests).


## Roadmap
* Auto reconnect feature (if desired, client automatically reconnects to the server after a connection loss)
* Notification when receiving message/request without corresponding handler
* Performance measurements
* TLS encryption
