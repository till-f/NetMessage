# NetMessage<br>
[![NuGet Download](https://img.shields.io/nuget/v/NetMessage.svg?style=flat)](https://www.nuget.org/packages/NetMessage/)
[![Build Status](https://img.shields.io/azure-devops/build/tsharpsoftware/netmessage/1/main)](https://tsharpsoftware.visualstudio.com/NetMessage/_build/latest?definitionId=1&branchName=main&view=codecoverage-tab)
[![Coverage](https://img.shields.io/azure-devops/coverage/tsharpsoftware/netmessage/1/main)](https://tsharpsoftware.visualstudio.com/NetMessage/_build/latest?definitionId=1&branchName=main&view=codecoverage-tab)

**Typesafe and lightweight RPC for .NET**

*NetMessage* is a small and easy to use RPC and messaging library. The `NetMessageClient` and `NetMessageServer` classes
provide typesafe communication for any kind of .NET application. All message types are defined by plain C# classes. No
configuration files, no external tools and no additional dependencies.


## Quickstart

### 1. Download
Get *NetMessage* from [NuGet](https://www.nuget.org/packages/NetMessage/ "NetMessage on NuGet.org").


### 2. Define Data Types
Create the message, request and response types, for example:

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

*Instances of these classes are (de)serialized by the selected `IDataSerializer`. By default, the `XmlSerializer` from
.NET is used.*

### 3. Initialize and start server
Create the server instance with the port number for listening and add handlers for events, messages and requests.
Then, start the server:

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
Create the client instance and add handlers for events, messages and requests.
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
When the connection was established successfully, you are ready to send messages and requests and to retrieve the
corresponding response:

```cs
client.SendMessageAsync("Hello World!");
var response = await client.SendRequestAsync(new WeatherRequest { City = "Bonn" });
Console.WriteLine($"RESPONSE: {response.Forecast}");
```

## Example
Check out the *TypeSafe* example in the [Examples](https://github.com/till-f/NetMessage/tree/main/Examples) folder.
It is a small console application that can send a few pre-defined messages on a key press from the client to the
server app and vice versa. Or just inspect the relevant files from here:

* [Messages](Examples/TypeSafe/Messages.cs)
* [ClientApp](Examples/TypeSafe.Client/NetMessageClientApp.cs)
* [ServerApp](Examples/TypeSafe.Server/NetMessageServerApp.cs)


## Working Principle
Three basic message kinds can be distinguished:

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


## Extension
If you want to implement a custom protocol, but still taking advantage of the event based notifications for messages, requests
and responses provided by *NetMessage*, you might want to use the *NetMessage.Base* [NuGet package](https://www.nuget.org/packages/NetMessage.Base "NetMessage.Base on NuGet.org").
The basic working principle described above is still valid, but the higher layer for transparent (de)serialization
of C# objects will not be available.

Using a custom protocol is as simple as implementing the `IProtocol` interface ([IProtocol.cs](NetMessage.Base/IProtocol.cs))
and adding the corresponding concrete classes for the server, client and session. See the *SimpleString* Example in the
[Examples](https://github.com/till-f/NetMessage/tree/main/Examples) folder for details.

If you just want to change the way how C# objects are (de)serialized, e.g. if you want to use Json instead of XML, all you have to do
is implementing the `IDataSerializer` interface ([IDataSerializer.cs](NetMessage/IDataSerializer.cs)) and pass the
corresponding instance to the `NetMessageServer` and `NetMessageClient` constructor respectively. Take a look at the existing
implementation for XML in [XmlDataSerializer.cs](NetMessage/XmlDataSerializer.cs) if you need an example.


## Tests
There is small but strong collection of integration tests, see [here](https://github.com/till-f/NetMessage/tree/main/Tests).
