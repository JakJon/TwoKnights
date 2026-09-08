using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// The beat between a cleared wave and the upgrade menu: "Wave Survived", plus
/// anything the wave happened to finish or open.
///
/// Timing is the whole design. A clean wave is the common case and gets one
/// second of acknowledgement before the run moves on — long enough to read,
/// short enough that nobody reaches for a button. A wave that completed a quest
/// — or crossed the threshold that reveals one — is the rare case and holds
/// until the player presses something, because a reward that flashes past is a
/// reward they will swear they never got.
///
/// Newly unlocked quests matter here specifically because a locked quest is
/// hidden entirely rather than greyed out. Without this the Order initiations
/// appear in camp with no event attached to them, and the run that actually
/// earned the invitation says nothing about it.
///
/// One panel, no button — this sits over the cleared arena rather than
/// replacing it.
/// </summary>
public class WaveSurvivedPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private StyleSheet styleSheet;

    [Tooltip("How long a wave that completed nothing lingers before the run moves on.")]
    [SerializeField] private float autoAdvanceSeconds = 1f;
    [Tooltip("Input ignored this long after the overlay appears, so a shot fired as the last enemy died can't skip it.")]
    [SerializeField] private float inputGraceSeconds = 0.25f;

    private VisualElement _root;
    private VisualElement _overlay;
    private VisualElement _questList;
    private Label _hint;

    // Quests finished since this last ran. Collected as they complete rather
    // than diffed at wave end, because completion cascades through chains.
    private readonly Queue<string> _pending = new Queue<string>();
    // Quests that became visible since this last ran. Same reasoning, and the
    // same reason it cannot be a diff: finishing one quest opens the next in
    // the same evaluation pass.
    private readonly Queue<string> _pendingUnlocked = new Queue<string>();

    public static bool IsVisible { get; private set; }

    public bool HasPending => _pending.Count > 0 || _pendingUnlocked.Count > 0;

    private void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        SetupUI();
        QuestProgress.OnQuestCompleted += HandleQuestCompleted;
        QuestProgress.OnQuestUnlocked += HandleQuestUnlocked;
    }

    private void OnDisable()
    {
        QuestProgress.OnQuestCompleted -= HandleQuestCompleted;
        QuestProgress.OnQuestUnlocked -= HandleQuestUnlocked;
        IsVisible = false;
    }

    private void HandleQuestCompleted(string questId)
    {
        _pending.Enqueue(questId);
    }

    private void HandleQuestUnlocked(string questId)
    {
        _pendingUnlocked.Enqueue(questId);
    }

    private void SetupUI()
    {
        if (uiDocument == null)
        {
            Debug.LogWarning("WaveSurvivedPanel missing UIDocument reference.");
            return;
        }

        _root = uiDocument.rootVisualElement;
        if (_root == null) return;

        if (styleSheet != null) _root.styleSheets.Add(styleSheet);

        _overlay = _root.Q<VisualElement>("wave-survived-screen");
        _questList = _root.Q<VisualElement>("ws-quests");
        _hint = _root.Q<Label>("ws-hint");

        Hide();
    }

    private bool EnsureUI()
    {
        // rootVisualElement can be null in the first frames after a UXML/USS
        // reimport, so retry setup lazily instead of trusting OnEnable
        if (_root == null || _overlay == null) SetupUI();
        return _overlay != null;
    }

    /// <summary>
    /// Shows the overlay and returns when the run should move on. Runs on
    /// unscaled time, so the caller may freeze timeScale first.
    /// </summary>
    public IEnumerator ShowWaveSurvived()
    {
        if (!EnsureUI())
        {
            _pending.Clear();
            _pendingUnlocked.Clear();
            yield break;
        }

        var completed = DrainCompletedQuests();
        var unlocked = DrainUnlockedQuests(completed);
        BuildQuestLines(completed, unlocked);

        bool needsInput = completed.Count > 0 || unlocked.Count > 0;
        if (_hint != null)
        {
            _hint.style.display = needsInput ? DisplayStyle.Flex : DisplayStyle.None;
        }

        _root.style.display = DisplayStyle.Flex;
        IsVisible = true;

        AudioManager.Instance?.PlaySFX(needsInput
            ? AudioManager.Instance.questComplete
            : AudioManager.Instance.waveComplete);

        if (!needsInput)
        {
            yield return new WaitForSecondsRealtime(autoAdvanceSeconds);
            Hide();
            yield break;
        }

        // Held-button grace: the shot that killed the last enemy is often still
        // down when this appears, and it would dismiss the reward instantly
        float readyAt = Time.unscaledTime + inputGraceSeconds;
        while (Time.unscaledTime < readyAt) yield return null;
        while (!AnyAdvancePressed()) yield return null;

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
        Hide();
    }

    private List<Quest> DrainCompletedQuests()
    {
        var list = new List<Quest>();
        while (_pending.Count > 0)
        {
            var quest = QuestDatabase.Get(_pending.Dequeue());
            if (quest != null) list.Add(quest);
        }
        return list;
    }

    /// <summary>
    /// Quests the wave revealed, minus any it also finished — a quest whose gate
    /// and objectives fell in the same pass already has a Completed line, and
    /// announcing it as new directly above that would read as two quests.
    /// </summary>
    private List<Quest> DrainUnlockedQuests(List<Quest> completed)
    {
        var list = new List<Quest>();
        while (_pendingUnlocked.Count > 0)
        {
            string id = _pendingUnlocked.Dequeue();
            var quest = QuestDatabase.Get(id);
            if (quest == null) continue;
            if (QuestProgress.IsCompleted(id)) continue;
            if (completed.Exists(q => q.Id == id)) continue;
            if (list.Exists(q => q.Id == id)) continue;
            list.Add(quest);
        }
        return list;
    }

    private void BuildQuestLines(List<Quest> completed, List<Quest> unlocked)
    {
        if (_questList == null) return;
        _questList.Clear();
        // The list carries its own top margin, which with a panel behind it
        // would otherwise show as dead space under "Wave Survived"
        _questList.style.display = (completed.Count > 0 || unlocked.Count > 0)
            ? DisplayStyle.Flex
            : DisplayStyle.None;

        if (completed.Count > 0)
        {
            var heading = new Label(completed.Count == 1 ? "Quest Completed" : "Quests Completed");
            heading.AddToClassList("ws-section-title");
            _questList.Add(heading);
        }

        for (int i = 0; i < completed.Count; i++)
        {
            var quest = completed[i];

            // The heading above says what happened to these, so the name stands
            // on its own rather than repeating "— Completed" on every row.
            var line = new Label(quest.Name);
            line.AddToClassList("ws-quest-name");
            _questList.Add(line);

            // What the wave actually did. Without it the panel names a quest and
            // hands over a reward while never saying which condition was met —
            // for a quest whose objective was the reason to keep going, that is
            // the one line the player wants confirmed.
            if (quest.HasObjectives)
            {
                for (int o = 0; o < quest.Objectives.Length; o++)
                {
                    var objectiveLine = new Label(DescribeMetObjective(quest.Objectives[o]));
                    objectiveLine.AddToClassList("ws-quest-objective");
                    _questList.Add(objectiveLine);
                }
            }

            // Crystals draw as count + icon; everything else is words. Both live on
            // one row so a mixed reward still reads as a single line.
            // The class goes on the ROW only — its font and colour inherit down to
            // whatever it holds, while its margin applies once instead of twice.
            var rewardRow = new VisualElement();
            rewardRow.AddToClassList("ws-quest-reward");
            rewardRow.style.flexDirection = FlexDirection.Row;
            rewardRow.style.alignItems = Align.Center;
            rewardRow.style.justifyContent = Justify.Center;

            if (quest.Reward != null && quest.Reward.Crystals > 0)
            {
                var crystals = CrystalText.Build(quest.Reward.Crystals, 14f);
                crystals.style.marginRight = 6;
                rewardRow.Add(crystals);
            }

            string items = quest.Reward != null ? quest.Reward.DescribeItems() : "";
            if (!string.IsNullOrEmpty(items))
            {
                rewardRow.Add(new Label(items));
            }

            if (rewardRow.childCount > 0) _questList.Add(rewardRow);
        }

        BuildUnlockedLines(completed.Count > 0, unlocked);
    }

    /// <summary>
    /// What the wave opened. Name plus what each one asks for — the reveal is
    /// only worth stopping the run for if the player can tell from it whether
    /// they want to go and do it.
    ///
    /// Rewards are deliberately absent: an unearned reward listed next to an
    /// earned one above it is a way to be confused about which you just got.
    /// </summary>
    private void BuildUnlockedLines(bool afterCompletions, List<Quest> unlocked)
    {
        if (unlocked.Count == 0) return;

        if (afterCompletions)
        {
            var divider = new VisualElement();
            divider.AddToClassList("ws-section-divider");
            _questList.Add(divider);
        }

        var caption = new Label(unlocked.Count == 1 ? "Quest Unlocked" : "Quests Unlocked");
        caption.AddToClassList("ws-section-title");
        _questList.Add(caption);

        for (int i = 0; i < unlocked.Count; i++)
        {
            var quest = unlocked[i];

            var line = new Label(quest.Name);
            line.AddToClassList("ws-quest-name");
            _questList.Add(line);

            if (!quest.HasObjectives) continue;
            for (int o = 0; o < quest.Objectives.Length; o++)
            {
                var task = new Label(DescribeOpenObjective(quest.Objectives[o]));
                task.AddToClassList("ws-quest-task");
                _questList.Add(task);
            }
        }
    }

    /// <summary>
    /// What a freshly opened quest asks for, without the counter. Progress on a
    /// quest revealed this second is either zero or an accident of a shared stat;
    /// either way "0/500" is a worse first impression of the ask than the words.
    /// </summary>
    private static string DescribeOpenObjective(QuestObjective objective)
    {
        string body = objective.HideProgress
            ? objective.DisplayLabel
            : $"{objective.Target} {objective.DisplayLabel}";
        return $"<color=#8D8266>›</color> {body}";
    }

    /// <summary>
    /// "✔ 5/5 waves into the mine" — the quest log's checklist phrasing with the
    /// unmet branch dropped, since a quest cannot reach this panel with an
    /// objective outstanding. Same words in both places, so the line the player
    /// was chasing in the log is the line they see paid off here.
    /// </summary>
    private static string DescribeMetObjective(QuestObjective objective)
    {
        string body = objective.HideProgress
            ? objective.DisplayLabel
            : $"{objective.Current}/{objective.Target} {objective.DisplayLabel}";
        return $"<color=#F0CD78>✔</color> {body}";
    }

    /// <summary>
    /// Any button at all. This is an acknowledgement, not a choice, so binding
    /// it to one face button would only be a thing to get wrong.
    /// </summary>
    private static bool AnyAdvancePressed()
    {
        var gp = Gamepad.current;
        if (gp != null)
        {
            if (MenuGamepad.SubmitPressed(gp) || MenuGamepad.CancelPressed(gp)) return true;
            if (gp.startButton.wasPressedThisFrame || gp.selectButton.wasPressedThisFrame) return true;
            if (gp.buttonNorth.wasPressedThisFrame || gp.buttonWest.wasPressedThisFrame) return true;
            if (gp.leftShoulder.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame) return true;
        }

        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;

        var mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    public void Hide()
    {
        IsVisible = false;
        if (_root != null) _root.style.display = DisplayStyle.None;
    }

    private void OnDestroy()
    {
        IsVisible = false;
    }
}
