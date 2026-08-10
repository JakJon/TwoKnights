using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// The quest log: quests grouped under collapsible map headers, with a detail
/// pane showing an objective checklist.
///
/// Only UNLOCKED quests appear. A locked quest is not shown greyed out — half
/// the content is Order lines whose very existence is a reveal, and a row
/// reading "???" would spoil that there is something there to find.
/// </summary>
public class QuestPanel : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement _root;
    private VisualElement _panel;
    private ScrollView _list;
    private Label _detailName;
    private Label _detailDescription;
    private Label _detailProgress;
    private Label _detailReward;
    private VisualElement _rewardIcons;
    private Label _rewardTip;
    private Button _closeButton;

    // One entry per rendered row. A row is either a header (Quest == null) — a
    // map group, or the single Completed shelf at the foot of the log — or a
    // quest; navigation walks this list, so folding anything simply rebuilds it
    // shorter.
    private struct Row
    {
        public Button Button;
        public Quest Quest;
        public string GroupId;
        public bool CompletedShelf;
    }

    private readonly List<Row> _rows = new List<Row>();
    private readonly HashSet<string> _collapsed = new HashSet<string>();
    // The shelf is tracked by whether it is OPEN rather than closed, so one the
    // player has never touched is shut — which is the point of it. The map
    // groups above keep the opposite default.
    private bool _completedShelfOpen;
    private int _selectedIndex = -1;

    // The sweeping gold band on every quest the player has never focused. Held
    // separately from _rows so the animation loop does not walk rows that have
    // nothing to animate.
    private readonly List<VisualElement> _sheens = new List<VisualElement>();

    // Reward squares are stepped through with left/right so their names are
    // reachable without a mouse. The panel polls its own input for this — the
    // camp's action set is only up/down/confirm/cancel — so it must be listed in
    // CampMenuController.PanelPollsOwnInput or both react to the same press.
    private readonly List<VisualElement> _rewardSquares = new List<VisualElement>();
    private readonly List<string> _rewardNames = new List<string>();
    private int _rewardIndex = -1;

    private const float RepeatDelay = 0.4f;
    private const float RepeatInterval = 0.12f;
    private const float EntryInputDelay = 0.25f;

    private float _inputReadyTime;
    private int _heldVertical, _heldHorizontal;
    private float _nextVerticalRepeat, _nextHorizontalRepeat;

    public System.Action OnCloseRequested;

    private const string SELECTED_CLASS = "quest-list-item--selected";
    private const string COMPLETED_CLASS = "quest-list-item--completed";
    private const string NEW_CLASS = "quest-list-item--new";
    private const string SHEEN_NAME = "quest-new-sheen";
    // Stands in for a map id on the one shelf row, which belongs to no map.
    // Cannot collide with a real group: "" is the camp and the rest are map ids.
    private const string COMPLETED_SHELF = "__completed";
    private const string GROUP_CLASS = "quest-group-header";
    private const string SHELF_CLASS = "quest-group-header--completed";
    private const string COMPLETED_CHECK = "<color=#F0CD78>✔</color> ";
    private const string GOLD = "#F0CD78";
    private const string DIM = "#8A7F68";

    private void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        BindUI();
        QuestProgress.OnQuestCompleted += HandleQuestChanged;
        QuestProgress.OnQuestUnlocked += HandleQuestChanged;
        QuestProgress.OnQuestProgressChanged += HandleProgressChanged;
    }

    private void OnDisable()
    {
        if (_closeButton != null) _closeButton.clicked -= HandleCloseClicked;
        QuestProgress.OnQuestCompleted -= HandleQuestChanged;
        QuestProgress.OnQuestUnlocked -= HandleQuestChanged;
        QuestProgress.OnQuestProgressChanged -= HandleProgressChanged;
    }

    // A completion or an unlock changes which rows exist, so the list is rebuilt
    private void HandleQuestChanged(string questId)
    {
        string keep = SelectedQuestId();

        // Completing the quest the player is reading moves it into the Completed
        // shelf. Open the shelf so the row stays under the cursor rather than
        // folding itself away mid-read.
        if (keep == questId && QuestProgress.IsCompleted(questId)) _completedShelfOpen = true;

        Rebuild();
        RestoreSelection(keep);
    }

    // Progress alone never changes the row set — only the detail pane
    private void HandleProgressChanged(string questId)
    {
        if (SelectedQuestId() == questId) ShowDetails(CurrentQuest());
    }

    private void BindUI()
    {
        if (uiDocument == null) return;
        _root = uiDocument.rootVisualElement;
        if (_root == null) return;

        _panel = _root.Q<VisualElement>("quest-panel");
        _list = _root.Q<ScrollView>("quest-list");
        _detailName = _root.Q<Label>("quest-detail-name");
        _detailDescription = _root.Q<Label>("quest-detail-description");
        _detailProgress = _root.Q<Label>("quest-detail-progress");
        _detailReward = _root.Q<Label>("quest-detail-reward");
        _rewardIcons = _root.Q<VisualElement>("quest-reward-icons");
        _rewardTip = _root.Q<Label>("quest-reward-tip");
        _closeButton = _root.Q<Button>("quest-close-button");

        if (_detailProgress != null) _detailProgress.enableRichText = true;

        if (_list != null)
        {
            _list.focusable = false;
            if (_list.contentContainer != null) _list.contentContainer.focusable = false;
        }

        if (_closeButton != null)
        {
            _closeButton.clicked -= HandleCloseClicked;
            _closeButton.clicked += HandleCloseClicked;
        }

        Rebuild();
    }

    // ---------- rows ----------

    private void Rebuild()
    {
        if (_list == null) return;
        _list.Clear();
        _rows.Clear();
        _sheens.Clear();

        // Everything already finished, from every map, folds under ONE row at the
        // very foot of the log, shut until asked for: a completed quest is a
        // trophy, not a task, and past the early game there are more of them than
        // there are live quests to read the list for. A shelf per map put those
        // trophies between the player and the next map's live quests.
        var finished = new List<Quest>();

        foreach (var groupId in QuestDatabase.Groups)
        {
            var active = new List<Quest>();
            int done = 0;
            foreach (var quest in QuestProgress.VisibleForMap(groupId))
            {
                if (QuestProgress.IsCompleted(quest.Id)) { finished.Add(quest); done++; }
                else active.Add(quest);
            }

            // An untouched map shows nothing at all, and a map with nothing live
            // left says everything it has to say down in the shelf
            if (active.Count == 0) continue;

            bool collapsed = _collapsed.Contains(groupId);
            AddHeader(groupId, GroupName(groupId), $"{done}/{done + active.Count}", collapsed, false, false);
            if (collapsed) continue;

            for (int i = 0; i < active.Count; i++) AddQuestRow(active[i]);
        }

        if (finished.Count > 0)
        {
            AddHeader(COMPLETED_SHELF, "Completed", finished.Count.ToString(),
                      !_completedShelfOpen, true, AnyUnseen(finished));
            if (_completedShelfOpen)
            {
                for (int i = 0; i < finished.Count; i++) AddQuestRow(finished[i]);
            }
        }

        if (_rows.Count == 0)
        {
            ShowEmptyDetails();
            return;
        }

        SelectRow(FirstQuestRow());
    }

    /// <summary>
    /// A foldable header: either a map group, or the log's one Completed shelf.
    ///
    /// The shelf takes the sweep when it holds a quest the player has never
    /// opened. A quest that unlocks and completes in the same run lands
    /// straight in the folded shelf, and QuestProgress.HasUnseen would keep the
    /// camp's notification dot lit over a row nothing on screen pointed at.
    /// </summary>
    private void AddHeader(string groupId, string label, string count, bool collapsed,
                           bool completedShelf, bool unseen)
    {
        string arrow = collapsed ? "▸" : "▾";
        string slug = string.IsNullOrEmpty(groupId) ? "camp" : groupId;
        var button = new Button(() => ToggleFold(groupId, completedShelf))
        {
            name = completedShelf ? "quest-completed-shelf" : "quest-group-" + slug,
            text = $"{arrow} {label}   {count}",
        };
        button.AddToClassList(GROUP_CLASS);
        if (completedShelf) button.AddToClassList(SHELF_CLASS);
        button.focusable = true;
        // Not cleared by focusing the header — it is the quests underneath that
        // are unread, so it retires only once they have actually been opened
        if (unseen) AttachSheen(button);

        int index = _rows.Count;
        button.RegisterCallback<FocusInEvent>(_ => SelectRow(index));
        _list.Add(button);
        _rows.Add(new Row { Button = button, Quest = null, GroupId = groupId, CompletedShelf = completedShelf });
    }

    private static bool AnyUnseen(List<Quest> quests)
    {
        for (int i = 0; i < quests.Count; i++)
        {
            if (!QuestProgress.IsSeen(quests[i].Id)) return true;
        }
        return false;
    }

    private void AddQuestRow(Quest quest)
    {
        bool completed = QuestProgress.IsCompleted(quest.Id);
        var button = new Button { name = "quest-item-" + quest.Id };
        button.enableRichText = true;
        button.text = completed ? COMPLETED_CHECK + quest.Name : quest.Name;
        button.AddToClassList("quest-list-item");
        if (completed) button.AddToClassList(COMPLETED_CLASS);
        button.focusable = true;
        if (!QuestProgress.IsSeen(quest.Id)) AttachSheen(button);

        int index = _rows.Count;
        button.clicked += () => SelectRow(index);
        button.RegisterCallback<FocusInEvent>(_ => SelectRow(index));
        _list.Add(button);
        _rows.Add(new Row { Button = button, Quest = quest, GroupId = quest.MapId });
    }

    private void ToggleFold(string groupId, bool completedShelf)
    {
        if (completedShelf)
        {
            _completedShelfOpen = !_completedShelfOpen;
        }
        else if (!_collapsed.Remove(groupId))
        {
            _collapsed.Add(groupId);
        }

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
        string keep = SelectedQuestId();
        Rebuild();

        // Folding usually swallows the row the cursor was on, so the header the
        // player just acted on takes the selection when that happens — landing
        // wherever the old index happens to point reads as the list jumping
        if (!TrySelectQuest(keep)) SelectHeader(groupId, completedShelf);
    }

    private static string GroupName(string mapId)
    {
        if (string.IsNullOrEmpty(mapId)) return "The Camp";
        var catalog = MapCatalog.Instance;
        var map = catalog != null ? catalog.Find(mapId) : null;
        return map != null ? map.DisplayName : mapId;
    }

    // ---------- selection ----------

    private int FirstQuestRow()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Quest != null) return i;
        }
        return 0;
    }

    private string SelectedQuestId()
    {
        var quest = CurrentQuest();
        return quest != null ? quest.Id : null;
    }

    private Quest CurrentQuest()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _rows.Count) return null;
        return _rows[_selectedIndex].Quest;
    }

    private void RestoreSelection(string questId)
    {
        if (TrySelectQuest(questId)) return;
        SelectRow(Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _rows.Count - 1)));
    }

    private bool TrySelectQuest(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return false;
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Quest != null && _rows[i].Quest.Id == questId) { SelectRow(i); return true; }
        }
        return false;
    }

    private void SelectHeader(string groupId, bool completedShelf)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Quest == null && _rows[i].GroupId == groupId
                && _rows[i].CompletedShelf == completedShelf)
            {
                SelectRow(i);
                return;
            }
        }
        SelectRow(FirstQuestRow());
    }

    private void SelectRow(int index)
    {
        if (_rows.Count == 0) return;
        _selectedIndex = Mathf.Clamp(index, 0, _rows.Count - 1);

        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Button == null) continue;
            if (i == _selectedIndex)
            {
                _rows[i].Button.AddToClassList(SELECTED_CLASS);
                _rows[i].Button.Focus();
            }
            else
            {
                _rows[i].Button.RemoveFromClassList(SELECTED_CLASS);
            }
        }

        // Group headers and quest rows are Buttons, but a focused button below
        // the fold does not always bring itself into view — be explicit
        if (_list != null && _rows[_selectedIndex].Button != null)
        {
            _list.ScrollTo(_rows[_selectedIndex].Button);
        }

        var quest = _rows[_selectedIndex].Quest;
        if (quest != null)
        {
            // Focusing a quest is what clears its half of the notification dot,
            // and what retires its gold sweep — the two say the same thing, so
            // they have to end together
            QuestProgress.MarkSeen(quest.Id);
            ClearSheen(_rows[_selectedIndex].Button);
            ShowDetails(quest);
        }
        else
        {
            ShowGroupDetails(_rows[_selectedIndex]);
        }
    }

    // ---------- detail pane ----------

    private void ShowDetails(Quest quest)
    {
        if (quest == null) return;
        if (_detailName != null) _detailName.text = quest.Name;
        if (_detailDescription != null) _detailDescription.text = quest.Description;

        if (_detailProgress != null)
        {
            _detailProgress.text = BuildChecklist(quest);
            _detailProgress.style.display = quest.HasObjectives ? DisplayStyle.Flex : DisplayStyle.None;
        }

        bool completed = QuestProgress.IsCompleted(quest.Id);
        if (_detailReward != null)
        {
            // The caption only says whether it is still owed; WHAT is owed is
            // the row of squares underneath
            if (completed)
            {
                _detailReward.text = $"COMPLETED {QuestProgress.GetCompletionDate(quest.Id)}";
                _detailReward.RemoveFromClassList("quest-detail-reward--active");
                _detailReward.AddToClassList("quest-detail-reward--completed");
            }
            else
            {
                _detailReward.text = "REWARD";
                _detailReward.RemoveFromClassList("quest-detail-reward--completed");
                _detailReward.AddToClassList("quest-detail-reward--active");
            }
        }

        BuildRewardSquares(quest);
    }

    /// <summary>
    /// One square per thing the quest gives, showing only its art. The name
    /// arrives on hover — three rewards side by side stay readable as pictures
    /// where three phrases would not.
    /// </summary>
    private void BuildRewardSquares(Quest quest)
    {
        if (_rewardIcons == null) return;
        _rewardIcons.Clear();
        _rewardSquares.Clear();
        _rewardNames.Clear();
        _rewardIndex = -1;
        HideRewardTip();

        var reward = quest.Reward;
        if (reward == null) return;
        var catalog = EquipmentCatalog.Instance;

        if (reward.Crystals > 0)
        {
            // Always show the number, including 1 — the amount is the whole point of
            // the reward, and hiding it at 1 made a single crystal look like a
            // different kind of reward from three.
            // The tip names the reward; it does not restate the amount as words.
            // The square already reads "3 <crystal icon>", which is the house form.
            AddRewardSquare(
                catalog != null ? catalog.CrystalIcon : null,
                reward.Crystals.ToString(),
                "Crystals");
        }

        if (reward.GrantsEquipment)
        {
            var def = catalog != null ? catalog.Find(reward.EquipmentId) : null;
            AddRewardSquare(def != null ? def.Icon : null, null,
                            def != null ? def.DisplayName : reward.EquipmentId);
        }

        if (reward.ExtraEquipmentSlot)
        {
            AddRewardSquare(catalog != null ? catalog.EquipmentSlotIcon : null, null,
                            "Another equipment slot");
        }

        if (reward.ExtraSpecialSlot)
        {
            AddRewardSquare(catalog != null ? catalog.SpecialSlotIcon : null, null,
                            "Another special slot");
        }
    }

    /// <summary>
    /// Steps the reward highlight. Called from left/right so a controller can
    /// read every reward name; the mouse path uses hover instead.
    /// </summary>
    private void CycleReward(int direction)
    {
        if (_rewardSquares.Count == 0) return;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);

        if (_rewardIndex < 0) _rewardIndex = direction > 0 ? 0 : _rewardSquares.Count - 1;
        else _rewardIndex = (_rewardIndex + direction + _rewardSquares.Count) % _rewardSquares.Count;

        for (int i = 0; i < _rewardSquares.Count; i++)
        {
            if (i == _rewardIndex) _rewardSquares[i].AddToClassList("reward-square--selected");
            else _rewardSquares[i].RemoveFromClassList("reward-square--selected");
        }
        ShowRewardTip(_rewardSquares[_rewardIndex], _rewardNames[_rewardIndex]);
    }

    private void ClearRewardHighlight()
    {
        _rewardIndex = -1;
        for (int i = 0; i < _rewardSquares.Count; i++)
        {
            _rewardSquares[i].RemoveFromClassList("reward-square--selected");
        }
        HideRewardTip();
    }

    private void AddRewardSquare(Sprite sprite, string count, string tipText)
    {
        var square = new VisualElement();
        square.AddToClassList("reward-square");

        // Count goes BEFORE the icon, same as every other crystal amount — it used
        // to ride in the corner as an overlay, which read as a badge rather than as
        // "this many of these".
        if (!string.IsNullOrEmpty(count))
        {
            var countLabel = new Label(count);
            countLabel.AddToClassList("reward-square-count");
            square.Add(countLabel);
        }

        var image = new VisualElement();
        image.AddToClassList("reward-square-image");
        if (sprite != null) image.style.backgroundImage = new StyleBackground(sprite);
        square.Add(image);

        square.RegisterCallback<PointerEnterEvent>(_ => ShowRewardTip(square, tipText));
        square.RegisterCallback<PointerLeaveEvent>(_ => HideRewardTip());

        _rewardIcons.Add(square);
        _rewardSquares.Add(square);
        _rewardNames.Add(tipText);
    }

    private void ShowRewardTip(VisualElement over, string text)
    {
        if (_rewardTip == null || string.IsNullOrEmpty(text)) return;
        _rewardTip.text = text;
        _rewardTip.style.display = DisplayStyle.Flex;

        // Position after layout, or the tip's own width is still 0 and it lands
        // off-centre on the first hover of every square
        _rewardTip.schedule.Execute(() =>
        {
            var box = over.worldBound;
            var panelRect = _panel != null ? _panel.worldBound : box;
            float left = box.center.x - _rewardTip.resolvedStyle.width * 0.5f - panelRect.x;
            float top = box.yMin - _rewardTip.resolvedStyle.height - 8f - panelRect.y;
            _rewardTip.style.left = Mathf.Max(4f, left);
            _rewardTip.style.top = Mathf.Max(4f, top);
        }).StartingIn(0);
    }

    private void HideRewardTip()
    {
        if (_rewardTip != null) _rewardTip.style.display = DisplayStyle.None;
    }

    /// <summary>
    /// One line per objective. Objectives that hide their progress show only
    /// their prose — the whole point of a hidden objective is that the count
    /// would give away what the quest is actually asking for.
    /// </summary>
    private static string BuildChecklist(Quest quest)
    {
        if (!quest.HasObjectives) return "";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < quest.Objectives.Length; i++)
        {
            var objective = quest.Objectives[i];
            bool met = objective.IsMet;
            if (i > 0) sb.Append('\n');
            sb.Append(met ? $"<color={GOLD}>✔</color> " : $"<color={DIM}>•</color> ");
            if (objective.HideProgress)
            {
                sb.Append(objective.DisplayLabel);
            }
            else
            {
                sb.Append($"{objective.Current}/{objective.Target} {objective.DisplayLabel}");
            }
        }
        return sb.ToString();
    }

    private void ShowGroupDetails(Row row)
    {
        bool shelf = row.CompletedShelf;
        if (_detailName != null) _detailName.text = shelf ? "Completed" : GroupName(row.GroupId);
        if (_detailDescription != null)
        {
            _detailDescription.text = shelf ? "Every quest you have finished." : "";
        }
        if (_detailProgress != null)
        {
            bool folded = shelf ? !_completedShelfOpen : _collapsed.Contains(row.GroupId);
            _detailProgress.text = folded ? "Collapsed." : "";
            _detailProgress.style.display = DisplayStyle.Flex;
        }
        if (_detailReward != null) _detailReward.text = "";
        ClearRewardSquares();
    }

    private void ShowEmptyDetails()
    {
        if (_detailName != null) _detailName.text = "No quests yet";
        if (_detailDescription != null) _detailDescription.text = "Take the field and they will find you.";
        if (_detailProgress != null) _detailProgress.text = "";
        if (_detailReward != null) _detailReward.text = "";
        ClearRewardSquares();
    }

    private void ClearRewardSquares()
    {
        if (_rewardIcons != null) _rewardIcons.Clear();
        _rewardSquares.Clear();
        _rewardNames.Clear();
        _rewardIndex = -1;
        HideRewardTip();
    }

    // ---------- the "never inspected" sweep ----------

    // A soft gold band, faded to nothing at both ends so it has no visible
    // edges when it slides across a row. One pixel tall and stretched: the
    // gradient only ever runs horizontally.
    private static Texture2D _sheenTexture;

    private static Texture2D SheenTexture()
    {
        if (_sheenTexture != null) return _sheenTexture;

        const int width = 128;
        var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
        {
            name = "QuestSheen",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            // Generated, not an asset — without this it leaks into the scene on
            // every domain reload in the editor
            hideFlags = HideFlags.HideAndDontSave,
        };

        for (int x = 0; x < width; x++)
        {
            float t = x / (float)(width - 1);
            // Raised cosine: 0 at both ends, 1 in the middle
            float alpha = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f);
            texture.SetPixel(x, 0, new Color(0.94f, 0.80f, 0.47f, alpha));
        }
        texture.Apply();

        _sheenTexture = texture;
        return texture;
    }

    private void AttachSheen(VisualElement row)
    {
        row.AddToClassList(NEW_CLASS);

        var sheen = new VisualElement { name = SHEEN_NAME, pickingMode = PickingMode.Ignore };
        sheen.AddToClassList(SHEEN_NAME);
        sheen.style.backgroundImage = new StyleBackground(SheenTexture());
        // The band is 128x1; without an explicit size it draws at that size in
        // the corner instead of filling the strip
        sheen.style.backgroundSize = new StyleBackgroundSize(
            new BackgroundSize(Length.Percent(100), Length.Percent(100)));
        row.Add(sheen);
        _sheens.Add(sheen);
    }

    private void ClearSheen(VisualElement row)
    {
        if (row == null || !row.ClassListContains(NEW_CLASS)) return;
        row.RemoveFromClassList(NEW_CLASS);

        var sheen = row.Q<VisualElement>(SHEEN_NAME);
        if (sheen == null) return;
        _sheens.Remove(sheen);
        sheen.RemoveFromHierarchy();
    }

    /// <summary>
    /// Slides every band left to right, then rests a beat before the next pass.
    /// Unscaled time: the pause screen shows this panel with the game frozen.
    /// </summary>
    private void UpdateSheens()
    {
        if (_sheens.Count == 0) return;

        const float SweepSeconds = 1.5f;
        const float RestSeconds = 0.8f;

        float phase = Mathf.Repeat(Time.unscaledTime, SweepSeconds + RestSeconds);
        bool sweeping = phase < SweepSeconds;
        float t = sweeping ? phase / SweepSeconds : 0f;

        for (int i = 0; i < _sheens.Count; i++)
        {
            var sheen = _sheens[i];
            if (sheen == null) continue;

            if (!sweeping)
            {
                sheen.style.opacity = 0f;
                continue;
            }

            float rowWidth = sheen.parent != null ? sheen.parent.resolvedStyle.width : 0f;
            float bandWidth = sheen.resolvedStyle.width;
            sheen.style.translate = new StyleTranslate(
                new Translate(Mathf.Lerp(-bandWidth, rowWidth, t), 0f));
            // Peaks mid-row so the band never appears or vanishes at an edge.
            // Kept low: the band passes over the quest's name, and anything
            // heavier washes the text out as it crosses.
            sheen.style.opacity = Mathf.Sin(t * Mathf.PI) * 0.32f;
        }
    }

    // ---------- input (self-polled: needs left/right for the reward squares) ----------

    public void NavigateUp()
    {
        if (_rows.Count == 0) return;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
        SelectRow(_selectedIndex <= 0 ? _rows.Count - 1 : _selectedIndex - 1);
    }

    public void NavigateDown()
    {
        if (_rows.Count == 0) return;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
        SelectRow((_selectedIndex + 1) % _rows.Count);
    }

    /// <summary>
    /// A on a header — a map group or its Completed shelf — folds it. A on a
    /// quest does nothing: the log is for reading, and B is what leaves it.
    /// </summary>
    public void Confirm()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _rows.Count) return;
        var row = _rows[_selectedIndex];
        if (row.Quest == null) ToggleFold(row.GroupId, row.CompletedShelf);
    }

    private void Update()
    {
        if (!IsVisible) return;
        UpdateSheens();
        if (Time.unscaledTime < _inputReadyTime) return;

        var gp = Gamepad.current;
        var kb = Keyboard.current;

        bool upHeld = (gp != null && (gp.dpad.up.isPressed || gp.leftStick.up.isPressed))
                      || (kb != null && (kb.upArrowKey.isPressed || kb.wKey.isPressed));
        bool downHeld = (gp != null && (gp.dpad.down.isPressed || gp.leftStick.down.isPressed))
                        || (kb != null && (kb.downArrowKey.isPressed || kb.sKey.isPressed));
        bool leftHeld = (gp != null && (gp.dpad.left.isPressed || gp.leftStick.left.isPressed))
                        || (kb != null && (kb.leftArrowKey.isPressed || kb.aKey.isPressed));
        bool rightHeld = (gp != null && (gp.dpad.right.isPressed || gp.leftStick.right.isPressed))
                         || (kb != null && (kb.rightArrowKey.isPressed || kb.dKey.isPressed));

        int vertical = ApplyRepeat((downHeld ? 1 : 0) - (upHeld ? 1 : 0), ref _heldVertical, ref _nextVerticalRepeat);
        int horizontal = ApplyRepeat((rightHeld ? 1 : 0) - (leftHeld ? 1 : 0), ref _heldHorizontal, ref _nextHorizontalRepeat);

        bool confirm = MenuGamepad.SubmitPressed(gp)
                       || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame));
        bool cancel = MenuGamepad.CancelPressed(gp)
                      || (kb != null && kb.escapeKey.wasPressedThisFrame);

        if (vertical > 0) NavigateDown();
        else if (vertical < 0) NavigateUp();
        else if (horizontal != 0) CycleReward(horizontal);
        else if (confirm) Confirm();
        else if (cancel) HandleCloseClicked();
    }

    private int ApplyRepeat(int held, ref int lastHeld, ref float nextRepeat)
    {
        if (held == 0) { lastHeld = 0; return 0; }
        if (held != lastHeld)
        {
            lastHeld = held;
            nextRepeat = Time.unscaledTime + RepeatDelay;
            return held;
        }
        if (Time.unscaledTime >= nextRepeat)
        {
            nextRepeat = Time.unscaledTime + RepeatInterval;
            return held;
        }
        return 0;
    }

    public void Show()
    {
        if (_panel == null) BindUI();
        if (_panel == null) return;
        Rebuild();
        _panel.style.display = DisplayStyle.Flex;
        // Swallow the button press that opened this panel
        _inputReadyTime = Time.unscaledTime + EntryInputDelay;
        _heldVertical = 0;
        _heldHorizontal = 0;
        if (_rows.Count > 0)
        {
            int target = Mathf.Clamp(_selectedIndex < 0 ? FirstQuestRow() : _selectedIndex, 0, _rows.Count - 1);
            SelectRow(target);
            var toFocus = _rows[target].Button;
            _panel.schedule.Execute(() => toFocus.Focus()).StartingIn(0);
        }
    }

    public void Hide()
    {
        if (_panel == null) return;
        _panel.style.display = DisplayStyle.None;
    }

    public bool IsVisible => _panel != null && _panel.style.display == DisplayStyle.Flex;

    private void HandleCloseClicked()
    {
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiCancel);
        Hide();
        OnCloseRequested?.Invoke();
    }
}
