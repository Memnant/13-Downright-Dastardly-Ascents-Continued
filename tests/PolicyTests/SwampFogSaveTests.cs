using dda;

internal static class SwampFogSaveTests
{
    internal static void Run(Action<bool,string> check)
    {
        var id=Guid.NewGuid();
        for(int patch=0;patch<=16;patch++)
        foreach(var selection in new[]{(17,0),(18,0),(20,0),(0,1<<9),(0,1<<11)})
        {
            var rules=new DifficultySnapshot(selection.Item1,selection.Item2,true,true);
            var record=new SavedRuleRecord{schema=patch==0?1:2,runId=id.ToString("N"),gameVersion="2.4.c",modVersion="1.5."+patch,
                rules=rules.Encode(),nativeSaveHash=new string('B',64),checkpointSegment=3,checkpointTime=90,tidePhase=1.2};
            var copy=SavedRuleRecord.FromJson(record.ToJson());
            check(copy.TryRead(id,"2.4.c","1.5.16",out var loaded)&&loaded.Encode()==rules.Encode()&&loaded.Enabled(18)==rules.Enabled(18),"swamp fog migration preserves selected and forced tier gates");
            check(!copy.TryRead(Guid.NewGuid(),"2.4.c","1.5.16",out _),"swamp fog cannot leak across RunId");
        }
        var current=new DifficultySnapshot(20,0,true,true);
        check(ReadinessPolicy.Check(current,"1.5.16","1.5.15",current.Encode())==PeerReadiness.VersionMismatch,"different visual-distance build blocked");
        check(ReadinessPolicy.Check(current,"1.5.16","1.5.16",current.Encode())==PeerReadiness.Ready,"matching swamp fog peers ready");
    }
}
