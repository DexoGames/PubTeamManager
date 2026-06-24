using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Headless A/B harness for tactics analysis. It builds synthetic "dummy" teams — players given random stats
/// that AVERAGE to the skill you set (so the squad has spread, not one flat number), and each player made
/// Natural at the position his formation slot assigns. Both teams are built the same way, so any difference in
/// results comes purely from the one thing you changed. It runs N quick simulations per variation and logs the
/// average scoreline + win rate, each variation compared against a "Baseline".
///
/// Answers questions like:
///   • does adding reliance instruction X actually help, and by how much?
///   • is formation A better than formation B for these players?
///   • what does a weak keeper cost vs a standard one, everything else equal?
///   • what is low vs high familiarity worth?
///
/// HOW TO USE: drop this on an empty GameObject, set the knobs in the Inspector, enter Play mode, then
/// right-click the component header → "Run Experiments" (or call RunExperiments()). Read the table in the
/// Console.
///
/// METHODOLOGY NOTES:
///   • Games are RNG-paired: game g uses seed+g for EVERY variation, so the same "match script" is replayed
///     and only your change differs — far lower variance than independent runs.
///   • Both teams are otherwise identical (same skill, formation, baseline instructions, familiarity), so the
///     baseline sits near 0 goal difference and a variation's "vs base" column is its true effect.
///   • Reliance / keeper variations are re-normalised to the baseline FAMILIARITY after the change, so you
///     measure the tactical effect, not the familiarity hit of toggling something. Only the familiarity
///     variations move familiarity.
/// </summary>
public class TacticLab : MonoBehaviour
{
    [Header("Sim settings")]
    [Tooltip("Games to simulate per variation. More = steadier averages (try 20–100).")]
    public int gamesPerVariation = 30;
    [Tooltip("Base RNG seed. Each game uses seed+gameIndex, identical across variations (paired sampling).")]
    public int randomSeed = 12345;

    [Header("Dummy team baseline")]
    [Tooltip("Outfield players get random stats whose AVERAGE equals this (0–100). Both teams share it.")]
    [Range(0, 100)] public int outfieldSkill = 60;
    [Tooltip("The keeper's stats average to this (its goalkeeping-relevant stats centre here too).")]
    [Range(0, 100)] public int keeperSkill = 60;
    [Tooltip("How far individual stats scatter around the average (±). 0 = flat (every stat the same).")]
    [Range(0, 50)] public int statSpread = 18;
    [Tooltip("Familiarity both teams start at (0–1). Held constant except by the familiarity variations.")]
    [Range(0f, 1f)] public float baselineFamiliarity = 0.6f;
    [Tooltip("Baseline formation (used by both teams). Leave empty to load the first under Resources/Formations/Usable.")]
    public Formation formation;
    [Tooltip("Instructions BOTH teams always run (the control set). Don't also list these under 'instructions to test'.")]
    public List<TacticInstruction> baselineInstructions = new List<TacticInstruction>();

    [Header("Key player (so reliances have something to amplify)")]
    [Tooltip("IMPORTANT for reliance tests: a reliance only changes the result if the player it leans on DIFFERS " +
             "from his group's average. This makes one XI slot a specialist on BOTH teams; a reliance that names " +
             "these same stats and allows this slot then binds to him and amplifies them. With identical players a " +
             "reliance reads ~0.")]
    public bool useKeyPlayer = true;
    [Tooltip("Which starting-XI slot (0–10) is the specialist. Match it to the reliance's eligible position.")]
    [Range(0, 10)] public int keyPlayerSlot = 10;
    [Tooltip("The specialist's level in the chosen stats (the rest of his stats stay at outfield skill).")]
    [Range(0, 100)] public int keyPlayerStatLevel = 90;
    [Tooltip("Which stats the specialist is elite in. Name the SAME stats in the reliance you're testing.")]
    public List<PlayerStat> keyPlayerStats = new List<PlayerStat>();

