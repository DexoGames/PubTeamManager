using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Game;

public enum TacticStat
{
    Complexity, Intensity, Control, Stability, Pressure, Security,
    Threat, Creativity, DefensiveWidth, AttackingWidth, Fouling, Provoking
}

/// <summary>
/// A team's overall mentality, from sitting very deep to throwing everyone forward. Shifts a whole
/// band of tactic stats at once (Threat/Security/Intensity/width…), giving a single dial that ranges
/// from "park the bus" to "all-out attack". The player can nudge this mid-match (request 2).
/// </summary>
public enum Mentality
{
    UltraDefensive, Defensive, Cautious, Balanced, Positive, Attacking, UltraAttacking
}

/// <summary>
/// Runtime per-team tactical state: chosen formation, stackable instructions, player dependencies,
/// an overall mentality and a Familiarity score. <see cref="RecalculateStats"/> folds all of these
/// into the 12 <see cref="TacticStat"/> values the match engine reads.
///
/// Design rule throughout: every lever has a trade-off. A more attacking mentality scores more but
/// concedes more; a player dependency amplifies a player's strengths AND his weaknesses; complex
/// setups need intelligent players; and a freshly-changed setup has low Familiarity until the team
/// drills it back up over matches and training.
/// </summary>
public class Tactic
{
    public Team Team { get; private set; }
    public Manager Manager { get; private set; }
    public Formation Formation { get; private set; }
    public List<TacticInstruction> Instructions { get; private set; } = new();

    /// <summary>
    /// For each active reliance instruction, the SQUAD INDEX (slot in Team.Players) it leans on, keyed by
    /// instruction name. Index-based so the reliance follows the slot: if that starter is subbed, the reliance
    /// transfers to whoever fills the slot; changing formation keeps it on the same player.
    /// </summary>
    public Dictionary<string, int> RelianceSlots { get; private set; } = new();

    /// <summary>Per reliance: the PersonID the team is currently "drilled" relying on, used to detect when a
    /// sub/swap has actually changed the reliant player (for the familiarity penalty). Rebuilt at runtime; not saved.</summary>
    [System.NonSerialized] private Dictionary<string, int> _relianceDrilled = new();

    /// <summary>Persistent base mentality the player sets in the tactics screen.</summary>
    public Mentality BaseMentality = Mentality.Balanced;

    /// <summary>
    /// Temporary in-match nudge from the "more attacking / more defensive" buttons. Added to the base
    /// mentality for the live match only; reset to 0 when the match ends (see <see cref="EndMatch"/>).
    /// </summary>
    public int InMatchMentalityShift;

    /// <summary>
    /// Per-setting habituation weights (0 = "the team is used to this being OFF", 1 = "used to it being
    /// ON", 0.5 = no habit either way). Keyed "form:&lt;name&gt;" per usable formation and "inst:&lt;name&gt;"
    /// per instruction. Weights only move when you play/train (<see cref="AdvanceFamiliarity"/>); changing
    /// a setting on the tactics screen does NOT move them, so flip-flopping a toggle can't drain familiarity.
    /// </summary>
    public Dictionary<string, float> SettingWeights = new();

    // ————— familiarity tuning —————
    /// <summary>How far each weight moves toward the current state per play/train (~3 months → ~100%).</summary>
    private const float FAMILIARITY_LEARN_RATE = 0.12f;
    /// <summary>Share of Familiarity that comes from the FORMATION (the rest from instructions). Switching to a
    /// never-drilled formation therefore costs roughly this fraction — ~40% — of your familiarity.</summary>
    private const float FORMATION_FAMILIARITY_SHARE = 0.4f;
    /// <summary>"No habit" weight for a binary instruction (fresh tactic → ~50% instruction familiarity).</summary>
    private const float DEFAULT_INSTRUCTION_WEIGHT = 0.5f;
    /// <summary>A formation the team has NEVER drilled is totally unfamiliar (so switching to one stings).</summary>
    private const float DEFAULT_FORMATION_WEIGHT = 0f;
    /// <summary>A new team half-knows its starting formation, so a fresh tactic still reads ~50% overall.</summary>
    private const float STARTING_FORMATION_WEIGHT = 0.5f;

    /// <summary>Cached 0–100 familiarity, recomputed whenever settings or weights change.</summary>
    private float _familiarity = 50f;

    // Stat fields (the 12 the engine reads via Team.*)
    public int Complexity;
    public int Intensity;
    public int Control;
    public int Stability;
    public int Pressure;
    public int Security;
    public int Threat;
    public int Creativity;
    public int DefensiveWidth;
    public int AttackingWidth;
    public int Fouling;
    public int Provoking;

    /// <summary>
    /// Formation-derived tempo (0–100). NOT a TacticStat slider — it drives the match engine's build-up
    /// choice: HIGH tempo goes direct (skips patient build, straight to Advance) more often; LOW tempo favours
    /// building from the back. Set from the formation in <see cref="ApplyFormationBase"/>.
    /// </summary>
    public int Tempo { get; private set; } = 50;

