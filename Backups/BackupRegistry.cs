namespace SentinelHub.Backups;
public static class BackupRegistry
{
    private static BackupField[] Flags(params string[] names)=>names.Select(n=>new BackupField(n,BackupValue.Boolean)).ToArray();
    private static BackupField N(string path,double min=-100000,double max=100000)=>new(path,BackupValue.Number,min,max);
    private static BackupField I(string path,int min=0,int max=100000)=>new(path,BackupValue.Integer,min,max);
    public static readonly BackupOwner[] Owners=[
        new("SentinelHub",1,"0.7.0.0",[
            ..Flags("AntiAfkEnabled","QuestConvenienceEnabled","QuestDialogue","QuestCutscenes","MountContextMenuEnabled","FashionContextMenuEnabled","PlayerInspectorEnabled","ReducedMotion","Collapsed"),
            I("Theme",0,1),I("Page",0,12),I("LatencyMode",0,2),I("QuestRewardMode",0,3),N("ExpandedWidth",480,4000),N("ExpandedHeight",500,3000)
        ],"Pause latch and import bookkeeping excluded. No actor/unlock collection state is saved."),
        new("SRankSentinel",16,"0.7.62.0",[
            ..Flags("Enabled","EnableCenturio","EnableShadowbringers","EnableEndwalker","EnableDawntrail","EnableEvercold","ModernWindowCollapsed"),
            I("WindowTheme",0,1),I("WindowPage",0,20),N("ModernExpandedWidth",0,10000),N("ModernExpandedHeight",0,10000),
            I("TravelTimeoutSeconds",0,36000),I("LocateTimeoutSeconds",0,36000),I("PostKillSsGraceSeconds",0,3600),I("SsChainTimeoutSeconds",0,36000),I("AlertFreshnessMinutes",0,10080),
            ..Distance("CloseSafeProfile"),..Distance("ProximitySensitiveProfile")
        ],"Faloop credentials/session/username, feed switches, pending alerts, killed records and histories excluded."),
        new("PvPSentinel",19,"0.3.1.23",[
            ..Flags("Enabled","NavigationEnabled","AutonomousStrategyEnabled","TargetSelectionEnabled","FinishKoPriorityEnabled","ObjectiveNavigationEnabled","QueueAutomationEnabled","ShowDiagnostics","VerboseLogging", "AllowBorderlandRuins","AllowSealRock","AllowFieldsOfGlory","AllowOnsalHakair","AllowWorqorChirteh","ModernWindowMinimized"),
            I("ConfigurationTheme",0,1),I("CombatProvider",0,3),I("NativeCombatMode",0,1),I("MatchLimit",0,1000),
            N("ModernExpandedWidth",0,10000),N("ModernExpandedHeight",0,10000),N("FriendlyClusterLinkRadius",0,1000),N("MainGroupFollowRadius",0,1000),N("MainGroupRegroupDistance",0,1000),
            N("RangedPositionOffset",0,1000),N("EnemyEngagementRadius",0,1000),N("FinishTargetHpPercent",0,100),N("FinishTargetMaxChaseDistance",0,1000),N("MountDistance",0,1000),N("DismountDistance",0,1000),N("MountEnemySafetyRadius",0,1000),
            N("PathRequestTimeoutSeconds",0,3600),N("StuckTimeoutSeconds",0,3600),I("MaximumPathFailures",0,1000)
        ],"Preferred mount identity/name pair, development logs and unreviewed native combat tuning excluded."),
        new("SentinelHUD",12,"0.8.5.2",[
            ..Flags("Enabled","Locked"),N("GlobalScale",0.1,10),N("GlobalOpacity",0,1),
            ..Module("Player"),..Module("Target"),..Module("FocusTarget"),..Module("TargetOfTarget"),..Module("TargetingMeCounter"),
            I("SelfHighlight.Mode",0,3),I("SelfHighlight.ColourPreset",0,4),N("SelfHighlight.Intensity",0,1),..Colour("SelfHighlight.CustomColour"),
            I("PlayerPositionMarker.Mode",0,3),I("PlayerPositionMarker.Style",0,1),I("PlayerPositionMarker.ColourPreset",0,4),N("PlayerPositionMarker.Radius",0,100),N("PlayerPositionMarker.Opacity",0,1),
            ..Flags("PlayerPositionMarker.ShowBorder","PlayerPositionMarker.DangerDetectionEnabled","Camera.Enabled","EncounterAwareness.Enabled","EncounterAwareness.NativeDetectionEnabled","EncounterAwareness.SplatoonIntegrationEnabled"),
            ..Colour("PlayerPositionMarker.CustomColour"),..Colour("PlayerPositionMarker.CustomDangerColour"),N("Camera.MaximumZoomDistance",0,1000),
            I("Appearance.ConfigurationTheme",0,1),..Flags("Appearance.ModernWindow.Minimized"),N("Appearance.ModernWindow.ExpandedWidth",0,10000),N("Appearance.ModernWindow.ExpandedHeight",0,10000)
        ],"Retired convenience preferences, native Dalamud window state and unreviewed visual fields excluded."),
        new("SentinelProfiles",3,"0.2.1.7",[
            ..Flags("ShowInternalNames","ShowProfileInServerInfoBar","ModernWindowCollapsed"),I("Theme",0,1),I("EditorFilter",0,5),N("ModernExpandedWidth",0,10000),N("ModernExpandedHeight",0,10000)
        ],"Presentation only. Profile names, definitions/IDs and last-applied selection are excluded; manage those in Profiles."),
        new("ClassySentinel",5,"0.8.6.0",[
            ..Flags("Locked","ShowCategoryHeaders","ClickThroughWhenLocked","ModernWindowCollapsed"),I("Theme",0,1),N("ButtonSize",1,1000),N("PanelScale",0.1,10),I("ButtonsPerRow",1,100),
            N("ModernExpandedWidth",0,10000),N("ModernExpandedHeight",0,10000),N("PanelPosition.X"),N("PanelPosition.Y")
        ],"Presentation only. Gear-set exact identities/names, category maps and legacy migration data excluded."),
        new("SentinelRelay",5,"0.5.0.7",[
            I("ConfigurationTheme",0,1),..Flags("ModernWindowCollapsed","ShowPrivacyWarning"),N("ModernExpandedWidth",0,10000),N("ModernExpandedHeight",0,10000)
        ],"Presentation only. Entire CharacterProfiles subtree excluded: credentials/ciphertext, webhooks, tokens, Discord IDs, character keys/names/worlds, keywords, channels and remote-control options.")
    ];
    private static BackupField[] Distance(string root)=>[N(root+".FlagApproachDistance",0,1000),N(root+".WaitingDistance",0,1000),N(root+".EmergencyDistance",0,1000),N(root+".EngageHpPercent",0,100)];
    private static BackupField[] Module(string root)=>[..Flags(root+".Enabled",root+".ClickToTarget",root+".RightClickContextMenu",root+".BorderEnabled"),I(root+".Visibility",0,3),I(root+".ClickableArea",0,1),
        N(root+".Scale",0.1,10),N(root+".Opacity",0,1),N(root+".Width",1,10000),N(root+".BarHeight",1,1000),N(root+".BorderOpacity",0,1),N(root+".Layout.AnchorX",-10,10),N(root+".Layout.AnchorY",-10,10)];
    private static BackupField[] Colour(string root)=>[N(root+".Red",0,1),N(root+".Green",0,1),N(root+".Blue",0,1),N(root+".Alpha",0,1)];
    public static BackupOwner? Find(string id)=>Owners.SingleOrDefault(x=>x.Id==id);
}
