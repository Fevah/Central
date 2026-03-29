using Central.Observability;

namespace Central.Api.Client;

/// <summary>
/// DelegatingHandler that adds X-Correlation-ID header to all outgoing HTTP requests.
/// Reads from CorrelationContext (AsyncLocal) — pairs with the API's CorrelationIdMiddleware.
/// </summary>
public class CorrelationIdHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", CorrelationContext.CorrelationId);
        return base.SendAsync(request, cancellationToken);
    }
}
