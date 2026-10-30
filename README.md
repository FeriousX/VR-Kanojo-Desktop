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
- Temporary in-game startup controls guide.
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

### Mouse interactions

Three separate mechanisms share the mouse buttons:

- **Native gaze hands:** LMB operates the left gaze hand and RMB the right where the scene allows them. Both can perform native touch/grab interactions. Click once to activate an available interaction; click the same button again to release/deactivate it. These native interactions do not require continuously holding either mouse button. Availability and release rules remain native and scene-dependent.
- **Separate physical virtual-hand implementation (technical detail):** the plugin reads held LMB for physical touch and held RMB for grip input; releasing RMB removes that physical grip input. This implementation is unchanged and is not click-to-toggle. It is distinct from the native interactions above.
- **Native action controls:** LMB requests start/continue and RMB can request stop/finish only where the active native controller/state supports that meaning. A click can have a different meaning in another state; neither button is a universal pause or scene-exit command.

Scrolling while physically grabbing changes the controller target's distance. The 6-meter setting applies to the physical virtual-hand target; native gaze interactions retain their own scene, collider, and range rules.

Capture is restored after focus recovery only when capture was already requested. Tab release remains respected; focus recovery does not force capture back on after a manual release. Esc is the default native menu key, subject to scene/UI availability.

### Mouse-wheel modes

- **Scroll:** Adjusts virtual-hand depth between 0.15 and 6.00 meters.
- **Shift + Scroll:** Adjusts the requested Fist percentage between 0% and 100%. Visible physical finger movement remains a known limitation.
- **Alt + Scroll:** Adjusts character action speed/pace. Alt takes priority when Shift is also held.

Alt + Scroll preserves the original native wheel input and sensitivity. It is not a dedicated scene-transition command.

### Native actions and scene progression

In supported scenes and eligible states, LMB starts or continues a native action. RMB can stop or finish an action when the game permits it.

Some actions require progressing through speed and animation stages before finishing becomes available. **Reaching maximum speed does not guarantee that clicking immediately advances the scene.**

Finishing an action and exiting a scene are separate operations. Scene progression remains controlled by the original game.

No custom keyboard scene-transition shortcut is currently implemented.

### Startup controls guide

A temporary, noninteractive controls guide appears in the upper-left corner when the desktop camera is ready and the application is focused.

The guide is designed to remain visible for approximately **20 seconds**, fading during its final second.

Losing focus hides the guide without pausing its timer. Scene changes do not restart it.

The guide does not capture mouse input or interfere with the crosshair, hand-depth indicator, or Fist indicator.

The original startup instructions relied on BepInEx logging and a separate Message Center plugin for in-game display. The updated guide renders independently of Message Center, avoiding the earlier loading-time visibility issue.

## Improvements since v1.0.3

The unreleased **v1.0.4 development build** includes the following improvements:

- Improved virtual-hand tracking after camera rotation by synchronizing with the desktop-controlled camera's final `LateUpdate()` pose.
- Separated ordinary, Shift-modified, and Alt-modified scrolling.
- Removed Shift sprint while preserving normal camera movement.
- Added adjustable requested Fist percentage and a fading `Fist: XX%` indicator.
- Preserved the cosmetic physical-hand Flex output path, although visible finger movement remains unverified.
- Removed experimental Q/T/E keyboard shortcuts and their temporary diagnostics.
- Improved Harmony initialization reliability and startup error reporting.
- Replaced the previous message-based startup instructions with a temporary, independent in-game controls guide.
- Improved documentation covering native interactions, wheel controls, and gameplay limitations.

The v1.0.4 development build has been **manually tested** for camera movement, mouse controls, native interactions, wheel controls, and startup-guide visibility. General scene-transition behavior and visible finger animation remain unresolved.

These improvements have not yet been published as an official v1.0.4 release.

## Known limitations

- **Visible finger clenching:** Shift + Scroll changes the requested Fist percentage, but the physical hands have not visibly clenched or unclenched in user testing. Physical hands can be hidden or inactive, native grip animations may take priority, and native gaze-hand models use separate Animators. The displayed percentage does not measure actual rendered finger movement.
- **Scene progression:** Action start, stop, finishing, restarting, and exiting are different operations. Their availability depends on the scene, active controller, animation state, dialogue, and story/replay conditions. No general scene-transition fix is claimed.
- **Interaction availability:** Native gaze interactions and physical controller grabbing are separate systems. Not every target or scene supports identical interaction behavior.
- **Other native wheel consumers:** Wheel separation targets the native action-speed handler. Other wheel-based controls, including certain menus or UI components, may retain their original behavior.
- **Compatibility:** Other game versions, modified animation controllers, and additional mod combinations have not been verified.

## Requirements and compatibility

- **Supported game:** Original VR Kanojo (2018), tested with **v1.31**.
- **Unsupported game:** VR-Kanojo (2025).
- **Mod framework:** Compatible BepInEx 5 installation. Local builds and tests used BepInEx 5.4.12 and Harmony assembly 2.5.1 with the original game's Unity 5.4.5 runtime.
- **Framework target:** .NET Framework 3.5 / CLR 2.0.
- **Build platform:** AnyCPU.

Published v1.0.3 was manually tested with VR Kanojo v1.31. Subsequent experimental testing established the working camera movement, virtual-hand tracking, interactions, wheel modes, and Fist indicator included in the v1.0.4 development build.

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

Alternatively, pass `/p:GameDir="C:\Path\To\VRKanojo"` to the build command.

The default project output is `bin\VRKanojoDesktop.dll`. Build outputs, dependencies, and runtime logs are excluded from version control.

## Credits and upstream

VR Kanojo Desktop is an extensively modified and expanded derivative of **ManlyMarco's `VRK_PlayWithoutVR`**, from **`VRKanojo_Plugins`**.

The original upstream authorship, GPL-3.0 licensing, and attribution are preserved. Ferious / FeriousX maintains this modified derivative and is not the author of the original plugin from scratch.

The derivative adds the desktop movement and FPS mouse-look model, virtual-hand positioning, native touch/grab integration, depth controls and HUD, cursor recovery, desktop haptic/OVR HUD handling, wheel modifiers, and standalone project packaging.

## License

GNU General Public License v3.0. See [LICENSE](LICENSE).

This is an unofficial community project, not affiliated with or endorsed by the game's developer or publisher. Game binaries and proprietary assets are not distributed in this repository.