    [Header("What to test")]
    [Tooltip("Creates one variation per entry: baseline + this instruction added to the HOME team only.")]
    public List<TacticInstruction> instructionsToTest = new List<TacticInstruction>();
    [Tooltip("Creates one variation per entry: the HOME team plays this formation instead of the baseline one " +
             "(same players, re-slotted and made natural in their new positions). The opponent keeps the baseline.")]
    public List<Formation> formationsToTest = new List<Formation>();
    [Tooltip("Add a 'Weak keeper' variation (home keeper nerfed, everything else equal).")]
    public bool testWeakKeeper = true;
    [Range(0, 100)] public int weakKeeperSkill = 25;
    [Tooltip("Add 'Low familiarity' and 'High familiarity' variations for the home team.")]
    public bool testFamiliarity = true;
    [Range(0f, 1f)] public float lowFamiliarity = 0.15f;
    [Range(0f, 1f)] public float highFamiliarity = 0.95f;

    int _nextPersonId;

    class Variation
    {
        public string Name;
        public Action<Team> Apply;          // mutates the freshly-built baseline HOME team
        public bool ControlsFamiliarity;    // if true, the harness won't re-normalise familiarity after Apply
        public Formation FormationOverride;  // null = build the home team with the baseline formation
    }

    [ContextMenu("Run Experiments")]
    public void RunExperiments()
    {
        Formation form = formation != null
            ? formation
            : Resources.LoadAll<Formation>("Formations/Usable").FirstOrDefault();

        if (form == null) { Debug.LogError("[TacticLab] No formation set and none found in Resources/Formations/Usable."); return; }
        if (form.Positions == null || form.Positions.Length < 11) { Debug.LogError("[TacticLab] Formation needs at least 11 positions."); return; }
        if (gamesPerVariation < 1) gamesPerVariation = 1;

        var variations = BuildVariations();

        var sb = new StringBuilder();
        sb.AppendLine($"=== TacticLab — {gamesPerVariation} games/variation, seed {randomSeed}, " +
                      $"skill {outfieldSkill} (GK {keeperSkill}), familiarity {baselineFamiliarity:0.00} ===");
        sb.AppendLine("Home & Away are identical dummies; positive 'vs base' = the change helped the home team.");
        sb.AppendLine(Row("Variation", "Home", "Away", "GD", "vs base", "W-D-L", "Win%"));
        sb.AppendLine(new string('-', 80));

        double baselineGD = 0; bool haveBaseline = false;

        foreach (var v in variations)
        {
            var res = RunSeries(
                buildHome: () =>
                {
                    Formation hf = v.FormationOverride != null ? v.FormationOverride : form;
                    Team t = BuildTeam($"HOME[{v.Name}]", outfieldSkill, keeperSkill, hf, baselineInstructions);
                    v.Apply?.Invoke(t);
                    if (!v.ControlsFamiliarity) SetFamiliarity(t, hf, baselineFamiliarity); // hold familiarity fixed
                    return t;
                },
                buildAway: () => BuildTeam("AWAY", outfieldSkill, keeperSkill, form, baselineInstructions),
                games: gamesPerVariation,
                seed: randomSeed);

            double gd = res.homeAvg - res.awayAvg;
            if (!haveBaseline) { baselineGD = gd; haveBaseline = true; }

            int total = res.w + res.d + res.l;
            sb.AppendLine(Row(
                v.Name,
                res.homeAvg.ToString("0.00"),
                res.awayAvg.ToString("0.00"),
                gd.ToString("+0.00;-0.00; 0.00"),
                (gd - baselineGD).ToString("+0.00;-0.00; 0.00"),
                $"{res.w}-{res.d}-{res.l}",
                total > 0 ? $"{100.0 * res.w / total:0}%" : "-"));
        }

        Debug.Log(sb.ToString());
    }

    static string Row(string a, string b, string c, string d, string e, string f, string g)
        => $"{a,-24}{b,7}{c,7}{d,9}{e,9}{f,10}{g,7}";

