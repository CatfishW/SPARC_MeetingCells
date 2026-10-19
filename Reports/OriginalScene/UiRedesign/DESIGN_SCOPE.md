# Meeting Cells UI design scope

This is the accepted scope for the October 17 continuation and October 18 visual UI follow-up. The main interface receives a complete sketch-paper redesign. The Red oxygen activity, White defense activity and word worksheet keep their source educational identity and rules; later approved pixel-and-sketch assets and notebook presentation replace selected artwork.

## Main interface

Warm paper surfaces, ink outlines, clean readable type and restrained teal accents apply to the Start card, spoken dialogue, interaction prompts, task HUD, chat and feedback panels, settings, character controls, navigation, tooltips and standalone loading chrome. This includes inactive controls, nested prefabs and controls instantiated during play. The world navigation badges use the same visual language.

The owned icon library contains 61 semantic definitions. It supplies the main interface where appropriate; the count is a library inventory, not a claim that all 61 icons appear in the minigames. Friendly dialogue labels such as “Red” and “White” affect display only. Original speaker identifiers, spoken strings, line IDs and voice references are retained. Intro subtitles wrap within the viewport with readable phone text; their native text, vertical anchors, visibility and Timeline activation remain intact. The responsive toolbar fits before Start as well as during gameplay.

The separate `Subtitle1_CAP` and `TIP` teaching-cue families use light outlined text, 20 CSS-pixel bodies and 28-pixel titles. Their full native text wraps with comfortable margins; child geometry responds to the safe viewport while the bound Content parent's entrance motion remains intact. Typewriter progress, native alpha, CanvasGroup values and activation remain source controlled. Waiting text/spinner and world-space teleport text receive readable light styling with their existing geometry.

## Native activities and approved art changes

| Surface | Preserved | Presentation changes |
| --- | --- | --- |
| Red oxygen activity |120-second timer, source movement/pickup/joints, score rules, original native controls | Generated red cell/oxygen/vessel art; wider targets with matched colliders; readable HUD, collapsible source Help and visual reward feedback |
| White defense activity |250-second timer, three levels, five hostiles, native friendly penalties, result/loading sequence | Generated white cell/bacteria/red cell/vessel art; visual-only motion; radius-aware spawn spacing; native Help and bounded reward effects |
| Word worksheet |13 page objects,14 slots, all words/questions, source diagrams, field rules and Submit callback | Notebook layout on every viewport; generated cover illustration; redesigned tiles/slots and full-width scroll hit rails |

The general UI theme protects these three hierarchies from unrelated main-interface styling. Their dedicated presentation adapter owns the approved replacements; dynamic art is protected from being overwritten by the general theme. Original source geometry/physics remain except the explicitly requested delivery-target spacing/size and collider-center correction. No score multipliers or new grading rules are introduced.

Both timed activities briefly color the existing score label green for a positive score change or red for a negative change, returning toward its original color over 0.65 seconds. Their timer label changes to a warning color during the last 15 seconds. These are presentation cues: the oxygen activity still lasts 120 seconds, and White still uses the scene's 250-second limit, three levels and five hostile bacteria per level.

White's loading tips follow the source order; its original loss overlay remains, with the approved germ art. A win waits for the native Continue button; a loss retains the source five-second continuation. Hostile bacteria and helpful Red use their distinct authored movement parameters, and spawn spacing consumes the original crowding parameter. The worksheet still has 13 pages and 14 drag/drop slots.

## Objective layout and mobile interaction

The objective is one responsive paper card with a small context title, prominent current instruction, native fold action and compact completion badge in the header. Utility actions retain at least 44 CSS-pixel targets. Settings hides the objective while open and returns through its native Back action.

Mobile movement and camera look own separate fingers. Analog movement has a dead zone and smoothing, with a visible thumb handle; look uses viewport-normalized deltas. Movement targets are at least 128 CSS pixels and Jump is 76 CSS pixels. Rotation, browser cancellation, focus loss and modal transitions release held input. Layout watches safe-area, viewport, device-pixel density and CanvasScaler changes, including scale-only settling after startup. Touch rendering caps DPR at 1.25 to limit GPU load.

