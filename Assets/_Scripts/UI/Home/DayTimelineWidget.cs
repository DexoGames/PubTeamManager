using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Home-screen timeline strip: a row of day boxes, today (lead, large) on the left and the
/// next few days (small) to the right, each showing an icon for that day's activity.
/// On a day advance the whole strip slides one slot left (DOTween) — today exits left, the
/// rest shift down a slot (and the new lead grows), and a fresh far day slides in from the
/// right. A reserved "spare" box gives the incoming day a clean entrance from off-screen.
/// </summary>
public class DayTimelineWidget : MonoBehaviour
{
    [SerializeField] private RectTransform container;
    [SerializeField] private DayBox boxPrefab;
    [SerializeField] private int visibleDays = 8;

    [Header("Slot sizing")]
    [SerializeField] private float leadWidth = 220f;
    [SerializeField] private float smallWidth = 110f;
    [SerializeField] private float spacing = 12f;
    [Tooltip("Height of the lead (today) box.")]
    [SerializeField] private float leadHeight = 200f;
    [Tooltip("Height of the smaller future-day boxes.")]
    [SerializeField] private float smallHeight = 130f;

    [Header("Animation")]
    [SerializeField] private float slideDuration = 0.35f;

    [Header("Day-type icons")]
    [SerializeField] private Sprite matchIcon;
    [SerializeField] private Sprite trainingIcon;
    [SerializeField] private Sprite interviewIcon;
    [SerializeField] private Sprite pubTripIcon;
    [SerializeField] private Sprite restIcon;

    private List<DayBox> boxes = new List<DayBox>(); // visible, ordered slot 0..N-1
    private DayBox spare;                            // parked off-right, used for entrances
    private Tween pendingReconcile;
    private bool initialised;

    // ————————————————————— layout math —————————————————————

    private float SlotX(int slot) => slot <= 0 ? 0f : leadWidth + spacing + (slot - 1) * (smallWidth + spacing);
    private Vector2 SlotSize(int slot) => new Vector2(
        slot == 0 ? leadWidth : smallWidth,
        slot == 0 ? leadHeight : smallHeight);
    private float OffLeftX => -(leadWidth + spacing);

    // ————————————————————— public API —————————————————————

    /// <summary>Lays the strip out instantly from today's schedule (no animation).</summary>
    public void SnapToToday()
    {
        EnsureBoxes();
        KillAll();

        var data = GetUpcoming();
        for (int i = 0; i < visibleDays; i++)
        {
            boxes[i].SetVisible(true);
            boxes[i].SetSlotInstant(new Vector2(SlotX(i), 0f), SlotSize(i));
            Populate(boxes[i], data, i, isLead: i == 0);
        }
        spare.SetVisible(false);
    }

    /// <summary>Slides the strip one slot left for a day advance, then reconciles.</summary>
    public void AdvanceAnimated(Action onComplete = null)
    {
        EnsureBoxes();
        if (ScheduleManager.Instance == null)
        {
            SnapToToday();
            onComplete?.Invoke();
            return;
        }

        KillAll();
        var data = GetUpcoming(); // already reflects the advanced day

        DayBox exiting = boxes[0];

        // Incoming far day: park the spare at the off-right slot, then slide it into the last slot.
        spare.SetVisible(true);
        spare.SetSlotInstant(new Vector2(SlotX(visibleDays), 0f), SlotSize(visibleDays - 1));
        Populate(spare, data, visibleDays - 1, isLead: false);
        spare.AnimateToSlot(new Vector2(SlotX(visibleDays - 1), 0f), SlotSize(visibleDays - 1), slideDuration);

        // Today's box slides off to the left and fades out.
        exiting.AnimateToSlot(new Vector2(OffLeftX, 0f), SlotSize(0), slideDuration);
        exiting.FadeOut(slideDuration);

        // Everything else shifts down one slot (slot 1 grows into the lead slot 0).
        for (int i = 1; i < visibleDays; i++)
            boxes[i].AnimateToSlot(new Vector2(SlotX(i - 1), 0f), SlotSize(i - 1), slideDuration);

        // Reorder the logical lists: drop the exiting box, append the spare; the exiting box
        // becomes the new spare for next time.
        var reordered = new List<DayBox>(visibleDays);
        for (int i = 1; i < visibleDays; i++) reordered.Add(boxes[i]);
        reordered.Add(spare);
        boxes = reordered;
        spare = exiting;

        // After the slide, snap to exact positions + data (idempotent) and park the spare.
        pendingReconcile = DOVirtual.DelayedCall(slideDuration, () =>
        {
            SnapToToday();
            onComplete?.Invoke();
        });
    }

    // ————————————————————— internals —————————————————————

    private void EnsureBoxes()
    {
        if (initialised) return;
        for (int i = 0; i < visibleDays; i++) boxes.Add(CreateBox());
        spare = CreateBox();
        spare.SetVisible(false);
        initialised = true;
    }

    private DayBox CreateBox()
    {
        DayBox box = Instantiate(boxPrefab, container);
        RectTransform rt = box.Rect;
        // Top-left anchored: anchoredPosition.x is the box's left edge, and boxes hang DOWN from
        // a common top, so the lead box can be taller than the small ones with their tops aligned.
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        return box;
    }

    private List<ScheduleEntry> GetUpcoming()
    {
        return ScheduleManager.Instance != null
            ? ScheduleManager.Instance.GetUpcoming(visibleDays)
            : new List<ScheduleEntry>();
    }

    private void Populate(DayBox box, List<ScheduleEntry> data, int index, bool isLead)
    {
        ScheduleEntry entry = (data != null && index < data.Count) ? data[index] : null;
        string label = entry != null
            ? (isLead ? entry.Date.ToString("ddd d MMM") : entry.Date.ToString("ddd"))
            : "";
        Sprite icon = entry != null ? IconFor(entry.Type) : null;
        box.SetData(label, icon, isLead);
    }

    private Sprite IconFor(ScheduleEntryType type)
    {
        switch (type)
        {
            case ScheduleEntryType.Match: return matchIcon;
            case ScheduleEntryType.Training: return trainingIcon;
            case ScheduleEntryType.Interview: return interviewIcon;
            case ScheduleEntryType.PubTrip: return pubTripIcon;
            default: return restIcon;
        }
    }

    private void KillAll()
    {
        pendingReconcile?.Kill();
        pendingReconcile = null;
        foreach (var b in boxes) b.KillTweens();
        if (spare != null) spare.KillTweens();
    }

    private void OnDisable() => KillAll();
}
