namespace WebSock.Abstractions;

using System.Security.Cryptography;
using System.Text;

public static class Handshake
{
    public static string HashKey(string key)
    {
        var bin = Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11");
        byte[] hash;

        using (var sha1 = SHA1.Create())
        {
            hash = sha1.ComputeHash(bin);
        }

        return Convert.ToBase64String(hash);
    }
}
