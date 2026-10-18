using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using CSCharacter=FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using FFObjectKind=FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind;
using FFBattleKind=FFXIVClientStructs.FFXIV.Client.Game.Object.BattleNpcSubKind;
using Slot=FFXIVClientStructs.FFXIV.Client.Game.Character.DrawDataContainer.EquipmentSlot;
namespace SentinelHub.Inspection;
public static class LiveBardingReader
{
    public static unsafe BardingObservation Capture(IPlayerCharacter player,IObjectTable objects,IFramework framework)
    {
        if(!framework.IsInFrameworkUpdateThread||!player.IsValid()||!player.IsTargetable||player.Address==0)return default;
        var guard=SentinelHub.Identification.LiveOrnamentReader.Capture(player,framework);
        if(!guard.Valid||!guard.DrawLoaded||guard.Transformed)return default;
        var owner=(CSCharacter*)player.Address;
        if(owner->GameObject.EntityId!=player.EntityId||owner->GameObject.ObjectIndex!=player.ObjectIndex)return default;
        var mounted=(player.CurrentMount?.RowId??0)==1;
        if(mounted&&owner->Mount.MountId!=1)return default;
        var mountAddress=mounted?(nint)owner->Mount.MountObject:0;
        // Only current object-table wrappers may provide a dereferenceable actor address. The mount pointer is a relationship key, never followed directly.
        var candidates=objects.Where(o=>o.IsValid()&&o.Address!=0&&(mounted?o.Address==mountAddress&&o.ObjectKind==ObjectKind.Mount:
            o.ObjectKind==ObjectKind.BattleNpc&&o.SubKind==(byte)FFBattleKind.Buddy&&o.OwnerId==player.EntityId)).Take(2).ToArray();
        if(candidates.Length==0)return mounted?default:new(true,false,0,0,0);
        if(candidates.Length!=1)return default;var target=candidates[0];
        var result=ObserveFields(target.Address,target.EntityId,target.ObjectIndex,player.EntityId,framework.IsInFrameworkUpdateThread,target.IsValid(),mounted);
        var again=objects.FirstOrDefault(o=>o.ObjectIndex==target.ObjectIndex&&o.Address==target.Address);
        if(again is null||!again.IsValid()||again.Address!=target.Address||again.EntityId!=target.EntityId||again.ObjectIndex!=target.ObjectIndex)return default;
        if(mounted&&(owner->Mount.MountId!=1||(nint)owner->Mount.MountObject!=mountAddress))return default;
        if(!mounted&&again.OwnerId!=player.EntityId)return default;
        return result;
    }
    internal static unsafe BardingObservation ObserveFields(nint address,uint entity,ushort index,uint ownerId,bool frameworkThread,bool valid,bool mounted)
    {
        if(!frameworkThread||!valid||address==0||ownerId is 0 or 0xE0000000)return default;
        var actor=(CSCharacter*)address;var game=actor->GameObject;
        if(game.EntityId!=entity||game.ObjectIndex!=index||game.DrawObject==null||actor->Transformation.IsCharacterNotReady||actor->Transformation.IsEffectPending
            ||actor->CharacterData.TransformationId!=0||(actor->Transformation.Flags&FFXIVClientStructs.FFXIV.Client.Game.Character.TransformationContainer.TransformationFlags.Active)!=0)return default;
        if(mounted){if(game.ObjectKind!=FFObjectKind.Mount)return default;}
        else if(game.ObjectKind!=FFObjectKind.BattleNpc||game.BattleNpcSubKind!=FFBattleKind.Buddy||game.OwnerId!=ownerId)return default;
        return new(true,true,actor->DrawData.Equipment(Slot.Head).Value,actor->DrawData.Equipment(Slot.Body).Value,actor->DrawData.Equipment(Slot.Feet).Value);
    }
}
