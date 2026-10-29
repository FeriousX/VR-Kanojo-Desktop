# VR Kanojo Desktop

**Play the original VR Kanojo (2018) without a VR headset using mouse and keyboard — including native touch, grabbing, hand interaction, WASD movement, and FPS-style mouse-look.**

VR Kanojo Desktop is a no-VR / desktop alternative to the original `VRK_PlayWithoutVR` plugin, expanding its desktop controls with interactive touch and grab support.

**Version:** 1.0.3
**Maintainer:** Ferious / FeriousX  
**Tested with:** VR Kanojo v1.31

This project is for the original 2018 VR Kanojo. It is **not** for the newer **VR-Kanojo** game released in 2025.

## What It Is

VR Kanojo Desktop is a BepInEx plugin that lets the original VR Kanojo run in a practical desktop/no-HMD mode. It adds camera movement, FPS-style mouse look, and mouse-driven native touch/grab interaction while reusing the game's existing VR hand, collider, touch, and grabbable systems.

The plugin does not recreate character reactions or object interaction logic. It drives the existing in-game systems from desktop controls.

## Features

- WASD movement.
- Space to move up.
- Left Ctrl to move down.
- Left Shift for faster movement.
- True FPS-style mouse look: rotates the view while preserving the camera's world position.
- Tab to capture or release the mouse; capture is restored when focus returns.
- Escape remains the native game menu key.
- LMB native touch; click briefly or hold to keep touching.
- RMB native grab; hold to grab and release to drop.
- Mouse wheel adjusts virtual hand depth from 0.15 m to 6.00 m.
- Live `Hand: X.XX m` depth indicator below the crosshair.
- Scrolling while grabbing moves held objects closer or farther.
- Suppresses the Oculus/OVR debug HUD in desktop/no-HMD mode.
- Reuses native VR Kanojo interaction systems instead of replacing them.

## Controls

| Control | Action |
| --- | --- |
| W / A / S / D | Move |
| Space | Move up |
| Left Ctrl | Move down |
| Left Shift | Move faster |
| Mouse | Look around |
| Tab | Capture / release mouse |
| Esc | Game menu |
| LMB | Touch; click or hold |
| RMB | Grab; hold |
| Release RMB | Release grabbed object |
| Mouse wheel | Adjust virtual hand distance |

The current virtual hand distance is shown below the crosshair. Version 1.0.2 increased the maximum reach from 2.00 m to 6.00 m for native touch and grabbing, including scrolling while holding objects. Version 1.0.3 preserves the 6.00 m reach, minimum reach, scroll sensitivity, and existing controls while improving cursor capture reliability and recovery after focus changes.

## Requirements

- A legally obtained copy of the original VR Kanojo from 2018.
- Tested on VR Kanojo v1.31.
- A BepInEx-compatible setup for the game.
- Other game versions are untested.

This repository does not include VR Kanojo, game assemblies, Unity assemblies, BepInEx binaries, or proprietary assets.

## Installation

1. Install and configure BepInEx for VR Kanojo.
2. Copy `VRKanojoDesktop.dll` into:

   ```text
   VRKanojo\BepInEx\plugins
   ```

3. Start the game without an HMD connected.

Do not load the old `VRK_PlayWithoutVR.dll` at the same time as `VRKanojoDesktop.dll`. The plugins overlap and patch many of the same game systems.

## Building

The project targets **.NET Framework 3.5 / CLR 2.0**, matching the game runtime.

It uses `Microsoft.NETFramework.ReferenceAssemblies.net35` as a build-only NuGet dependency so modern Windows installations do not need the .NET 3.5 targeting pack installed system-wide.

The project references DLLs from your own local VR Kanojo and BepInEx installation. These files are not legal or appropriate to include in this public source repository.

Set `VRKANOJO_DIR` to your game directory:

```powershell
$env:VRKANOJO_DIR = "C:\Path\To\VRKanojo"
```

Then build:

```powershell
msbuild .\VRKanojoDesktop\VRKanojoDesktop.csproj /t:Restore
msbuild .\VRKanojoDesktop\VRKanojoDesktop.csproj /t:Build /p:Configuration=Debug /p:Platform=AnyCPU
```

You can also pass the path directly:

```powershell
msbuild .\VRKanojoDesktop\VRKanojoDesktop.csproj /t:Build /p:GameDir="C:\Path\To\VRKanojo" /p:Configuration=Debug /p:Platform=AnyCPU
```

Required local reference files:

- `VRKanojo_Data\Managed\Assembly-CSharp.dll`
- `VRKanojo_Data\Managed\UnityEngine.dll`
- `VRKanojo_Data\Managed\UnityEngine.UI.dll`
- `BepInEx\core\BepInEx.dll`
- `BepInEx\core\BepInEx.Harmony.dll`
- `BepInEx\core\0Harmony.dll`

## Compatibility

VR Kanojo Desktop v1.0.3 supports the original VR Kanojo (2018). Version 1.0.3 has been manually tested and confirmed working with VR Kanojo v1.31. Other game versions remain untested and unverified. The newer VR-Kanojo (2025) is not supported.

## Credits And Upstream

VR Kanojo Desktop is an extensively modified and expanded derivative of ManlyMarco's `VRK_PlayWithoutVR` plugin from `VRKanojo_Plugins`.

Original upstream work:

- ManlyMarco
- `VRK_PlayWithoutVR`
- `VRKanojo_Plugins`

VR Kanojo Desktop adds the current desktop control model, mouse-look behavior, virtual hand movement, native LMB touch gate, native RMB grab integration, hand depth control/indicator, desktop/no-HMD haptic and OVR debug HUD handling, and the renamed standalone project packaging.

The original GPL-3.0 licensing and attribution are preserved. Ferious is the maintainer of this modified derivative, not the author of the original plugin from scratch.

## License

This project is licensed under the GNU General Public License v3.0. See `LICENSE`.

## Disclaimer

This is an unofficial community project. It is not affiliated with, sponsored by, or endorsed by VR Kanojo's developer or publisher.

This repository does not contain or distribute the game, game binaries, proprietary Unity/game assemblies, or game assets.
