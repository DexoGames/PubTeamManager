# Completeness Audit — code vs. scene vs. docs

**What's described somewhere but not actually finished.** Cross-checks three sources against each other:

1. **Code** — every script under `Assets/_Scripts`.
2. **Scene & prefabs** — `Menu.unity`, every prefab: which components are placed, which serialized fields are
   assigned, and every Inspector `OnClick` hook (including prefab-instance overrides).
3. **Docs** — all 20 markdown files in `Docs/`, specifically their wiring instructions and status claims.

UI *layout* problems are out of scope here — those are in
[`UI_AUDIT_AND_LAYOUT_PLAN.md`](UI_AUDIT_AND_LAYOUT_PLAN.md). This doc is about **things that don't run at all**.

---

## 1. Documented wiring that was never done

Every Inspector hook the docs ask for, against every hook that actually exists in the project.

| Hook the docs specify | Doc | Status |
|---|---|---|
| `TeamTalkUI.Open` | `MATCH_SIM…BUILD` | ✅ hooked (half-time **Team Talk** button) |
| `MatchSimPageUI.Advance` / `.Pause` / `.OpenHalfTimeTactics` | `MATCH_SIM…BUILD` | ✅ hooked |
| `MatchSimPageUI.SetMentalitySliderInMatch` | `WIRING…` §2b | ✅ hooked (slider) |
| `RecruitmentPageUI.OnInterview/Hire/Reject/Skip` | `WIRING…` §1c | ✅ hooked |
| `InterviewQuestionButton.Ask` | `WIRING…` §1d | ✅ hooked (on the prefab) |
| `UIManager.ShowMyTeam / ShowTactics / ShowTraining / ShowRecruitment / ShowSchedule / ShowFixtureList / Exit` | various | ✅ hooked |
| `AdvanceOrHomeButton` replacing `CalenderManager.AdvanceDay` | `HOME_SCREEN_BUILD` Step 8 | ✅ done |
| **`TeamTalkUI.Close`** | `MATCH_SIM…BUILD` | ❌ **never hooked — soft-locks the match** |
| **`TacticsPageUI.ResumeMatch`** | `NEW_SYSTEMS_BUILD:188`, `WIRING…` §3b | ❌ **never hooked — strands you on the tactics page** |
| **`TeamTalkUI.CancelIndividual`** | `HALFTIME…REWORK_BUILD` §3b | ❌ no Cancel button exists |
| **`CompetitionPageUI.ChangeWeekFocus`** | — | ❌ **hook targets the deleted class `FixtureListUI`** |
| **`UIManager.ShowStats`** | `STATS_WIDGETS_BUILD` §5.3 | ❌ no Stats nav button exists |
| **`GameManager.NewGame`** | `GameManager.cs:30` ("Hook this up to a New Game UI button") | ❌ no such button — a fresh start needs the save file deleted by hand |
| `MatchSimPageUI.MoreAttacking` / `.MoreDefensive` | `WIRING…` §2b, `NEW_SYSTEMS_BUILD:174` | ⚪ not hooked, but the mentality **slider** covers the same ground — the two methods are now redundant entry points |

> **Root cause worth noting:** Unity matches Inspector hooks by **class-name string**. Renaming `FixtureListUI` →
> `CompetitionPageUI` silently unhooked both week-arrow buttons. Re-check Inspector hooks after any UI class rename.

---

## 2. Components that exist in code but are placed nowhere

Scanned every project MonoBehaviour against `Menu.unity` and all prefabs.

| Component | What it does | Consequence |
|---|---|---|
| **`StatsPageUI`** | The dedicated stats page (`STATS_WIDGETS_BUILD` §5) | The page doesn't exist. `UIManager.ShowStats()` is null-guarded, so it silently no-ops — and there's no nav button to call it anyway. The `StatLeaderboardWidget` itself **is** live on the Home page. |
| **`BackButton`** | Auto-wiring back button over `UIManager`'s 50-deep nav history | The whole `PushNav`/`Back`/`CanGoBack` system in `UIManager` is unreachable. `PROJECT_OVERVIEW` §15 lists it as delivered "this pass". |
| **`TacticOptionToggle`** | Abstract-ish base for tactic toggles | Unused — `TacticsToggle` (on `Base UI/Toggle.prefab`) is the one in service. |
| **`DraggableUI`** | Drag helper | Unused — `DraggableWithLinkUI` is the one in service. |
| **`DOTweenLayoutAnimator`** | Layout tween helper | Never placed. |

