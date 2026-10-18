namespace SentinelHub.Inspection;
public enum BardingSlot { Head, Body, Legs }
public sealed record BardingModel(uint Id,ulong Head,ulong Body,ulong Legs);
public readonly record struct BardingObservation(bool Valid,bool Present,ulong Head,ulong Body,ulong Legs);
public static class BardingPolicy
{
    public static uint? Match(IEnumerable<BardingModel> rows,BardingSlot slot,ulong encodedModel)
    {
        if(encodedModel==0)return 0;
        // Entire encoded model equality; never substitute model numbers for row IDs, strip unknown bits or choose an ambiguous alias.
        var candidates=rows.Where(r=>r.Id is >0 and <=byte.MaxValue&&(slot switch{BardingSlot.Head=>r.Head,BardingSlot.Body=>r.Body,_=>r.Legs})==encodedModel)
            .Select(r=>r.Id).Distinct().Take(2).ToArray();
        return candidates.Length==1?candidates[0]:null;
    }
    public static InspectionPart[] Resolve(BardingObservation observation,Func<BardingSlot,ulong,InspectionEntry?> lookup)
    {
        if(!observation.Valid)return [];
        if(!observation.Present)return [];
        return new[]{(BardingSlot.Head,observation.Head),(BardingSlot.Body,observation.Body),(BardingSlot.Legs,observation.Legs)}.Select(x=>{
            if(x.Item2==0)return new InspectionPart(InspectionState.None,null,"No barding model in this slot.");
            var entry=lookup(x.Item1,x.Item2);return entry is null?new InspectionPart(InspectionState.UnknownId,null,"No unique exact local barding model match."):new(InspectionState.Found,entry);
        }).ToArray();
    }
}
