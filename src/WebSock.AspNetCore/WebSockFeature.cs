namespace WebSock.AspNetCore;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

internal sealed class WebSockFeature : IWebSockFeature
{
    private readonly HttpContext context;
    private readonly IHttpUpgradeFeature upgrade;
    private readonly string key;

    public WebSockFeature(HttpContext context, IHttpUpgradeFeature upgrade, string key)
    {
        this.context = context;
        this.upgrade = upgrade;
        this.key = key;
    }

    public async Task<Stream> AcceptAsync()
    {
        // An |Upgrade| header field with value "websocket" as per RFC 2616 [RFC2616].
        var headers = this.context.Response.Headers;

        headers["Upgrade"] = "websocket";

        // A |Sec-WebSocket-Accept| header field.
        headers["Sec-WebSocket-Accept"] = this.GetAcceptKey();

        // A Status-Line with a 101 response code as per RFC 2616 [RFC2616]. Such a response could look like "HTTP/1.1 101 Switching Protocols".
        // A |Connection| header field with value "Upgrade".
        return await this.upgrade.UpgradeAsync();
    }

    private string GetAcceptKey()
    {
        // The value of this header field is constructed by concatenating /key/, defined above in step 4 in Section 4.2.2, with the string
        // "258EAFA5-E914-47DA-95CA-C5AB0DC85B11", taking the SHA-1 hash of this concatenated value to obtain a 20-byte value and base64-encoding (see Section 4
        // of [RFC4648]) this 20-byte hash.
        var key = Encoding.ASCII.GetBytes(this.key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11");
        byte[] hash;

        using (var sha1 = SHA1.Create())
        {
            hash = sha1.ComputeHash(key);
        }

        return Convert.ToBase64String(hash);
    }
}
