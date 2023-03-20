namespace WebSock.AspNetCore;

using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using WebSock.Abstractions;

internal sealed class WebSockMiddleware
{
    private readonly RequestDelegate next;

    public WebSockMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Build the HTTP feature.
        IWebSockFeature? feature;

        try
        {
            feature = BuildWebSockFeature(context);
        }
        catch (MalformedHandshakeException)
        {
            // If the server, while reading the handshake, finds that the client did not send a handshake that matches the description below (note that as
            // per [RFC2616], the order of the header fields is not important), including but not limited to any violations of the ABNF grammar specified
            // for the components of the handshake, the server MUST stop processing the client's handshake and return an HTTP response with an appropriate
            // error code (such as 400 Bad Request).
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        if (feature is not null)
        {
            context.Features.Set(feature);
        }

        await this.next.Invoke(context);
    }

    private static IWebSockFeature? BuildWebSockFeature(HttpContext context)
    {
        // An HTTP/1.1 or higher GET request.
        var request = context.Request;

        if (request.Method != "GET")
        {
            return null;
        }

        // An |Upgrade| header field containing the value "websocket", treated as an ASCII case-insensitive value.
        var headers = request.Headers;

        if (!string.Equals(headers["Upgrade"], "websocket", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // A |Connection| header field that includes the token "Upgrade", treated as an ASCII case-insensitive value.
        var upgrade = context.Features.Get<IHttpUpgradeFeature>();

        if (upgrade is null)
        {
            throw new MalformedHandshakeException("Missing 'Connection' header.");
        }
        else if (!upgrade.IsUpgradableRequest)
        {
            throw new MalformedHandshakeException("Header 'Connection' has invalid value.");
        }

        // A |Sec-WebSocket-Key| header field with a base64-encoded (see Section 4 of [RFC4648]) value that, when decoded, is 16 bytes in length.
        var key = headers["Sec-WebSocket-Key"];

        if (key == StringValues.Empty)
        {
            throw new MalformedHandshakeException("Missing 'Sec-WebSocket-Key' header.");
        }

        try
        {
            if (Convert.FromBase64String(key).Length != 16)
            {
                throw new MalformedHandshakeException("Header 'Sec-WebSocket-Key' has invalid value.");
            }
        }
        catch (FormatException ex)
        {
            throw new MalformedHandshakeException("Header 'Sec-WebSocket-Key' has invalid value.", ex);
        }

        // A |Sec-WebSocket-Version| header field, with a value of 13.
        if (headers["Sec-WebSocket-Version"] != "13")
        {
            throw new MalformedHandshakeException("Header 'Sec-WebSocket-Version' has invalid value.");
        }

        return new WebSockFeature(context, upgrade, key);
    }
}
