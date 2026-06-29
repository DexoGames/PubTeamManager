# Stat Leaderboard Widgets — build guide

Top-N stat lists (goals, assists, shots, saves, cards) shown like the league table — ranked rows with clickable
player/team links. One reusable widget covers all three placements you asked for.

## 0. What the code already does (no editor work)
- **Data:** every shot is now persisted (not just goals). `Match.Shot` gained a `keeper`, and each side's
  `Match.TeamStats` gained a `shots` list (all attempts). Assists are credited on goals (a weighted-random team-mate,
  ~75% of goals). So shots / saves / assists / goals / cards are all queryable. **Stats accumulate as fixtures are
  played** — a brand-new save shows zeroes until matches happen.
- **Scope = a set of fixtures.** `StatLeaderboards` ranks players or teams across whatever fixtures you give it; the
  widget resolves these from a competition (a "tournament"), so each league/cup tallies separately.
- Old archived matches keep goals but drop the full shot log (save-size), so shots/saves read 0 for past seasons.

## 1. Row prefab — `StatLeaderboardRow` (like the league-table row)
1. Create a UI prefab: a horizontal row (HorizontalLayoutGroup) with three **TextMeshPro - Text (UI)** children:
   **Rank**, **Name**, **Value**.
2. Add the **`StatLeaderboardRowUI`** component to the root and assign `Rank Text` / `Name Text` / `Value Text`.
3. On the **Name** text object add a **`LinkHandler`** component (same as the league table) so the
   `<link=…>` player/team names are clickable → opens that player/team page. (Make sure the text's Raycast Target is on.)

## 2. The widget — `StatLeaderboardWidget`
Add it to any panel. It's a `UIObject`, so whatever `UIPage` it sits under calls its `Setup()` automatically when
the page is shown — no manual refresh code. Fields:

| Field | Meaning |
|---|---|
| **Mode** | `Players` (top scorers, etc.) or `Teams` (team with most shots, etc.) |
| **Scope** | `MyMainLeague` (default), `ShownTeamCompetitions`, or `AllCurrentSeason` — which fixtures to tally |
| **Restrict To Shown Team** | *Players mode only* — rank only the club currently open on the Team page |
| **Top N** | how many rows |
| **Categories** | the stats offered: Goals, Assists, Shots, ShotsOnTarget, BigMisses, Saves, CleanSheets, GoalsConceded, OwnGoals, YellowCards, RedCards |
| **Category Dropdown** | *optional* `TMP_Dropdown` — assign it and the user can switch stat; leave empty to lock to the first category |
| **Title Text** | *optional* — shows e.g. "Top Goals" / "Team Shots" |
| **Row Prefab** | the §1 prefab |
| **Container** | the `RectTransform` rows spawn under (give it a Vertical Layout Group + Content Size Fitter, inside a Scroll View) |

## 3. Placement A — main screen (stats **by team**, switchable)
1. Under the **HomePageUI** Elements, add a panel with a header, an (optional) `TMP_Dropdown`, and a scroll list.
2. Add `StatLeaderboardWidget`: **Mode = Teams**, **Scope = MyMainLeague**, assign the **Category Dropdown** and set
   **Categories** to e.g. `Goals, Shots, Saves, YellowCards`. Set Row Prefab + Container.
3. Done — it shows the league's teams ranked by the chosen stat, and the dropdown re-ranks live.

## 4. Placement B — My Team page (stats **by player**)
1. Under the **TeamDetailsUI** Elements (the club page — works for any team you open, including your own), add a panel.
2. Add `StatLeaderboardWidget`: **Mode = Players**, tick **Restrict To Shown Team**, **Categories** e.g.
   `Goals, Assists, Shots, Saves`. Optional dropdown. Set Row Prefab + Container.
3. It ranks just that club's players. (`TeamDetailsUI.CurrentTeam` drives the filter — already wired.)

## 5. Placement C — the dedicated Stats page
1. Create a page GameObject like the other pages (a root with a child **"Elements"** RectTransform), add the
   **`StatsPageUI`** component. It's a singleton and self-wires.
2. Drop **several** `StatLeaderboardWidget`s under its Elements, each fixed to one stat (no dropdown needed), e.g.:
   - Top Scorers — Players / MyMainLeague / `Goals`
   - Most Assists — Players / MyMainLeague / `Assists`
   - Most Shots (team) — Teams / MyMainLeague / `Shots`
   - Most Saves (player) — Players / MyMainLeague / `Saves`
   - Most Cards — Players / MyMainLeague / `YellowCards` (or a dropdown over both card types)
   Each widget gets its own Row Prefab + Container (own scroll list).
3. `UIManager` already has **`ShowStats()`** (null-safe until the page exists). Add a nav **Button** whose OnClick
   calls `UIManager.Instance.ShowStats()`, the same way other nav buttons call `ShowMyTeam` / `ShowTraining` etc.

## Stat meanings & attribution
All stats work in both Teams and Players mode (pick sensible ones per widget):
- **Goals / Assists / Shots / Shots on Target** — attacking output. Players: the scorer/assister/shooter. Teams: totals.
- **Big Misses** — clear chances fluffed: a shot with xG ≥ `StatLeaderboards.BigMissXg` (default 0.5) that wasn't
  scored. Players: the wasteful shooter. Teams: totals. (Threshold is one tunable const, reusable for highlights too.)
- **Saves / Clean Sheets / Goals Conceded** — goalkeeping & defence. Players: credited to the **keeper** (the keeper
  who played is captured per match in `TeamStats.keeper`, so this is reliable even in a 0-shot game). Teams: the defending club.
- **Own Goals** — the unlucky scorer (Players) or the club that scored into its own net (Teams). Sunday-league gold.
- **Yellow / Red Cards** — discipline. Players: the offender. Teams: totals.

These read nicely together as a "how this team plays" picture: lots of Shots but few on Target = wasteful; high Clean
Sheets + low Goals Conceded = a defensive side; high Goals + high Conceded = end-to-end chaos.

## Notes
- **Links** work everywhere via the `LinkHandler` on the row's Name text — clicking a name opens that player/team,
  exactly like the league table.
- To rank a **specific cup/tournament** rather than the main league, set Scope to `AllCurrentSeason` (or extend
  `StatLeaderboardWidget.ResolveFixtures` with a competition picker — the aggregator already takes any fixture set).
- Assists are engine-generated on goals (`MatchEngine.DecideAssister`); tune the 25% "unassisted" rate or the
  `AssistWeight` there if you want more/fewer assists or different distribution.
