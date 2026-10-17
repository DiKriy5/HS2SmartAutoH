# HS2 Smart Auto H — Installation

## What you need

- **Honey Select 2** (DX recommended) with **BepInEx 5**.
  If you installed the game with **BetterRepack** or **HF Patch**, BepInEx is already there — nothing else to install.
- **ConfigurationManager** (the F1 settings window) — also included in BetterRepack and HF Patch.

## Install

1. Close the game.
2. Open the archive `HS2SmartAutoH_1.0.0.zip`. Inside is a `BepInEx` folder.
3. Extract it **into your game folder** — the folder that contains `HoneySelect2.exe`.
   When asked, allow merging the folders.
   The file must end up here:
   ```
   <game folder>\BepInEx\plugins\HS2SmartAutoH\HS2SmartAutoH.dll
   ```
4. **Turn off the original Take Charge plugin — the two plugins conflict.** Search your `BepInEx\plugins` folder for `HS2TakeChargePlugin.dll`
   and move it out of the game folder (or rename it to `HS2TakeChargePlugin.dll.off`).
   Both plugins drive the same Auto mode — they must not run together.
5. Start the game.

On the first start Smart Auto H copies your Take Charge settings once (all except the Ctrl+A hotkey).

## Check that it works

- Start any H scene. At the top right, left of the game's position menu, you will see the **Smart Auto H panel**
  (goal, Auto button, toggles, finish choice).
- Press **F1** → find **HS2 Smart Auto H** in the list for all settings.

If the panel does not appear: check that the dll is in `BepInEx\plugins\HS2SmartAutoH\`,
and look into `BepInEx\LogOutput.log` for lines with `HS2SmartAutoH`.

## Hotkey

The Auto hotkey is **not set** by default (Ctrl+A belonged to the original plugin).
Use the **Auto** button of the panel, or set a key in F1 → HS2 Smart Auto H → 5. Hotkeys → Enable Auto Mode.

## Update

Close the game and extract the new archive over the old one (replace the file). Your settings are kept.

## Uninstall

Delete the folder `BepInEx\plugins\HS2SmartAutoH`.
Optionally delete the settings file `BepInEx\config\HS2SmartAutoH.cfg`.
If you want the original Take Charge back, return `HS2TakeChargePlugin.dll` to its place.

## Mod Organizer 2

If you use MO2: install the archive as a mod (it already has the `BepInEx\plugins\...` structure)
and disable the mod that contains HS2TakeChargePlugin.
