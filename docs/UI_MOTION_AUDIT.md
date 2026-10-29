# UI motion audit against Emil Kowalski's design-engineering skill (28 Oct 2026)

Source: Emil Kowalski's public skill repository (github.com/emilkowalski/skill): `emil-design-eng`, `review-animations/STANDARDS.md` and `ui-cheatsheet.md`. The skill was written for CSS on the web. Here it is applied to Unity uGUI: easing curves become `Logic.Easing`, transitions become `UI.UiTween`, `:active` becomes `ButtonPop`, and tabular numbers use TextMesh Pro `<mspace>`.

| Before | After | Why |
| --- | --- | --- |
| `ButtonPop` pressed to 0.92 and overshot on release (velocity kick) | 0.96 press, critically damped spring (settles in about 120 ms), no overshoot | Press feedback should be subtle (0.95–0.98). A bounce on something tapped hundreds of times reads as a gimmick. |
| `OverlayPop`: scale 0.90 → 1, scale only, 140 ms; scrim snapped on | Scale 0.97 + opacity 0 → 1 over 200 ms on cubic-bezier(0.23,1,0.32,1); the scrim fades in with it | Never appear from far below full size; pair scale with opacity; use a strong custom ease-out; menus open often, so keep it quiet. Modals stay centred. |
| City cards (bottom sheets) appeared in one frame | Rise by their own height on the drawer curve (0.32,0.72,0,1), 260 ms; scrim fades | A sheet should come from the edge it lives on (spatial consistency); things appearing with no transition feel broken. |
| Cards destroyed instantly on close | 140 ms fade + 24 px drop; stops taking taps at once | Exits are faster than entrances, but they shouldn't vanish in one frame. |
| Card → next card (menu → submenu) | No entrance animation, it just changes | High-frequency navigation shouldn't replay an entrance every step. |
| City and match toasts, drive banners: text snapped on/off | Rise 12–14 px + fade in (180 ms ease-out); drift up and fade out (140 ms) | Prevents jarring changes; asymmetric timing (out quicker than in). |
| Commentary ("MIC") ticker: SetActive on/off | Fades and slides in, fades out before hiding; interruptible | Same rule. A new line during the fade-out retargets instead of flashing. |
| Comic page turn swapped in one frame | New page slides 28 px from the right and fades (160 ms) | Spatial consistency: pages move forward in reading direction. |
| World map +/−, ME, ALL jumped in a frame | Glide 300 ms on the strong ease-in-out (0.77,0,0.175,1), zoom interpolated in log space | On-screen movement uses ease-in-out; log-space zoom feels even at every scale. |
| Map drag stopped dead on release; hard wall at the edge | Momentum fling with friction; rubber-band resistance past the edge, springs back | Gestures: a flick should be enough; friction instead of hard stops; damping at boundaries. |
| Map: any touch during a glide fought it | A touch stops glides and flings at once | Interruptibility: the user's finger always wins. |
| Map: keyboard zoom | Stays instant | Never animate keyboard-initiated, repeated actions. |
| Map district frame pulsed constantly; YOU pulsed ±12% fast | Frame is a still gold outline; YOU breathes ±6%, slower | Constant decorative motion on something always on screen is noise. |
| Map tap sheet appeared in one frame / vanished | Rises on the drawer curve; a new pick swaps it without replaying the entrance; closes in 180 ms | Sheet behaviour, and no re-entrance on rapid re-picks. |
| Score bump: jumped to 140% on the frame you scored, settled in 0.3 s | Rises to 115% over the first 60 ms (ease-out), settles by 0.24 s | It happens dozens of times a game: smaller, quicker, and no single-frame jump. |
| Cash banner: 0.15 s in, 0.6 s linear out | 0.18 s in, 0.2 s out, both strong ease-out | Linear isn't for UI; exit faster than enter. |
| Game intro card faded out over 0.4 s linear | 0.25 s ease-out | Same. |
| Screen wipe: 0.32 s linear, both ways | Cover 0.26 s ease-in-out, reveal 0.24 s ease-out | Under 300 ms; eased; it answers sooner. Players change districts and screens constantly. |
| Drive clock, speed and errand timer digits jittered sideways | Fixed-width digits (`<mspace=0.58em>`) | Tabular numbers for values that change every frame. |
| Guide arrow bobbed ±10 px fast, even with Reduce Motion | ±6 px, slower; still with Reduce Motion | Gentler decorative motion; reduced motion removes movement and keeps fades. |
| Easing written ad hoc (`1-(1-t)^3`, linear, quadratic) in many places | One `Easing` set: Out (0.23,1,0.32,1), InOut (0.77,0,0.175,1), Drawer (0.32,0.72,0,1), tested | Built-in or ad-hoc curves are too weak; cohesion comes from one vocabulary. |

Reduce Motion everywhere: `UiTween` keeps opacity fades and drops slides and scales. The map glide becomes an instant move. Flings are off. `ButtonPop` doesn't press.

Not covered: in-game court animation (players, ball, camera) and the shot meter. Those are gameplay feel, not UI chrome.