And one **prefab** that's built but never instanced:

| Prefab | Doc | Consequence |
|---|---|---|
| **`WIdgets/SquadStatus Variant.prefab`** (`SquadStatusWidget`) | `HOME_SCREEN_BUILD` Step 7 | The home page's squad-status read-out (injuries/suspensions/morale summary, with clickable player links) is built and never shown. Home currently has `CompetitionContext`, `StatLeaderboard`, `FixturesPanel`, `PlayerEvents` — no `SquadStatus`. |

---

## 3. Features described in the docs that were never built

| Feature | Doc | State |
|---|---|---|
| **Past-seasons / history browser** | `PROJECT_OVERVIEW` §12, §15 | `SaveManager.LoadSeasonForViewing(year)` + `FixturesManager.RegisterViewedSeason`/`UnloadViewedSeason` all work and are called by nothing. No UI. |
| **Release-before-hire panel** | `WIRING…` §1e, `PROJECT_OVERVIEW` §15 | `RecruitmentManager.HirePlayerReplacing(candidate, toRelease)` exists and is called by nothing. Hitting the 25-man cap currently just prints "release someone" text with no way to do it from the page. |
| **Chemistry display** | `PROJECT_OVERVIEW` §5 | `Player.GetChemistryWith(other)` + `Chemistry.Label(level)` are ready; nothing renders them. Chemistry is fully live in the match sim but completely invisible to the player. |
| **Contextual events system** | `MATCH_SIM_AND_CONTEXTUAL_EVENTS_PLAN` (817 lines) | Design only, explicitly. State-linked opportunities, event threads/memory/cooldowns, promises & follow-ups, squad propagation. Nothing implemented. |
| **Instruction synergies** | `NEW_SYSTEMS_BUILD` | `complementaryInstructions` is fully implemented (perks applied at step 1b of `RecalculateStats`) but **only `FocusWideOn` authors one**, so 18 of 19 instructions have no synergy. Content gap, not a code gap. |
| **Reliance content** | `NEW_SYSTEMS_BUILD` | Only 3 of 19 instructions are reliances (`BuildupAround`, `FocusWideOn`, `FreeRole`). The system — picker filtering, phase scoping, familiarity penalty, slot binding — is complete and thinly used. |
| **AI-team injuries & suspensions** | `PROJECT_OVERVIEW` §15 | AI fouls are recorded for display but never gate AI availability. Player-team only. |
| **Penalty / free-kick / corner shot types** | `PROJECT_OVERVIEW` §15 | Only partially wired in the engine. |
| **Fatigue driven by play** | `PROJECT_OVERVIEW` §15 | `Fatigue` exists but isn't meaningfully driven by match events. |
| **New Game / title screen** | — | One scene, no menu. `Menu.unity` boots straight into the save (or a fresh game if none). `GameManager.NewGame()` is the intended entry point and is unreachable. |

---

## 4. Dead code — safe to delete

| Item | Evidence |
|---|---|
| **`TacticStats.cs`** (130 lines) + **`TacticInstruction.Apply(TacticStats)`** | `PROJECT_OVERVIEW` §15 called this "safe to delete once confirmed unreferenced". **Now confirmed:** `TacticStats` has exactly one reference — `TacticInstruction.Apply` — and `.Apply(` has **zero** callers anywhere in the project. Both are dead; the live path is `Tactic`. |
| **`TeamTalkMinigame` / `GerrymanderGame` / `BalanceGame`** + `TeamTalkController.CreateRandom` / `ApplyResult` / `Summarise` | The abandoned minigame talk. Documented as dormant in three places. Deleting them would also let you drop `maxMoodSwing`, `maxPassionSwing` and `flopThreshold` from the `TeamTalkController` Inspector, where they currently read as live tuning knobs for the talk that's actually shipping. |
| **~192 commented-out lines in `Fixture.cs`** | Legacy sim, noted as historical in `PROJECT_OVERVIEW` §15. |
| **`Assets/_Recovery/` — 3.7 MB, four stale copies of `Menu.unity`, tracked in git** | They contain live script GUIDs and real OnClick hooks, so they read as genuine scenes to any tooling (and to anyone who double-clicks one). `0 (3).unity` is currently untracked in the working tree. |
| **6 leftover `Stat` GameObjects** under `StatContainer` in the scene | Design-time placeholders with an empty `colors[]` array. Harmless at runtime — `PlayerDetailsUI`/`TeamDetailsUI` both call `Game.ClearContainer` before spawning — but if one ever *did* render, `StatUI.SetText` would throw (`colors[Clamp(v/20, 0, -1)]`). Delete them. |

