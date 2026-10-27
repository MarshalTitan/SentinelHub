using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace SentinelHub.TreasureMaps;

public sealed class TreasureGameData(IDataManager data)
{
    private readonly Dictionary<uint, (uint Territory, string Zone, ushort Size, short X, short Y)> maps = [];
    private readonly Dictionary<uint, uint> rankItems = [];
    public TreasureLocation? Resolve(uint territory, uint mapId, int rawX, int rawY, byte[] payload)
    {
        if (territory == 0 || mapId == 0 || Math.Abs((long)rawX) > 100_000_000 || Math.Abs((long)rawY) > 100_000_000) return null;
        try
        {
            if (!maps.TryGetValue(mapId, out var meta))
            {
                var map = data.GetExcelSheet<Map>().GetRowOrDefault(mapId);
                if (map is not { } m || m.SizeFactor == 0 || m.TerritoryType.RowId == 0) return null;
                var zone = m.PlaceName.Value.Name.ToString();
                if (string.IsNullOrWhiteSpace(zone)) zone = m.TerritoryType.Value.PlaceName.Value.Name.ToString();
                if (zone.Length == 0) return null;
                meta = (m.TerritoryType.RowId, zone, m.SizeFactor, m.OffsetX, m.OffsetY);
                maps[mapId] = meta;
            }
            if (meta.Territory != territory) return null;
            return new(territory, mapId, rawX, rawY, meta.Zone,
                TreasurePolicy.Display(rawX, meta.Size, meta.X), TreasurePolicy.Display(rawY, meta.Size, meta.Y), payload);
        }
        catch { return null; }
    }
    public OwnTreasureMap? ResolveTreasure(uint rankId, ushort spotId)
    {
        try
        {
            if (!rankItems.TryGetValue(rankId, out var item))
            {
                var rank = data.GetExcelSheet<TreasureHuntRank>().GetRowOrDefault(rankId);
                if (rank is not { } r || r.KeyItemName.RowId == 0 || r.ItemName.RowId == 0) return null;
                rankItems[rankId] = item = r.KeyItemName.RowId;
            }
            var spot = data.GetSubrowExcelSheet<TreasureSpot>().GetSubrowOrDefault(rankId, spotId);
            if (spot is not { } s || s.Location.RowId == 0) return null;
            var level = s.Location.Value;
            if (!float.IsFinite(level.X) || !float.IsFinite(level.Z)) return null;
            var map = level.Map.Value;
            var payload = new Dalamud.Game.Text.SeStringHandling.Payloads.MapLinkPayload(map.TerritoryType.RowId,
                map.RowId, TreasurePolicy.Raw(level.X), TreasurePolicy.Raw(level.Z));
            var location = Resolve(map.TerritoryType.RowId, map.RowId, payload.RawX, payload.RawY, payload.Encode());
            return location is null ? null : new(rankId, spotId, item, location);
        }
        catch { return null; }
    }
}
