using System.Net;
using System.Net.Sockets;

namespace DocumentIntelligence.Api.RateLimiting;

/// <summary>
/// The key that per-client limits are counted under. An IPv4 address is one client, but a single IPv6
/// subscriber usually gets a whole /64, so counting per IPv6 address would give one attacker 2^64 budgets.
/// </summary>
internal static class ClientPartition
{
    public static string For(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out _);
        bytes[8..].Clear();

        return $"{new IPAddress(bytes)}/64";
    }
}
