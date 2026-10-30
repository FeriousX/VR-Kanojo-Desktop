# VR Kanojo Desktop

Play the **original VR Kanojo (2018)** with a mouse and keyboard, using the game's native interaction systems without a VR headset.

- **Development version:** 1.0.4 — unreleased
- **Latest published stable version:** v1.0.3
- **Tested game:** VR Kanojo v1.31
- **Maintainer:** Ferious / FeriousX

This project is a modified derivative of ManlyMarco's `VRK_PlayWithoutVR` plugin. It is **not compatible with VR-Kanojo (2025)**.

## Features

- FPS-style mouse-look that preserves the camera's world position.
- WASD camera movement, with Space and Left Ctrl for vertical movement.
- Improved virtual-hand tracking after camera rotation, using the owned camera's final pose.
- Adjustable physical virtual-hand depth from **0.15 to 6.00 meters**.
- Native touch, physical grabbing, and scene-dependent left/right gaze interactions.
- Separate wheel controls for hand depth, the requested Fist setting, and native action speed.
- A center crosshair, live `Hand: X.XX m` depth text, and a fading `Fist: XX%` indicator.
- Cursor confinement, Tab capture/release, and recovery after focus changes.
- Desktop-mode suppression of the Oculus/OVR debug HUD.
- Core Harmony patch registration checks with explicit startup error logging.

The Fist indicator shows a **requested setting**. Visible finger clenching has not worked in the user's gameplay tests; see [Known limitations](#known-limitations).

## Installation

1. Use a legally obtained copy of the original VR Kanojo and an existing BepInEx setup compatible with that game. This plugin does not install BepInEx or the HF Patch.
2. Close the game and back up your current `VRKanojoDesktop.dll` before replacing it.
3. Copy the plugin DLL into:

   ```text
   VRKanojo\BepInEx\plugins\VRKanojoDesktop.dll
   ```

4. Start the game without an HMD connected.

Install only one copy of this plugin. Do not load `VRK_PlayWithoutVR.dll` alongside it: the plugins overlap and patch many of the same systems. Keep a known-working DLL when testing development builds.

## Controls

| Control | Function |
| --- | --- |
| W / A / S / D | Move the camera at normal speed |
| Mouse | Look around |
| Space | Move the camera upward |
| Left Ctrl | Move the camera downward |
| LMB | Click to activate an available native left touch/grab interaction; click again to release. Starts/continues eligible actions or selects menus, depending on scene/state |
| RMB | Click to activate an available native right touch/grab interaction; click again to release. Stops/finishes eligible actions, depending on scene/state |
| Scroll | Adjust virtual-hand depth, 0.15–6.00 m |
| Shift + Scroll | Adjust the requested Fist percentage, 0–100% |
| Alt + Scroll | Adjust character action speed/pace |
| Tab | Capture or release the mouse |
| Esc | Open or close the native game menu |

Both Shift keys and both Alt keys are recognized for wheel modifiers. **Alt takes priority over Shift** when both are held. Shift sprint has been removed; Shift does not accelerate WASD movement and is used here as the Fist wheel modifier.

Three separate mechanisms share the mouse buttons:

- **Native gaze hands:** LMB operates the left gaze hand and RMB the right where the scene allows them. Both can perform native touch/grab interactions. Click once to activate an available interaction; click the same button again to release/deactivate it. These native interactions do not require continuously holding either mouse button. Availability and release rules remain native and scene-dependent.
- **Separate physical virtual-hand implementation (technical detail):** the plugin reads held LMB for physical touch and held RMB for grip input; releasing RMB removes that physical grip input. This implementation is unchanged and is not click-to-toggle. It is distinct from the native click-to-toggle interactions above.
- **Native action controls:** LMB requests start/continue and RMB can request stop/finish only where the active native controller/state supports that meaning. A click can have a different meaning in another state; neither button is a universal pause or scene-exit command.

Scrolling while physically grabbing changes the controller target's distance. The 6-meter setting applies to the physical virtual-hand target; native gaze interactions retain their own scene, collider, and range rules.

Alt + Scroll adjusts character action speed/pace, preserving the original signed wheel input and sensitivity. It is not a direct scene-transition command. Native menus, animation timing, dialogue, first-time/story flags, and completion requirements still determine when an operation is available.

Capture is restored after focus recovery only when capture was already requested. Tab release remains respected; focus recovery does not force capture back on after a manual release. Esc is the default native menu key, subject to scene/UI availability.

### Wheel modes

- **Scroll:** changes only the virtual-hand depth through this plugin.
- **Shift + Scroll:** changes the requested Fist percentage; visible physical clenching did not work in user testing.
- **Alt + Scroll:** adjusts character action speed/pace. Alt wins when Shift is also held.

### Native actions and progression

In supported scenes and eligible states, LMB starts or continues a native action. In eligible active states, RMB can stop or finish an action. These meanings vary by native controller and current state; neither button is a universal scene-exit command.

Some actions require speed and animation stages before native finishing becomes available. Reaching maximum speed does **not** guarantee that clicking immediately advances the scene. Finishing an action does not necessarily leave the scene. Scene transitions and their requirements remain controlled by the original game.

There are **no custom Q, T, or E shortcuts** and no dedicated keyboard scene-transition shortcut. Use the native controls/menu when the game makes finishing available.

### Startup guide

