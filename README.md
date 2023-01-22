# NetMessage
Typesafe and lightweight RPC for .NET

## About
NetMessage is a small, super easy to use library for messaging between client and server. Its `NetMessageClient` and `NetMessageServer`
classes require only minimal setup to establish typesafe communication between your .NET applications. The type of your messages can be defined by
arbitrary C# classes, e.g., with the default protocol, any class that can be (de)serialized using the standard `XmlSerializer` is suitable.

Each client and the server can individually configure the message types on which they are notified when a message is received. Once connected, all clients
as well the server can initiate sending of messages. Three basic message kinds exist:

* Message: uni-directional notifications
* Request: message that expect a response of a specific type
* Response: response to a request
