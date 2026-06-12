# Home Screen Overhaul — Plan

> Status: **PLAN (awaiting go-ahead)**
> Goal: rebuild the home page as a dashboard — an animated upcoming-days timeline across the
> top, a windowed league table bottom-left, and a set of informational widgets filling the rest.

---

## 1. Vision (from the sketch)

- **Top strip:** a row of day boxes, left→right = today→future. The **leftmost (today)** box is large ("HOME"), the rest are smaller squares. Each box shows an **icon** for what happens that day (match / training / pub trip / nothing).
- **On "Next Day":** the whole strip **slides left smoothly** — today's box exits, every box shifts one slot, a new day slides in on the right, and the new leftmost box grows to the large size.
- **Bottom-left:** a **league table** windowed around the player's position (sketch shows 3–7 with "My Team" at 5).
- **Remaining space:** **widgets** — current round fixtures, player events, and more (suggested below).

---

## 2. Layout regions

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  DAY TIMELINE STRIP  (today large, future days small, slides on advance)      │
│  ┌──────────┐ ┌────┐ ┌────┐ ┌────┐ ┌────┐ ┌────┐ ┌────┐ ┌────┐                  │
│  │  TODAY   │ │Wed │ │Thu │ │Fri │ │Sat │ │Sun │ │Mon │ │Tue │  …              │
│  │  (crest) │ │ zZ │ │ zZ │ │cone│ │game│ │pint│ │    │ │    │                  │
│  └──────────┘ └────┘ └────┘ └────┘ └────┘ └────┘ └────┘ └────┘                  │
├──────────────────────────┬────────────────────────────────────────────────────┤
│  LEAGUE TABLE (windowed)  │  WIDGET GRID                                         │
│  3  Team Wee   …          │  ┌───────────────┐ ┌───────────────┐                 │
│  4  Team Poo   …          │  │ Next Match    │ │ This Round     │                 │
│  5  MY TEAM ◄  …          │  │ preview       │ │ fixtures       │                 │
│  6  Evil Team  …          │  └───────────────┘ └───────────────┘                 │
│  7  Tottenham  …          │  ┌───────────────┐ ┌───────────────┐                 │
│  [View full table]        │  │ Inbox/Events  │ │ Squad status   │                 │
│                           │  └───────────────┘ └───────────────┘                 │
└──────────────────────────┴────────────────────────────────────────────────────┘
```

Implementation: the page root has the standard `Elements` child (UIPage convention). Under it, three regions via a **Vertical Layout Group** (top strip fixed height) then a **Horizontal split** (left table column + right widget area). The widget area is a **Grid Layout Group** (or a flexible layout) of widget cards.

---

## 3. Component architecture (coordinator + widgets)

Rather than one giant `HomePageUI`, split into a **coordinator** + independent **widget components**, each with a `Refresh()` method. This keeps each piece testable and lets us add/remove widgets freely.

```
HomePageUI (coordinator : UIPage)
  • OnShow()      → Refresh all widgets, snap timeline to current state
  • OnHide()      → (nothing / unsubscribe handled in OnEnable/OnDisable)
  • subscribes to CalenderManager.NewDay → animate timeline + refresh widgets
  └ holds references to the widget components below

Widgets (each a MonoBehaviour on its own panel, with Refresh()):
  • DayTimelineWidget     — the animated day strip (the headline feature)
  • CompetitionContextWidget — adaptive bottom-left panel: a windowed league table if the
                               next fixture is a league game, or the current knockout round
                               (bracket state) if the next fixture is a cup tie
  • CurrentRoundWidget    — this gameweek's fixtures (reuses FixtureUI) — today's home logic
  • InboxWidget           — player events (reuses EventsManager + existing event row prefab)
  • NextMatchWidget       — opponent preview (new)
  • SquadStatusWidget     — injuries/fatigue/morale/suspensions (new)
  • …more (see §8)
