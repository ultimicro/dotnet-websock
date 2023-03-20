# Low-level WebSocket implementation for .NET

This is an implementation of WebSocket for .NET. Unlike `System.Net.WebSockets.WebSocket`, this implementation provides a low-level access to WebSocket. The following is a list of problem with `System.Net.WebSockets.WebSocket`:

- No way to receive a frame with arbitrary length because you need to supply a fixed-length buffer when receiving.
- No way to check if the received frame is bigger than the supplied buffer because `WebSocketReceiveResult.Count` will either the size of supplied buffer of the frame size depend on which on is smaller.

## Development

### Prerequisites

- .NET 6 SDK (or later)

### Build

```sh
dotnet build src/WebSock.sln
```

## License

MIT