    // ————— in-match gating (request 2: proper changes only at half-time) —————
    /// <summary>True while this tactic's team is in the interactive live match.</summary>
    public bool InMatch { get; private set; }
    /// <summary>True only during the half-time break, when structural changes are permitted.</summary>
    public bool IsHalfTime { get; set; }
    /// <summary>Formation / instruction / dependency edits are only allowed out of a match, or at half-time.</summary>
    public bool CanMakeStructuralChange => !InMatch || IsHalfTime;

    public Tactic(Team team, Manager manager)
    {
        Manager = manager;
        Team = team;
        Formation = manager.ManStats.Formation;
        ResetStats();
        // Assign the manager's starting instructions directly (no familiarity penalty for the default setup).
        Instructions = manager.ManStats.Instructions != null
            ? manager.ManStats.Instructions.ToList()
            : new List<TacticInstruction>();

        SeedStartingFormation();
        RecalculateStats();
    }

    // ————————————————————— effective mentality —————————————————————

    /// <summary>Mentality index after the in-match nudge, clamped to the enum range.</summary>
    public int EffectiveMentalityIndex =>
        Mathf.Clamp((int)BaseMentality + InMatchMentalityShift, 0, GetEnumLength<Mentality>() - 1);

    public Mentality EffectiveMentality => (Mentality)EffectiveMentalityIndex;

    /// <summary>-3 (ultra defensive) … 0 (balanced) … +3 (ultra attacking).</summary>
    private int MentalityStep => EffectiveMentalityIndex - (int)Mentality.Balanced;

    // ————————————————————— structural edits —————————————————————

    // Changing the setup no longer "costs" familiarity directly — Familiarity is computed from how well
    // the current settings match their habituated weights (see the familiarity section). Toggling an
    // instruction shifts its current state, so familiarity reflects the change immediately, but the
    // weights only move when you actually play/train — so flip-flopping a toggle no longer drains it.
    public void SetFormation(Formation newFormation)
    {
        if (newFormation == null) return;
        Formation = newFormation;
        RecalculateStats();
    }

    /// <summary>
    /// Adds an instruction. If it's a reliance instruction, the reliant player is set from
    /// <paramref name="reliancePlayer"/> (the UI passes the picked player); leave null for AI/defaults and a
    /// sensible eligible player is auto-bound later via <see cref="RefreshReliances"/>.
    /// </summary>
    public void AddInstruction(TacticInstruction instruction, Player reliancePlayer = null)
    {
        if (instruction == null || Instructions.Contains(instruction)) return;

        Instructions.Add(instruction);
        if (instruction.hasReliance && reliancePlayer != null)
        {
            RelianceSlots[instruction.name] = reliancePlayer.GetTeamIndex();
            _relianceDrilled[instruction.name] = reliancePlayer.PersonID; // initial pick — no penalty
        }
        RecalculateStats();
    }

    public void RemoveInstruction(TacticInstruction instruction)
    {
        if (!Instructions.Contains(instruction)) return;

        Instructions.RemoveAll(i => i == instruction);
        if (instruction != null)
        {
            RelianceSlots.Remove(instruction.name);
            _relianceDrilled.Remove(instruction.name);
        }
        RecalculateStats();
    }

    public void ResetInstructions()
    {
        Instructions.Clear();
        RelianceSlots.Clear();
        _relianceDrilled.Clear();
        RecalculateStats();
    }

    // ————————————————————— reliances (instructions that lean on one player) —————————————————————

    /// <summary>Sets/updates which player a reliance instruction leans on (null clears it). Stores the squad slot.</summary>
    public void SetReliancePlayer(TacticInstruction instruction, Player player)
    {
        if (instruction == null || !instruction.hasReliance) return;
        if (player == null)
        {
            RelianceSlots.Remove(instruction.name);
            _relianceDrilled.Remove(instruction.name);
        }
        else
        {
            RelianceSlots[instruction.name] = player.GetTeamIndex();
            _relianceDrilled[instruction.name] = player.PersonID;
        }
        RecalculateStats();
    }

    /// <summary>The player currently filling a reliance instruction's slot (null if unset / out of range).</summary>
    public Player GetReliancePlayer(TacticInstruction instruction) => ResolveReliancePlayer(instruction);

    private Player ResolveReliancePlayer(TacticInstruction instruction)
    {
        if (instruction == null || Team == null || Team.Players == null) return null;
        if (!RelianceSlots.TryGetValue(instruction.name, out int slot)) return null;
        return slot >= 0 && slot < Team.Players.Count ? Team.Players[slot] : null;
    }

