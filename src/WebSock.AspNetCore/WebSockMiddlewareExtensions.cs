namespace Microsoft.AspNetCore.Builder;

using WebSock.AspNetCore;

public static class WebSockMiddlewareExtensions
{
    public static IApplicationBuilder UseWebSock(this IApplicationBuilder app)
    {
        return app.UseMiddleware<WebSockMiddleware>();
    }
}
