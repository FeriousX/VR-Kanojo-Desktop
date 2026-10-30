# Changelog

## v1.0.4 (Unreleased)

- Improved physical virtual-hand tracking after desktop camera rotation, retaining native interaction and grab systems.
- Separated wheel controls: Scroll for 0.15–6.00 m hand depth, Shift + Scroll for the requested Fist setting, and Alt + Scroll for native action speed and supported wheel-based transitions. Alt takes priority over Shift.
- Removed Shift sprint; camera movement uses normal speed.
- Added the fading `Fist: XX%` requested-setting indicator. The percentage is confirmed working; visible physical finger clenching did not work in the user's gameplay tests and remains a known limitation.
- Removed experimental Q/T/E action and scene shortcuts, their optional Harmony registrations, and shortcut-specific diagnostics. Native LMB/RMB and scene-progression pathways remain in place; no scene-transition fix is claimed.
- Retained core Harmony registration verification and explicit startup failure logging, removing the optional shortcut path responsible for the earlier invalid wrapper IL failure.
- Restored a temporary in-game startup controls guide after desktop camera setup, independent of console/Message Center timing, without duplicate message overlays. Expanded README notes on state-dependent actions, wheel modes, and native finishing requirements.
- Prepared a clean development build for manual regression testing; v1.0.4 has not been published.

## v1.0.3

- Improved FPS mouse capture reliability and fixed Windows cursor escaping during gameplay.
- Improved cursor-lock recovery after application focus changes.
- Preserved 6-meter virtual-hand reach and existing controls.
- Manually tested with the original VR Kanojo v1.31.

## v1.0.2

- Increased maximum virtual-hand depth from 2.00 m to 6.00 m.

## v1.0.1

- Fixed mouse-look orbiting around the IllusionCamera focus/pivot.
- Mouse-look now behaves as true FPS-style view rotation while preserving the camera's world position.
- Prevented native IllusionCamera mouse-drag controls from interfering with LMB touch and RMB grab while mouse capture is active.
- Preserved native movement, touch, grab, hand-depth, UI, and scene-transition behavior.

## v1.0.0

- Initial public release.