A temporary, noninteractive guide appears at the upper left when the desktop camera is ready and the application is focused. It displays movement, menu, interaction, and wheel controls for 20 seconds from its first display, fading during the final second. Losing focus hides it but does not pause that timer; scene changes do not restart it. It does not capture mouse input or change the crosshair, hand-depth, or Fist HUD. Full state-dependent gameplay notes are provided here rather than crowded onto the guide.

The original startup instructions used BepInEx message logging and depended on a separate Message Center plugin for in-game display. The guide now draws independently of console/Message Center settings; its log copy uses Info level to avoid duplicate on-screen messages. Logging configuration is unchanged.

## Improvements since v1.0.3

The unreleased v1.0.4 development build incorporates these changes from the tested experimental builds:

- Refresh the existing virtual-hand target after the desktop-controlled camera's `LateUpdate()` to improve rotation following.
- Separate ordinary, Shift-modified, and Alt-modified scrolling.
- Remove Shift sprint while keeping normal camera movement.
- Add the fading requested Fist percentage indicator and retain the cosmetic physical-hand Flex output path.
- Remove experimental keyboard action/scene shortcuts and their temporary diagnostics.
- Keep core Harmony registration verification and explicit failure reporting; remove the optional shortcut path that previously generated invalid wrapper IL.
- Replace the startup control instructions with a temporary in-game guide matching the current controls, independent of external message-overlay timing.

The combined cleanup DLL requires manual regression testing after compilation. These notes do not announce a published v1.0.4 release.

## Known limitations

- **Visible finger clenching is not confirmed working.** Shift + Scroll changes the Fist percentage, but the physical hands did not visibly clench or unclench in the user's tests. Physical hands can be hidden or inactive, native grip/pose animation takes priority, and native gaze-hand models use separate Animators. The percentage is not a measurement of the rendered fingers. No hand visibility is forced.
- **Scene progression remains native.** Action start, stop/finish, restart, and scene exit are different operations. Their availability varies with scene/controller, animation, dialogue, and story/replay state. No general scene-transition fix is claimed.
- Native gaze interactions and physical controller grabbing are separate systems; not every target or scene supports the same behavior. There is no dedicated keyboard scene-transition shortcut: Q/E were removed and T is unbound.
- Wheel separation targets the native action-speed handler. Other native wheel consumers, such as UI or phone controls, retain their own behavior.
- Other game versions, modified Animator/controllers, and additional mod combinations remain untested and unverified.

## Requirements and compatibility

- **Supported game:** original VR Kanojo (2018), tested with **v1.31**.
- **Unsupported game:** VR-Kanojo (2025).
- A compatible BepInEx 5 installation. Local builds and tests used BepInEx 5.4.12 and Harmony assembly 2.5.1 with the original game's Unity 5.4.5 runtime.
- The plugin targets **.NET Framework 3.5 / CLR 2.0** and is built as AnyCPU.

Published v1.0.3 was manually tested with VR Kanojo v1.31. Later experimental testing established the working movement, hand-following, interactions, wheel modes, and Fist indicator described above. It did not establish visible finger animation or a general scene-progression repair.

This repository does not include the game, proprietary game/Unity assemblies, BepInEx binaries, or game assets.

## Building from source

The project uses `Microsoft.NETFramework.ReferenceAssemblies.net35` **1.0.3** as a build-only NuGet package. Modern MSBuild can use these reference assemblies without installing a system-wide .NET 3.5 targeting pack.

Provide references from your own local game and BepInEx installation:

- `VRKanojo_Data\Managed\Assembly-CSharp.dll`
- `VRKanojo_Data\Managed\UnityEngine.dll`
- `VRKanojo_Data\Managed\UnityEngine.UI.dll`
- `BepInEx\core\BepInEx.dll`
- `BepInEx\core\BepInEx.Harmony.dll`
- `BepInEx\core\0Harmony.dll`

From the repository root, set the game directory, restore, and build:

```powershell
$env:VRKANOJO_DIR = "C:\Path\To\VRKanojo"
msbuild .\VRKanojoDesktop\VRKanojoDesktop.csproj /t:Restore /p:Configuration=Release /p:Platform=AnyCPU
msbuild .\VRKanojoDesktop\VRKanojoDesktop.csproj /t:Build /p:Configuration=Release /p:Platform=AnyCPU
```

Alternatively pass `/p:GameDir="C:\Path\To\VRKanojo"` to the build command. The default project output is `bin\VRKanojoDesktop.dll`. Build outputs, dependencies, and runtime logs are excluded from version control.

## Credits and upstream

VR Kanojo Desktop is an extensively modified and expanded derivative of **ManlyMarco's `VRK_PlayWithoutVR`**, from **`VRKanojo_Plugins`**.

The original upstream authorship, GPL-3.0 licensing, and attribution are preserved. Ferious / FeriousX maintains this modified derivative and is not the author of the original plugin from scratch.

The derivative adds the desktop movement and FPS mouse-look model, virtual-hand positioning, native touch/grab integration, depth controls and HUD, cursor recovery, desktop haptic/OVR HUD handling, wheel modifiers, and standalone project packaging.

## License

GNU General Public License v3.0. See [LICENSE](LICENSE).

This is an unofficial community project, not affiliated with or endorsed by the game's developer or publisher. Game binaries and proprietary assets are not distributed in this repository.