    /// <summary>
    /// True if any active reliance leans on this player's slot AND fires in the given phase (fast pre-check for
    /// AverageStats). <paramref name="phaseGroup"/> is the position band the match engine is currently evaluating;
    /// pass null for a non-phase / team-wide context (the reliance then applies unconditionally).
    /// </summary>
    public bool IsReliantPlayer(Player player, PlayerGroup? phaseGroup = null)
    {
        if (player == null || RelianceSlots.Count == 0) return false;
        int idx = player.GetTeamIndex();
        foreach (var instr in Instructions)
            if (instr != null && instr.hasReliance &&
                RelianceSlots.TryGetValue(instr.name, out int slot) && slot == idx &&
                ReliancePhaseActive(instr.reliance.eligibleGroups, phaseGroup))
                return true;
        return false;
    }

    /// <summary>
    /// Extra weight the player's given stat gets from the active reliances on his slot — how much more that
    /// stat counts toward the team's effective ability (his strengths AND weaknesses in it amplified). The bonus
    /// only counts a reliance whose eligible groups cover the phase being evaluated (see <paramref name="phaseGroup"/>),
    /// so e.g. a defenders reliance amplifies a defensive phase but not the finishing phase. Null = unconditional.
    /// </summary>
    public float RelianceBonus(Player player, PlayerStat stat, PlayerGroup? phaseGroup = null)
    {
        if (player == null || RelianceSlots.Count == 0) return 0f;
        int idx = player.GetTeamIndex();
        float bonus = 0f;
        foreach (var instr in Instructions)
        {
            if (instr == null || !instr.hasReliance) continue;
            if (!RelianceSlots.TryGetValue(instr.name, out int slot) || slot != idx) continue;
            if (!ReliancePhaseActive(instr.reliance.eligibleGroups, phaseGroup)) continue;
            var stats = instr.reliance.stats;
            if (stats != null && Array.IndexOf(stats, stat) >= 0)
                bonus += instr.reliance.multiplier;
        }
        return bonus;
    }

    // ————————————————————— reliance phase-gating —————————————————————
    //
    // A reliance's eligibleGroups do double duty: they say who can HOLD it (picker / auto-disable) AND which
    // phases of play it influences. The match engine evaluates one position band per phase (Defenders for build-up,
    // Attackers for finishing, the unions for transitions, …). A reliance fires in a phase only when that phase's
    // band is WITHIN the reliance's eligible bands — so a defenders-only reliance sways the build-up but not the
    // finish, a target-man (attackers) reliance sways the finish but not the build-up, and so on.

    [System.Flags]
    private enum Bands { None = 0, GK = 1, Def = 2, Mid = 4, Atk = 8 }

    /// <summary>The position bands a <see cref="PlayerGroup"/> spans (for phase gating). Wide/Outfield span all
    /// outfield bands — there's no wide-specific match phase, so a wide reliance applies wherever its player does.</summary>
    private static Bands BandsOf(PlayerGroup g) => g switch
    {
        PlayerGroup.Goalkeeper          => Bands.GK,
        PlayerGroup.Defenders           => Bands.Def,
        PlayerGroup.Midfielders         => Bands.Mid,
        PlayerGroup.Attackers           => Bands.Atk,
        PlayerGroup.DefenseAndMidfield  => Bands.Def | Bands.Mid,
        PlayerGroup.MidfieldAndAttack   => Bands.Mid | Bands.Atk,
        PlayerGroup.GoalkeeperAndDefense => Bands.GK | Bands.Def,
        PlayerGroup.Outfield            => Bands.Def | Bands.Mid | Bands.Atk,
        PlayerGroup.WidePlayers         => Bands.Def | Bands.Mid | Bands.Atk,
        _                               => Bands.None
    };

    private static Bands BandsOf(PlayerGroup[] groups)
    {
        Bands b = Bands.None;
        if (groups != null) foreach (var g in groups) b |= BandsOf(g);
        return b;
    }

    /// <summary>Does a reliance with these eligible groups influence the phase evaluating <paramref name="phaseGroup"/>?
    /// True when the phase's bands are a subset of the eligible bands. Null phase = team-wide → always true.</summary>
    private static bool ReliancePhaseActive(PlayerGroup[] eligibleGroups, PlayerGroup? phaseGroup)
    {
        if (phaseGroup == null) return true;
        Bands ctx = BandsOf(phaseGroup.Value);
        Bands elig = BandsOf(eligibleGroups);
        return ctx != Bands.None && (ctx & elig) == ctx; // ctx ⊆ elig
    }

    /// <summary>The starting-XI players eligible to be picked for this reliance (filtered by its position groups).</summary>
    public IEnumerable<Player> EligibleReliancePlayers(TacticInstruction instruction)
    {
        if (Team == null) return Enumerable.Empty<Player>();
        return Team.StartingPlayers.Where(p => IsEligible(p, instruction.reliance));
    }

    /// <summary>Is the player currently in one of the reliance's allowed position groups? (No groups = not a
    /// reliance, so this never gates an active one — returns true as a harmless guard.)</summary>
    private bool IsEligible(Player player, TacticInstruction.Reliance reliance)
    {
        var groups = reliance.eligibleGroups;
        if (groups == null || groups.Length == 0) return true;
        foreach (var g in groups)
            if (Team.GetGroup(g).Contains(player)) return true;
        return false;
    }

