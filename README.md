# NetMessage
**Typesafe and lightweight RPC for .NET**

*NetMessage* is a small, super easy to use RPC and messaging library. The `NetMessageClient` and `NetMessageServer` classes
provide typesafe communication for any kind of .NET application. The message data types are defined by arbitrary C# classes.

## Quickstart

### 1. Download
Get *NetMessage* from [NuGet](https://www.nuget.org "TODO: insert deep link here").

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

*Note that the message classes are (de)serialized by the selected `IPayloadSerializer`. By default, the `XmlSerializer` from
.NET is used. Furthermore, a special string is used to terminate every message. The default terminator is "\u0004", i.e., the
EOT (End Of Transmission) character, but it can be changed to anything else. Note that the terminator must not be contained in
the payload (e.g. in a string property).*

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

// of course, our client can also receive communication that was initiated by the server
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

* `void AddMessageHandler<TTPld>(Action<NetMessageClient, TTPld> messageHandler)`
  * Where `TTPld` is the expected message type without constraints.
* `void AddRequestHandler<TTPld, TTRsp>(Action<NetMessageClient, TypedRequest<TTPld, TTRsp>> requestHandler)`
  * Where `TTPld` is the expected request type and must derive `IRequest<TTRsp>`. `TTRsp` is the expected response type without 
    constraints. The `IRequest` interface is otherwise empty.

Once connected, both the client and the server application can initiate sending of messages or requests on the
`NetMessageClient` or `NetMessageSession` class (one session is created on the server for every connected client):

* `Task<bool> SendMessageAsync(object message)`
  * Note that a message can be of any type, but the receiver must have added a handler for the appropriate type to receive it.
* `Task<TTRsp> SendRequestAsync<TTRsp>(IRequest<TTRsp> request)`
  * Note that like for messages, the receiver must have added a handler for the appropriate type to receive the request.

A response is sent by calling the following method diretly on the received `TypedRequest<TTPld, TTRsp>` object, which wraps
the original user request object of type `TTPld`:

* `Task<bool> SendResponseAsync(TTRsp response)`
  * The type of the response is constrained by the received request. The sender of the request is notified transparently,
    it is not possible to add an explicit handler.


## Extension
*NetMessage* can be extended on two levels:

On the lower level, custom protocols (including binary protocols) can be implemented. See *SimpleString* in the
[Examples](https://github.com/till-f/NetMessage/tree/main/Examples) folder, especially [SimpleStringProtocol.cs](Examples/SimpleString/SimpleStringProtocol.cs)
for details. If you want to use a custom low-level protocol, you will only need the *NetMessage.Base* NuGet package and the
typesafe layer in *NetMessage* will not be available.

On the upper level, custom implementations can replace the default `XmlSerializer` that is used to (de)serialize the message
objects. Take a look at [XmlPayloadSerializer.cs](NetMessage/XmlPayloadSerializer.cs) for details.
