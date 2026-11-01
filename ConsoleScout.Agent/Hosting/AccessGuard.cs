using System.Security.Cryptography;
using System.Text;
using ConsoleScout.Contracts;

namespace ConsoleScout.Agent.Hosting;

/// <summary>Only the CLI may use the agent. Two checks, because a localhost port is reachable by more
/// than the CLI: the bearer token keeps out other local programs, and the Host check keeps out web pages
/// that use DNS rebinding to send requests to 127.0.0.1 from inside the user's own browser.</summary>
internal sealed class AccessGuard(AccessToken token, EndpointAnnouncer announcer) : IMiddleware
{
    private readonly byte[] _expected = Encoding.UTF8.GetBytes("Bearer " + token.Value);

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!IsLocalHost(context.Request.Host) || !HasToken(authorization))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            var error = new AgentError(AgentErrorCodes.Unauthorized, "Missing or invalid access token.");
            await context.Response.WriteAsJsonAsync(error, ContractsJsonContext.Default.AgentError);
            return;
        }

        await next(context);
    }

    private bool IsLocalHost(HostString host) =>
        host.Port == announcer.Address?.Port
        && host.Host is "127.0.0.1" or "localhost";

    private bool HasToken(string header)
    {
        var actual = Encoding.UTF8.GetBytes(header);
        return CryptographicOperations.FixedTimeEquals(actual, _expected);
    }
}
