using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using CSCharacter=FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
namespace SentinelHub.Identification;
// Typed read only; no address arithmetic, pattern scan, setup function, native hook or ornament-object dereference.
public static class LiveOrnamentReader
{
    public static unsafe ActorObservation Capture(IPlayerCharacter player,IFramework framework)
    {
        if(!framework.IsInFrameworkUpdateThread||!player.IsValid()||!player.IsTargetable||player.Address==0)
            return new(false,false,false,0);
        return ObserveFields(player.Address,player.EntityId,player.ObjectIndex,true,true,true);
    }
    internal static unsafe ActorObservation ObserveFields(nint address,uint entityId,ushort index,bool frameworkThread,bool valid,bool targetable)
    {
        if(!frameworkThread||!valid||!targetable||address==0)return new(false,false,false,0);
        var actor=(CSCharacter*)address;
        if(actor->GameObject.EntityId!=entityId||actor->GameObject.ObjectIndex!=index)
            return new(false,false,false,0);
        var loaded=actor->GameObject.DrawObject!=null&&!actor->Transformation.IsCharacterNotReady&&!actor->Transformation.IsEffectPending;
        var transformed=actor->CharacterData.TransformationId!=0||actor->TransformationNameId!=0
            ||actor->ModelContainer.ModelCharaId!=0
            ||(actor->ModelContainer.ModelCharaId_2!=-1&&actor->ModelContainer.ModelCharaId_2!=0)
            ||(actor->Transformation.Flags&FFXIVClientStructs.FFXIV.Client.Game.Character.TransformationContainer.TransformationFlags.Active)!=0;
        if(!loaded||transformed)return new(true,loaded,transformed,0);
        // AutoUmbrellaOrnamentId is a preference and must never be substituted here.
        return new(true,true,false,actor->OrnamentData.OrnamentId);
    }
}
