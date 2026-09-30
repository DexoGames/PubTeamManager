# Working Preferences

How I (the developer) like an AI assistant to work on this project. Read this first; it overrides default
instincts. The deep code/design context lives in [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md).

## Unity workflow — what NOT to do
- **Don't dig through the Unity Editor logs** (`Editor.log`) to check whether code compiled. I have the Editor
  open and can read the console myself — just tell me what you changed and what to watch for. Don't grep logs or
  try to infer compile state.
- **Don't try to run, launch, play, or screenshot the game**, and don't try to drive the Unity Editor. You can't,
  and I'd rather test it myself. No "let me verify by running it" — hand me code I can drop in.
- **Don't try to open/focus Unity** to force a recompile. Assume it compiles if it's syntactically sound; I'll
  report back any errors.

## Unity workflow — what TO do
- **I build code now and wire it in the Editor later.** So make C# **drop-in**: complete, compiling, no half-done
  stubs that need me to fill blanks.
- **For anything needing Editor/Inspector wiring, write (or update) a `*_BUILD.md` in `Docs/`** — be specific:
  exact serialized field names, OnClick method hooks, GameObject hierarchy, and which singletons must sit on a
  scene object. This is the single most useful thing you can do for me.
- When you hand-author a ScriptableObject `.asset`, Unity (if open) auto-generates its `.meta` — that's fine, you
  can also author the `.meta` yourself with a fresh GUID. Don't fuss over it either way.
- Keep [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md) and the relevant build doc **in sync** when a system changes.

## Code conventions (short version — full house style in PROJECT_OVERVIEW.md §1)
- **Pure logic in plain testable C#; thin MonoBehaviour/`UIObject` renderers over it.** I like being able to A/B
  logic headlessly (see `TacticLab`).
- **Reuse one system, don't build a parallel one.** Layer onto existing mechanics; extract shared bases rather
  than copy-paste. (Strong preference — I've pushed back on this before.)
- **Derive-on-read** where consistency matters (one source of truth); only denormalize deliberately.
- **Tunables as `const`/`SerializeField` with a short comment**, never buried magic numbers.
- **Let it crash on a wiring mistake — do NOT null-check `[SerializeField]` references (IMPORTANT).** If a
  reference isn't assigned in the Inspector that's a *bug I need to see*, so I want the `NullReferenceException`
  pointing straight at it. Never write `if (x != null) x.foo = ...` around an inspector field, never silently
  no-op, and never invent an "Optional" field with a `GetComponent` fallback to justify a guard. Every serialized
  reference is assumed wired. If something genuinely should be optional, say so and ask — don't guard by default.
- **Don't null-check core singletons/state either:** `TeamManager.Instance`, `.MyTeam`, `.MyTeam.Tactic`, and the
  persistent managers (`GameManager`/`UIManager`/`SaveManager`/`CalenderManager`/`ScheduleManager`/`InterviewManager`/…).
  They exist for the whole session — guard once at load, or in the few genuinely relevant spots, not on every access.
  (Controllers' load/generation-order guards and page-instance cross-refs that may not be in the scene are fine.)
- **Guards that DO earn their place:** genuine runtime state that can legitimately be absent (no interviewee yet,
  an empty slot, a dictionary/lookup miss, a nullable the caller may omit), and rebuilding any `[JsonIgnore]` field
  the runtime reads in `OnAfterDeserialize` (recurring bug class). Graceful degradation of *data*, yes; defensive
  padding around *wiring*, no.
- **Comments earn their place.** Only comment complex/non-obvious logic — the WHY, a hidden constraint, a balance
  reason. Don't restate what the code already says when the name or flow is self-explanatory, and delete
  conversational/narrative asides ("// 1. …", "// CRITICAL FIX", "request 2"). Strip restating comments on sight.
- **Same bar for `[Tooltip]` and `/// <summary>` (IMPORTANT).** No tooltip on a field whose name already says it
  (`nameText`, `background`, `selectButton`); no `<summary>` on a method whose name already says it (`RefreshMorale`,
  `SetSelectable`, `Setup`). Reserve tooltips for balance tunables whose *meaning or range* isn't obvious, and
  summaries for the class-level "what is this screen/system and how does it fit together" note plus genuinely
  non-obvious methods. Doc-comment noise is as unwelcome as comment noise.
- **Match the surrounding file's style and idioms.**

## The one design rule
**Everything has a trade-off.** Anything that can improve the team must also be able to hurt it if misused. If a
mechanic is pure upside, add an authored downside. I push back on free upgrades.

## Communication
- **Give a recommendation, not a survey.** When there's a judgment call, pick the sensible default, say what you
  picked and why, and move on. Be specific and technical — I know the codebase.
- Don't re-explain things already established; don't narrate options you won't take.
- Flagging a genuinely better approach is welcome; pad­ding with caveats is not.

## Docs layout
All design/build/plan docs live in **`Docs/`**. [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md) is the map; each system
has a companion `*_BUILD.md`/`*_PLAN.md` (see the §14 table). `README.md` stays at the repo root.