    /// <summary>
    /// Re-validates every reliance — call whenever the lineup/formation/tactic changes or a match starts:
    ///  • auto-binds any reliance with no player yet (AI / defaults) to the best ELIGIBLE starter;
    ///  • auto-DISABLES (removes) a reliance whose slot is empty or whose player has moved out of its groups;
    ///  • applies the per-reliance familiarity penalty when a sub/swap has changed who fills the slot.
    /// </summary>
    public void RefreshReliances()
    {
        if (Team == null || Team.Players == null || Team.Players.Count < 11) return;

        bool changed = false;
        List<TacticInstruction> toRemove = null;

        foreach (var instr in Instructions)
        {
            if (instr == null || !instr.hasReliance) continue;

            // Auto-bind missing (AI / default-template reliances) to the best eligible starter.
            if (!RelianceSlots.ContainsKey(instr.name))
            {
                Player auto = AutoPickReliancePlayer(instr);
                if (auto == null) { (toRemove ??= new List<TacticInstruction>()).Add(instr); continue; }
                RelianceSlots[instr.name] = auto.GetTeamIndex();
                _relianceDrilled[instr.name] = auto.PersonID;
                changed = true;
            }

            Player current = ResolveReliancePlayer(instr);

            // Invalid: slot empty, or the player has moved out of the allowed groups → disable the instruction.
            if (current == null || !IsEligible(current, instr.reliance))
            {
                (toRemove ??= new List<TacticInstruction>()).Add(instr);
                continue;
            }

            // Familiarity penalty when a sub/swap has changed who fills the slot since we last accounted for it.
            if (_relianceDrilled.TryGetValue(instr.name, out int drilledId))
            {
                if (drilledId != current.PersonID)
                {
                    ApplyReliancePenalty(instr);
                    _relianceDrilled[instr.name] = current.PersonID;
                    changed = true;
                }
            }
            else
            {
                _relianceDrilled[instr.name] = current.PersonID; // first time seen (e.g. after load) — no penalty
            }
        }

        if (toRemove != null)
            foreach (var instr in toRemove)
            {
                Instructions.Remove(instr);
                RelianceSlots.Remove(instr.name);
                _relianceDrilled.Remove(instr.name);
                changed = true;
            }

        if (changed) RecalculateStats();
    }

    /// <summary>Knocks an instruction's habituation weight toward neutral by its reliance familiarityPenalty.</summary>
    private void ApplyReliancePenalty(TacticInstruction instruction)
    {
        float pen = Mathf.Clamp01(instruction.reliance.familiarityPenalty);
        if (pen <= 0f) return;
        string key = $"inst:{instruction.name}";
        float w = SettingWeights.TryGetValue(key, out var v) ? v : DEFAULT_INSTRUCTION_WEIGHT;
        SettingWeights[key] = Mathf.Lerp(w, DEFAULT_INSTRUCTION_WEIGHT, pen);
    }

    private Player AutoPickReliancePlayer(TacticInstruction instruction)
    {
        if (Team == null || Team.Players == null || Team.Players.Count < 11) return null;

        var stats = instruction.reliance.stats;
        Player best = null;
        int bestScore = int.MinValue;
        foreach (var p in Team.StartingPlayers)
        {
            if (!IsEligible(p, instruction.reliance)) continue; // only eligible players
            int score = 0;
            var s = p.GetStats();
            if (stats != null) foreach (var st in stats) score += s.GetStat(st);
            if (score > bestScore) { bestScore = score; best = p; }
        }
        return best;
    }

    // ————————————————————— familiarity (muscle-memory model) —————————————————————
    //
    // Each tactical "setting" — the chosen formation, and each instruction — has a habituation weight in
    // [0,1] tracking how drilled the team is in it. Familiarity is a weighted blend of two parts:
    //   • formation familiarity = how drilled the team is in the CURRENT formation (one weight per formation,
    //     0 for a formation never used) — worth FORMATION_FAMILIARITY_SHARE (~40%) of the total, so switching
    //     to an undrilled formation costs ~40% from a settled side;
    //   • instruction familiarity = how well each instruction's on/off state matches its habit — the rest.
    // Weights only move when you play/train (AdvanceFamiliarity); editing the tactic doesn't move them, so
    // flip-flopping a toggle (or switching formation and back) doesn't permanently drain familiarity — it
    // recovers once the old setup is current again. ~3 months of a stable tactic drives it to ~100%.

    /// <summary>Current familiarity, 0–100.</summary>
    public float Familiarity => _familiarity;

    /// <summary>Familiarity as a 0–1 fraction (used by the match engine for mistakes/cohesion).</summary>
    public float Familiarity01 => Mathf.Clamp01(_familiarity / 100f);

    private static TacticInstruction[] _instructionUniverse;

    private static void EnsureUniverse()
    {
        if (_instructionUniverse == null) _instructionUniverse = Resources.LoadAll<TacticInstruction>("Tactics/Instructions");
    }