    List<Variation> BuildVariations()
    {
        var list = new List<Variation> { new Variation { Name = "Baseline", Apply = null } };

        foreach (var instr in instructionsToTest.Where(i => i != null))
        {
            TacticInstruction captured = instr;     // avoid the foreach-closure capture trap
            list.Add(new Variation
            {
                Name = "+ " + captured.name,
                Apply = team => team.Tactic.AddInstruction(captured)
            });
        }

        foreach (var f in formationsToTest.Where(f => f != null && f.Positions != null && f.Positions.Length >= 11))
        {
            Formation captured = f;
            list.Add(new Variation
            {
                Name = "Formation: " + captured.name,
                FormationOverride = captured
            });
        }

        if (testWeakKeeper)
            list.Add(new Variation
            {
                Name = $"Weak keeper ({weakKeeperSkill})",
                Apply = team => SetKeeperStats(team, weakKeeperSkill)
            });

        if (testFamiliarity)
        {
            list.Add(new Variation
            {
                Name = $"Low fam ({lowFamiliarity:0.00})",
                ControlsFamiliarity = true,
                Apply = team => SetFamiliarity(team, team.Tactic.Formation, lowFamiliarity)
            });
            list.Add(new Variation
            {
                Name = $"High fam ({highFamiliarity:0.00})",
                ControlsFamiliarity = true,
                Apply = team => SetFamiliarity(team, team.Tactic.Formation, highFamiliarity)
            });
        }

        return list;
    }

    (double homeAvg, double awayAvg, int w, int d, int l) RunSeries(Func<Team> buildHome, Func<Team> buildAway, int games, int seed)
    {
        long homeGoals = 0, awayGoals = 0;
        int w = 0, d = 0, l = 0;

        for (int g = 0; g < games; g++)
        {
            UnityEngine.Random.InitState(seed + g);   // same script per game index across all variations
            Team home = buildHome();
            Team away = buildAway();

            Match.Result result = new Match(home, away).SimulateMatch();
            int hg = result.home.goals?.Count ?? 0;
            int ag = result.away.goals?.Count ?? 0;

            homeGoals += hg; awayGoals += ag;
            if (hg > ag) w++; else if (hg == ag) d++; else l++;

            // Teams are throwaway ScriptableObjects — free them so repeated runs don't accumulate instances.
            DestroyImmediate(home);
            DestroyImmediate(away);
        }

        return (homeGoals / (double)games, awayGoals / (double)games, w, d, l);
    }

    // ————————————————————— synthetic team / player construction —————————————————————

    Team BuildTeam(string name, int outfield, int gk, Formation form, List<TacticInstruction> instructions)
    {
        var team = ScriptableObject.CreateInstance<Team>();
        team.name = name;
        team.Name = name;

        // Parameterless Manager so we never touch the PersonManager/Resources singletons the random ctor uses;
        // the Tactic constructor only needs its ManStats for a formation + starting instruction list.
        var manager = new Manager();
        manager.ManStats.Formation = form;
        manager.ManStats.Instructions = instructions != null ? instructions.ToArray() : new TacticInstruction[0];

        // Players sit in formation-slot order, so their group (GK/Def/Mid/Att) is decided by the slot they fill,
        // and each is made Natural in that slot's position so swapping formation doesn't add off-position penalties.
        team.Players = new List<Player>();
        for (int i = 0; i < 11; i++)
        {
            Player.Position slotPos = form.Positions[i].ID;
            int mean = slotPos == Player.Position.GK ? gk : outfield;
            team.Players.Add(MakePlayer(team, mean, slotPos));
        }

        var tactic = new Tactic(team, manager);   // sets Formation + Instructions, runs RecalculateStats
        team.SetTactic(tactic);

        ApplyKeyPlayer(team);                      // same specialist on both teams (part of the controlled baseline)
        SetFamiliarity(team, form, baselineFamiliarity);

        if (team.Goalkeeper == null)
            Debug.LogWarning($"[TacticLab] '{name}': no GK in the XI — does the formation have a GK in slot 0?");

        return team;
    }