The existing third-person camera's two native obstacle solvers ignore the player capsule's actual tag while retaining their environment masks. A post-brain visibility guard temporarily suppresses only the PlayerModel render bit when the actual output camera enters the body's 0.85-meter safety margin during native blends or wall occlusion. It keeps the native rig/animation active, honors explicit show/hide intent and unregisters on scene destruction. No authored camera transforms, lenses, distances, blend settings or Timeline assets are changed.

The browser's native Unity edit widget has scoped paper/ink/teal styling, 16 CSS-pixel text, at least 52-pixel input height and a 76×52-pixel OK button. An observer recognizes the exact engine-owned input/button references on the direct body container; it does not replace nodes, values or event handlers. Multiline height remains native. CSS reserves safe-area padding; nonzero notch layout and real soft-keyboard behavior are not certified by desktop emulation.

Timed minigames retain their native Input System joystick, placed bottom-left at 104 CSS pixels on compact screens or 128 on larger touch screens, within reserved space outside the board and instruction panel. Their arena and source outer walls scale together; source movement/physics and scoring stay original, with explicitly resized/recentered delivery targets. Instructions can scroll without covering Exit. Worksheet mobile targets are at least 44 CSS pixels, with readable text and scrollable original content; selecting and dragging remain answer-preserving operations. Placed words match their native blank's visible center and bounds regardless of pivot, and reflow cancels old movement animations before restoring assigned words or bank positions. The original Tutorial field keeps its 16-character alphanumeric rule. No new page content or grading rules are introduced.

## October 18 visual follow-up

The activity adapter now uses viewport-aware layout on desktop as well as mobile. Wide screens use two columns; square/tall screens stack the unchanged logical arena above the original instruction panel. Approved generated subject icons sit uniformly beside the source text. Instructions use a clipped scroll viewport when needed; score, time and Exit remain reachable. Loading, exit and result overlays follow the same viewport bounds while keeping their native callbacks and activity-specific palettes.

Play RBC/WBC uses each original native button and listener, capped at320 by52 CSS pixels and placed lower-center with reserved mobile control space. It hides while spoken dialogue owns the screen. Overlay canvases no longer inherit world rotation/scale from their NPC parent.

Quiz, peer-input and feedback forms are adapted on every viewport. The explanation card uses a compact lower-screen field, original placeholder/character rule and a visible48-pixel Send action bound to the original unified peer Submit contract. Empty/pending/inactive LAM forms cannot submit. Modal forms pause player movement and suppress the foreground avatar render bit without disabling its rig. Feedback has an explicitly opaque owned paper background, content-sized height, a bounded scroll viewport and labeled Try again/Continue actions. The source unavailable-service fallback is retained.

The final gallery manifest records the exact current native and browser capture counts. It covers local Start/loading, HUD, settings/tooltips, story captions/cues, dialogue, launchers, both minigames and secondary overlays, quiz, input/waiting/feedback/retry, all13 worksheet pages and completion/replay. Source hashes, per-image hashes and separate native/browser review reports bind the review. Native result/loading and inactive LAM previews are presentation coverage; they do not imply an input-driven win or an active legacy integration.

## Original moving RBC crowd

The enabled source NPC spawner uses its authored five spawn points, one-second interval and five-actor cap. It clones the scene's original blocking RBC instance, retains its native solid capsule/navigation settings and original effect/sound references, and replaces actors after their authored randomized lifetime. Collision applies the original push profile while player input remains responsive. The separately disabled source spawner stays disabled. Existing source walkers and transient RBC bursts use their original path and lifetime parameters.

## Narrative, media and lighting boundaries

The source graph remains 29 blocks, 416 commands and 107 original dialogue/voice records. Native callbacks, objective sequence, Timeline assets and animation bindings retain their authored roles. The external Heart module remains an integration boundary.

