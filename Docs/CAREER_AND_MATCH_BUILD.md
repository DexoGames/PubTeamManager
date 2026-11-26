# Career and match flows

The controls are assigned in `Assets/Scenes/Menu.unity`. The scene opens the pub picker for a new career and Home for a loaded one.

| Surface | Wiring and behaviour |
| --- | --- |
| Career | The navigation button calls `UIManager.ShowStats`. `StatsPageUI` owns two leaderboard instances, a vertical list, a TMP search input and the History, New career and Return buttons. Starting a new club preserves the previous autosave directory under a dated name. |
| Recruitment | `RecruitmentPageUI.questionMenu` is a single TMP dropdown. Interview starts a conversation and becomes Ask. Signing at capacity uses the existing player-picker prefab. Leaving and reloading restores the candidate, questions and last answer. |
| Training | Five `PlayerSlotUI` instances share the training slot prefab. Its secondary label displays positional fit. Tactical training advances familiarity twice and lets skill boosts decay. |
| Half-time | The team talk and private conversation have separate response managers. Cancel closes the private conversation. Done returns to the match. The tactics Resume button calls `TacticsPageUI.ResumeMatch`. |
| Medical news | InjuryManager references the Injury, Sick and Death event assets. Both teams serve bans, incur injuries and recover. Home displays the human club's affected players. |
| Concerns | SelectionHistory records available bench spells and difficult starts under a particular setup. A concern offers a specific next-match promise. An eligible fixture resolves it and updates the inbox. |
| Results | The archive browser shows season, competition, round and match. Drawn knockout matches store the deciding club. Full saves commit a live season before the core manifest. Old saves can use the original season filename. |

## Editor acceptance checks

- Start a career by postcode. Reload after asking a comparison question. At the squad cap, cancel a release, then confirm one and check both players' profiles.
- During half-time, deliver a talk, close it, return for a private word, edit tactics and resume. Check card dimensions and click targets at the smallest supported resolution.
- Advance a completed league through both playoff rounds. Reload between rounds and open the completed season from Career after rollover.
- Compare score, foul and fatigue distributions over several seasons before settling the new tuning.

## Screen layout and controls

The half-time roster fits five columns and five rows. Cards show role, surname, morale face and reaction. Hover a player to read their remark. After the squad talk, select a card for a private conversation. The home squad-status counts open a filtered player picker.

Schedule shows 21 days in chronological rows of seven, with the same activity sprites as Home. Match cells identify the opponent and venue. Training uses three columns of drills and a boost gauge showing the squad average against the cap. Both grids size their cells from the available panel. On Tactics the gauges show their values and the summary shows whether the squad can manage the required intelligence.

The match mentality slider is available during play and at the break. Paused and half-time adjustments are silent. A live touchline announcement is recorded when the change takes effect during a simulated minute.
