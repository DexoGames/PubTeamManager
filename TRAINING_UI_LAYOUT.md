# Training Page — UI Layout Guide

Visual reference for building the Training page that `TrainingPageUI.cs` drives.
All `→ fieldName` markers are the `[SerializeField]`s to assign in the inspector.

---

## Wireframe — normal state (a stat drill selected)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  TRAINING                                            Next session: Wed 12 Aug   │
│  → headerText                                        → nextSessionText          │
├───────────────────────────┬────────────────────────────────────────────────────┤
│  Choose Training Session  │  ┌──────────────────────────────────────────────┐  │
│                           │  │ Shooting Practice                 [Technical]│  │
│  ┌─────────────────────┐  │  │ → drillNameText              → typeBadge+typeText│
│  │ Shooting Practice   │◄─┼──│                                              │  │
│  ├─────────────────────┤  │  │ Affects: Shooting   (squad Boost +2.3/15)    │  │
│  │ Passing Drills      │  │  │ → affectedStatsText                          │  │
│  ├─────────────────────┤  │  │                                              │  │
│  │ Defensive Training  │  │  │ Sharpens finishing. Boosts Shooting.         │  │
│  ├─────────────────────┤  │  │ +1 Boost per session to Shooting (max +15).  │  │
│  │ Dribbling Drills    │  │  │ Other Boost fades.                           │  │
│  ├─────────────────────┤  │  │ → effectText                                 │  │
│  │ Crossing Practice   │  │  └──────────────────────────────────────────────┘  │
│  │ Heading Drills      │  │                                                     │
│  │ Positioning Drills  │  │   Currently training: Passing Drills                │
│  │ … (scrollable)      │  │   → currentlySetText                                 │
│  │                     │  │                                                     │
│  │                     │  │              ┌────────────────────┐                 │
│  └─────────────────────┘  │              │    SET TRAINING    │                 │
│   → optionContainer       │              └────────────────────┘                 │
│     (ScrollRect content)  │                → setTrainingButton                   │
│   buttons = optionButtonPrefab                                                   │
└───────────────────────────┴────────────────────────────────────────────────────┘
        LEFT COLUMN                              RIGHT COLUMN
```

Selected drill button is auto-highlighted (lightened) by code; type colour is set by code too.

---

## Wireframe — positional state (Positional Training selected)

`positionalPanel` is hidden by default and shown only when the Positional drill is selected.

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  TRAINING                                            Next session: Wed 12 Aug   │
├───────────────────────────┬────────────────────────────────────────────────────┤
│  Choose Training Session  │  Positional Training                  [Positional]  │
│  ┌─────────────────────┐  │  Retrains up to 5 players in a chosen position…     │
│  │ … other drills …    │  │  ┌──────────────────────────────────────────────┐  │
│  │ Tactic Drills       │  │  │  positionalPanel                             │  │
│  │ Team Social Activity│  │  │  Position: [ Striker                    ▼]   │  │
│  │ Positional Training │◄─┼──│             → positionDropdown               │  │
│  └─────────────────────┘  │  │  ┌────────────────────────────────────────┐ │  │
│                           │  │  │ [✓] Jack Smith    — ST (Good)          │ │  │
│                           │  │  │ [✓] Alan Jones    — ST (Okay)          │ │  │
│                           │  │  │ [ ] Bob White     — ST (Poor)          │ │  │
│                           │  │  │ [ ] Carl Brown    — ST (None)          │ │  │
│                           │  │  │ … (scrollable)    → playerListContainer │ │  │
│                           │  │  └────────────────────────────────────────┘ │  │
│                           │  │   row = playerRowPrefab                      │  │
│                           │  │   2/5 selected     → selectedCountText       │  │
│                           │  └──────────────────────────────────────────────┘  │
│                           │              ┌────────────────────┐                 │
│                           │              │    SET TRAINING    │                 │
│                           │              └────────────────────┘                 │
└───────────────────────────┴────────────────────────────────────────────────────┘
```

---

## GameObject hierarchy (build this)

