================
HS2 Smart Auto H
================

A smarter Auto mode for Honey Select 2 (BepInEx plugin).
Turn Auto on and the H scene plays itself: it picks positions with a purpose, follows your own stage order,
respects the girl's sex trait, synchronises the finish and keeps to what the game allows right now.

  Based on HS2TakeChargePlugin by OrangeSpork — the Take Charge / Auto mode framework this mod grew from.
  Thank you, OrangeSpork. This is a free, non-commercial fan mod.

Authors: DiKriy & Claude (Anthropic)

  ⚠ Conflicts with the original HS2TakeChargePlugin. Both plugins control the same Auto mode of the H scene.
  Running them together makes two autopilots fight over positions, speed and finishes. Remove or disable HS2TakeChargePlugin before using Smart Auto H.

FEATURES AT A GLANCE
--------------------

- Goals — tell Auto mode which feeling to raise (Favour, Enjoyment, Slavery, Aversion, Broken, Dependence), or pick FFM / MMF for scenes for three.
- Stages — your own order of the scene in up to 8 slots (e.g. SERVICE → CARESS → SPECIAL → SEX), with the number of positions per stage.
- Panel in the H scene — goal, Auto on/off, stage reset, toggles and finish choice, all with tooltips. No need to open the settings.
- Finish sync — whoever reaches the top first waits for the partner; finish together, inside, outside or random.
- Sex trait support — Sensitive Breasts / Ass / Pussy, Love Kisses, Clean Freak, Sadist, Masochist are taken into account, and the game's bug that counted the trait only in the last position of the scene is fixed.
- Anal toggle — keep anal positions out completely (and let every finish count as normal H experience).
- Masochism — pain positions with a time limit, so Auto mode never gets stuck in them.
- Sadism — positions where she leads go first.
- Whole location — positions from the whole map, or only from the spot where you are.
- Only what the game allows — event, map, her state, fainting and unlocked positions are respected (bath, toilet and similar events stay within their few positions).
- Game fixes — a new position chosen during the rest after a climax starts from the pause; a sex trait counts for any position of the scene, not only the last one.

THE PANEL
---------

The panel appears in the H scene at the top right, left of the game's position menu. Hover any element for a tooltip.

  Element                What it does
  --------------------   ---------------------------------------------------------------------------
  Goal (icons + name)    The feeling to raise. Click the arrows / side icons or use the mouse wheel.
  Auto                   Auto mode on / off.
  ⟲                      Start the stages again from Stage 1.
  T                      Sex Trait on / off.
  A                      Anal positions allowed / skipped.
  M                      Masochism: pain positions in the SPECIAL stage.
  S                      Sadism: positions where she leads go first.
  L                      Whole location / only the current spot.
  SEX / SERVICE finish   How the finish is chosen. Click or wheel to change.

Changes are used from the next position (the panel shows a short hint).

GOALS
-----

Every position in HS2 has a type, and the game's own table (Taii parameters) says how each type changes the girl's feelings.
Smart Auto H reads that table. For the chosen goal, positions are sorted into four groups and taken in this order:

1. Strong — raises the goal by +5…10
2. Mild — raises it by +1…5
3. Neutral — random effect, or the position has no type
4. Lowers — used only when nothing else is left

Inside a group the choice is random, and positions not used yet in this scene go first.

  Note: goals follow the game's own numbers, not appearances. Auto mode picks the positions the game itself
  counts as the most effective for raising the chosen feeling — and they may differ from what you would expect.
  A position that looks gentle may raise Slavery, and one that looks rough may raise Favour, simply because that is
  how the game's table rates it.

  Goal         What Auto mode does
  ----------   --------------------------------------------------------------------------------------------------------------------------
  Free         No preference: any position the stages allow (only the last position is not repeated).
  Favour       Prefers positions the game rewards with Favour. The gentle route.
  Enjoyment    Prefers positions that raise Enjoyment — she learns to love the pleasure itself.
  Slavery      Prefers positions that raise Slavery — obedience and submission.
  Aversion     Prefers positions that raise Aversion. She will like you less.
  Broken       Prefers positions that push her towards Broken. They usually raise Aversion too.
  Dependence   Prefers positions that raise Dependence.
  FFM          Scene for two girls and you: only positions for three, only the SERVICE and SEX stages. Needs a second girl in the scene.
  MMF          Scene for her, you and a second man / futa: only positions for three, only SERVICE and SEX. Needs a second male character.

