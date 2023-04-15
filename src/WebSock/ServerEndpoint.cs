namespace WebSock;

using System.IO;
using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using global::WebSock.Abstractions;

public sealed class ServerEndpoint : WebSock
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServerEndpoint"/> class from a completed handshake connection.
    /// </summary>
    /// <param name="connection">
    /// The TCP/IP connection to the server that already completed a WebSocket handshake.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="connection"/> is not readable or writable.
    /// </exception>
    /// <remarks>
    /// Use <see cref="ConnectAsync(Uri, CancellationToken)"/> to connect to the server using built-in handshake.
    ///
    /// This class will takeover <paramref name="connection"/>.
    /// </remarks>
    public ServerEndpoint(Stream connection)
        : base(connection)
    {
    }

    [UnsupportedOSPlatform("Browser")]
    public static async Task<ServerEndpoint> ConnectAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient(); // On non-browser the default constructor will always using SocketsHttpHandler.

        return await ConnectAsync(uri, client, cancellationToken);
    }

    [UnsupportedOSPlatform("Browser")]
    public static async Task<ServerEndpoint> ConnectAsync(Uri uri, HttpClient client, CancellationToken cancellationToken = default)
    {
        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("The value is not an absolute URL.", nameof(uri));
        }

        if (uri.Scheme != "ws" && uri.Scheme != "wss")
        {
            throw new ArgumentException("The value is not a WebSocket URL.", nameof(uri));
        }

        using var handshake = new HttpRequestMessage(HttpMethod.Get, uri);

        return await ConnectAsync(handshake, client, cancellationToken);
    }

    [UnsupportedOSPlatform("Browser")]
    public static async Task<ServerEndpoint> ConnectAsync(HttpRequestMessage handshake, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient(); // On non-browser the default constructor will always using SocketsHttpHandler.

        return await ConnectAsync(handshake, client, cancellationToken);
    }

    /// <summary>
    /// Open a new WebSocket connection to the specified URL.
    /// </summary>
    /// <param name="handshake">
    /// The handshake request to use.
    /// </param>
    /// <param name="client">
    /// The HTTP client to establish the connection. The inner <see cref="HttpMessageHandler"/> must be <see cref="SocketsHttpHandler"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests.
    /// </param>
    /// <returns>
    /// The <see cref="ServerEndpoint"/> instance represent the ready to use WebSocket connection.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="handshake"/> is not a GET request or not a HTTP 1.1 or the headers contains more than one Sec-WebSocket-Key or
    /// <see cref="HttpRequestMessage.RequestUri"/> is <see langword="null"/> or not an absolute URI or not a WebSocket URI.
    /// </exception>
    /// <exception cref="HandshakeFailedException">
    /// The handshake response is not a switching protocols response.
    /// </exception>
    /// <exception cref="MalformedHandshakeException">
    /// The handshake response is invalid.
    /// </exception>
    [UnsupportedOSPlatform("Browser")]
    public static async Task<ServerEndpoint> ConnectAsync(HttpRequestMessage handshake, HttpClient client, CancellationToken cancellationToken = default)
    {
        // The method of the request MUST be GET, and the HTTP version MUST be at least 1.1.
        if (handshake.Method != HttpMethod.Get)
        {
            throw new ArgumentException("The request is not a GET request.", nameof(handshake));
        }

        if (handshake.Version != HttpVersion.Version11)
        {
            throw new ArgumentException("The request is not a HTTP/1.1 request.", nameof(handshake));
        }

        // The "Request-URI" part of the request MUST match the /resource name/ defined in Section 3 (a relative URI) or be an absolute http/https URI that,
        // when parsed, has a /resource name/, /host/, and /port/ that match the corresponding ws/wss URI.
        var uri = handshake.RequestUri;

        if (uri is null)
        {
            throw new ArgumentException($"No {nameof(handshake.RequestUri)} is specified.", nameof(handshake));
        }
        else if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException($"{nameof(handshake.RequestUri)} is not an absolute URI.", nameof(handshake));
        }
        else if (uri.Scheme != "ws" && uri.Scheme != "wss")
        {
            throw new ArgumentException($"{nameof(handshake.RequestUri)} is not a WebSocket URI.", nameof(handshake));
        }

        // The request MUST contain a |Connection| header field whose value MUST include the "Upgrade" token.
        if (!handshake.Headers.Connection.Any(v => v.Equals("Upgrade", StringComparison.OrdinalIgnoreCase)))
        {
            handshake.Headers.Connection.Add(new("Upgrade"));
        }

        // The request MUST contain an |Upgrade| header field whose value MUST include the "websocket" keyword.
        if (!handshake.Headers.Upgrade.Any(v => v.Name.Equals("websocket", StringComparison.OrdinalIgnoreCase)))
        {
            handshake.Headers.Upgrade.Add(new("websocket"));
        }

        // The request MUST include a header field with the name |Sec-WebSocket-Key|. The value of this header field MUST be a nonce consisting of a
        // randomly selected 16-byte value that has been base64-encoded (see Section 4 of [RFC4648]). The nonce MUST be selected randomly for each
        // connection.
        string key;

        if (handshake.Headers.TryGetValues("Sec-WebSocket-Key", out var values))
        {
            try
            {
                key = values.Single();
            }
            catch (InvalidOperationException ex)
            {
                throw new ArgumentException("The request headers contains more than one Sec-WebSocket-Key.", nameof(handshake), ex);
            }
        }
        else
        {
            var nonce = new byte[16];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonce);
            }

            handshake.Headers.Add("Sec-WebSocket-Key", key = Convert.ToBase64String(nonce));
        }

        // The request MUST include a header field with the name |Sec-WebSocket-Version|. The value of this header field MUST be 13.
        if (!handshake.Headers.Contains("Sec-WebSocket-Version"))
        {
            handshake.Headers.Add("Sec-WebSocket-Version", "13");
        }

        // Send the handshake.
        var response = await client.SendAsync(handshake, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        try
        {
            // If the status code received from the server is not 101, the client handles the response per HTTP [RFC2616] procedures.
            if (response.StatusCode != HttpStatusCode.SwitchingProtocols)
            {
                throw new HandshakeFailedException(response);
            }

            // If the response lacks a |Connection| header field or the |Connection| header field doesn't contain a token that is an ASCII case-insensitive
            // match for the value "Upgrade", the client MUST _Fail the WebSocket Connection_.
            if (!response.Headers.TryGetValues("Connection", out values))
            {
                throw new MalformedHandshakeException("No Connection field in the response header.");
            }
            else if (!values.Any(v => v.Equals("Upgrade", StringComparison.OrdinalIgnoreCase)))
            {
                throw new MalformedHandshakeException("Invalid Connection field in the response header.");
            }

            // If the response lacks an |Upgrade| header field or the |Upgrade| header field contains a value that is not an ASCII case-insensitive match for
            // the value "websocket", the client MUST _Fail the WebSocket Connection_.
            if (!response.Headers.TryGetValues("Upgrade", out values))
            {
                throw new MalformedHandshakeException("No Upgrade field in the response header.");
            }
            else if (!values.Any(v => v.Equals("websocket", StringComparison.OrdinalIgnoreCase)))
            {
                throw new MalformedHandshakeException("Invalid Upgrade field in the response header.");
            }

            // If the response lacks a |Sec-WebSocket-Accept| header field or the |Sec-WebSocket-Accept| contains a value other than the base64-encoded SHA-1 of
            // the concatenation of the |Sec-WebSocket-Key| (as a string, not base64-decoded) with the string "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" but
            // ignoring any leading and trailing whitespace, the client MUST _Fail the WebSocket Connection_.
            if (!response.Headers.TryGetValues("Sec-WebSocket-Accept", out values))
            {
                throw new MalformedHandshakeException("No Sec-WebSocket-Accept field in the response header.");
            }

            try
            {
                if (values.Single().Trim() != Handshake.HashKey(key))
                {
                    throw new MalformedHandshakeException("Invalid Sec-WebSocket-Accept field in the response header.");
                }
            }
            catch (InvalidOperationException ex)
            {
                throw new MalformedHandshakeException("Multiple Sec-WebSocket-Accept fields in the response header.", ex);
            }

            // If the response includes a |Sec-WebSocket-Extensions| header field and this header field indicates the use of an extension that was not present
            // in the client's handshake (the server has indicated an extension not requested by the client), the client MUST _Fail the WebSocket Connection_. (
            // The parsing of this header field to determine which extensions are requested is discussed in Section 9.1.)
            if (response.Headers.Contains("Sec-WebSocket-Extensions"))
            {
                throw new MalformedHandshakeException("Unexpected Sec-WebSocket-Extensions field in the response header.");
            }

            // If the response includes a |Sec-WebSocket-Protocol| header field and this header field indicates the use of a subprotocol that was not present in
            // the client's handshake (the server has indicated a subprotocol not requested by the client), the client MUST _Fail the WebSocket Connection_.
            if (response.Headers.Contains("Sec-WebSocket-Protocol"))
            {
                throw new MalformedHandshakeException("Unexpected Sec-WebSocket-Protocol field in the response header.");
            }

            // Get the underlying stream. The call to ReadAsStreamAsync() will end up at HttpConnectionResponseContent.CreateContentReadStreamAsync() in the
            // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/HttpConnectionResponseContent.cs,
            // which return the stream that was comming from the call to HttpConnectionResponseContent.SetStream() in the
            // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/HttpConnection.cs.
            var connection = await response.Content.ReadAsStreamAsync(cancellationToken);

            return new(connection);
        }
        catch (Exception ex)
        {
            if (ex is not HandshakeFailedException)
            {
                response.Dispose();
            }

            throw;
        }

        // We can't dispose the response in case of success because it will also dispose the underlying stream.
    }
}
