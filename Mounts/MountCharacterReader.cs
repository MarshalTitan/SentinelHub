using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace SentinelHub.Mounts;

public sealed class MountCharacterReader(IDataManager data, IUnlockState unlocks)
{
    public MountCharacterSnapshot Capture(MountCatalog catalog)
    {
        var sheet = data.GetExcelSheet<Lumina.Excel.Sheets.Mount>();
        var owned = new Dictionary<uint, bool>();
        foreach (var mount in catalog.Entries)
            owned.Add(mount.Id, unlocks.IsMountUnlocked(sheet.GetRow(mount.Id)));
        var counts = catalog.Exchanges.Length == 0 ? null : ReadMainBags(catalog.Exchanges.Select(e => e.ItemId).ToHashSet());
        return new(true, counts is not null, owned, counts ?? new Dictionary<uint, long>());
    }
    // Synchronous read of four loaded main bags only. No native pointer survives this call.
    // A partial/unloaded/changed-layout container is unknown, never silently zero.
    private static unsafe Dictionary<uint, long>? ReadMainBags(HashSet<uint> wanted)
    {
        var manager = InventoryManager.Instance();
        if (manager == null) return null;
        var bags = new List<MountBagSnapshot>();
        foreach (var type in new[] { InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4 })
        {
            var bag = manager->GetInventoryContainer(type);
            if (bag == null || !bag->IsLoaded || bag->Type != type || bag->Items == null || bag->Size != 35) return null;
            var copied = new MountBagItem[35];
            for (int i = 0; i < bag->Size; i++)
            {
                var item = bag->Items[i]; // Copy while the container is current.
                copied[i] = new(item.ItemId, item.Quantity, item.IsSymbolic);
            }
            bags.Add(new((int)type, bag->IsLoaded, bag->Size, copied));
        }
        return MountInventoryPolicy.Count(bags, wanted);
    }
}
