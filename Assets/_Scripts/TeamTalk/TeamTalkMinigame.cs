using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base for a half-time "team talk" microgame. Each game is pure logic (no Unity rendering) so it can be
/// driven by any UI and unit-tested. The shared contract is a 0–1 <see cref="Score"/> (quality of the
/// player's solution) and an <see cref="IsComplete"/> flag; <see cref="TeamTalkController"/> turns the
/// final score into a morale swing — do well and you fire the team up, fluff it and you flatten them.
///
/// To add a new microgame: subclass this, implement Score/IsComplete, and add it to
/// <see cref="TeamTalkController.CreateRandom"/>. The design brief: easy to finish, hard to perfect quickly.
/// </summary>
public abstract class TeamTalkMinigame
{
    public abstract string Title { get; }
    public abstract string Instructions { get; }

    /// <summary>Quality of the current solution, 0 (awful) … 1 (perfect).</summary>
    public abstract float Score { get; }

    /// <summary>True when the game has a finished, scorable solution.</summary>
    public abstract bool IsComplete { get; }
}

/// <summary>
/// "Rally the dressing room" gerrymander: a grid of cells is either ON-side (your colour) or against.
/// Carve the grid into equal, contiguous districts so as many districts as possible have an ON-side
/// majority. The board is generated slightly against you, so it takes clever boundary-drawing to win
/// most districts — easy to make a valid map, hard to win them all.
/// </summary>
public class GerrymanderGame : TeamTalkMinigame
{
    public int Size { get; }
    public int DistrictSize { get; }
    public int DistrictCount { get; }

    /// <summary>true = your colour (ON-side) for each cell.</summary>
    public bool[,] Friendly { get; }

    /// <summary>District index assigned to each cell, or -1 if unassigned.</summary>
    public int[,] District { get; private set; }

    public override string Title => "Win the Room";
    public override string Instructions =>
        $"Split the {Size}×{Size} grid into {DistrictCount} connected zones of {DistrictSize}. " +
        "Win a zone by having more of your colour in it. Win as many zones as you can!";

    public GerrymanderGame(int size = 6, int districtSize = 6)
    {
        Size = Mathf.Max(2, size);
        DistrictSize = Mathf.Max(1, districtSize);
        DistrictCount = (Size * Size) / DistrictSize;

        Friendly = new bool[Size, Size];
        District = new int[Size, Size];

        // Slightly against you (~45% friendly) so winning a majority of districts takes skill.
        int target = Mathf.RoundToInt(Size * Size * 0.45f);
        int placed = 0;
        while (placed < target)
        {
            int x = Random.Range(0, Size);
            int y = Random.Range(0, Size);
            if (!Friendly[x, y]) { Friendly[x, y] = true; placed++; }
        }

        ResetDistricts();
    }

