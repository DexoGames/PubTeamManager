# Home Screen — Editor Build Guide

The scripts are done, but they do nothing until they're on GameObjects with their fields wired.
Build this in the scene. You can do it **incrementally** — every widget is independent and
null-guarded, so add one, press Play, repeat.

> Reminder: `UIPage.Elements` is `transform.GetChild(0)`. Everything visible on the home page
> must live under that **first child** of the page root (call it `Elements`). The page root keeps
> the `HomePageUI` component.

---

## Target hierarchy

```
HomePage                         (HomePageUI component)   ← already exists
└── Elements                     (RectTransform, child index 0; stretched full)
    ├── TopStrip                 (RectTransform — the day timeline area; NO layout group)
    │   └── DayTimeline          (DayTimelineWidget; its `container` = this same RectTransform)
    │         · DayBox instances spawn here at runtime
    │
    └── Lower                    (Horizontal Layout Group — left column + right area)
        ├── LeftColumn
        │   └── CompetitionContext   (CompetitionContextWidget)
        │       ├── LeagueView        (GameObject)
        │       │   ├── LeagueHeader  (TMP)
        │       │   └── LeagueRows    (Vertical Layout Group)   ← leagueRowContainer
        │       └── BracketView       (GameObject)
        │           ├── BracketHeader (TMP)
        │           └── BracketRows   (Vertical Layout Group)   ← bracketRowContainer
        │
        └── RightArea            (Grid or Vertical Layout Group)
            ├── CurrentRound     (CurrentRoundWidget)
            │   ├── CompetitionName (TMP)
            │   └── FixtureList     (Vertical Layout Group)     ← fixtureContainer
            ├── Inbox            (PlayerEventsUI, allPlayers = true)
            │   └── EventContainer                              ← container
            └── SquadStatus      (SquadStatusWidget)
                └── StatusText   (TMP + **LinkHandler** component)  ← statusText
```

`LeagueView` and `BracketView` are switched on/off by code at runtime — initial state doesn't matter.

---

## Step 1 — Make the **DayBox** prefab

1. `Elements` ▸ right-click ▸ UI ▸ **Image** → name it `DayBox`. (This is the box background.)
2. Add a **Canvas Group** component to it.
3. Add the **`DayBox`** script.
4. Children:
   - **DayLabel** — UI ▸ Text - TextMeshPro (e.g. "Tue").
   - **Icon** — UI ▸ Image (the activity icon).
   - **LeadExtras** — an empty GameObject holding whatever should show **only on today's box** (e.g. the club crest + a "HOME" label). Put the crest/label inside it.
5. Wire the `DayBox` component:
   | Field | Assign |
   |---|---|
   | `rect` | the DayBox's own RectTransform |
   | `canvasGroup` | the Canvas Group |
   | `dayLabel` | DayLabel (TMP) |
   | `icon` | Icon (Image) |
   | `leadExtras` | LeadExtras GameObject |
6. Drag `DayBox` into your **Prefabs** folder, then **delete it from the scene** (it's spawned at runtime).
   *(Don't put it in a layout group — the widget positions/sizes it itself.)*

## Step 2 — Make the **CupTieRow** prefab

1. Create a UI row (an Image with a Horizontal Layout Group) named `CupTieRow`.
2. Children: **HomeText** (TMP), **ScoreText** (TMP), **AwayText** (TMP).
3. Add the **`CupTieRowUI`** script and wire `homeText`, `scoreText`, `awayText`, and `background` (the row's Image, optional).
4. Save as a prefab; delete from scene.

---

## Step 3 — Top strip (`DayTimelineWidget`)

1. Under `Elements`, create an empty UI object `TopStrip` (a RectTransform spanning the top band). **No layout group on it.**
2. Add the **`DayTimelineWidget`** script to it.
3. Wire it:
   | Field | Value |
   |---|---|
   | `container` | the `TopStrip` RectTransform (its own) |
   | `boxPrefab` | the **DayBox** prefab |
   | `visibleDays` | 8 |
   | `leadWidth / smallWidth / spacing` | tune so 8 boxes fit (e.g. 220 / 110 / 12) |
   | `leadHeight / smallHeight` | the lead box is taller, e.g. 200 / 130 (boxes are top-aligned; the lead hangs lower) |
   | `slideDuration` | 0.35 |
   | `matchIcon / trainingIcon / interviewIcon / pubTripIcon / restIcon` | your 5 day-type sprites |

## Step 4 — Bottom-left (`CompetitionContextWidget`)

1. Build `LeftColumn ▸ CompetitionContext` with the two sub-views from the hierarchy above.
2. `LeagueRows` and `BracketRows` each need a **Vertical Layout Group** (+ ContentSizeFitter if scrolling).
3. Add **`CompetitionContextWidget`** and wire:
   | Field | Assign |
   |---|---|
   | `leagueView` | LeagueView GameObject |
   | `leagueRowContainer` | LeagueRows transform |
   | `leagueRowPrefab` | the existing **LeagueTableTeamUI** prefab |
   | `leagueHeader` | LeagueHeader (TMP) |
   | `windowSize` | 5 |
   | `bracketView` | BracketView GameObject |
   | `bracketRowContainer` | BracketRows transform |
   | `tieRowPrefab` | the **CupTieRow** prefab |
   | `bracketHeader` | BracketHeader (TMP) |

## Step 5 — Current-round fixtures (`CurrentRoundWidget`)

1. Build `RightArea ▸ CurrentRound` with a `CompetitionName` (TMP) and a `FixtureList` (Vertical Layout Group).
2. Add **`CurrentRoundWidget`** and wire `fixtureContainer` = FixtureList, `fixturePrefab` = existing **FixtureUI** prefab, `competitionName` = the TMP.

## Step 6 — Events inbox (reuses existing widget)

1. Add an `Inbox` object with the existing **`PlayerEventsUI`** component, `allPlayers = true`, and wire its `container` + `playerEventPrefab` (same as wherever it's used today).

## Step 7 — Squad status (`SquadStatusWidget`)

1. Add `SquadStatus` with a single **StatusText** (TMP).
2. **Add a `LinkHandler` component to that StatusText** (so the player name links are clickable).
3. Add **`SquadStatusWidget`** and wire `statusText` = StatusText. (Thresholds are tunable.)

## Step 8 — Next Day / Home button

On your top-nav **Next Day** button:
1. **Remove** the existing inspector `OnClick → CalenderManager.AdvanceDay` entry.
2. Add the **`AdvanceOrHomeButton`** component; wire `button` = the Button, `label` = its TMP text.
   It will advance the day when you're on the home page, and read/act as "Home" otherwise.

## Step 9 — Hook the timeline to the page

Select the `HomePage` root and assign **`HomePageUI ▸ dayTimeline`** = the `DayTimeline` object from Step 3.

---

## Test

Press Play and load/start a game:
- [ ] Top strip shows 8 day boxes, today large with its icon/crest, future days small with icons.
- [ ] Bottom-left shows your league table windowed around your team (or the cup round if your next game is a cup tie).
- [ ] Right area shows this round's fixtures, the events inbox, and squad status.
- [ ] Click **Next Day** → strip slides left smoothly; widgets refresh.
- [ ] Navigate to another page → the button now reads **Home**; clicking it returns home (doesn't advance).

If a panel is blank, its component just isn't wired yet — the rest still work. Build/verify one widget at a time.
