# Stat Leaderboard Widgets

Top-N stat lists (goals, assists, shots, saves, cards) shown like the league table — ranked rows with clickable
player/team links.

**Status:** the row prefab (`Rows/StatLeaderboardRow.prefab`), the widget prefab (`WIdgets/StatWidget.prefab`) and
two of the three placements are **done** — a Teams-mode board on the **Home page** and a Players-mode board on the
**Team Details page**. Only the dedicated Stats page is left.

---

## Remaining work — the dedicated Stats page

`StatsPageUI` is written and self-wiring, but **the page does not exist in the scene**, so `UIManager.ShowStats()`
silently no-ops (it's null-guarded) and there is no nav button to call it anyway.

1. Create a page GameObject under `Canvas > Pages` — a root carrying **`StatsPageUI`**, with a single child named
   **`Elements`** (child index 0; `UIPage.Elements` is `transform.GetChild(0)`). Leave the root active and
   `Elements` inactive, like every other page.
2. Drop **several** `StatWidget.prefab` instances under `Elements`, each fixed to one stat (no dropdown needed).
   Suggested set:

   | Widget | Mode | Scope | Category |
   |---|---|---|---|
   | Top Scorers | Players | MyMainLeague | `Goals` |
   | Most Assists | Players | MyMainLeague | `Assists` |
   | Most Shots (team) | Teams | MyMainLeague | `Shots` |
   | Most Saves | Players | MyMainLeague | `Saves` |
   | Most Cards | Players | MyMainLeague | `YellowCards` |

   Each instance needs its own **Container** (its own scroll list) — they can't share one.
3. Add a **Stats** nav Button to `Canvas > Constants`, OnClick → drag the `UIManager` object →
   `UIManager.ShowStats`. Add it to `UIManager ▸ hideForMatchSim` alongside the other nav buttons.

> The page must exist **at startup** — `UIManager.Setup()` caches every page's `Instance` once, so a page added at
> runtime is invisible to the manager.

---

## Widget reference (for any new placement)

`StatLeaderboardWidget` is a `UIObject`, so whichever `UIPage` it sits under calls its `Setup()` when that page is
shown — no refresh code.

| Field | Meaning |
|---|---|
| **Mode** | `Players` (top scorers, etc.) or `Teams` (team with most shots, etc.) |
| **Scope** | `MyMainLeague` (default), `ShownTeamCompetitions`, or `AllCurrentSeason` — which fixtures to tally |
| **Restrict To Shown Team** | *Players mode only* — rank only the club currently open on the Team page |
| **Top N** | how many rows |
| **Categories** | Goals, Assists, Shots, ShotsOnTarget, BigMisses, Saves, CleanSheets, GoalsConceded, OwnGoals, YellowCards, RedCards |
| **Category Dropdown** | *optional* `TMP_Dropdown` — assign it and the user can switch stat; leave empty to lock to the first category |
| **Title Text** | *optional* — shows e.g. "Top Goals" / "Team Shots" |
| **Row Prefab** | `Rows/StatLeaderboardRow.prefab` |
| **Container** | the `RectTransform` rows spawn under (Vertical Layout Group + Content Size Fitter, inside a Scroll View) |

The row's **Name** text carries a `LinkHandler`, so clicking a name opens that player/team — same as the league table.

## Stat meanings & attribution

All stats work in both Teams and Players mode (pick sensible ones per widget):

- **Goals / Assists / Shots / Shots on Target** — attacking output. Players: the scorer/assister/shooter. Teams: totals.
- **Big Misses** — clear chances fluffed: a shot with xG ≥ `StatLeaderboards.BigMissXg` (default 0.5) that wasn't
  scored. Players: the wasteful shooter. Teams: totals.
- **Saves / Clean Sheets / Goals Conceded** — goalkeeping & defence. Players: credited to the **keeper** (captured per
  match in `TeamStats.keeper`, so it's reliable even in a 0-shot game). Teams: the defending club.
- **Own Goals** — the unlucky scorer (Players) or the club that scored into its own net (Teams). Sunday-league gold.
- **Yellow / Red Cards** — discipline. Players: the offender. Teams: totals.

Read together they sketch how a team plays: many Shots but few on Target = wasteful; high Clean Sheets + low Goals
Conceded = a defensive side; high Goals + high Conceded = end-to-end chaos.

## Notes

- **Stats accumulate as fixtures are played** — a brand-new save shows zeroes until matches happen.
- Archived matches keep goals but drop the full shot log (save size), so shots/saves read 0 for past seasons.
- To rank a **specific cup** rather than the main league, set Scope to `AllCurrentSeason`, or extend
  `StatLeaderboardWidget.ResolveFixtures` with a competition picker — the aggregator takes any fixture set.
- Assists are engine-generated on goals (`MatchEngine.DecideAssister`); tune the 25% "unassisted" rate or
  `AssistWeight` there.
