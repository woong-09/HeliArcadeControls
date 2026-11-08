# HeliArcadeControls

Arcade style helicopter controls for Unturned.

HeliArcadeControls replaces Unturned's default helicopter controls with simple arcade style controls. When you let go of every key, the helicopter hovers in place and levels itself out. The mouse no longer tilts the aircraft, and the right click control mode switch has been removed.

The mod was made with the California 2 map in mind.

The helicopter dips its nose slightly when moving and banks a little when turning, so it still looks natural. While parked, it stays on its wheels until you hold Space, and when you come down with Shift it slows near the ground for a soft landing. Each helicopter keeps its original top speed.

## Controls

| Key | Action |
|---|---|
| Space | Climb |
| Shift | Descend |
| W / S | Move forward / backward |
| A / D | Turn left / right |
| No keys | Hover in place and level out |

## Requirements

- Unturned on Windows
- The game must be launched without BattlEye, because Unturned does not load third party modules while BattlEye is active
- Intended for singleplayer

This is a code module, not a Workshop item. It does not include or change any California 2 files.

## Installation

1. Download the latest zip from the [Releases](../../releases) page.
2. In your Steam library, right click Unturned and choose Manage, then Browse local files. This opens the Unturned game folder.
3. Open the Modules folder inside the game folder. If there is no Modules folder, create a new folder with that name.
4. Copy the HeliArcadeControls folder from the zip into the Modules folder. Do not rename the folder or any of the files inside it.
5. In your Steam library, right click Unturned again and choose Properties. On the General tab, find Launch Options and open the Selected launch option dropdown.
6. Choose Play without BattlEye Anti-Cheat. You can also leave it set to Ask when starting game, and then choose Play without BattlEye Anti-Cheat in the window that appears every time you press Play.
7. Start Unturned. A warning at the top of the screen will say that BattlEye Anti-Cheat is disabled. This is expected, so you can ignore it. Load a singleplayer world, get into the pilot seat of a helicopter that has fuel, and hold Space to take off.

## Settings

The first time the game runs with the mod, it creates `Unturned\Modules\HeliArcadeControls\HeliArcadeControls.cfg`. Open it with Notepad to change climb speed, turn speed, how far the helicopter tilts while moving and more. Each setting is explained in Korean, with English below. Changes take effect the next time you start the game.

- `ExtraHelicopters` lists helicopters that their map defines as planes. The California 2 helicopters are included by default. Vehicles that use the helicopter engine are always supported.
- `BlockHeadlightsToggle` set to `false` allows right click on helicopters again.

## Uninstall

Delete the `Unturned\Modules\HeliArcadeControls` folder. To play on online servers that require BattlEye, set the Selected launch option back to Play Unturned. The mod will not load while BattlEye is active, so you do not have to remove it first.

## Building from source

The full source code of the mod is in this repository, so you can read it or build the DLL yourself instead of using the release.

1. Install the [.NET SDK](https://dotnet.microsoft.com/download).
2. Make sure Unturned is installed. If it is not in the default Steam folder, change `UnturnedDir` in `HeliArcadeControls.csproj`.
3. Run `dotnet build -c Release` in this folder. The built files are copied into `Unturned\Modules\HeliArcadeControls` automatically, so close the game before building.

## How it works

- `HeliSimulatePatch.cs` uses Harmony to patch `InteractableVehicle.simulate`, which the game calls while you drive. For supported helicopters it reads the W, S, A, D, Space and Shift input and clears the vanilla flight input.
- `ArcadeHeliController.cs` applies the arcade flight every physics step. It uses the plane formed by the wheels as the real level reference.
- `HeliFilter.cs` decides which vehicles are supported. California 2 defines its helicopters with the plane engine, so they are recognized by name.
- `HeadlightsPatch.cs` blocks right click, which toggles headlights in vehicles. California 2 uses it to switch to a hover mode that stops forward movement.
- `Nexus.cs` and `ModConfig.cs` load the module and the settings file.

## License

This mod is released under the MIT License. It includes [Harmony](https://github.com/pardeike/Harmony) by Andreas Pardeike, also under the MIT License. See `LICENSE` and `THIRD-PARTY-NOTICES.txt`.

This project is not affiliated with Smartly Dressed Games or the authors of California 2.