    Player MakePlayer(Team team, int meanSkill, Player.Position naturalPos)
    {
        var p = new Player
        {
            Team = team,
            PersonID = ++_nextPersonId,
            FirstName = "Dummy",
            // Mood == IdealMood & Passion == IdealPassion → MoraleModifier is a no-op (neutral player).
            Morale = new Morale(50, 50, 50, 50)
        };
        p.Surname = "#" + p.PersonID;

        // Random skills that AVERAGE exactly to meanSkill (so the squad has spread, not one flat number).
        int[] skills = RandomStatsWithMean(meanSkill, statSpread);

        // Natural ONLY in the position this formation slot assigns; the rest don't matter (he never plays them,
        // and group membership is decided by the slot, not these ratings).
        var positions = new Dictionary<Player.Position, Player.PositionStrength>();
        foreach (Player.Position pos in Enum.GetValues(typeof(Player.Position)))
            positions[pos] = Player.PositionStrength.None;
        positions[naturalPos] = Player.PositionStrength.Natural;

        int height = Mathf.Clamp(meanSkill + UnityEngine.Random.Range(-statSpread, statSpread + 1), 1, 99);

        p.RawStats = new Player.Stats { Skills = skills, Positions = positions, Height = height };
        return p;
    }

    /// <summary>
    /// Builds <see cref="Player.SKILL_NO"/> stats scattered by ±<paramref name="spread"/> around
    /// <paramref name="mean"/> but whose integer average is EXACTLY <paramref name="mean"/>. After the random
    /// scatter it nudges values ±1 (within 1–99) to land the total on mean × count — deterministic for a given
    /// RNG state, so the harness's paired-seed comparison still holds.
    /// </summary>
    static int[] RandomStatsWithMean(int mean, int spread)
    {
        int n = Player.SKILL_NO;
        var v = new int[n];
        int sum = 0;
        for (int i = 0; i < n; i++)
        {
            v[i] = Mathf.Clamp(mean + UnityEngine.Random.Range(-spread, spread + 1), 1, 99);
            sum += v[i];
        }

        int diff = mean * n - sum;          // total we still need to add (+) or remove (−)
        int idx = 0, guard = 0, maxGuard = n * 200;
        while (diff != 0 && guard++ < maxGuard)
        {
            int step = diff > 0 ? 1 : -1;
            int next = v[idx] + step;
            if (next >= 1 && next <= 99) { v[idx] = next; diff -= step; }
            idx = (idx + 1) % n;
        }
        return v;
    }

    /// <summary>
    /// Sets a tactic's Familiarity exactly to <paramref name="target01"/> (0–1) by writing the habituation
    /// weights so both the formation- and instruction-familiarity terms equal the target, then recomputing
    /// (RecalculateStats refreshes Familiarity from the weights). Uses only public members.
    /// </summary>
    void SetFamiliarity(Team team, Formation form, float target01)
    {
        Tactic tactic = team.Tactic;
        target01 = Mathf.Clamp01(target01);

        // InstructionFamiliarity averages (1 - |state - weight|) over the whole instruction universe.
        // Pick weight = target for active (state 1) and 1-target for inactive (state 0) so every term == target.
        foreach (var i in Resources.LoadAll<TacticInstruction>("Tactics/Instructions"))
        {
            bool active = tactic.Instructions.Contains(i);
            tactic.SettingWeights["inst:" + i.name] = active ? target01 : 1f - target01;
        }
        if (form != null) tactic.SettingWeights["form:" + form.name] = target01;

        tactic.RecalculateStats();   // recomputes Familiarity from the weights above
    }

    /// <summary>Makes one XI slot a specialist (elite in the chosen stats) so a matching reliance has something
    /// to amplify. Applied to both teams, so it's part of the even baseline.</summary>
    void ApplyKeyPlayer(Team team)
    {
        if (!useKeyPlayer || keyPlayerStats == null || keyPlayerStats.Count == 0) return;
        if (keyPlayerSlot < 0 || keyPlayerSlot >= team.Players.Count) return;

        Player p = team.Players[keyPlayerSlot];
        foreach (var st in keyPlayerStats) p.RawStats.SetStat(st, keyPlayerStatLevel);
    }

    /// <summary>Sets the keeper's goalkeeping-relevant stats (Goalkeeping = avg of these) to a level.</summary>
    void SetKeeperStats(Team team, int level)
    {
        Player gk = team.Goalkeeper;
        if (gk == null) return;
        foreach (var st in new[] { PlayerStat.Jumping, PlayerStat.Aggression, PlayerStat.Composure, PlayerStat.Positioning, PlayerStat.Height })
            gk.RawStats.SetStat(st, level);
    }
}
