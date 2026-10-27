using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace SentinelHub.TreasureMaps;

public readonly record struct CapturedTreasureFlag(TreasureOwner Owner, uint TerritoryId, uint MapId, int RawX, int RawY, byte[] Payload);
public static class TreasureLinks
{
    public static bool TryIdentity(MapLinkPayload payload, out uint territory, out uint map)
    {
        // DataString exposes the actual IDs without resolving global Lumina services.
        var fields = payload.DataString.AsSpan(2);
        var comma = fields.IndexOf(',');
        territory = map = 0;
        if (comma < 1 || !uint.TryParse(fields[..comma], out territory)) return false;
        fields = fields[(comma + 1)..]; comma = fields.IndexOf(',');
        return comma > 0 && uint.TryParse(fields[..comma], out map) && territory > 0 && map > 0;
    }
    public static List<CapturedTreasureFlag> Capture(int channel, SeString sender, SeString message,
        bool enabled, bool party, bool alliance, Func<PlayerPayload, TreasureOwner>? resolveOwner = null)
    {
        var result = new List<CapturedTreasureFlag>();
        if (!TreasurePolicy.Capture(channel, enabled, party, alliance)) return result;
        TreasureOwner? owner = null;
        foreach (var p in sender.Payloads)
            if (p is PlayerPayload player) { owner = resolveOwner?.Invoke(player) ?? new(player.PlayerName, player.World.RowId); break; }
        // Do not invent a world/owner from arbitrary display text or collect bot labels.
        if (owner is null || !owner.Valid) return result;
        return Extract(owner, message);
    }
    public static List<CapturedTreasureFlag> Extract(TreasureOwner owner, SeString message)
    {
        var result = new List<CapturedTreasureFlag>();
        if (!owner.Valid) return result;
        foreach (var p in message.Payloads)
        {
            if (result.Count == 8) break; // Bound work on one malformed/oversized chat line.
            if (p is not MapLinkPayload link || !TryIdentity(link, out var territory, out var map)) continue;
            var bytes = link.Encode(); if (bytes.Length > 2048) continue;
            result.Add(new(owner, territory, map, link.RawX, link.RawY, bytes));
        }
        return result;
    }
    public static MapLinkPayload Restore(TreasureLocation location)
        => new(location.TerritoryId, location.MapId, location.RawX, location.RawY);
}