    private string FormationKey => Formation != null ? $"form:{Formation.name}" : null;

    /// <summary>Habituation in the CURRENT formation (0 = never drilled it, 1 = fully drilled).</summary>
    private float FormationFamiliarity()
    {
        if (FormationKey == null) return 1f;
        return SettingWeights.TryGetValue(FormationKey, out var w) ? w : DEFAULT_FORMATION_WEIGHT;
    }

    /// <summary>Average over instructions of (1 − |currentState − habituatedWeight|).</summary>
    private float InstructionFamiliarity()
    {
        EnsureUniverse();
        if (_instructionUniverse == null || _instructionUniverse.Length == 0) return 1f;

        float sum = 0f;
        foreach (var i in _instructionUniverse)
        {
            float state = Instructions.Contains(i) ? 1f : 0f;
            float w = SettingWeights.TryGetValue($"inst:{i.name}", out var v) ? v : DEFAULT_INSTRUCTION_WEIGHT;
            sum += 1f - Mathf.Abs(state - w);
        }
        return sum / _instructionUniverse.Length;
    }

    /// <summary>Blend the formation and instruction familiarity by FORMATION_FAMILIARITY_SHARE.</summary>
    private void RecomputeFamiliarity()
    {
        float fam = FORMATION_FAMILIARITY_SHARE * FormationFamiliarity()
                  + (1f - FORMATION_FAMILIARITY_SHARE) * InstructionFamiliarity();
        _familiarity = Mathf.Clamp01(fam) * 100f;
    }

    /// <summary>
    /// One play/training session: drill the current formation toward fully familiar and nudge each
    /// instruction's weight toward its current on/off state. Repeated with a stable tactic this drives the
    /// weights to the extremes → Familiarity climbs to ~100% over ~3 months. Call this after a match or a
    /// training session — NOT when the player edits the tactic.
    /// </summary>
    public void AdvanceFamiliarity()
    {
        EnsureUniverse();

        // Drill the current formation toward "fully familiar".
        if (FormationKey != null)
        {
            float w = SettingWeights.TryGetValue(FormationKey, out var v) ? v : DEFAULT_FORMATION_WEIGHT;
            SettingWeights[FormationKey] = w + FAMILIARITY_LEARN_RATE * (1f - w);
        }

        // Drill each instruction toward its current state.
        if (_instructionUniverse != null)
        {
            foreach (var i in _instructionUniverse)
            {
                string key = $"inst:{i.name}";
                float state = Instructions.Contains(i) ? 1f : 0f;
                float w = SettingWeights.TryGetValue(key, out var v) ? v : DEFAULT_INSTRUCTION_WEIGHT;
                SettingWeights[key] = w + FAMILIARITY_LEARN_RATE * (state - w);
            }
        }

        RecomputeFamiliarity();
    }

    /// <summary>
    /// Seeds the team's INITIAL formation so a brand-new side reads ~50% familiar with it rather than 0%.
    /// Only seeds when there's no existing habit for it, and is only called at team creation — a player
    /// switching to a new formation gets the full (undrilled) hit, as intended.
    /// </summary>
    private void SeedStartingFormation()
    {
        if (FormationKey != null && !SettingWeights.ContainsKey(FormationKey))
            SettingWeights[FormationKey] = STARTING_FORMATION_WEIGHT;
    }

    // ————————————————————— complexity / intelligence —————————————————————

    /// <summary>
    /// The Intelligence the tactic demands — its Complexity directly. The more instructions/dependencies
    /// you stack, the higher this gets, so a demanding tactic needs a smart XI.
    /// </summary>
    public int IntelligenceThreshold => Mathf.Clamp(Complexity, 0, 100);

    /// <summary>Average intelligence of the starting XI, cached at match start (<see cref="RefreshMatchCache"/>).</summary>
    public float CachedStartingIntelligence { get; private set; } = 50f;

    /// <summary>Recomputes the per-match caches (squad IQ + chemistry) — call once at the start of each match.</summary>
    public void RefreshMatchCache()
    {
        RefreshReliances();
        CachedStartingIntelligence = CurrentStartingIntelligence();
        ComputeChemistry();
    }

    /// <summary>Average TacticalIntelligence of the on-field XI only (subs are irrelevant to execution).</summary>
    public float CurrentStartingIntelligence()
    {
        if (Team == null || Team.Players == null || Team.Players.Count < 11) return 50f;
        var xi = Team.StartingPlayers;
        float sum = 0f;
        foreach (var p in xi) sum += p.TacticalIntelligence();
        return sum / xi.Count;
    }

