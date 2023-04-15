namespace WebSock;

using System;
using global::WebSock.Abstractions;

public class HandshakeFailedException : WebSockException, IDisposable
{
    private bool disposed;

    public HandshakeFailedException(HttpResponseMessage response)
    {
        this.Response = response;
    }

    public HttpResponseMessage Response { get; }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        if (disposing)
        {
            this.Response.Dispose();
        }

        this.disposed = true;
    }
}
