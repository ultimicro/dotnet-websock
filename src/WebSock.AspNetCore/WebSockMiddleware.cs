namespace WebSock.AspNetCore;

using Microsoft.AspNetCore.Http;

internal sealed class WebSockMiddleware
{
    private readonly RequestDelegate next;

    public WebSockMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await this.next.Invoke(context);
    }
}
