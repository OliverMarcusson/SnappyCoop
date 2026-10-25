# Snappy Coop

A [BTD6 Mod Helper](https://github.com/gurrenm3/BTD-Mod-Helper) mod that makes co-op stall less.

BTD6 co-op is lockstep: each player promises the others "I have no actions before step N" (a step signoff), and
everyone's simulation pauses when it reaches a promise that hasn't been extended yet. The game sends these every
0.25 s; this mod sends them every 0.05 s (configurable), so promises run out far less often. The wire protocol is
unchanged, so it works with unmodded players.

Install it from the in-game Mod Browser (search "Snappy Coop"), or drop `SnappyCoop.dll` from the latest release into
`BloonsTD6\Mods`. Mod Helper offers updates automatically.
