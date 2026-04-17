using System.IO;
using System.Net;

namespace Central.Desktop.Auth;

/// <summary>
/// Lightweight HTTP listener for OAuth/SAML redirect callbacks.
/// Binds to an ephemeral localhost port, waits for the callback, extracts query parameters.
/// Used by Entra ID, Okta, and SAML providers.
/// </summary>
public class OAuthCallbackListener : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _path;

    public int Port { get; }
    public string RedirectUri => $"http://localhost:{Port}{_path}";

    public OAuthCallbackListener(string path = "/auth/callback")
    {
        _path = path;
        // Find an available port
        var tempListener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        tempListener.Start();
        Port = ((IPEndPoint)tempListener.LocalEndpoint).Port;
        tempListener.Stop();

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{Port}{path}/");
    }

    /// <summary>
    /// Start listening and wait for a single callback request.
    /// Returns the query parameters from the redirect URL.
    /// </summary>
    public async Task<Dictionary<string, string>> WaitForCallbackAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        _listener.Start();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            var contextTask = _listener.GetContextAsync();
            var completed = await Task.WhenAny(contextTask, Task.Delay(timeout, cts.Token));

            if (completed != contextTask)
                throw new TimeoutException("OAuth callback timed out");

            var context = await contextTask;
            var query = context.Request.QueryString;
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string? key in query.AllKeys)
            {
                if (key != null)
                    result[key] = query[key] ?? "";
            }

            // Also check for POST body (SAML uses POST binding)
            if (context.Request.HttpMethod == "POST" && context.Request.HasEntityBody)
            {
                using var reader = new StreamReader(context.Request.InputStream);
                var body = await reader.ReadToEndAsync();
                foreach (var pair in body.Split('&'))
                {
                    var parts = pair.Split('=', 2);
                    if (parts.Length == 2)
                        result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
                }
            }

            // Send a friendly response to the browser
            var responseHtml = """
                <html><body style="font-family:Segoe UI; text-align:center; padding:40px;">
                <h2>Authentication Complete</h2>
                <p>You can close this browser tab and return to Central.</p>
                <script>window.close();</script>
                </body></html>
                """;
            var responseBytes = System.Text.Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = responseBytes.Length;
            await context.Response.OutputStream.WriteAsync(responseBytes);
            context.Response.Close();

            return result;
        }
        finally
        {
            _listener.Stop();
        }
    }

    public void Dispose()
    {
        try { _listener.Close(); } catch { }
    }
}