FFM / MMF details:
- If the scene has no positions for three, Auto mode stops and says so in the corner of the screen.
- When every position for three of a stage has been used, they are repeated instead of stopping.
- Sadism has no effect in these goals; the game does not count the second girl's sex trait.

STAGES
------

Section 2. Stages of the settings: 8 slots, each with a Type and a number of Positions (1–10).

  Type      Positions used
  -------   --------------------------------------------------------------
  SERVICE   she serves you (hands, mouth, breasts, feet …)
  CARESS    you caress her (kissing, fingers, licking …)
  SPECIAL   tentacles, masturbation and, with Masochism on, pain positions
  SEX       intercourse
  None      the slot is not used

Default: SERVICE 2 → CARESS 2 → SPECIAL 1 → SEX 3. A type may repeat, e.g. CARESS → SEX → CARESS → SEX.
A stage with nothing available is skipped. After Last Stage: Stay (keep using the last stage), Loop (start again), Free (any position).

Lesbian positions are never used by Auto mode.

FINISH
------

SEX Finish (like BetterHScenes): whoever reaches the top first waits at 98 % for the partner, then:
Same (together), Inside, Outside, Random, or Game (the mod does not interfere).
If the chosen type is impossible in the position, another one is used.

SERVICE Finish: Drink, Vomit (spit out), Outside, Random.

Also: chance to change the position after a finish, chance to pull out, all timings (start wait, restart wait, loop change, pull-out, spanking) in section 4. Timings.

SEX TRAIT (T)
-------------

- Sensitive Breasts / Ass / Pussy, Love Kisses — a position of her kind goes first in every stage and is kept until its finish (Sensitive Ass needs Anal on).
- Clean Freak — positions with an outside finish only are used last.
- Sadist / Masochist — in SEX the finish is together until he finishes / she climaxes once.
- Game fix, always on: the game credits a sex trait only if a position of that trait is the last position of the scene —
  a whole scene of Sensitive Breasts positions counted for nothing if it ended in a different position.
  Smart Auto H fixes this: a trait position at any moment of the scene now counts for the whole scene.

MASOCHISM (M) AND PAIN POSITIONS
--------------------------------

Desk / Chair Spanking, Whipping, Wooden Horses, G-Spot Attack are used in the SPECIAL stage only with M on.
Each lasts at most Pain Position Time (90 s by default). A girl with the Masochist trait or full pain experience can climax there;
others cannot (the game stops her gauge at 74 %), but the time still raises her pain experience.

OTHER BEHAVIOUR
---------------

- Auto mode switches off when she faints — you continue by hand.
- A position that does not start is logged and not offered again in this scene.
- A near-finish loop that lasts too long is logged with the gauges (diagnostics for rare hangs).
- Preference Bonus and Sweet Spot Chance make the game favour her preferred positions and her own sweet-spot speed.

INSTALLATION
------------

Extract the archive into your game folder (where HoneySelect2.exe is), so that the file lands in
BepInEx\plugins\HS2SmartAutoH\HS2SmartAutoH.dll, and turn off the original HS2TakeChargePlugin.
Full step-by-step guide (BetterRepack / HF Patch, update, uninstall, MO2): INSTALL.txt.

Settings: F1 → HS2 Smart Auto H. The Auto hotkey is empty by default — use the panel's Auto button or set a key.

COMPATIBILITY
-------------

- Made for Honey Select 2 DX with BepInEx 5.
- Conflicts with HS2TakeChargePlugin by OrangeSpork (the original it is based on). Both patch the same Auto mode code:
  with both active, positions change twice, the speed and the finish are driven by two plugins at once.
  Keep only one of them — remove or disable HS2TakeChargePlugin.dll.
- Other mods that drive the H scene automatically (their own auto modes or autopilots) may conflict in the same way.
- Works alongside our other mods (HS2 FutaVoice, HS2 Girl H Voice Options), but needs neither.

CREDITS
-------

- OrangeSpork — HS2TakeChargePlugin, the base of this mod.
- DiKriy — idea, design, testing.
- Claude (Anthropic) — code and texts.

CHANGELOG
---------

1.0.0 — first public release.
