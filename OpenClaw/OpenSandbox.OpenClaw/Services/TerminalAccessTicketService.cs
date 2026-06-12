using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace OpenSandbox.OpenClaw.Services;

public sealed class TerminalAccessTicketService
{
    private readonly ConcurrentDictionary<string, TerminalAccessTicketPayload> _tickets = new(StringComparer.Ordinal);

    public string Issue(Guid userId, string containerId, TimeSpan lifetime)
    {
        CleanupExpired();

        var ticket = $"oct_{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}";
        var payload = new TerminalAccessTicketPayload(
            userId,
            containerId,
            DateTimeOffset.UtcNow.Add(lifetime));

        _tickets[ticket] = payload;
        return ticket;
    }

    public bool TryConsume(string ticket, out TerminalAccessTicketPayload payload)
    {
        payload = default!;
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return false;
        }

        CleanupExpired();
        if (!_tickets.TryRemove(ticket.Trim(), out var storedPayload))
        {
            return false;
        }

        if (storedPayload.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        payload = storedPayload;
        return true;
    }

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in _tickets)
        {
            if (entry.Value.ExpiresAt <= now)
            {
                _tickets.TryRemove(entry.Key, out _);
            }
        }
    }
}

public sealed record TerminalAccessTicketPayload(Guid UserId, string ContainerId, DateTimeOffset ExpiresAt);