```

`HomePageUI.OnShow()` calls `Refresh()` on every assigned widget (null-guarded, so unbuilt widgets are simply skipped — incremental build-out).

---

## 4. Day Timeline strip (headline feature)

### 4.1 Data
- Source: `ScheduleManager.GetUpcoming(N)` (already returns `List<ScheduleEntry>` from today, where `[0]` = today). Use **N = 8–9** visible days.
- Each `ScheduleEntry` has `Type` (`Match` / `Training` / `Interview` / `RestDay`) and, for matches, `MatchFixture`.

### 4.2 Visuals per box
- **Day label** (`Mon`, `Tue`, … via `CalenderManager.ShortDateWordsNoYear` or `Date.DayOfWeek`), plus the date for the lead box.
- **Icon** in the centre, by day type (see mapping).
- **Lead box (slot 0)** is larger, labelled "HOME"/shows the club crest, and may show extra detail (e.g. today's match opponent).

### 4.3 Icon mapping (and the "pub trip" gap)
| Day type | Icon idea |
|---|---|
| Match | club crest / ball |
| Training | cone / whistle |
| Interview | microphone |
| **Pub trip** | pint glass |
| Rest / nothing | "zZ" / empty |

⚠️ **`ScheduleEntryType` currently has no "Pub trip".** Two options:
- **(Recommended)** add `ScheduleEntryType.PubTrip` and a generation rule in `ScheduleManager.GenerateSchedule` (e.g. an occasional social day), plus an icon. Saves/serialization unaffected (schedule is regenerated on load).
- Or reuse `Interview` as the "pub/social" day for now.
I'll wire a `DayIcon` lookup (`Sprite` per `ScheduleEntryType`) exposed as serialized fields so you assign the art in-editor.

### 4.4 The slide animation (DOTween)
Use a **fixed pool of box objects** positioned **absolutely** (not via a LayoutGroup, so we can tween), with **precomputed slot rects**:
- Slot 0 = large rect (left). Slots 1…N = smaller square rects marching right.
- Each box stores its data + current slot.

On `CalenderManager.NewDay`:
1. Rebind data: box that was in slot `i` now represents slot `i-1`'s day. The slot-0 box "expires".
2. **Tween** every box's `anchoredPosition` + `sizeDelta` from its current rect to its new slot's rect (≈0.35 s, `Ease.OutCubic`). Slot 1's box grows into the large slot 0; the old slot-0 box shrinks and slides off-screen left (fade out).
3. **Recycle** the expired box to the right end (slot N), populate it with the new far-future day, and tween it in from off-screen right.
4. On complete, refresh icons/labels so nothing pops mid-flight.

Guards:
- Only animate if the home page **is visible** when the day advances; otherwise just **snap** to the new layout in `OnShow()` (no animation).
- Re-entrancy: if the player spams "Next Day", `DOKill` in-flight tweens and snap-then-restart, or queue.

New helper methods:
- `DayTimelineWidget.SnapToToday()` (build/lay out from scratch).
- `DayTimelineWidget.AdvanceAnimated()` (the tween described).
- Slot-rect calculation from the container width + a `[SerializeField] leadBoxWidth/smallBoxWidth/spacing`.

---

## 5. Competition context widget (bottom-left) — table OR bracket

The bottom-left panel adapts to **the competition of the player's next fixture**:

```
upcomingFixture = MyTeam.GetUpcomingFixture();
comp = upcomingFixture.Competition;
if   (comp is League) → render windowed standings   (LeagueView)
else if (comp is Cup) → render current knockout round (BracketView)
```

### 5a. League view (next fixture is a league game)
- Reuse `League.GetStandings()`, `LeagueTableTeamUI.SetLeagueStandingText(entry, standing)`, and the existing `LeagueTable` pattern.
- Show a **window of K rows centred on the player's position** (K = 5, like the sketch). Helper: find My Team's index in standings, clamp a `[start, start+K)` window (handle top/bottom edges), render with real positions.
- **Highlight** the My Team row; header = the league name.
- Optional **"View full table"** → existing standings page.

### 5b. Bracket view (next fixture is a cup tie)
- Determine the current round: `roundIndex = upcomingFixture.Round` (equivalently `cup.GetUpcomingRound()`); the ties are `cup.Rounds[roundIndex]`.
- Render each tie as a row: `Home  [score]  Away`, with the **winner emphasised** if `BeenPlayed`, and **My Team's tie highlighted**. Unplayed ties show "vs".
- **Round label** derived from the number of ties in the round:
  `1 → Final, 2 → Semi-Final, 4 → Quarter-Final, 8 → Round of 16, 16 → Round of 32, else "Round n"`. (Small `CupRoundName(int ties)` helper.)
- Header = `"{cup.Name} — {roundName}"`.
- Caveat: bye teams (`Cup.autoSecondRoundTeams`) are `[JsonIgnore]` (not persisted), so we can only show the *ties* in the round, not a full bracket tree — which matches "state of the current round". A full bracket tree would need persisting byes (future).

### Shared
- A single `CompetitionContextWidget` with two child sub-panels (`leagueView`, `bracketView`); show one, hide the other based on the next fixture's competition type. Reuses `LeagueTableTeamUI` for league rows and a small **cup-tie row prefab** for bracket rows.
- **Fallback:** if there is no upcoming fixture (e.g. between seasons), show the player's main league final table.
- Refresh on `OnShow` and on day-advance (standings/round state change after match days).

---

## 6. Current-round fixtures widget

- This is essentially today's `HomePageUI.OnShow` logic, moved into `CurrentRoundWidget`:
  - Find the focused competition + round for My Team (the existing "latest round" logic that keeps showing the just-played round for a day).
  - List that round's fixtures via `FixtureUI`.
  - Header = `"{competition.Name}, Round {n}"`.
- Keep the existing "stick to the last round for a day after it ends" behaviour.

---

## 7. Player events / inbox widget

- Source: `EventsManager.Instance.Events` (the player-facing events; `PlayerEventsUI` already filters these).
- Render a compact list of clickable event rows (each opens the discussion/response page via the existing `ShowDiscussion` flow + `LinkBuilder`).
- Show an **empty state** ("No new messages") when none.
- Refresh on day-advance (events are generated on match/processing days).

---

## 8. Suggested additional widgets

Pick any; each is an independent panel with `Refresh()`:

1. **Next Match preview** — opponent crest/name, home/away, competition, date, recent form (W/D/L), and a quick strength comparison vs your XI. High value — it's the thing a manager checks most.
2. **Squad status** — counts/links for injured, suspended, low-morale, and fatigued players; flags anyone unavailable for the next match. Pulls from player `Fatigue`/`Morale`/(future) injuries.
3. **Form & momentum** — your last 5 results as W/D/L pips + a tiny league-position trend.
4. **Top performers** — best player(s) by recent rating / goals, surfacing your in-form players (ties into the match `Shot`/scorer data we now persist).
5. **Training summary** — current drill (from `TrainingManager.CurrentSession`), next session date (`ScheduleManager.GetNextTrainingDay`), and squad-average Boost — a quick link to the training page.
6. **Objectives / season goals** — board expectation vs current league position (needs a small "objectives" data model; future).
7. **News ticker / results** — other notable results from your division this round (we have all fixtures), e.g. "Title rivals dropped points."
8. **Finances / club snapshot** — if/when finances exist; placeholder for later.

My recommended first set: **Next Match preview + Squad status** alongside the existing **Current-round fixtures + Inbox**. They give the most "dashboard" value for the least new data.

> **DECIDED — first widget set:** **Current-round fixtures** + **Player events inbox** (both ported from today's home page into their own widget components) + **Squad status** (new). Next Match preview is deferred for later.

---

## 9. New / changed code summary

**New scripts**
- `HomePageUI` (rework into coordinator).
- `DayTimelineWidget` (+ a `DayBox` row component, + `DayIcon` sprite lookup).
- `CompetitionContextWidget` (league window OR cup bracket, switched by the next fixture's competition; + a cup-tie row prefab + `CupRoundName(ties)` helper). Reuses `LeagueTableTeamUI` for league rows.
- `CurrentRoundWidget`, `InboxWidget`, `SquadStatusWidget` (the chosen first set).

**Changed**
- `ScheduleEntry.cs` — add `ScheduleEntryType.PubTrip` (if we go with option A).
- `ScheduleManager.cs` — generate occasional pub-trip days; maybe a `GetUpcomingIncludingToday(n)` convenience (today already included by `GetUpcoming`).
- `League.cs` (or the widget) — windowed-standings helper + "My Team index".
- `CalenderManager` — no change (already exposes `NewDay` + `CurrentDay`).

**Reused as-is:** `FixtureUI`, `LeagueTableTeamUI`, `LeagueTableEntry`, `EventsManager`, DOTween.

---

## 10. Editor build plan (me vs you)

**I implement:** all widget scripts + the coordinator + the timeline animation logic, exposing `[SerializeField]` slots.

**You build in-editor (nicer by hand):**
- The overall layout (top strip region, left table column, right widget grid) under `Elements`.
- The **DayBox prefab** (background image, day label TMP, date TMP, icon Image) — one prefab used for both large and small (script drives size).
- Widget **card frames** (backgrounds/headers) and the small row prefabs where not already existing.
- Assign the **day-type icon sprites** to the `DayIcon` lookup.
- (If adding pub trips) the pint icon art.

I'll provide an exact `SerializeField` checklist per widget, like the training page guide.

---

## 11. Execution stages

1. **Scaffold** `HomePageUI` coordinator + region layout; move current fixtures logic into `CurrentRoundWidget` (no behaviour change). 
2. **DayTimelineWidget** static layout (no animation) from `ScheduleManager.GetUpcoming`, with icon lookup. (+ `PubTrip` type if chosen.)
3. **Timeline slide animation** via DOTween on `NewDay`; snap-on-show when hidden.
4. **LeagueTableWidget** windowed standings + My Team highlight.
5. **InboxWidget** (events) — port from `PlayerEventsUI`.
6. **NextMatchWidget + SquadStatusWidget** (and any others you pick).
7. Polish: empty states, click-throughs, re-entrancy guards, refresh-on-advance wiring.

---

## 12. Decisions (locked)
1. **Pub trip:** ✅ add `ScheduleEntryType.PubTrip` + a generation rule in `ScheduleManager` + a pint icon.
2. **Visible days:** ✅ **8** (today + 7).
3. **League window:** ✅ **5 rows** centred on My Team.
4. **First widget set:** ✅ Current-round fixtures + Player events inbox (ported) + Squad status (new).
5. **Animation:** ~0.35 s `OutCubic` slide (tweak later if needed).

---

## 13. "Next Day" → "Home" button toggle

Requirement: the **Next Day** button only advances the day **while on the home page**. On any other page it instead becomes a **Home** button that returns to the home page (you must be home to advance).

Design:
- A small `AdvanceOrHomeButton` controller on the top-nav button.
- It tracks whether the home page is active. UIManager has no current-page field today, so add `UIManager.IsHomeActive` (set true in `ShowHomePage`, false in the other `Show*` methods / `HideAllUI`), or have the button check `HomePageUI.Instance` visibility.
- **On click:** if home is active → advance the day (the existing Next-Day/advance call); else → `UIManager.ShowHomePage()`.
- **Label/icon:** update on every page change — show "Next Day" when home is active, "Home" otherwise. Easiest: a `UIManager.OnPageChanged` event (or refresh the button in `ShowHomePage`/`HideAllUI`).
- Wiring note: I'll need to locate the current Next-Day button + its advance call during execution and route it through this controller.

This also pairs nicely with the day-timeline animation: advancing always happens *on* the home screen, so the slide is always visible.

---

## 14. Execution order (final)
1. Scaffold `HomePageUI` coordinator + region layout; port fixtures + inbox into `CurrentRoundWidget` and `InboxWidget`.
2. `ScheduleEntryType.PubTrip` + schedule generation + `DayIcon` lookup.
3. `DayTimelineWidget` static layout (8 days, icons).
4. Timeline DOTween slide on `NewDay` (snap when hidden).
5. `CompetitionContextWidget` — league view (5-row window + My Team highlight) **and** cup bracket view (current round ties), switched by the next fixture's competition.
6. `SquadStatusWidget`.
7. `AdvanceOrHomeButton` toggle (§13).
8. Polish: empty states, click-throughs, re-entrancy guards, refresh-on-advance.