    /// <summary>
    /// Whether under-intelligent players get penalised. The SQUAD has to be smart enough collectively:
    /// if the XI's AVERAGE intelligence clears the bar, the brains cover for the odd dim player and nobody
    /// is penalised. Only when the average falls short do below-bar players take the harsh hit. Uses the
    /// value cached at match start so it's stable and cheap during the sim.
    ///
    /// CPU teams are EXEMPT: the human can dodge this with smart selection or a simpler tactic, but the AI
    /// won't, so penalising it just gifts the player easy games. See <see cref="Team.IsCpuControlled"/>.
    /// </summary>
    public bool ShouldApplyComplexityPenalty =>
        (Team == null || !Team.IsCpuControlled) && CachedStartingIntelligence < IntelligenceThreshold;

    /// <summary>Fraction of a mental stat lost per point of intelligence shortfall (10 below → ~50%).</summary>
    private const float INTELLIGENCE_PENALTY_PER_POINT = 0.05f;
    /// <summary>Cap on the reduction, so even a hopeless player keeps a little of his mental stats.</summary>
    private const float MAX_INTELLIGENCE_PENALTY = 0.9f;

    /// <summary>
    /// The FRACTION (0–1) by which an under-intelligent player's mental stats are cut — only applied when the
    /// squad average has already fallen short. Zero at/above the bar; calibrated so a player ~10 below the bar
    /// loses ~50%, capped at <see cref="MAX_INTELLIGENCE_PENALTY"/>. Intelligence itself is never cut (that
    /// would feed back into the shortfall and recurse).
    /// </summary>
    public float ComplexityPenaltyFraction(int intelligence)
    {
        int shortfall = IntelligenceThreshold - intelligence;
        if (shortfall <= 0) return 0f;
        return Mathf.Clamp(shortfall * INTELLIGENCE_PENALTY_PER_POINT, 0f, MAX_INTELLIGENCE_PENALTY);
    }

    /// <summary>The mental stats cut when a player can't keep up with a complex tactic — the whole mental
    /// group EXCEPT Intelligence (which is the gate, so cutting it would be recursive).</summary>
    public static readonly PlayerStat[] ComplexityAffectedStats =
    {
        PlayerStat.Positioning, PlayerStat.Creativity, PlayerStat.Teamwork,
        PlayerStat.Composure, PlayerStat.Aggression
    };

    // ————————————————————— chemistry (per-match cache) —————————————————————
    //
    // Chemistry is a hidden, deterministic relationship between two players (see Chemistry / ChemistryLevel).
    // It only bites when two starters line up in LINKED positions (CB/CB, LB/LM, AM/CM, RW/ST, …): a non-
    // neutral pair nudges a few of BOTH players' "combination" stats up (In-Sync) or down (Frosty / Bad Blood).
    // Positions are fixed for the match, so we resolve every pair once here and cache a per-stat delta per slot;
    // AverageStats folds it into each player's effective stats. The trade-off: chasing a chemistry boost (or
    // dodging a clash) constrains who you can field next to whom — it costs you selection freedom elsewhere.

    /// <summary>Per starting-XI slot (squad index 0–10): cached chemistry stat deltas for the current match.
    /// Null outside a match. Rebuilt each match start in <see cref="ComputeChemistry"/>; never serialized.</summary>
    [System.NonSerialized] private int[][] _chemistryDeltas;

    /// <summary>Resolve every linked starter pair's chemistry into a per-slot, per-stat delta for this match.</summary>
    private void ComputeChemistry()
    {
        _chemistryDeltas = null;
        if (Team == null || Team.Players == null || Team.Players.Count < 11 || Formation == null) return;

        var xi = Team.StartingPlayers;
        int n = Mathf.Min(xi.Count, Formation.Positions.Length);

        var deltas = new int[xi.Count][];
        for (int i = 0; i < xi.Count; i++) deltas[i] = new int[Player.SKILL_NO];

        for (int i = 0; i < n; i++)
        {
            Player.Position posI = Formation.Positions[i].ID;
            for (int j = i + 1; j < n; j++)
            {
                if (!Chemistry.ArePositionsLinked(posI, Formation.Positions[j].ID)) continue;

                ChemistryLevel level = Chemistry.GetLevel(xi[i], xi[j]);
                if (level == ChemistryLevel.Neutral) continue;

                int step = Chemistry.StatDelta(level);
                foreach (PlayerStat stat in Chemistry.AffectedStats(xi[i], xi[j]))
                {
                    int s = (int)stat;
                    if (s < 0 || s >= Player.SKILL_NO) continue; // Height has no skill slot
                    deltas[i][s] += step;
                    deltas[j][s] += step;
                }
            }
        }

        _chemistryDeltas = deltas;
    }

    /// <summary>The cached chemistry delta for a starter's stat this match (0 for subs / before kickoff / Height).
    /// Folded into effective ability by <see cref="PlayerExtensions.AverageStats"/>.</summary>
    public int ChemistryDelta(Player player, PlayerStat stat)
    {
        if (_chemistryDeltas == null || player == null || stat == PlayerStat.Height) return 0;
        int idx = player.GetTeamIndex();
        if (idx < 0 || idx >= _chemistryDeltas.Length) return 0;
        int s = (int)stat;
        if (s < 0 || s >= Player.SKILL_NO) return 0;
        return _chemistryDeltas[idx][s];
    }