---

## 5. Doc drift — statements that are no longer true

| Doc | Claim | Reality |
|---|---|---|
| `TRAINING_SYSTEM_PLAN.md` | "Status: **Plan (not yet executed)**" | Was fully implemented. **Deleted**; the positional-progression thresholds it documented moved to `PROJECT_OVERVIEW` §8. |
| `TRAINING_UI_LAYOUT.md` | Specified `playerListContainer` + `playerRowPrefab` (Toggle rows) | Those fields no longer existed — the code uses `playerSlots[]` + `PlayerPickerPopup`. **Deleted**; `UI_AUDIT_AND_LAYOUT_PLAN` §6 is now the training page reference. |
| `PROJECT_OVERVIEW.md:640` | Day-type icons + `LoadingOverlay` spinner "not yet authored / wired in-scene" | **Done.** All five icons are assigned on `DayTimelineWidget`; `LoadingOverlay.root` + `spinner` are assigned. |
| `PROJECT_OVERVIEW.md` §15 | "a `BackButton` + `UIManager` navigation history" delivered this pass | The code shipped; the button was never placed (§2). |
| `PROJECT_OVERVIEW.md` §15 | Interview remaining work: "build the question buttons" | 16 buttons **are** built — but three question types have no button at all (see `UI_AUDIT` §5.1). "Release-before-hire panel" is still correct. |
| `MATCH_SIM_AND_TEAM_TALK_BUILD.md` | "Left to do" list mixed done and outstanding items | **Rewritten** — it now leads with the three genuinely remaining fixes and keeps the rest as reference. |
| `HALFTIME_TEAM_TALK_REWORK_BUILD.md` | Described the individual-talk wiring as if it were done | **Rewritten** as the four outstanding problems, starting with the shared `ResponseManager`. |
| `CHEMISTRY_AND_AI_TACTICS_BUILD.md` | "EDITOR STEP — verify import" of `KeepItSimple` / `HoldPositions` / `CutOutRisks` | All three `.asset` files are present and healthy. **Deleted**; its tuning consts and the "considered but not exempted" list moved to `PROJECT_OVERVIEW` §5. |

---

## 6. Genuinely unassigned serialized fields

Scanned every MonoBehaviour in the scene and all prefabs for `{fileID: 0}` references. Most hits are populated at
runtime (`PersonManager.People`, `FormationUI.currentTeam`) or overridden per instance
(`FormationDisplayModeControl.formationUI` — assigned on both scene instances). The ones that matter:

| Field | Where | Effect |
|---|---|---|
| **`PlayerMoraleBoxUI.bubbleBackground`** | `Matchca.prefab` | The team-talk speech bubbles render as bare text with no backing graphic, and `ShowBubble(line, tint)`'s reaction tint has nothing to tint. The chatter *is* the mechanic. |
| **`InjuryManager.illnessEvent`** | `Controller` | `RaiseEvent` null-checks and returns, so illnesses fire but generate **no event and no notification** — they're invisible to the player. Assign an `EventType` from `Resources/Events`. |
| `StatUI.colors[]` | 6 leftover `Stat` objects | See §4 — delete the objects; `Stat.prefab` itself has all five colours. |
| `BenchPositionUI.StrengthColors[]` | `BenchPositionUI.prefab` | **Safe** — `BenchPositionUI` overrides `UpdatePosition` and never indexes the array. Noted so it isn't "fixed" unnecessarily. |

---

## 7. Suggested order

1. **The three P0 hooks** in `UI_AUDIT_AND_LAYOUT_PLAN.md` §8 — `TeamTalkUI.Close`, `TacticsPageUI.ResumeMatch`,
   `CompetitionPageUI.ChangeWeekFocus`. Two of them are match soft-locks; all three are one Inspector click each.
2. **`InjuryManager.illnessEvent`** — one drag, makes a whole event category visible.
3. **Place `SquadStatus Variant.prefab`** on the Home page — the widget is finished, it just isn't there.
4. **Add a Back button** — unlocks navigation code that already exists.
5. **Delete the dead code** in §4 (`TacticStats` + `Apply`, the minigame classes, `_Recovery/`).
6. **Fix the doc drift** in §5 as each item lands, rather than leaving a fifth doc to reconcile.
7. Bigger calls, in rough value order: the **release-before-hire** picker (a real dead end today), a **chemistry
   display** (a live system the player can't see), the **Stats page**, then the history browser.