> Important: `UIPage.Elements` is `transform.GetChild(0)`, so **everything visible lives under a single root child** (here "Elements"). The `TrainingPageUI` component sits on the page root.

```
TrainingPage                         (TrainingPageUI + your UIPage setup)
└── Elements                         (RectTransform — MUST be child index 0; stretched full)
    ├── Header                       (Horizontal Layout Group)
    │   ├── HeaderText               TMP            → headerText
    │   └── NextSessionText          TMP (right-aligned)  → nextSessionText
    │
    └── Body                         (Horizontal Layout Group, stretch)
        │
        ├── LeftColumn               (LayoutElement: flexibleWidth ~1)
        │   └── DrillScroll          (ScrollRect, vertical)
        │       └── Viewport         (Mask + Image)
        │           └── Content      (Vertical Layout Group + ContentSizeFitter:Vert=PreferredSize)
        │                            → optionContainer
        │               · drill buttons spawned here from optionButtonPrefab
        │
        └── RightColumn              (Vertical Layout Group, LayoutElement: flexibleWidth ~1.4)
            ├── InfoPanel            (Vertical Layout Group + background Image)
            │   ├── TitleRow         (Horizontal Layout Group)
            │   │   ├── DrillNameText TMP           → drillNameText
            │   │   └── TypeBadge     Image         → typeBadge
            │   │       └── TypeText  TMP           → typeText
            │   ├── AffectedStatsText TMP           → affectedStatsText
            │   └── EffectText        TMP (wrap)    → effectText
            │
            ├── PositionalPanel       (Vertical Layout Group) — START INACTIVE
            │   │                                    → positionalPanel
            │   ├── PositionDropdown  TMP_Dropdown   → positionDropdown
            │   ├── PlayerScroll      (ScrollRect, vertical)
            │   │   └── Viewport      (Mask + Image)
            │   │       └── Content   (Vertical Layout Group + ContentSizeFitter)
            │   │                                    → playerListContainer
            │   │           · player rows spawned here from playerRowPrefab
            │   └── SelectedCountText TMP            → selectedCountText
            │
            ├── CurrentlySetText      TMP            → currentlySetText
            └── SetTrainingButton     Button         → setTrainingButton
```

---

## Prefab specs

### optionButtonPrefab (a drill button)
```
DrillButton                  Button + Image (background)   ← code sets .color (type colour / highlight)
└── Label                    TextMeshProUGUI                ← code sets drill name
```
- Add a `LayoutElement` (min height ≈ 48–56) so the vertical list spaces evenly.
- Don't add an inspector OnClick — the code wires the click by `DrillId`.

### playerRowPrefab (a selectable player row)
```
PlayerRow                    (Horizontal Layout Group + LayoutElement minHeight ≈ 40)
├── Toggle                   UI Toggle (the checkbox)       ← code reads/sets isOn, enforces max 5
│   ├── Background
│   │   └── Checkmark
│   └── (no label needed on the toggle itself)
└── Label                    TextMeshProUGUI                ← code sets "Name — POS (Strength)"
```
- The code finds the row's `Toggle` and `TextMeshProUGUI` via `GetComponentInChildren`, so exact names don't matter — just make sure **one Toggle and one TMP text** exist in the prefab.

---

## Build tips / gotchas
- **PositionalPanel must start inactive** in the editor (un-tick the GameObject). Code calls `SetActive(true/false)` based on the selected drill.
- The old **`previewText` field is gone** and **`ConfirmTraining` was renamed**. If your existing Set/Confirm button has an inspector OnClick → `ConfirmTraining`, delete that entry; the button now works purely via the `setTrainingButton` reference.
- Use **Layout Groups + ContentSizeFitter** on both scroll `Content` objects so spawned buttons/rows lay out and the scrollbar sizes correctly.
- The `TMP_Dropdown` options are filled by code (all 12 positions) — leave its options list empty.
- Colours: drill button backgrounds and the type badge are coloured by code per `TrainingType` (blue/yellow/cyan/green/magenta/orange). You don't need to colour them by hand.
- Suggested split: LeftColumn ~40% width, RightColumn ~60%.
