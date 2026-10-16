using System.Reflection;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using SentinelCore.Diagnostics;

namespace SentinelHub.Utilities.Quest;

/// <summary>
/// Owns opt-in quest convenience actions independently from HUD rendering. Every action is scoped
/// to a dedicated game addon, re-resolved immediately before use, and throttled. No keyboard,
/// controller, movement, chat, or generic-confirm input is synthesized.
/// </summary>
public sealed unsafe class QuestConvenienceService : IDisposable
{
    private const long DialogueIntervalMilliseconds = 120;
    private const long CutsceneRetryIntervalMilliseconds = 1_000;
    private const long CutsceneConfirmationRetryIntervalMilliseconds = 250;
    private const long CutsceneConfirmationTimeoutMilliseconds = 5_000;
    private const long RewardPollIntervalMilliseconds = 100;
    private const long RewardConfirmDelayMilliseconds = 250;
    private const int RewardCountMaximum = 5;
    private const int RewardBaseOffset = 82;
    private const int RewardAmountOffset = 11;
    private const uint HighQualityItemOffset = 1_000_000;
    private static readonly HashSet<uint> AllaganPieceItemIds = [5824, 5825, 5826, 5827];
    private static readonly PropertyInfo[] ClassJobFlags = typeof(ClassJobCategory)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(property => property.PropertyType == typeof(bool) && property.GetMethod is not null)
        .ToArray();
    private static readonly string[] ChoiceAddonNames =
    [
        "SelectString",
        "SelectIconString",
        "SelectYesno",
    ];
    private static readonly string[] CutsceneSkipPromptAddonNames =
    [
        CutsceneSkipPromptPolicy.SelectStringAddonName,
        CutsceneSkipPromptPolicy.CutSceneSelectStringAddonName,
    ];

    private readonly IGameGui gameGui;
    private readonly ICondition condition;
    private readonly IObjectTable objectTable;
    private readonly IDataManager dataManager;
    private readonly DiagnosticBuffer diagnostics;
    private long nextDialogueTick;
    private long nextCutsceneTick;
    private long nextCutsceneConfirmationTick;
    private long nextRewardTick;
    private long rewardConfirmAfterTick;
    private uint? lastTerritoryId;
    private bool cutsceneSessionObserved;
    private bool cutsceneAttemptFinished;
    private CutsceneSkipConfirmationState cutsceneConfirmation;
    private uint cachedCurrentJobId;
    private ulong activeRewardSignature;
    private QuestRewardSelectionMode activeRewardMode = QuestRewardSelectionMode.Manual;
    private bool rewardSelectionSent;
    private bool rewardCompletionSent;
    private QuestRewardDecision pendingReward;

    public QuestConvenienceService(
        IGameGui gameGui,
        ICondition condition,
        IObjectTable objectTable,
        IDataManager dataManager,
        DiagnosticBuffer diagnostics)
    {
        this.gameGui = gameGui;
        this.condition = condition;
        this.objectTable = objectTable;
        this.dataManager = dataManager;
        this.diagnostics = diagnostics;
    }

    public string DialogueState { get; private set; } = "Disabled";
    public string CutsceneState { get; private set; } = "Disabled";
    public string CutsceneDetectedAddon { get; private set; } = "None";
    public string CutsceneLastResult { get; private set; } = "None";
    public bool AwaitingCutsceneSkipConfirmation => cutsceneConfirmation.Pending;
    public string RewardState { get; private set; } = "Manual";
    public string CurrentJob { get; private set; } = "Unavailable";
    public bool RewardWindowDetected { get; private set; }
    public uint LastSelectedRewardId { get; private set; }
    public string LastSelectedRewardName { get; private set; } = "None";
    public string LastSelectionReason { get; private set; } = "None";