    // ————————————————————— in-match control —————————————————————

    public void BeginMatch()
    {
        InMatch = true;
        IsHalfTime = false;
        InMatchMentalityShift = 0;
        RecalculateStats();
    }

    public void EndMatch()
    {
        InMatch = false;
        IsHalfTime = false;
        InMatchMentalityShift = 0;
        RecalculateStats();
    }

    /// <summary>
    /// "More attacking / more defensive" nudge available at any time during a match (request 2). Clamped
    /// so the effective mentality stays in range. Returns the resulting effective mentality.
    /// </summary>
    public Mentality ShiftMentalityInMatch(int delta)
    {
        int maxShift = GetEnumLength<Mentality>() - 1 - (int)BaseMentality;
        int minShift = -(int)BaseMentality;
        InMatchMentalityShift = Mathf.Clamp(InMatchMentalityShift + delta, minShift, maxShift);
        RecalculateStats();
        return EffectiveMentality;
    }

    // ————————————————————— stat assembly —————————————————————

    private void ResetStats()
    {
        Intensity = Control = Stability = Pressure = Security =
        Threat = Creativity = DefensiveWidth = AttackingWidth = Fouling = Provoking = 50;

        Complexity = 20;
        Tempo = 50;
    }

    /// <summary>
    /// Base-replace: the chosen formation sets the BASE for the stats it defines (Complexity, Intensity,
    /// Control, Threat, Security) plus the engine Tempo, on top of which instructions + mentality then layer.
    /// Stats with no formation equivalent (Stability, Pressure, Creativity, the widths, Fouling, Provoking)
    /// keep their neutral 50. So e.g. 5-3-2 starts defensive (Security 65 / Threat 35) and 4-3-3 attacking.
    /// </summary>
    private void ApplyFormationBase()
    {
        if (Formation == null) return;

        var fs = Formation.Stats;
        Complexity = fs.Complexity;
        Intensity = fs.Intensity;
        Control = fs.Control;
        Threat = fs.Threat;
        Security = fs.Security;
        Tempo = fs.Tempo;
    }

    public int GetStat(TacticStat stat) => GetStatRef(stat);

    private ref int GetStatRef(TacticStat stat)
    {
        switch (stat)
        {
            case TacticStat.Complexity: return ref Complexity;
            case TacticStat.Intensity: return ref Intensity;
            case TacticStat.Control: return ref Control;
            case TacticStat.Stability: return ref Stability;
            case TacticStat.Pressure: return ref Pressure;
            case TacticStat.Security: return ref Security;
            case TacticStat.Threat: return ref Threat;
            case TacticStat.Creativity: return ref Creativity;
            case TacticStat.DefensiveWidth: return ref DefensiveWidth;
            case TacticStat.AttackingWidth: return ref AttackingWidth;
            case TacticStat.Fouling: return ref Fouling;
            case TacticStat.Provoking: return ref Provoking;
            default: throw new System.ArgumentOutOfRangeException(nameof(stat), stat, null);
        }
    }

    private void ModifyStat(TacticStat stat, int value)
    {
        ref int s = ref GetStatRef(stat);
        s += value;
    }

    public void RecalculateStats()
    {
        ResetStats();
        ApplyFormationBase(); // the formation sets the base; instructions + mentality layer on top

        List<TacticInstruction.TacticDependency> tacticDependencies = new();

        // 1. Stackable instructions.
        foreach (var instruction in Instructions)
        {
            if (instruction == null) continue;
            foreach (var mod in instruction.statModifications)
                ModifyStat(mod.stat, mod.value);

            foreach (var tacDep in instruction.tacticDependencies)
                tacticDependencies.Add(tacDep);
        }

        // (Reliances are ordinary instructions handled in step 1 — their stat effects are authored
        //  statModifications; their per-player weighting is applied at match time in AverageStats, not here.)

        // 1b. Complementary synergies — an instruction grants extra perks when a partner it pairs well with is
        //     also active. Runs after step 1 so the full active set is known and the order doesn't matter.
        foreach (var instruction in Instructions)
        {
            if (instruction?.complementaryInstructions == null) continue;
            foreach (var comp in instruction.complementaryInstructions)
            {
                if (comp.instruction == null || comp.perks == null) continue;
                if (!Instructions.Contains(comp.instruction)) continue;
                foreach (var perk in comp.perks)
                    ModifyStat(perk.stat, perk.value);
            }
        }

        // 2. Overall mentality — one dial spanning very defensive → very open.
        ApplyMentality();

        // 3. Floors/ceilings from instruction dependencies.
        ApplyTacticDependencies(tacticDependencies);

        // 4. Clamp everything to a sane range.
        foreach (TacticStat stat in Enum.GetValues(typeof(TacticStat)))
        {
            ref int s = ref GetStatRef(stat);
            s = Mathf.Clamp(s, 0, 100);
        }

        // 5. Refresh familiarity (current settings vs their habituated weights).
        RecomputeFamiliarity();
    }

