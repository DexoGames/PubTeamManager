# Home Screen — remaining editor work

**Status: built.** The `DayBox` and `CupTieRow` prefabs exist, the day-strip icons and `LoadingOverlay` spinner are
assigned, `AdvanceOrHomeButton` has replaced the old `CalenderManager.AdvanceDay` hook, and `DayTimelineWidget`,
`CompetitionContextWidget`, `CurrentRoundWidget` (`FixturesPanel Variant`) and the events inbox (`PlayerEvents
Variant`) are all live on the page.

Design rationale lives in [`HOME_SCREEN_PLAN.md`](HOME_SCREEN_PLAN.md). Current hierarchy:

```
Home Page                        HomePageUI
└── Elements
    ├── TopStrip
    │   └── DayTimeline          DayTimelineWidget          ✅
    └── Lower
        ├── LeftColumn
        │   └── CompetitionContext   CompetitionContextWidget   ✅
        └── OtherColumns         HorizontalLayoutGroup
            ├── StatLeaderboard  <StatWidget.prefab>            ✅
            ├── FixturesPanel    <FixturesPanel Variant>        ✅  CurrentRoundWidget
            └── PlayerEvents     <PlayerEvents Variant>         ✅  PlayerEventsUI
```

---

## The one thing left: place the Squad Status widget

`Assets/Prefabs/Widgets/SquadStatus Variant.prefab` is finished and wired, but **it is never instanced in the
scene**, so the squad read-out (fatigue / low morale, with clickable player links) never appears.

1. Drag `SquadStatus Variant.prefab` into `Home Page > Elements > Lower > OtherColumns`.
2. Confirm its `SquadStatusWidget ▸ statusText` is assigned and that the TMP carries a **`LinkHandler`** component
   (needed for the `<link=…>` player names to be clickable) with **Raycast Target** on.
3. Nothing else — it's a `UIObject`, so `HomePageUI.Show()` calls its `Setup()` automatically.

`OtherColumns` is an HLG with Control/Force-Expand on, so it will re-flow to four columns on its own. If that gets
cramped, give the four children `LayoutElement` flexible widths rather than hand-sizing them.

### Optional follow-up

`SquadStatusWidget.Setup()` currently flags fatigue and low morale. Extending it to also list unavailable players
(`team.AvailablePlayers` / `team.UnavailableStarters`, and `player.AvailabilityStatus` for the reason string) is
the natural home for the injury/suspension read-out — see `NEW_SYSTEMS_BUILD.md` §1.