    public void ResetDistricts()
    {
        for (int x = 0; x < Size; x++)
            for (int y = 0; y < Size; y++)
                District[x, y] = -1;
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;

    /// <summary>Assigns a cell to a district (pass -1 to clear). District index must be in range.</summary>
    public void AssignCell(int x, int y, int district)
    {
        if (!InBounds(x, y)) return;
        if (district < -1 || district >= DistrictCount) return;
        District[x, y] = district;
    }

    /// <summary>True if every district has exactly DistrictSize cells and is 4-connected.</summary>
    public bool IsValidPartition(out string error)
    {
        var counts = new int[DistrictCount];
        for (int x = 0; x < Size; x++)
            for (int y = 0; y < Size; y++)
            {
                int d = District[x, y];
                if (d == -1) { error = "Every cell must be in a zone."; return false; }
                counts[d]++;
            }

        for (int d = 0; d < DistrictCount; d++)
        {
            if (counts[d] != DistrictSize) { error = $"Zone {d + 1} must have exactly {DistrictSize} cells."; return false; }
            if (!IsContiguous(d)) { error = $"Zone {d + 1} must be one connected shape."; return false; }
        }

        error = null;
        return true;
    }

    private bool IsContiguous(int district)
    {
        // BFS from the first cell of this district; all of its cells must be reachable.
        int startX = -1, startY = -1, total = 0;
        for (int x = 0; x < Size; x++)
            for (int y = 0; y < Size; y++)
                if (District[x, y] == district)
                {
                    total++;
                    if (startX == -1) { startX = x; startY = y; }
                }

        if (total == 0) return false;

        var seen = new bool[Size, Size];
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        seen[startX, startY] = true;
        int reached = 0;

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            reached++;
            for (int i = 0; i < 4; i++)
            {
                int nx = c.x + dx[i], ny = c.y + dy[i];
                if (!InBounds(nx, ny) || seen[nx, ny]) continue;
                if (District[nx, ny] != district) continue;
                seen[nx, ny] = true;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        return reached == total;
    }

    /// <summary>Number of districts where your colour has a strict majority.</summary>
    public int DistrictsWon()
    {
        var friendly = new int[DistrictCount];
        var total = new int[DistrictCount];
        for (int x = 0; x < Size; x++)
            for (int y = 0; y < Size; y++)
            {
                int d = District[x, y];
                if (d < 0) continue;
                total[d]++;
                if (Friendly[x, y]) friendly[d]++;
            }

        int won = 0;
        for (int d = 0; d < DistrictCount; d++)
            if (total[d] > 0 && friendly[d] * 2 > total[d]) won++;
        return won;
    }

    public override bool IsComplete => IsValidPartition(out _);

    public override float Score => IsComplete && DistrictCount > 0
        ? (float)DistrictsWon() / DistrictCount
        : 0f;
}

/// <summary>
/// "Steady the ship" balance beam: a pivot with weighted kit bags. Place each bag a signed distance from
/// the pivot (negative = left, positive = right). Net torque of zero is a perfect, balanced talk; the
/// further out of balance, the worse the score. Easy to get roughly level, fiddly to nail dead-even.
/// </summary>
public class BalanceGame : TeamTalkMinigame
{
    public int[] Weights { get; }
    public int MaxDistance { get; }

    /// <summary>Signed distance of each box from the pivot. 0 means "not placed yet".</summary>
    public int[] Placement { get; private set; }

    public override string Title => "Steady the Ship";
    public override string Instructions =>
        $"Hang each weighted bag on the beam, up to {MaxDistance} either side of the pivot. " +
        "Balance the beam as level as you can.";

    public BalanceGame(int boxes = 4, int maxDistance = 5)
    {
        int count = Mathf.Max(2, boxes);
        MaxDistance = Mathf.Max(1, maxDistance);
        Weights = new int[count];
        Placement = new int[count];
        for (int i = 0; i < count; i++)
            Weights[i] = Random.Range(1, 10);
    }

    /// <summary>Places a box at a signed distance from the pivot (clamped to ±MaxDistance).</summary>
    public void Place(int boxIndex, int signedDistance)
    {
        if (boxIndex < 0 || boxIndex >= Placement.Length) return;
        Placement[boxIndex] = Mathf.Clamp(signedDistance, -MaxDistance, MaxDistance);
    }

    public int NetTorque()
    {
        int torque = 0;
        for (int i = 0; i < Weights.Length; i++)
            torque += Weights[i] * Placement[i];
        return torque;
    }

    private int MaxTorque()
    {
        int max = 0;
        for (int i = 0; i < Weights.Length; i++)
            max += Weights[i] * MaxDistance;
        return Mathf.Max(1, max);
    }

    /// <summary>Complete once every bag is on the beam (placed at a non-zero distance).</summary>
    public override bool IsComplete
    {
        get
        {
            for (int i = 0; i < Placement.Length; i++)
                if (Placement[i] == 0) return false;
            return true;
        }
    }

    public override float Score
    {
        get
        {
            if (!IsComplete) return 0f;
            float imbalance = Mathf.Abs(NetTorque()) / (float)MaxTorque();
            return Mathf.Clamp01(1f - imbalance);
        }
    }
}
