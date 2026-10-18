namespace SentinelHub.Coordination;
public enum PauseState { Pending, Acknowledged, Running, Missing, Disabled, Unsupported, Rejected, Failed, NotRequested, RemainsPaused }
public sealed record PauseParticipant(string Id,PauseState State,string Explanation,bool Accepted=false);
public sealed record PauseView(bool Requested,PauseParticipant[] Participants)
{
    public static PauseView Empty=>new(false,[]);
    public bool FullyAcknowledged=>Requested&&Participants.Length>0&&Participants.All(p=>p.State is PauseState.Acknowledged or PauseState.Missing or PauseState.Disabled);
}
public static class PausePolicy
{
    public static PauseParticipant Resolve(string id,bool requested,bool installed,bool loaded,Func<int> protocol,Func<bool,bool> set,Func<bool> read,bool owned=false)
    {
        if(!installed)return new(id,PauseState.Missing,"Not installed; no automation controlled.");
        if(!loaded)return new(id,PauseState.Disabled,"Not loaded. Hub does not enable or disable plugins.");
        try{
            int version;try{version=protocol();}catch{return new(id,PauseState.Unsupported,"This loaded build has no reviewed pause contract and may still automate. Use its own controls.");}
            if(version!=1)return new(id,PauseState.Unsupported,"Unsupported pause contract; no request sent and no stop assumed.");
            if(!requested&&!owned)return new(id,PauseState.NotRequested,"Hub holds no pause lease and sends no resume request.");
            if(!set(requested))return new(id,PauseState.Rejected,"The participant rejected the request; no stop/resume is assumed.");
            var actual=read();
            if(requested&&!actual)return new(id,PauseState.Pending,"Request accepted, but actual pause is not acknowledged.",true);
            if(!requested&&actual)return new(id,PauseState.RemainsPaused,"Hub's lease was released; another pause/restriction still applies.",true);
            return new(id,requested?PauseState.Acknowledged:PauseState.Running,requested?"Participant reports automation paused.":"Hub's pause lease released; the participant's own preferences/readiness still apply.",true);
        }catch{return new(id,PauseState.Failed,"Participant response failed. Its automation state is unknown; use its own controls.");}
    }
}