The canonical Heart transition latches a terminal state: world movement, jumping, nearby interaction, delayed talk and Settings cannot return after the handoff. The source ending still plays its full 20.3167 seconds. Completion requires retained native progress through that duration and a stopped director; an early stop stays incomplete. A responsive paper completion card then offers **Play again**, which shuts down the old runtime and reloads only the current included scene. It adds no Heart gameplay or new narrative branch.

Educational diagrams and video/render-texture content remain original. Full-screen intro, vessel and Heart-ending cutouts retain their original Graphic materials and opacity; animated `cutoutSize` and distortion contracts are excluded from paper styling. CanvasGroup opacity and gameplay colliders are not theme controls.

`OriginalLighting` supplies neutral ambient fill, softer source shadows, muted fog and a restrained private color grade. It avoids the imported non-sky material and inherited bloom/vignette without editing original material assets, environment geometry or camera tracks.

## Coverage and verification

The inventory covers 746 explicit UI objects across the scene and retained prefab sources. Runtime coverage includes 163 native Buttons, 635 native Graphics and the dynamically instantiated prefab Graphics in the recorded audit. These counts describe the inspected surfaces, including deliberately preserved minigame/media/story Graphics; they do not mean every Graphic was replaced.

`../ui-inventory.json` records the source inventory. This directory contains icon metrics/atlas, focused Unity evidence and runtime coverage snapshots. IMGUI Start/dialogue/Talk/diagnostics and browser loading chrome are separate dynamic surfaces and require visual verification in addition to native Graphic coverage.

Current verification totals, exact fingerprints and browser directories are recorded in [CURRENT_STATUS.md](../CURRENT_STATUS.md) and the joined `delivery-audit.json`. Earlier passed builds and failed exploratory runs remain historical; only matching input and artifact hashes establish current acceptance.

The native Settings pointer test includes desktop and 960×600 views plus the original Back control. The multiplayer chat launcher did not expose a local panel in the isolated player; styled chat surfaces are not accepted multiplayer functionality. External Heart, live signed-in PING outfits, physical mobile/Safari and every optional branch remain outside this scope. An unavailable peer provider follows the original fallback. Recordings are silent and do not establish audio acceptance.

## Approved interaction and minigame follow-up

The later user request authorizes seven built-in imagegen replacements in a pixel-and-ink-sketch style. This supersedes the earlier artwork-preservation boundary for timed-game subjects/backgrounds and the notebook cover. Original educational diagrams, all source questions/words, native callback objects and rules remain. Asset paths, hashes and full prompts are recorded in `../GeneratedArt/`.

The native three-character interaction controllers, line references and two original option buttons are reused. A per-character outline and curved connector identify the nearby actor; Talk/Observe opens through the existing source graph. Leaving range and modal/story transitions suppress the card and restore exact source material arrays.

Oxygen uses208-unit delivery targets at58-unit insets, giving432 units between their visible edges instead of224. Collider centers follow the source RectTransform center. Timed-game subjects breathe, tilt and pulse through visual-only children. Twenty reusable pixel particles bound effect allocation; clean-streak cues never multiply source scores. Help is a native toggle over the original instructions. White spawn spacing accounts for actual source actor radii without changing counts.

All13 worksheet pages now use a responsive notebook layout on desktop and mobile. Source page objects,14 slots, words, diagrams, input limits and Submit stay intact. Placed words leave the bank, selection is visible, scroll rails retain44-pixel hit regions, and touch cancellation releases even queued native drags. Return positions use top-left anchored coordinates so a first-time bank pivot/size change cannot displace every word after cancellation.

The objective card has one explicit content-size owner rather than nested native fitters reserving blank space. A short task is about120 CSS pixels tall, the actual source completion count occupies a header badge, and only decorative ink pulses/reveals when the task or completion count changes. Native fold callbacks and objective text/state remain intact.