    /// <summary>Shifts a band of stats by the mentality step (-3…+3). Extremes also cost stability.</summary>
    private void ApplyMentality()
    {
        int step = MentalityStep;
        if (step == 0) return;

        ModifyStat(TacticStat.Threat, step * 8);
        ModifyStat(TacticStat.Security, step * -8);
        ModifyStat(TacticStat.Intensity, step * 4);
        ModifyStat(TacticStat.AttackingWidth, step * 4);
        ModifyStat(TacticStat.Creativity, step * 3);
        ModifyStat(TacticStat.Control, step * 2);
        ModifyStat(TacticStat.Pressure, step * 3);
        // The further from balanced (either way), the more chaotic — extremes are less stable.
        ModifyStat(TacticStat.Stability, -Mathf.Abs(step) * 3);
        // Going gung-ho tends to invite fouls under pressure at the back.
        if (step > 0) ModifyStat(TacticStat.DefensiveWidth, step * 3);
        // Mentality extremes are conceptually harder to pull off.
        Complexity += Mathf.Abs(step) * 2;
    }

    private void ApplyTacticDependencies(List<TacticInstruction.TacticDependency> tacticDependencies)
    {
        Dictionary<(TacticStat stat, bool inverse), TacticInstruction.TacticDependency> dependencyMap = new();

        foreach (var dep in tacticDependencies)
        {
            var key = (dep.stat, dep.inverse);

            if (dependencyMap.TryGetValue(key, out var existing))
            {
                if (dep.inverse)
                {
                    if (dep.minimumValue < existing.minimumValue)
                        dependencyMap[key] = dep;
                }
                else
                {
                    if (dep.minimumValue > existing.minimumValue)
                        dependencyMap[key] = dep;
                }
            }
            else
            {
                dependencyMap[key] = dep;
            }
        }

        List<TacticInstruction.TacticDependency> sortedList = dependencyMap.Values.OrderBy(d => d.inverse ? 0 : 1).ToList();

        foreach (var dep in sortedList)
        {
            ref int stat = ref GetStatRef(dep.stat);
            if (stat < dep.minimumValue && !dep.inverse)
                stat -= (dep.minimumValue - stat);
            if (dep.inverse && stat > dep.minimumValue)
                stat = stat + (dep.minimumValue - stat);
        }
    }

    // ————————————————————— serialization —————————————————————

    /// <summary>Snapshot the persistent part of the tactic (formation, instructions, reliance player bindings,
    /// mentality, familiarity weights) for saving. Player ordering is persisted separately via Team.Players.</summary>
    public TacticState CaptureState()
    {
        return new TacticState
        {
            FormationName = Formation != null ? Formation.name : null,
            InstructionNames = Instructions.Where(i => i != null).Select(i => i.name).ToList(),
            RelianceSlots = new Dictionary<string, int>(RelianceSlots),
            MentalityIndex = (int)BaseMentality,
            SettingWeights = new Dictionary<string, float>(SettingWeights)
        };
    }

    /// <summary>Restore a saved snapshot. Applied directly (no familiarity penalties — this isn't a live edit).</summary>
    public void ApplyState(TacticState state)
    {
        if (state == null) return;

        if (!string.IsNullOrEmpty(state.FormationName))
        {
            Formation resolved = ResolveFormation(state.FormationName);
            if (resolved != null) Formation = resolved;
        }

        if (state.InstructionNames != null)
        {
            var all = Resources.LoadAll<TacticInstruction>("Tactics/Instructions");
            Instructions = state.InstructionNames
                .Select(n => Array.Find(all, i => i.name == n))
                .Where(i => i != null)
                .ToList();
        }

        RelianceSlots = state.RelianceSlots ?? new Dictionary<string, int>();
        _relianceDrilled = new Dictionary<string, int>(); // rebuilt by RefreshReliances; no load penalty
        BaseMentality = (Mentality)Mathf.Clamp(state.MentalityIndex, 0, GetEnumLength<Mentality>() - 1);
        SettingWeights = state.SettingWeights ?? new Dictionary<string, float>();

        RecalculateStats();
    }

    private static Formation ResolveFormation(string name)
    {
        var usable = Resources.LoadAll<Formation>("Formations/Usable");
        var found = Array.Find(usable, f => f.name == name);
        if (found != null) return found;

        var other = Resources.LoadAll<Formation>("Formations/Other");
        return Array.Find(other, f => f.name == name);
    }
}

/// <summary>Serializable snapshot of a <see cref="Tactic"/> (see <see cref="Tactic.CaptureState"/>).</summary>
[Serializable]
public class TacticState
{
    public string FormationName;
    public List<string> InstructionNames = new();
    public Dictionary<string, int> RelianceSlots = new();
    public int MentalityIndex = (int)Mentality.Balanced;
    public Dictionary<string, float> SettingWeights = new();
}