    public void Update(QuestConvenienceOptions configuration, bool isLoggedIn, uint territoryId)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var now = Environment.TickCount64;
        if (lastTerritoryId is not null && lastTerritoryId != territoryId)
        {
            ResetCutsceneSession();
            CutsceneLastResult = "Pending state cleared on territory change";
        }
        lastTerritoryId = territoryId;
        UpdateDialogue(configuration.SkipDialogue, isLoggedIn, now);
        UpdateCutscene(configuration.SkipCutscenes, isLoggedIn, now);
        UpdateRewards(configuration.QuestRewardSelection, isLoggedIn, now);
    }

    public void Dispose()
    {
        nextDialogueTick = 0;
        nextCutsceneTick = 0;
        nextRewardTick = 0;
        ResetCutsceneSession();
        ResetRewardWindow();
        DialogueState = "Disabled";
        CutsceneState = "Disabled";
        RewardState = "Manual";
        lastTerritoryId = null;
    }

    private void UpdateDialogue(bool enabled, bool isLoggedIn, long now)
    {
        if (!enabled)
        {
            DialogueState = "Disabled";
            nextDialogueTick = 0;
            return;
        }
        if (!isLoggedIn)
        {
            DialogueState = "Waiting for login";
            return;
        }
        if (now < nextDialogueTick)
            return;
        nextDialogueTick = now + DialogueIntervalMilliseconds;

        if (ChoiceAddonNames.Any(IsAddonVisible))
        {
            DialogueState = "Paused for a player choice";
            return;
        }

        var talk = GetVisibleAddon<AtkUnitBase>("Talk");
        if (talk is null)
        {
            DialogueState = "Enabled; no ordinary dialogue open";
            return;
        }

        if (talk->FireCallbackInt(0))
            DialogueState = "Advanced ordinary dialogue";
        else
            DialogueState = "Dialogue is visible but not ready to advance";
    }

    private void UpdateCutscene(bool enabled, bool isLoggedIn, long now)
    {
        if (!enabled)
        {
            CutsceneState = "Disabled";
            nextCutsceneTick = 0;
            ResetCutsceneSession();
            CutsceneDetectedAddon = "None";
            CutsceneLastResult = "None";
            return;
        }
        if (!isLoggedIn)
        {
            CutsceneState = "Waiting for login";
            ResetCutsceneSession();
            CutsceneDetectedAddon = "None";
            CutsceneLastResult = "Pending state cleared on logout";
            return;
        }

        var inCutscene = condition.Any(
            ConditionFlag.OccupiedInCutSceneEvent,
            ConditionFlag.WatchingCutscene,
            ConditionFlag.WatchingCutscene78);

        var agentModule = AgentModule.Instance();
        var agent = agentModule is null
            ? null
            : (AgentCutscene*)agentModule->GetAgentByInternalId(AgentId.Cutscene);

        // The game's cutscene condition can fall while its dedicated confirmation is modal.
        // A short-lived request token therefore owns confirmation; no unrelated prompt is ever
        // eligible simply because it happens to be visible.
        if (cutsceneConfirmation.Pending)
        {
            var promptVisible = TryResolveCutsceneSkipPrompt(
                agent,
                out var promptAddonName,
                out var promptAddonId,
                out var addonIdentity,
                out var confirmationAvailable);
            if (promptVisible)
            {
                CutsceneDetectedAddon = addonIdentity;
                CutsceneState = confirmationAvailable
                    ? $"Skip confirmation addon detected: {addonIdentity}"
                    : $"Skip confirmation detected but Yes is unavailable: {addonIdentity}";
            }

            var decision = CutsceneSkipConfirmationPolicy.Evaluate(
                ref cutsceneConfirmation, now, promptVisible, confirmationAvailable);
            switch (decision)
            {
                case CutsceneSkipConfirmationDecision.Confirm:
                    if (now < nextCutsceneConfirmationTick)
                        return;
                    nextCutsceneConfirmationTick = now + CutsceneConfirmationRetryIntervalMilliseconds;
                    if (TryConfirmCutsceneSkipPrompt(agent, promptAddonName, promptAddonId))
                    {
                        cutsceneConfirmation = default;
                        cutsceneAttemptFinished = true;
                        CutsceneState = $"Yes selected on {addonIdentity}";
                        CutsceneLastResult = $"Skip confirmation accepted through {addonIdentity}";
                        diagnostics.Information(
                            $"Quest convenience selected Yes on the game-provided cutscene skip dialog ({addonIdentity}).");
                    }
                    else
                    {
                        CutsceneState = $"Skip confirmation callback was unavailable on {addonIdentity}";
                    }
                    return;

                case CutsceneSkipConfirmationDecision.Dismissed:
                    cutsceneConfirmation = default;
                    cutsceneAttemptFinished = true;
                    CutsceneState = "Skip confirmation was dismissed; no further action for this cutscene";
                    CutsceneLastResult = "Skip confirmation dismissed manually";
                    diagnostics.Information("Quest convenience observed the cutscene skip dialog close without confirmation; pending state cleared.");
                    return;

                case CutsceneSkipConfirmationDecision.TimedOut:
                    cutsceneConfirmation = default;
                    cutsceneAttemptFinished = true;
                    CutsceneState = "Skip confirmation timed out; no further action for this cutscene";
                    CutsceneLastResult = "Skip confirmation timed out";
                    diagnostics.Information("Quest convenience cutscene skip confirmation timed out; pending state cleared.");
                    return;

                case CutsceneSkipConfirmationDecision.Wait:
                    CutsceneState = promptVisible
                        ? CutsceneState
                        : "Skip requested; waiting for the dedicated confirmation addon";
                    return;
            }
        }

        if (!inCutscene)
        {
            if (cutsceneSessionObserved)
            {
                cutsceneSessionObserved = false;
                cutsceneAttemptFinished = false;
                nextCutsceneTick = 0;
                CutsceneState = CutsceneLastResult.StartsWith("Skip confirmation accepted", StringComparison.Ordinal)
                    ? "Skip completed"
                    : "Enabled; no cutscene active";
            }
            else
            {
                CutsceneState = "Enabled; no cutscene active";
            }
            return;
        }

        cutsceneSessionObserved = true;
        if (cutsceneAttemptFinished)
        {
            CutsceneState = CutsceneLastResult.StartsWith("Skip confirmation accepted", StringComparison.Ordinal)
                ? "Yes selected; waiting for the cutscene to close"
                : CutsceneState;
            return;
        }

        if (now < nextCutsceneTick)
            return;
        nextCutsceneTick = now + CutsceneRetryIntervalMilliseconds;

        var uiModule = UIModule.Instance();
        if (uiModule is null || agentModule is null)
        {
            CutsceneState = "Cutscene active; game UI services are unavailable";
            return;
        }

        var input = uiModule->GetUIInputModule();
        if (input is null || agent is null || input->CutsceneSkipCallback is null)
        {
            CutsceneState = "Cutscene active; the game does not currently permit skipping";
            return;
        }

        // Never open a skip prompt over an existing selection dialog. Besides being confusing to
        // the player, an already-visible selection cannot be proven to have resulted from our
        // request token.
        if (CutsceneSkipPromptAddonNames.Any(IsAddonVisible))
        {
            CutsceneState = "Cutscene active; waiting for an existing selection dialog to close";
            return;
        }

        if (agent->OpenSkipDialog(input->CutsceneSkipCallback))
        {
            cutsceneConfirmation = CutsceneSkipConfirmationState.Begin(
                now, CutsceneConfirmationTimeoutMilliseconds);
            nextCutsceneConfirmationTick = 0;
            CutsceneDetectedAddon = agent->SkipDialogAddonId == 0
                ? "Pending game-provided cutscene addon"
                : $"AgentCutscene skip addon #{agent->SkipDialogAddonId}";
            CutsceneState = "Skip requested; waiting for the dedicated confirmation addon";
            CutsceneLastResult = "Skip requested";
            diagnostics.Information("Quest convenience requested FFXIV's normal cutscene skip dialog; awaiting contextual confirmation.");
        }
        else
        {
            CutsceneState = "Cutscene active; the game rejected the skip request";
        }
    }

    private bool TryResolveCutsceneSkipPrompt(
        AgentCutscene* agent,
        out string promptAddonName,
        out ushort promptAddonId,
        out string addonIdentity,
        out bool confirmationAvailable)
    {
        promptAddonName = string.Empty;
        promptAddonId = 0;
        addonIdentity = "None";
        confirmationAvailable = false;

        if (agent is null || agent->SkipDialogAddonId is 0 or > ushort.MaxValue)
            return false;

        foreach (var addonName in CutsceneSkipPromptAddonNames)
        {
            var candidate = GetVisibleAddon<AtkUnitBase>(addonName);
            if (!IsAddonVisible(candidate)
                || !CutsceneSkipPromptPolicy.TryMatch(
                    addonName,
                    candidate->Id,
                    agent->SkipDialogAddonId,
                    out var promptKind))
            {
                continue;
            }

            promptAddonName = addonName;
            promptAddonId = candidate->Id;
            addonIdentity = $"{promptKind} #{promptAddonId}";
            confirmationAvailable = true;
            return true;
        }

        return false;
    }

    private bool TryConfirmCutsceneSkipPrompt(
        AgentCutscene* agent,
        string addonName,
        ushort expectedAddonId)
    {
        if (agent is null || agent->SkipDialogAddonId != expectedAddonId)
            return false;

        // Re-resolve on the action frame. No pointer is retained between updates, and no derived
        // addon layout is dereferenced: the two live crash dumps showed that treating the ID-only
        // result as AddonCutSceneSelectString could read OptionList from the wrong layout.
        var prompt = GetVisibleAddon<AtkUnitBase>(addonName);
        return IsAddonVisible(prompt)
               && CutsceneSkipPromptPolicy.TryMatch(
                   addonName,
                   prompt->Id,
                   expectedAddonId,
                   out _)
               && prompt->FireCallbackInt(0);
    }

    private void UpdateRewards(QuestRewardSelectionMode mode, bool isLoggedIn, long now)
    {
        if (!isLoggedIn)
        {
            RewardState = mode == QuestRewardSelectionMode.Manual ? "Manual" : "Waiting for login";
            CurrentJob = "Unavailable";
            cachedCurrentJobId = 0;
            RewardWindowDetected = false;
            ResetRewardWindow();
            return;
        }

        UpdateCurrentJob();
        var addon = GetVisibleAddon<AddonJournalResult>("JournalResult");
        RewardWindowDetected = addon is not null;
        if (addon is null)
        {
            RewardState = mode == QuestRewardSelectionMode.Manual
                ? "Manual; no reward window open"
                : "Enabled; no reward window open";
            ResetRewardWindow();
            return;
        }
        if (mode == QuestRewardSelectionMode.Manual)
        {
            RewardState = "Manual; reward window left untouched";
            ResetRewardWindow();
            return;
        }
        if (now < nextRewardTick)
            return;
        nextRewardTick = now + RewardPollIntervalMilliseconds;

        if (!TryReadRewards(addon, out var candidates, out var signature, out var failureReason))
        {
            RewardState = failureReason;
            ResetRewardWindow();
            return;
        }
        if (candidates.Count == 0)
        {
            RewardState = "Reward window has no choose-one rewards; guaranteed rewards are untouched";
            ResetRewardWindow();
            return;
        }

        if (signature != activeRewardSignature || mode != activeRewardMode)
        {
            activeRewardSignature = signature;
            activeRewardMode = mode;
            rewardSelectionSent = false;
            rewardCompletionSent = false;
            rewardConfirmAfterTick = 0;
        }
        if (rewardCompletionSent)
        {
            RewardState = $"Completion sent for {pendingReward.Name}; waiting for the window to close";
            return;
        }

        if (!rewardSelectionSent)
        {
            var decision = QuestRewardSelectionPolicy.Select(mode, candidates);
            if (decision is null)
            {
                RewardState = "No valid automatic reward decision";
                return;
            }
            if (!TrySelectReward(addon, decision.Value.Index))
            {
                RewardState = "Reward selection event could not be delivered safely";
                return;
            }

            pendingReward = decision.Value;
            rewardSelectionSent = true;
            rewardConfirmAfterTick = now + RewardConfirmDelayMilliseconds;
            LastSelectedRewardId = decision.Value.ItemId;
            LastSelectedRewardName = decision.Value.Name;
            LastSelectionReason = decision.Value.Reason;
            RewardState = $"Selected {decision.Value.Name} ({decision.Value.ItemId}): {decision.Value.Reason}";
            diagnostics.Information($"Quest reward selected: {decision.Value.Name} ({decision.Value.ItemId}); reason: {decision.Value.Reason}.");
            return;
        }

        if (now < rewardConfirmAfterTick)
            return;

        // The addon and candidate fingerprint were re-read above. Completion is only clicked when
        // the same choose-one window remains visible and the game has enabled its Complete button.
        if (addon->CompleteButton is null
            || !addon->CompleteButton->IsEnabled
            || addon->CompleteButton->AtkResNode is null
            || !addon->CompleteButton->AtkResNode->IsVisible())
        {
            RewardState = $"Selected {pendingReward.Name}; waiting for the game to enable Complete";
            return;
        }

        if (!TryClickButton((AtkUnitBase*)addon, addon->CompleteButton))
        {
            RewardState = "Complete button is enabled but its click event is unavailable";
            return;
        }

        rewardCompletionSent = true;
        RewardState = $"Completed reward selection: {pendingReward.Name} ({pendingReward.ItemId})";
        diagnostics.Information($"Quest reward completion sent for {pendingReward.Name} ({pendingReward.ItemId}).");
    }

    private bool TryReadRewards(
        AddonJournalResult* addon,
        out List<QuestRewardCandidate> candidates,
        out ulong signature,
        out string failureReason)
    {
        candidates = [];
        signature = 14695981039346656037UL;
        failureReason = "Reward window data is not ready";
        var unit = (AtkUnitBase*)addon;
        if (unit->AtkValues is null || unit->AtkValuesCount <= RewardBaseOffset)
            return false;

        var player = objectTable.LocalPlayer;
        var currentJobId = player?.ClassJob.RowId ?? 0;
        var items = dataManager.GetExcelSheet<Item>();
        for (var index = 0; index < RewardCountMaximum; index++)
        {
            var itemValueIndex = RewardBaseOffset + index;
            if (itemValueIndex >= unit->AtkValuesCount)
                break;
            var value = unit->AtkValues[itemValueIndex];
            if (value.Type is AtkValueType.Undefined or AtkValueType.Null)
                break;
            if (value.Type != AtkValueType.UInt)
            {
                failureReason = $"Reward {index + 1} has an unexpected item identifier type";
                return false;
            }

            var itemId = value.UInt % HighQualityItemOffset;
            if (itemId == 0 || !items.TryGetRow(itemId, out var item))
            {
                failureReason = $"Reward {index + 1} could not be positively identified ({itemId})";
                return false;
            }

            var quantityIndex = RewardBaseOffset + RewardAmountOffset + index;
            var quantity = quantityIndex < unit->AtkValuesCount
                           && unit->AtkValues[quantityIndex].Type == AtkValueType.UInt
                ? Math.Max(1u, unit->AtkValues[quantityIndex].UInt)
                : 1u;
            var compatibility = GetJobCompatibility(item, currentJobId);
            candidates.Add(new QuestRewardCandidate(
                index,
                itemId,
                quantity,
                item.Name.ToString(),
                compatibility.IsCompatible,
                compatibility.Specificity,
                item.LevelItem.RowId,
                AllaganPieceItemIds.Contains(itemId),
                item.PriceLow));
            signature = AddToSignature(signature, itemId);
            signature = AddToSignature(signature, quantity);
            signature = AddToSignature(signature, (uint)index);
        }

        failureReason = candidates.Count == 0
            ? "Reward window has no choose-one rewards; guaranteed rewards are untouched"
            : string.Empty;
        return true;
    }

    private JobCompatibility GetJobCompatibility(Item item, uint currentJobId)
    {
        if (currentJobId == 0 || item.EquipSlotCategory.RowId == 0 || item.ClassJobCategory.RowId == 0)
            return default;
        if (!dataManager.GetExcelSheet<ClassJob>().TryGetRow(currentJobId, out var currentJob)
            || !dataManager.GetExcelSheet<ClassJobCategory>().TryGetRow(item.ClassJobCategory.RowId, out var category))
            return default;

        var abbreviation = currentJob.Abbreviation.ToString();
        var currentFlag = ClassJobFlags.FirstOrDefault(property =>
            string.Equals(property.Name, abbreviation, StringComparison.OrdinalIgnoreCase));
        if (currentFlag?.GetValue(category) is not true)
            return default;

        var compatibleJobCount = 0;
        foreach (var property in ClassJobFlags)
        {
            if (property.GetValue(category) is true)
                compatibleJobCount++;
        }
        return new JobCompatibility(true, Math.Max(1, 1_000 - compatibleJobCount));
    }

    private void UpdateCurrentJob()
    {
        IPlayerCharacter? player = objectTable.LocalPlayer;
        var jobId = player?.ClassJob.RowId ?? 0;
        if (jobId == cachedCurrentJobId)
            return;
        cachedCurrentJobId = jobId;
        CurrentJob = jobId != 0 && dataManager.GetExcelSheet<ClassJob>().TryGetRow(jobId, out var job)
            ? $"{job.Abbreviation} ({jobId})"
            : "Unavailable";
    }

    private static bool TrySelectReward(AddonJournalResult* addon, int index)
    {
        if (index is < 0 or >= RewardCountMaximum || addon->AtkComponentJournalCanvas268 is null)
            return false;
        Span<byte> journalData = stackalloc byte[100];
        Span<byte> inputData = stackalloc byte[50];
        journalData.Clear();
        inputData.Clear();
        fixed (byte* journalDataPointer = journalData)
        fixed (byte* inputDataPointer = inputData)
        {
            var listener = (AtkEventListener*)addon->AtkComponentJournalCanvas268;
            listener->ReceiveEvent(
                AtkEventType.MouseClick,
                7 + index,
                (AtkEvent*)journalDataPointer,
                (AtkEventData*)inputDataPointer);
        }
        return true;
    }

    private static bool TryClickButton(AtkUnitBase* addon, AtkComponentButton* button)
    {
        var node = button->AtkResNode;
        if (node is null)
            return false;
        var clickEvent = (AtkEvent*)node->AtkEventManager.Event;
        for (var inspected = 0; clickEvent is not null && inspected < 32; inspected++, clickEvent = clickEvent->NextEvent)
        {
            if (clickEvent->State.EventType != AtkEventType.MouseClick)
                continue;
            addon->ReceiveEvent(clickEvent->State.EventType, (int)clickEvent->Param, clickEvent);
            return true;
        }
        return false;
    }

    private bool IsAddonVisible(string name) => GetVisibleAddon<AtkUnitBase>(name) is not null;

    private static bool IsAddonVisible(AtkUnitBase* addon)
        => addon is not null && addon->IsReady && addon->IsVisible;

    private T* GetVisibleAddon<T>(string name)
        where T : unmanaged
    {
        try
        {
            var addon = gameGui.GetAddonByName<T>(name);
            return addon is not null && ((AtkUnitBase*)addon)->IsVisible ? addon : null;
        }
        catch
        {
            return null;
        }
    }

    private static ulong AddToSignature(ulong signature, uint value)
        => (signature ^ value) * 1099511628211UL;

    private void ResetCutsceneSession()
    {
        cutsceneConfirmation = default;
        cutsceneSessionObserved = false;
        cutsceneAttemptFinished = false;
        nextCutsceneTick = 0;
        nextCutsceneConfirmationTick = 0;
    }

    private void ResetRewardWindow()
    {
        activeRewardSignature = 0;
        activeRewardMode = QuestRewardSelectionMode.Manual;
        rewardSelectionSent = false;
        rewardCompletionSent = false;
        rewardConfirmAfterTick = 0;
        pendingReward = default;
    }

    private readonly record struct JobCompatibility(bool IsCompatible, int Specificity);
}
