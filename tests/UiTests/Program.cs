using System.Numerics;
using System.Runtime.Loader;
using System.Runtime.InteropServices;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using SentinelCore.UI;
using SentinelHub.UI;
using SentinelHub.Configuration;
using SentinelHub.Diagnostics;
using SentinelHub.Ecosystem;
using SentinelHub.Utilities;
using SentinelHub.Mounts;
using SentinelHub.Fashion;
using SentinelHub.Identification;
using SentinelHub.Inspection;
using SentinelHub.Health;
using SentinelHub.Activity;
using SentinelHub.Compatibility;
using SentinelHub.Profiles;
using CSCharacter=FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
internal static class Program
{
    static void Check(bool value, string why) { if (!value) throw new Exception(why); }
    static string DirectoryPath => Environment.GetEnvironmentVariable("DALAMUD_TEST_LIB_PATH") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "Hooks", "dev");
    static int Main()
    {
        AssemblyLoadContext.Default.Resolving += (_, n) => {
            var p = Path.Combine(DirectoryPath, n.Name + ".dll");
            return File.Exists(p) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(p) : null;
        };
        try { Run(); return 0; } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static unsafe void Run()
    {
        IdlePerformanceTests.Run();
        ServiceSchedulingTests.Run();
        TreasureIntegrationTests.Run();
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO(); io.IniFilename = null; io.LogFilename = null;
            io.DisplaySize = new(3200, 2600); io.DeltaTime = 1f / 60;
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard | ImGuiConfigFlags.NavEnableGamepad;
            io.BackendFlags |= ImGuiBackendFlags.HasGamepad;
            io.Fonts.AddFontDefault();
            ushort[] ranges = [0xF000, 0xF8FF, 0];
            fixed (ushort* range = ranges)
            {
                var font = io.Fonts.AddFontFromFileTTF(Environment.GetEnvironmentVariable("DALAMUD_ICON_FONT")!, 16, null, range);
                Check(io.Fonts.Build(), "Font atlas");
                var fixtures = new[]
                {
                    StatusPolicy.Build([], true, _ => Response.Unknown),
                    StatusPolicy.Build(Catalog.Components.Select(c => new InstalledPlugin(c.Id, true, "0.1.0.0"))
                        .Concat(Catalog.Dependencies.Select(d => new InstalledPlugin(d.Id, true, "99.99.99.99"))), true, _ => Response.Ready),
                    HubSnapshot.Unknown,
                    StatusPolicy.Build(Catalog.Components.Select(c => new InstalledPlugin(c.Id, false, "0.1.0.0"))
                        .Concat(Catalog.Dependencies.Select(d => new InstalledPlugin(d.Id, true, "1.0.0.0"))), true, _ => Response.Unavailable),
                    StatusPolicy.Build(Catalog.Components.Select(c => new InstalledPlugin(c.Id, false, "0.1.0.0", true))
                        .Concat(Catalog.Dependencies.Select(d => new InstalledPlugin(d.Id, true, "1.0.0.0"))), true, _ => Response.NotReady),
                    StatusPolicy.Build([new("SRankSentinel", true, "0.7.61.0", IsDev: true)], true, _ => Response.Unknown),
                    StatusPolicy.Build([new("SRankSentinel", true, "0.7.61.0", IsDev: true),
                        new("SRankSentinel", false, "0.7.56.0")], true, _ => Response.Unknown),
                };
                int pages = 0;
                foreach (var scale in new[] { 1f, 1.5f, 2f })
                foreach (var theme in new[] { 0, 1 })
                foreach (var width in new[] { 480f, 860f })
                foreach (var data in fixtures)
                {
                    io.FontGlobalScale = scale;
                    var settings = new HubSettings { Theme = theme };
                    using var w = new MainWindow(settings, () => data, new FaultJournal(), () => {}, () => {},
                        (_, _) => true, () => {}, () => true, () => font);
                    foreach (var page in Enum.GetValues<HubPage>())
                    {
                        settings.Page = page;
                        var saved = JsonSerializer.Serialize(settings);
                        for (var i = 0; i < 12; i++) Frame(w, new(width, 500), scale);
                        var root = Find(MainWindow.WindowId);
                        var content = theme == 1 ? Find(MainWindow.WindowId, "##Modern2Page") : root;
                        Check(ImGui.GetDrawData().TotalVtxCount > 0, "No render geometry");
                        Check(content.ContentSize.X <= content.Size.X + 1, $"Horizontal overflow {theme}/{page}/{scale}/{width}: {content.ContentSize} vs {content.Size}");
                        Check(content.ScrollMax.Y >= 0 && !content.ScrollbarX, "Invalid scroll region");
                        Check(root.Pos == new Vector2(120, 80), "Window moved");
                        if (theme == 1)
                        {
                            var rail = Find(MainWindow.WindowId, "##Modern2Rail");
                            Check(rail.ContentSize.Y <= rail.Size.Y + 1, "Navigation icons clipped");
                            Check((root.Flags & ImGuiWindowFlags.NoTitleBar) != 0, "Duplicate title bar");
                        }
                        Check(JsonSerializer.Serialize(settings) == saved, "Drawing changed settings");
                        pages++;
                    }
                    if (theme == 1)
                    {
                        settings.Page = HubPage.Dashboard;
                        for (var i = 0; i < 3; i++) Frame(w, new(width, 800), scale);
                        w.ToggleCollapse();
                        for (var i = 0; i < 3; i++) Frame(w, new(width, 56), scale);
                        Check(settings.Collapsed && Math.Abs(Find(MainWindow.WindowId).Size.Y - 56 * scale) < 1, "Minimize wrong height");
                        w.OpenAndExpand();
                        for (var i = 0; i < 3; i++) Frame(w, new(width, settings.ExpandedHeight), scale);
                        Check(!settings.Collapsed && Find(MainWindow.WindowId).Size.Y >= 800 * scale - 1, "Restore lost expanded size");
                    }
                }
                var treasureCases=0;
                foreach(var treasureTheme in new[]{0,1})foreach(var treasureScale in new[]{1f,1.5f,2f})foreach(var count in new[]{0,4,50})
                {
                    io.FontGlobalScale=treasureScale;
                    var settings=new HubSettings{Theme=treasureTheme,Page=HubPage.TreasureMaps};settings.TreasureMaps.Enabled=true;
                    var entries=Enumerable.Range(0,count).Select(i=>new SentinelHub.TreasureMaps.TreasureEntry(Guid.NewGuid(),
                        new("A long player display name for wrapping "+i,42),new(100,401,123456,-789012,"A long localized territory with a specific map variant",24.2f,18.5f,[]),DateTimeOffset.UtcNow)).ToArray();
                    var v=new SentinelHub.TreasureMaps.TreasureView(true,entries,null,true,"Listening", "Flag prepared. Paste /p <flag> into chat.");
                    var actions=0;var before=JsonSerializer.Serialize(settings);
                    using var w=new MainWindow(settings,()=>fixtures[0],new(),()=>{},()=>{},(_,_)=>false,()=>{},()=>true,()=>font,
                        treasureView:()=>v,treasureRequest:(_,_)=>actions++);
                    foreach(var width in new[]{480f,1000f,480f}){
                        for(var i=0;i<3;i++)Frame(w,new(width,800),treasureScale);
                        var content=treasureTheme==1?Find(MainWindow.WindowId,"##Modern2Page"):Find(MainWindow.WindowId);
                        Check(content.ContentSize.X<=content.Size.X+1&&!content.ScrollbarX,"Treasure Maps horizontal overflow");
                        Check(before==JsonSerializer.Serialize(settings)&&actions==0,"Drawing Treasure Maps altered queue/preferences or sent action");treasureCases++;
                    }
                    using var floating=new TreasureQueueWindow(settings,()=>v,(_,_)=>actions++,()=>{},()=>{});
                    floating.PreOpenCheck();Check(!floating.IsOpen,"Closed floating window rendered/opened itself");
                    settings.TreasureMaps.FloatingOpen=true;floating.PreOpenCheck();Check(floating.IsOpen,"Floating queue did not open independently");
                    foreach(var width in new[]{300f,600f,300f}){
                        for(var i=0;i<3;i++){
                            ImGui.NewFrame();floating.PreDraw();ImGui.SetNextWindowPos(new(50,50),ImGuiCond.Always);ImGui.SetNextWindowSize(new(width*treasureScale,500*treasureScale),ImGuiCond.Always);
                            ImGui.Begin(TreasureQueueWindow.WindowId);floating.Draw();ImGui.End();floating.PostDraw();ImGui.Render();
                        }
                        var content=Find(TreasureQueueWindow.WindowId);Check(content.ContentSize.X<=content.Size.X+1&&!content.ScrollbarX,$"Floating queue overflow: theme={treasureTheme} scale={treasureScale} width={width} content={content.ContentSize} size={content.Size} scroll={content.ScrollbarX}");Check(actions==0,"Floating drawing sent action");treasureCases++;
                    }
                    floating.OnClose();Check(!settings.TreasureMaps.FloatingOpen,"Floating close state was not saved");
                }
                Console.WriteLine($"PASS {treasureCases} Treasure Maps populated/empty/full-queue, theme/scale/resize and independent floating-window cases.");
                var profileId=Guid.NewGuid().ToString("D");var appliedId=Guid.NewGuid().ToString("D");
                var profileCases=0;
                var profileStates=new[]{ProfilesView.Unavailable,
                    new ProfilesView(ProfilesAvailability.Missing,[],null,null,"Unavailable",false),
                    new ProfilesView(ProfilesAvailability.Disabled,[],null,null,"Unavailable",false),
                    new ProfilesView(ProfilesAvailability.Ready,[],null,null,"NoProfileApplied",false),
                    new ProfilesView(ProfilesAvailability.Ready,[new(profileId,"Hunts"),new(appliedId,new string('W',64))],profileId,appliedId,"Drifted",false),
                    new ProfilesView(ProfilesAvailability.Ready,[new(profileId,"Raid")],profileId,profileId,"Matched",false),
                    new ProfilesView(ProfilesAvailability.Ready,[new(profileId,"Raid")],profileId,profileId,"Matched",true)};
                foreach(var profileView in profileStates)
                foreach(var profileTheme in new[]{0,1})foreach(var profileScale in new[]{1f,1.5f,2f})
                {
                    io.FontGlobalScale=profileScale;
                    var settings=new HubSettings{Theme=profileTheme,Page=HubPage.Profiles};var opens=0;
                    using var overview=new MainWindow(settings,()=>fixtures[1],new(),()=>{},()=>{},(_,_)=>true,()=>{},()=>true,()=>font,
                        profilesView:()=>profileView,openProfile:_=>opens++);
                    var saved=JsonSerializer.Serialize(settings);
                    foreach(var width in new[]{480f,860f,480f})
                    {
                        for(var i=0;i<12;i++)Frame(overview,new(width,680),profileScale);
                        var content=profileTheme==1?Find(MainWindow.WindowId,"##Modern2Page"):Find(MainWindow.WindowId);
                        Check(content.ContentSize.X<=content.Size.X+1&&!content.ScrollbarX,"Profiles overview overflow");
                        Check(saved==JsonSerializer.Serialize(settings)&&opens==0,"Rendering Profiles changed settings or sent an action");
                        profileCases++;
                    }
                }
                Console.WriteLine($"PASS {profileCases} authoritative Profiles availability/empty/selected/applied/busy/theme/scale/resize cases.");
                io.FontGlobalScale=1;
                using(var rendered=new MainWindow(new HubSettings{Theme=1,Page=HubPage.Dashboard},()=>fixtures[1],new(),()=>{},()=>{},(_,_)=>true,()=>{},()=>true,()=>font))
                    IdlePerformanceTests.Measure("modern-dashboard-open",()=>Frame(rendered,new(860,680),1),300);
                var pluginResizeCases=0;
                foreach(var theme in new[]{0,1})
                foreach(var scale in new[]{1f,1.5f,2f})
                foreach(var pluginPage in new[]{HubPage.Dashboard,HubPage.Plugins,HubPage.Profiles})
                {
                    io.FontGlobalScale=scale;
                    var settings=new HubSettings{Theme=theme,Page=pluginPage};
                    var cardId=pluginPage==HubPage.Profiles?"SentinelProfiles":"SRankSentinel";
                    var pluginData=fixtures[1];var opened=new List<(string Id,bool Diagnostics)>();
                    using var plugins=new MainWindow(settings,()=>pluginData,new FaultJournal(),()=>{},()=>{},(id,diagnostics)=>{opened.Add((id,diagnostics));return true;},()=>{},()=>true,()=>font);
                    var saved=JsonSerializer.Serialize(settings);float narrowHeight=0;
                    foreach(var width in new[]{480f,860f,480f})
                    {
                        for(var i=0;i<12;i++)Frame(plugins,new(width,680),scale);
                        var root=Find(MainWindow.WindowId);
                        var card=Find(MainWindow.WindowId,cardId);
                        Check((root.Flags&ImGuiWindowFlags.NoResize)==0,"Hub cannot be resized");
                        Check(card.ContentSize.X<=card.Size.X+1&&!card.ScrollbarX,"Plugin card overflows horizontally after resize");
                        Check(card.ContentSize.Y+2*card.WindowPadding.Y<=card.Size.Y+1,$"Plugin actions are clipped after resize: theme={theme} scale={scale} width={width} content={card.ContentSize} padding={card.WindowPadding} size={card.Size} scroll={root.Scroll}");
                        var unusedHeight=card.Size.Y-card.ContentSize.Y-2*card.WindowPadding.Y;
                        // Fractional font scaling rounds each row separately; keep only a small rounding allowance.
                        Check(unusedHeight<=6*scale+1,$"Excess plugin card bottom space: theme={theme} page={pluginPage} scale={scale} width={width} unused={unusedHeight}");
                        if(width==480){if(narrowHeight>0)Check(Math.Abs(narrowHeight-card.Size.Y)<1,"Narrow fallback changed after resize");narrowHeight=card.Size.Y;}
                        else
                        {
                            // Classic can already fit side actions at the minimum width.
                            Check(card.Size.Y<=narrowHeight+1,"Wider plugin card increased its height");
                            if(theme==1)Check(card.Size.Y<narrowHeight,"Wide Modern actions did not reduce card height");
                        }
                        Check(JsonSerializer.Serialize(settings)==saved,"Resizing changed saved feature settings");
                    }
                    void ClickSettings()
                    {
                        for(var i=0;i<12;i++)Frame(plugins,new(860,680),scale);
                        var card=Find(MainWindow.WindowId,cardId);
                        if(pluginPage==HubPage.Profiles)
                        {
                            var status=Find(MainWindow.WindowId,"profiles-contract");
                            var spare=status.Size.Y-status.ContentSize.Y-2*status.WindowPadding.Y;
                            Check(spare>=-1&&spare<=6*scale+1,$"Profile status card clipping/excess bottom space: theme={theme} scale={scale} spare={spare}");
                        }
                        io.AddMousePosEvent(card.Pos.X+card.Size.X-card.WindowPadding.X-70*scale,
                            card.Pos.Y+card.WindowPadding.Y+ImGui.GetStyle().CellPadding.Y+14*scale);
                        io.AddMouseButtonEvent(0,true);Frame(plugins,new(860,680),scale);
                        io.AddMouseButtonEvent(0,false);Frame(plugins,new(860,680),scale);
                        io.AddMousePosEvent(-100,-100);Frame(plugins,new(860,680),scale);
                    }
                    ClickSettings();
                    Check(opened.SequenceEqual(new[]{(cardId,false)}),"Right-side settings action did not open its owning plugin");
                    pluginData=fixtures[0];ClickSettings();
                    Check(opened.Count==1,"Unavailable right-side action was clickable");
                    Check(JsonSerializer.Serialize(settings)==saved,"Plugin action changed feature preferences");
                    pluginResizeCases++;
                }
                Console.WriteLine("PASS "+pluginResizeCases+" plugin narrow/wide/narrow resize, clipping, mouse actions/disabled gating and preference cases.");
                foreach (var scale in new[] { 1f, 1.5f, 2f })
                foreach (var theme in new[] { 0, 1 })
                foreach (var state in Enum.GetValues<AntiAfkState>())
                {
                    io.FontGlobalScale = scale;
                    var config = new HubSettings { Theme = theme, Page = HubPage.Utilities, AntiAfkEnabled = true };
                    using var utility = new MainWindow(config, () => HubSnapshot.Unknown, new FaultJournal(), () => {}, () => {}, (_,_) => false, () => {}, () => true,
                        () => font, () => new AntiAfkView(state, 42, true));
                    for (var i=0;i<12;i++) Frame(utility, new(480,500), scale);
                    var content = theme == 1 ? Find(MainWindow.WindowId,"##Modern2Page") : Find(MainWindow.WindowId);
                    Check(content.ContentSize.X <= content.Size.X + 1 && !content.ScrollbarX, "Anti-AFK state overflow");
                    Check(content.ScrollMax.Y > 0, $"Narrow utility page cannot scroll: theme={theme} scale={scale} state={state} content={content.ContentSize} size={content.Size} flags={content.Flags} page={config.Page} collapsed={config.Collapsed}");
                }
                foreach(var scale in new[]{1f,1.5f,2f})
                foreach(var theme in new[]{0,1})
                foreach(var state in Enum.GetValues<QuestConvenienceState>())
                foreach(var preference in new int?[]{null,15})
                foreach(var mode in Enum.GetValues<SentinelHub.Utilities.Quest.QuestRewardSelectionMode>())
                {
                    io.FontGlobalScale=scale;
                    var config=new HubSettings { Theme=theme,Page=HubPage.Utilities,QuestConvenienceEnabled=true,QuestRewardMode=mode };
                    using var utility=new MainWindow(config,()=>HubSnapshot.Unknown,new FaultJournal(),()=>{},()=>{},(_,_)=>false,()=>{},()=>true,
                        ()=>font,questView:()=>new(state,preference,"Paused for a player choice","Skip requested; waiting for the dedicated confirmation addon","Selected reward; waiting for Complete"));
                    for(var i=0;i<12;i++)Frame(utility,new(480,500),scale);
                    var content=theme==1?Find(MainWindow.WindowId,"##Modern2Page"):Find(MainWindow.WindowId);
                    Check(content.ContentSize.X<=content.Size.X+1&&!content.ScrollbarX,"Quest state overflow");
                    Check(content.ScrollMax.Y>0,"Quest page cannot scroll to all controls");
                }
                foreach(var theme in new[]{0,1})
                foreach(var scale in new[]{1f,1.5f,2f})
                foreach(var state in Enum.GetValues<LatencyState>())
                foreach(var mode in Enum.GetValues<LatencyMode>())
                {
                    io.FontGlobalScale=scale;
                    var settings=new HubSettings{Theme=theme,Page=HubPage.Utilities,LatencyMode=mode};
                    using var latency=new MainWindow(settings,()=>HubSnapshot.Unknown,new FaultJournal(),()=>{},()=>{},(_,_)=>true,()=>{},()=>true,
                        ()=>font,latencyView:()=>new(state,123,100,170,160,130,13000));
                    var saved=JsonSerializer.Serialize(settings);
                    for(var i=0;i<12;i++)Frame(latency,new(480,500),scale);
                    var content=theme==1?Find(MainWindow.WindowId,"##Modern2Page"):Find(MainWindow.WindowId);
                    Check(content.ContentSize.X<=content.Size.X+1&&!content.ScrollbarX,"Latency state overflow");
                    Check(content.ScrollMax.Y>0,"Latency controls cannot be reached by scrolling");
                    Check(JsonSerializer.Serialize(settings)==saved,"Latency drawing changed opt-ins");
                }

                var operationsCases=0;
                foreach(var theme in new[]{0,1})
                foreach(var scale in new[]{1f,1.5f,2f})
                foreach(var width in new[]{480f,860f})
                foreach(var populated in new[]{false,true})
                foreach(var operationPage in new[]{HubPage.Dependencies,HubPage.Utilities,HubPage.Dashboard,HubPage.RecentActivity}){
                    io.FontGlobalScale=scale;
                    var settings=new HubSettings{Theme=theme,Page=operationPage};
                    var health=populated?new HealthView(DateTimeOffset.UtcNow,true,Enum.GetValues<HealthState>().Select(s=>new HealthCheck("SRankSentinel",s,"0.7.99.0","0.7.62.0",true,"A long explanation distinguishes installation/load/internal enable/IPC response/readiness and development override without declaring a DLL healthy.")).ToArray(),DateTimeOffset.UtcNow,"Dated public catalog; feature readiness remains separate"):HealthView.Empty;
                    var compatibility=populated?new CompatibilityView([new(OverlapProvider.TextAdvance,CompatibilityFeature.Cutscene,OverlapKind.Definite,true,"Enabled and responding; Hub self-suspends and preserves saved preferences."),new(OverlapProvider.NoClippy,CompatibilityFeature.Latency,OverlapKind.Possible,true,"Loaded; module activity unknown.")]):CompatibilityView.Empty;
                    var events=populated?Enumerable.Range(0,200).Select(i=>new ActivityEntry(DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,"SentinelHub",ActivityCode.DiagnosticsSaved,9999)).ToArray():[];
                    using var operations=new MainWindow(settings,()=>HubSnapshot.Unknown,new FaultJournal(),()=>{},()=>{},(_,_)=>false,()=>{},()=>true,
                        ()=>font,healthView:()=>health,compatibilityView:()=>compatibility,activityEntries:()=>events);
                    var saved=JsonSerializer.Serialize(settings);for(var i=0;i<12;i++)Frame(operations,new(width,500),scale);
                    var content=theme==1?Find(MainWindow.WindowId,"##Modern2Page"):Find(MainWindow.WindowId);
                    Check(content.ContentSize.X<=content.Size.X+1&&!content.ScrollbarX,"Operations content overflows: "+operationPage+"/"+theme+"/"+scale);
                    Check(JsonSerializer.Serialize(settings)==saved,"Operations rendering mutated configuration");operationsCases++;
                }
                Console.WriteLine("PASS "+operationsCases+" populated/empty operations page/theme/scale/width cases.");
                var inspectorCases=0;
                foreach(var theme in new[]{0,1})
                foreach(var scale in new[]{1f,1.5f,2f})
                foreach(var state in Enum.GetValues<InspectionState>())
                foreach(var width in new[]{480f,860f}){
                    io.FontGlobalScale=scale;
                    var settings=new HubSettings{Theme=theme,Page=HubPage.PlayerInspector,PlayerInspectorEnabled=true};
                    var entry=new InspectionEntry(InspectionKind.Emote,123,"A long localized explicitly identified emote display name",0,"/lean",true,
                        ["A lengthy local quest/vendor source with requirements that must wrap at minimum width"],"Game reward/shop evidence");
                    var view=InspectorView.Empty with{Tracking=true,At=DateTimeOffset.UtcNow,Emote=new(state,state==InspectionState.Found?entry:null,"No animation or pose inference")};
                    using var inspector=new MainWindow(settings,()=>HubSnapshot.Unknown,new FaultJournal(),()=>{},()=>{},(_,_)=>false,()=>{},()=>true,
                        ()=>font,inspectorView:()=>view);
                    var saved=JsonSerializer.Serialize(settings);for(var i=0;i<12;i++)Frame(inspector,new(width,500),scale);
                    var page=theme==1?Find(MainWindow.WindowId,"##Modern2Page"):Find(MainWindow.WindowId);
                    Check(page.ContentSize.X<=page.Size.X+1&&!page.ScrollbarX,"Inspector content overflows");
                    Check(JsonSerializer.Serialize(settings)==saved,"Inspector rendering mutated settings");inspectorCases++;
                }
                Console.WriteLine("PASS "+inspectorCases+" live Inspector state/theme/scale/width cases.");
                // Exercise populated mount browser and details using the production renderer.
                var mountRows = new[] {
                    new MountEntry(1,"A very long localized mount name that must wrap at the smallest supported width","Firebird",0,8,
                        ["A lengthy mount action label"],123,2,1,MountKnowledge.No,MountKnowledge.Unknown,
                        [new(MountCategory.Quest,"An independently authored long acquisition source with conditions and explicit unknown availability","Game data: Quest #123"),
                         new(MountCategory.OnlineStore,"Online Store purchase with a long localized product title and regional availability requirements","Community source: FFXIV Collect; 2026-10-17","https://store.finalfantasyxiv.com/ffxivstore/en-us/product/30",true),
                         new(MountCategory.GilVendor,"Edelina, Mor Dhona (X:22.1, Y:4.8), 50,000,000 gil","Official Eorzea Database","https://eu.finalfantasyxiv.com/lodestone/playguide/db/shop/7fa3c777312/")],
                        [new(99,"A very long localized whistle item name",false)]),
                    new MountEntry(2,"Missing mount","White Lanner",0,1,[],0,0,null,MountKnowledge.Unknown,MountKnowledge.Unknown,[],[]),
                };
                var mountTrack=MountPolicy.ResolveTrack(new("test",1,"Firebird","Fiery Wings, Fiery Hearts","Official",["White Lanner","Unresolved mount"]),mountRows);
                var mountCatalog=new MountCatalog(mountRows,[mountTrack],[new(1,99,"Expanse Totem",99,1)],new Dictionary<int,string>{{0,"A Realm Reborn"},{1,"Heavensward"}},1) { MetadataDate="2026-10-17", MetadataMatched=356, MetadataRejected=1 };
                var mountCharacters=new[]{
                    MountCharacterSnapshot.Unknown,
                    new MountCharacterSnapshot(true,false,new Dictionary<uint,bool>{{1,true},{2,false}},new Dictionary<uint,long>()),
                    new MountCharacterSnapshot(true,true,new Dictionary<uint,bool>{{1,false},{2,true}},new Dictionary<uint,long>{{99,200}}),
                };
                int mountCases=0;
                foreach(var theme in new[]{0,1})
                foreach(var scale in new[]{1f,1.5f,2f})
                foreach(var width in new[]{480f,860f})
                foreach(var character in mountCharacters)
                foreach(var details in new[]{false,true})
                {
                    io.FontGlobalScale=scale;
                    var settings=new HubSettings{Theme=theme,Page=HubPage.Mounts};
                    using var mounts=new MainWindow(settings,()=>HubSnapshot.Unknown,new FaultJournal(),()=>{},()=>{},(_,_)=>false,()=>{},()=>true,
                        ()=>font,mountView:()=>new(MountLoadState.Ready,mountCatalog,character));
                    if(details)mounts.OpenMount(1);
                    var saved=JsonSerializer.Serialize(settings);
                    for(int i=0;i<12;i++)Frame(mounts,new(width,500),scale);
                    var content=theme==1?Find(MainWindow.WindowId,"##Modern2Page"):Find(MainWindow.WindowId);
                    Check(content.ContentSize.X<=content.Size.X+1&&!content.ScrollbarX,"Mount collection overflow");
                    Check(content.ContentSize.Y<=content.Size.Y+1||content.ScrollMax.Y>0,"Mount content is clipped without a scroll path");
                    Check(JsonSerializer.Serialize(settings)==saved,"Mount drawing changed settings");
                    mountCases++;
                }
                foreach(var theme in new[]{0,1})
                foreach(var state in Enum.GetValues<MountLoadState>())
                {
                    io.FontGlobalScale=1;
                    var settings=new HubSettings{Theme=theme,Page=HubPage.Mounts};
                    using var mounts=new MainWindow(settings,()=>HubSnapshot.Unknown,new FaultJournal(),()=>{},()=>{},(_,_)=>false,()=>{},()=>true,
                        ()=>font,mountView:()=>new(state,MountCatalog.Empty,MountCharacterSnapshot.Unknown));
                    for(int i=0;i<12;i++)Frame(mounts,new(480,500),1);
                    var content=theme==1?Find(MainWindow.WindowId,"##Modern2Page"):Find(MainWindow.WindowId);
                    Check(content.ContentSize.X<=content.Size.X+1&&!content.ScrollbarX,"Mount loading/failure state overflow");
                    mountCases++;
                }
                Console.WriteLine($"PASS {mountCases} populated mount browser/details/character/loading/theme/width/scale cases.");


                // Exercise the typed adapter against owned fixture memory, never a game actor or copied offset.
                var actor=new CSCharacter();
                actor.GameObject.EntityId=42;actor.GameObject.ObjectIndex=5;
                actor.GameObject.DrawObject=(FFXIVClientStructs.FFXIV.Client.Graphics.Scene.DrawObject*)1;
                actor.ModelContainer.ModelCharaId_2=-1;
                actor.OrnamentData.OrnamentId=16;actor.OrnamentData.AutoUmbrellaOrnamentId=9;
                ActorObservation Read(CSCharacter* current)=>LiveOrnamentReader.ObserveFields((nint)current,42,5,true,true,true);
                Check(Read(&actor)==new ActorObservation(true,true,false,16),"Typed ornament field did not resolve current ID");
                actor.OrnamentData.OrnamentId=0;
                Check(Read(&actor).OrnamentId==0,"Auto umbrella preference leaked into active ID");
                actor.OrnamentData.OrnamentId=16;actor.CharacterData.TransformationId=1;
                Check(Read(&actor).Transformed&&Read(&actor).OrnamentId==0,"Transformed actor was read as an active accessory");
                actor.CharacterData.TransformationId=0;actor.GameObject.DrawObject=null;
                Check(!Read(&actor).DrawLoaded&&Read(&actor).OrnamentId==0,"Unloaded draw actor was not rejected");
                Check(!LiveOrnamentReader.ObserveFields(1,42,5,false,true,true).Valid,"Wrong-thread read was not blocked before dereference");
                Check(!LiveOrnamentReader.ObserveFields(1,42,5,true,false,true).Valid,"Invalid wrapper read was not blocked before dereference");
                actor.GameObject.EntityId=43;Check(!Read(&actor).Valid,"Reused entity identity was accepted");
                Console.WriteLine("PASS typed ornament reader memory/auto-umbrella/transformation/unloaded/thread/identity fixtures; binding "+typeof(CSCharacter).Assembly.GetName().Version);
                actor.GameObject.EntityId=42;actor.GameObject.DrawObject=(FFXIVClientStructs.FFXIV.Client.Graphics.Scene.DrawObject*)1;
                actor.EmoteController.EmoteId=123;actor.Mode=FFXIVClientStructs.FFXIV.Client.Game.Character.CharacterModes.EmoteLoop;actor.ModeParam=25;
                var emoteGuard=new ActorObservation(true,true,false,0);
                Check(LiveEmoteReader.ObserveFields((nint)(&actor),42,5,true,emoteGuard).EmoteId==123,"Explicit typed emote ID was not read");
                actor.EmoteController.EmoteId=0;
                Check(LiveEmoteReader.ObserveFields((nint)(&actor),42,5,true,emoteGuard).EmoteId==0,"Ended emote was retained");
                Check(!LiveEmoteReader.ObserveFields(1,42,5,false,emoteGuard).Valid,"Wrong-thread emote reader dereferenced a pointer");
                Check(!LiveEmoteReader.ObserveFields(1,42,5,true,new(false,false,false,0)).Valid,"Invalid emote actor was read");
                actor.GameObject.EntityId=43;
                Check(!LiveEmoteReader.ObserveFields((nint)(&actor),42,5,true,emoteGuard).Valid,"Reused emote actor identity was accepted");
                Console.WriteLine("PASS typed emote reader active/ended/thread/identity fixtures.");
                var fashionRows=new[]{
                    new FashionEntry(16,"An extremely long localized fashion accessory title that wraps in a narrow window","Red Moon Parasol",0,3288,15,"5.55",3,
                        [new("Seasonal / event","The Rising 2021, a historical source with a lengthy explanation of eligibility and current uncertainty","Community snapshot 2026-10-18","https://ffxivcollect.com/fashions/16"),
                         new("Online Store","Regional store purchase; verify the current product page","Official source link","https://store.finalfantasyxiv.com/ffxivstore/en-gb/product/882")],
                        [new(33711,"A lengthy localized unlock-item name for the parasol",false,false)],FashionKnowledge.No,FashionKnowledge.No,FashionKnowledge.Yes,FashionKnowledge.Unknown,"Not verified","Former event does not certify current availability"),
                    new FashionEntry(42,"Mandragora Parasol","Mandragora Parasol",0,4406,40,null,null,[],[],FashionKnowledge.Unknown,FashionKnowledge.Unknown,FashionKnowledge.Unknown,FashionKnowledge.Unknown,"Not verified"),
                };
                var fashionCatalog=new FashionCatalog(fashionRows,new Dictionary<int,string>{{3,"Shadowbringers"},{4,"Endwalker"},{5,"Dawntrail"}},52,1,"2026-10-18");
                var fashionCharacters=new[]{FashionCharacter.Unknown,new FashionCharacter(true,new Dictionary<uint,bool>{{16,true},{42,false}}),
                    new FashionCharacter(true,new Dictionary<uint,bool>{{16,false},{42,true}})};
                int fashionCases=0,identifyCases=0;
                foreach(var theme in new[]{0,1})
                foreach(var scale in new[]{1f,1.5f,2f})
                foreach(var width in new[]{480f,860f})
                foreach(var character in fashionCharacters)
                foreach(var details in new[]{false,true}){
                    io.FontGlobalScale=scale;
                    var settings=new HubSettings{Theme=theme,Page=HubPage.Fashion};
                    using var window=new MainWindow(settings,()=>HubSnapshot.Unknown,new FaultJournal(),()=>{},()=>{},(_,_)=>false,()=>{},()=>true,
                        ()=>font,fashionView:()=>new(FashionLoad.Ready,fashionCatalog,character));
                    if(details)window.OpenFashion(16);
                    var saved=JsonSerializer.Serialize(settings);
                    for(int i=0;i<12;i++)Frame(window,new(width,500),scale);
                    var content=theme==1?Find(MainWindow.WindowId,"##Modern2Page"):Find(MainWindow.WindowId);
                    Check(content.ContentSize.X<=content.Size.X+1&&!content.ScrollbarX,"Fashion page overflow");
                    Check(content.ContentSize.Y<=content.Size.Y+1||content.ScrollMax.Y>0,"Fashion controls not reachable");
                    Check(saved==JsonSerializer.Serialize(settings),"Fashion rendering changed opt-ins");fashionCases++;
                }
                foreach(var theme in new[]{0,1})foreach(var state in Enum.GetValues<FashionLoad>()){
                    io.FontGlobalScale=1;var settings=new HubSettings{Theme=theme,Page=HubPage.Fashion};
                    using var window=new MainWindow(settings,()=>HubSnapshot.Unknown,new FaultJournal(),()=>{},()=>{},(_,_)=>false,()=>{},()=>true,
                        ()=>font,fashionView:()=>new(state,FashionCatalog.Empty,FashionCharacter.Unknown));
                    for(int i=0;i<12;i++)Frame(window,new(480,500),1);
                    var content=theme==1?Find(MainWindow.WindowId,"##Modern2Page"):Find(MainWindow.WindowId);
                    Check(content.ContentSize.X<=content.Size.X+1&&!content.ScrollbarX,"Fashion loading state overflow");fashionCases++;
                }
                foreach(var theme in new[]{0,1})foreach(var scale in new[]{1f,1.5f,2f})foreach(var width in new[]{480f,860f})
                foreach(var state in Enum.GetValues<IdentificationState>())foreach(var mountFound in new[]{false,true}){
                    io.FontGlobalScale=scale;var settings=new HubSettings{Theme=theme,Page=HubPage.Fashion};
                    using var window=new MainWindow(settings,()=>HubSnapshot.Unknown,new FaultJournal(),()=>{},()=>{},(_,_)=>false,()=>{},()=>true,
                        ()=>font,mountView:()=>new(MountLoadState.Ready,mountCatalog,mountCharacters[1]),
                        fashionView:()=>new(FashionLoad.Ready,fashionCatalog,fashionCharacters[1]));
                    window.OpenIdentification(new(new(mountFound?IdentificationState.Found:IdentificationState.None,1),new(state,16),Environment.TickCount64));
                    var saved=JsonSerializer.Serialize(settings);
                    for(int i=0;i<12;i++)Frame(window,new(width,500),scale);
                    var content=theme==1?Find(MainWindow.WindowId,"##Modern2Page"):Find(MainWindow.WindowId);
                    Check(content.ContentSize.X<=content.Size.X+1&&!content.ScrollbarX,"Shared identification overflow");
                    Check(saved==JsonSerializer.Serialize(settings),"Identification rendering mutated settings");
                    window.ClearIdentification();identifyCases++;
                }
                Console.WriteLine($"PASS {fashionCases} fashion database/details/loading/theme/width/scale cases.");
                Console.WriteLine($"PASS {identifyCases} shared mount/fashion identification result cases.");

                Console.WriteLine("PASS 198 native Latency mode/state/theme/scale cases.");
                Console.WriteLine("PASS "+(Enum.GetValues<QuestConvenienceState>().Length*48)+" native Quest state/import/mode/theme/scale cases.");
                Console.WriteLine("PASS "+(Enum.GetValues<AntiAfkState>().Length*6)+" native Anti-AFK state/theme/scale cases.");
                Console.WriteLine($"PASS {pages} native rendered page/state/width/theme/scale cases; collapse/restore and balanced stacks.");
                // Exercise actual Hub appearance switch via native keyboard/gamepad activation.
                foreach (var key in new[] { ImGuiKey.Space, ImGuiKey.GamepadFaceDown })
                {
                    var value = false; using var motion = new SentinelModernMotion();
                    void SwitchFrame(bool focus = false)
                    {
                        ImGui.NewFrame(); motion.BeginFrame(io.DeltaTime, true);
                        ImGui.Begin("HubSwitch"); SentinelModernSwitch.Draw("reduced", "Reduced motion", ref value, motion);
                        if (focus) ImGui.SetKeyboardFocusHere(-1);
                        ImGui.End(); ImGui.Render();
                    }
                    SwitchFrame(true); SwitchFrame();
                    io.AddKeyEvent(key, true); SwitchFrame();
                    io.AddKeyEvent(key, false); SwitchFrame();
                    Check(value, key + " did not activate shared switch");
                }
                Console.WriteLine("PASS keyboard/controller shared appearance activation.");
            }
        }
        finally { ImGui.DestroyContext(context); }
    }
    static unsafe ImGuiWindowPtr Find(string parent, string? child = null)
    {
        var all = ImGui.GetCurrentContext().Windows;
        for (int i = 0; i < all.Size; i++)
        {
            var name = Marshal.PtrToStringUTF8((nint)all[i].Name)!;
            if ((child is null ? name == parent : name.StartsWith(parent + "/") && name.Contains(child)) && all[i].Active) return all[i];
        }
        throw new Exception("Window missing " + parent + "/" + child);
    }
    static void Frame(MainWindow w, Vector2 logicalSize, float scale)
    {
        ImGui.NewFrame();
        var context = ImGui.GetCurrentContext();
        var colours = context.ColorStack.Size; var styles = context.StyleVarStack.Size; var fonts = context.FontStack.Size;
        w.PreDraw();
        ImGui.SetNextWindowPos(new(120, 80), ImGuiCond.Always);
        ImGui.SetNextWindowSize(logicalSize * scale, ImGuiCond.Always);
        ImGui.Begin(MainWindow.WindowId, w.Flags);
        w.Draw(); ImGui.End(); w.PostDraw();
        Check(context.ColorStack.Size == colours && context.StyleVarStack.Size == styles && context.FontStack.Size == fonts, "Unbalanced styles");
        ImGui.Render();
    }
}

