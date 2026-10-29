using System;
using BepInEx;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using Manager;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VR;
using VRKanojo;
using VRKanojoTouch;

namespace VRKanojoDesktop
{
    [BepInPlugin(GUID, Name, Version)]
    public class VRKanojoDesktopPlugin : BaseUnityPlugin
    {
        public const string Name = "VR Kanojo Desktop";
        public const string Version = "1.0.3";
        public const string GUID = "com.ferious.vrkanojodesktop";

        private static new ManualLogSource Logger;
        private static VRKanojoDesktopPlugin Instance;
        private static OVRCameraRig _cameraRig;
        private static IllusionCamera _illusionCamera;
        private static bool _mouseCaptured;
        // Capture intent survives focus loss; wait for Unity's focus callback before locking.
        private static bool _applicationHasFocus;
        private static Hand _virtualLeftHand;
        private static TrackedController _virtualLeftController;
        private static Transform _virtualLeftTrackTrans;
        private static float _virtualHandDepth = DefaultVirtualHandDepth;

        private static readonly System.Reflection.MethodInfo IllusionCameraRotateMethod =
            AccessTools.Method(typeof(IllusionCamera), "Rotate", new[] { typeof(Vector3) });
        private static readonly System.Reflection.MethodInfo CameraCalcPosMethod = AccessTools.Method(typeof(IllusionCamera), "CalcPos");
        private static readonly System.Reflection.FieldInfo CameraRotationField = AccessTools.Field(typeof(IllusionCamera), "rotate");
        private static readonly System.Reflection.FieldInfo CameraNowFocusField = AccessTools.Field(typeof(IllusionCamera), "nowFocus");
        private static readonly System.Reflection.FieldInfo CameraSetFocusField = AccessTools.Field(typeof(IllusionCamera), "setFocus");
        private static readonly System.Reflection.FieldInfo CameraPrevFocusField = AccessTools.Field(typeof(IllusionCamera), "prevFocus");
        private static readonly System.Reflection.FieldInfo CameraFocusRateField = AccessTools.Field(typeof(IllusionCamera), "focusIpoRate");
        private static readonly System.Reflection.FieldInfo IllusionCameraButtonStateField =
            AccessTools.Field(typeof(IllusionCamera), "buttonState");
        private static readonly System.Reflection.FieldInfo IllusionCameraDraggingField =
            AccessTools.Field(typeof(IllusionCamera), "dragging");
        private static readonly System.Reflection.FieldInfo IllusionCameraDragLockField =
            AccessTools.Field(typeof(IllusionCamera), "dragLock");
        private static readonly System.Reflection.FieldInfo IllusionCameraKeySensitivityField =
            AccessTools.Field(typeof(IllusionCamera), "keySensitivity");
        private static readonly System.Reflection.FieldInfo HandTrackedControllerField =
            AccessTools.Field(typeof(Hand), "m_trackedController");
        private static readonly System.Reflection.FieldInfo HandGrabVolumesField =
            AccessTools.Field(typeof(Hand), "m_grabVolumes");
        private static readonly System.Reflection.FieldInfo HandGrabCandidatesField =
            AccessTools.Field(typeof(Hand), "m_grabCandidates");
        private static readonly System.Type VrkOvrDebugInfoType =
            AccessTools.TypeByName("VRKanojoOVRDebugInfo");
        private static readonly System.Reflection.FieldInfo VrkOvrDebugInfoDebugUIManagerField =
            VrkOvrDebugInfoType != null ? AccessTools.Field(VrkOvrDebugInfoType, "debugUIManager") : null;
        private static readonly System.Reflection.FieldInfo VrkOvrDebugInfoShowVRVarsField =
            VrkOvrDebugInfoType != null ? AccessTools.Field(VrkOvrDebugInfoType, "showVRVars") : null;

        private const float FastMoveMultiplier = 3f;
        private const float MouseLookSensitivity = 2.25f;
        private const float DefaultVirtualHandDepth = 1f;
        private const float MinVirtualHandDepth = 0.15f;
        private const float MaxVirtualHandDepth = 6f;
        private const float VirtualHandWheelSensitivity = 0.75f;
        private const float IllusionCameraWheelKeySensitivityScale = 0.01f;
        private static bool _loggedOvrDebugInfoSuppressed;

        private void Awake()
        {
            Logger = base.Logger;
            Instance = this;

            var harmony = new Harmony(GUID);
            harmony.PatchAll(typeof(VRKanojoDesktopPlugin));
            PatchVrkOvrDebugInfo(harmony);

            enabled = false;
        }

        private static void PatchVrkOvrDebugInfo(Harmony harmony)
        {
            if (VrkOvrDebugInfoType == null)
                return;

            var update = AccessTools.Method(VrkOvrDebugInfoType, "Update");
            var awake = AccessTools.Method(VrkOvrDebugInfoType, "Awake");
            var changeShowNoArgs = AccessTools.Method(VrkOvrDebugInfoType, "ChangeShowVRVars", System.Type.EmptyTypes);
            var changeShowFlag = AccessTools.Method(VrkOvrDebugInfoType, "ChangeShowVRVars", new[] { typeof(bool) });

            var updatePrefix = new HarmonyMethod(typeof(VRKanojoDesktopPlugin), "SuppressVrkOvrDebugInfoUpdate");
            var awakePostfix = new HarmonyMethod(typeof(VRKanojoDesktopPlugin), "SuppressVrkOvrDebugInfoAfterAwake");
            var changePrefix = new HarmonyMethod(typeof(VRKanojoDesktopPlugin), "SuppressVrkOvrDebugInfoChange");

            if (update != null)
                harmony.Patch(update, updatePrefix, null);
            if (awake != null)
                harmony.Patch(awake, null, awakePostfix);
            if (changeShowNoArgs != null)
                harmony.Patch(changeShowNoArgs, changePrefix, null);
            if (changeShowFlag != null)
                harmony.Patch(changeShowFlag, changePrefix, null);
        }

        private static bool SuppressVrkOvrDebugInfoUpdate(object __instance)
        {
            if (!IsDesktopNoHmdMode())
                return true;

            HideVrkOvrDebugInfo(__instance);
            return false;
        }

        private static void SuppressVrkOvrDebugInfoAfterAwake(object __instance)
        {
            if (IsDesktopNoHmdMode())
                HideVrkOvrDebugInfo(__instance);
        }

        private static bool SuppressVrkOvrDebugInfoChange(object __instance)
        {
            if (!IsDesktopNoHmdMode())
                return true;

            HideVrkOvrDebugInfo(__instance);
            return false;
        }

        private static void HideVrkOvrDebugInfo(object instance)
        {
            if (instance == null)
                return;

            if (VrkOvrDebugInfoShowVRVarsField != null)
                VrkOvrDebugInfoShowVRVarsField.SetValue(instance, false);

            var debugUIManager = VrkOvrDebugInfoDebugUIManagerField != null
                ? VrkOvrDebugInfoDebugUIManagerField.GetValue(instance) as GameObject
                : null;
            if (debugUIManager && debugUIManager.activeSelf)
                debugUIManager.SetActive(false);

            if (!_loggedOvrDebugInfoSuppressed)
            {
                _loggedOvrDebugInfoSuppressed = true;
                Logger.LogDebug("Disabled VRKanojo OVR debug/status overlay in desktop/no-HMD mode.");
            }
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(VK_Interactive), "vSettingTouchCollision")]
        private static Exception TouchCrashFix(Exception __exception)
        {
            if (__exception != null) Logger.LogWarning("Preventing game crash: " + __exception);
            return null;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InitScene2), "CheckHMD")]
        private static void AllowNoHmdPatch(ref bool __result)
        {
            if (!__result && !VRDevice.isPresent)
            {
                Singleton<Game>.Instance.playCount++;
                GlobalVRKanojo.ForceMouse_ = true;
                __result = true;

                LogDesktopControlGuide();
            }
        }

        private static void LogDesktopControlGuide()
        {
            Logger.LogMessage("No HMD detected - Desktop Mode");
            Logger.LogMessage("");
            Logger.LogMessage("Movement");
            Logger.LogMessage("W / A / S / D  - Move");
            Logger.LogMessage("Space          - Move Up");
            Logger.LogMessage("Left Ctrl      - Move Down");
            Logger.LogMessage("Left Shift     - Move Faster");
            Logger.LogMessage("");
            Logger.LogMessage("Camera");
            Logger.LogMessage("Mouse          - Look Around");
            Logger.LogMessage("Tab            - Capture / Release Mouse");
            Logger.LogMessage("Esc            - Game Menu");
            Logger.LogMessage("");
            Logger.LogMessage("Interaction");
            Logger.LogMessage("LMB            - Touch / Hold to Continue Touching");
            Logger.LogMessage("RMB            - Grab / Hold");
            Logger.LogMessage("Mouse Wheel    - Adjust Hand Distance");
            Logger.LogMessage("Release RMB    - Release Grabbed Object");
            Logger.LogMessage("");
            Logger.LogMessage("The current hand distance is shown below the crosshair.");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(IllusionCamera), "KeyInput_Value")]
        private static void RemapIllusionCameraMovementKeys(ref float move, ref KeyCode addKey, ref KeyCode subKey)
        {
            var isMovementPair = IsIllusionCameraMovementPair(addKey, subKey);

            if (addKey == KeyCode.UpArrow && Input.GetKey(KeyCode.W))
                addKey = KeyCode.W;
            else if (addKey == KeyCode.RightArrow && Input.GetKey(KeyCode.D))
                addKey = KeyCode.D;
            else if (addKey == KeyCode.PageUp && Input.GetKey(KeyCode.Space))
                addKey = KeyCode.Space;

            if (subKey == KeyCode.DownArrow && Input.GetKey(KeyCode.S))
                subKey = KeyCode.S;
            else if (subKey == KeyCode.LeftArrow && Input.GetKey(KeyCode.A))
                subKey = KeyCode.A;
            else if (subKey == KeyCode.PageDown && Input.GetKey(KeyCode.LeftControl))
                subKey = KeyCode.LeftControl;

            if (isMovementPair && Input.GetKey(KeyCode.LeftShift))
                move *= FastMoveMultiplier;
        }

        private static bool IsIllusionCameraMovementPair(KeyCode addKey, KeyCode subKey)
        {
            return addKey == KeyCode.UpArrow && subKey == KeyCode.DownArrow ||
                   addKey == KeyCode.RightArrow && subKey == KeyCode.LeftArrow ||
                   addKey == KeyCode.PageUp && subKey == KeyCode.PageDown;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(IllusionCamera), "ButtonStateCheck")]
        private static void NeutralizeIllusionCameraDragState(IllusionCamera __instance)
        {
            if (!OwnsCamera(__instance))
                return;

            IllusionCameraDraggingField?.SetValue(__instance, false);
            IllusionCameraDragLockField?.SetValue(__instance, false);

            var buttonState = IllusionCameraButtonStateField?.GetValue(__instance) as Array;
            if (buttonState == null)
                return;

            var noButtonState = Enum.ToObject(buttonState.GetType().GetElementType(), 0);
            for (var i = 0; i < buttonState.Length; i++)
                buttonState.SetValue(noButtonState, i);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(IllusionCamera), "LateUpdate")]
        private static void SuppressIllusionCameraWheelSpeed(IllusionCamera __instance)
        {
            if (!OwnsCamera(__instance) || IllusionCameraKeySensitivityField == null)
                return;

            var wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Approximately(wheel, 0f))
                return;

            var keySensitivity = (float)IllusionCameraKeySensitivityField.GetValue(__instance);
            IllusionCameraKeySensitivityField.SetValue(
                __instance,
                keySensitivity - wheel * IllusionCameraWheelKeySensitivityScale);
        }

        // Gate only this camera's button reads; hand and UI input is untouched.
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(IllusionCamera), "LateUpdate")]
        private static IEnumerable<CodeInstruction> GateCameraMouseButtons(IEnumerable<CodeInstruction> instructions)
        {
            var held = AccessTools.Method(typeof(Input), "GetMouseButton", new[] { typeof(int) });
            var down = AccessTools.Method(typeof(Input), "GetMouseButtonDown", new[] { typeof(int) });
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(held) || instruction.Calls(down))
                {
                    var replacement = AccessTools.Method(typeof(VRKanojoDesktopPlugin),
                        instruction.Calls(held) ? "CameraMouseButton" : "CameraMouseButtonDown");
                    var loadCamera = new CodeInstruction(OpCodes.Ldarg_0);
                    loadCamera.labels.AddRange(instruction.labels);
                    instruction.labels.Clear();
                    loadCamera.blocks.AddRange(instruction.blocks);
                    instruction.blocks.Clear();
                    yield return loadCamera;
                    instruction.operand = replacement;
                }
                yield return instruction;
            }
        }

        private static bool OwnsCamera(IllusionCamera camera)
        {
            return _mouseCaptured && IsDesktopNoHmdMode() && camera && camera == _illusionCamera;
        }

        private static bool CameraMouseButton(int button, IllusionCamera camera)
        {
            return !OwnsCamera(camera) && Input.GetMouseButton(button);
        }

        private static bool CameraMouseButtonDown(int button, IllusionCamera camera)
        {
            return !OwnsCamera(camera) && Input.GetMouseButtonDown(button);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Scene), "SetCameraRigPos")]
        private static void AfterCameraPositionReset() { SynchronizeNativeCameraReset(); }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Scene), "SetCameraRigRot", new[] { typeof(Vector3) })]
        private static void AfterCameraEulerReset() { SynchronizeNativeCameraReset(); }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Scene), "SetCameraRigRot", new[] { typeof(Quaternion) })]
        private static void AfterCameraQuaternionReset() { SynchronizeNativeCameraReset(); }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Scene), "ReSetCameraRigPos")]
        private static void AfterCameraPositionRestore() { SynchronizeNativeCameraReset(); }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Scene), "ReSetCameraRigRot")]
        private static void AfterCameraRotationRestore() { SynchronizeNativeCameraReset(); }

        private static void SynchronizeNativeCameraReset()
        {
            if (!OwnsCamera(_illusionCamera) || !Camera.main)
                return;
            if (Camera.main.GetComponentInParent<OVRCameraRig>() == _cameraRig)
                SynchronizeCameraPose();
        }

        private static void SynchronizeCameraPose()
        {
            // Adopt the native pose instead of restoring angles from a previous scene.
            var root = _illusionCamera.transform;
            var position = root.position;
            CameraRotationField.SetValue(_illusionCamera, root.eulerAngles);
            var calculated = (Vector3)CameraCalcPosMethod.Invoke(_illusionCamera, null);
            var focus = (Vector3)CameraNowFocusField.GetValue(_illusionCamera) + position - calculated;
            CameraNowFocusField.SetValue(_illusionCamera, focus);
            CameraSetFocusField.SetValue(_illusionCamera, focus);
            CameraPrevFocusField.SetValue(_illusionCamera, focus);
            CameraFocusRateField.SetValue(_illusionCamera, 1f);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TrackedController), "Vibration")]
        private static bool SuppressDesktopVibration()
        {
            return !IsDesktopNoHmdMode();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TrackedController), "get_GripTrigger")]
        private static bool SubstituteDesktopLeftGrip(TrackedController __instance, ref float __result)
        {
            if (!IsDesktopNoHmdMode() || !__instance)
                return true;

            if (!_virtualLeftController)
                ResolveVirtualLeftHand();

            if (__instance != _virtualLeftController)
                return true;

            __result = IsVirtualLeftGrabEnabled() ? 1f : 0f;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Hand), "OnTriggerEnter")]
        private static bool GateVirtualLeftHandTriggerEnter(Hand __instance, Collider otherCollider)
        {
            if (!IsDesktopVirtualLeftHand(__instance))
                return true;

            if (IsVirtualLeftTouchEnabled())
                return true;

            TrackVirtualLeftGrabCandidate(__instance, otherCollider);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Hand), "OnTriggerStay")]
        private static bool GateVirtualLeftHandTriggerStay(Hand __instance)
        {
            return ShouldRunVirtualLeftHandTrigger(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LogoScene), "Start")]
        private static void AllowNoHmdPatch2()
        {
            if (!VRDevice.isPresent)
            {
                var mainGameObject = Camera.main.gameObject;
                var rig = mainGameObject.GetComponentInParent<OVRCameraRig>();
                _cameraRig = rig;
                _illusionCamera = rig.GetComponent<IllusionCamera>();
                Logger.LogDebug(_illusionCamera != null
                    ? "Found existing IllusionCamera. Reusing it for compatibility."
                    : "No IllusionCamera found. Adding one for compatibility.");

                if (!_illusionCamera)
                {
                    var cam = rig.gameObject.AddComponent<IllusionCamera>();
                    cam.Set(new Vector3(0.65f, 1.02f, 0), Vector3.zero, 1f);
                    _illusionCamera = cam;

                    // Need to reset the camera settings
                    rig.gameObject.SetActive(false);
                    rig.gameObject.SetActive(true);

                    var look = mainGameObject.transform.FindChild("Look");
                    look.localPosition = Vector3.zero;
                    look.localEulerAngles = Vector3.zero;
                }

                if (_illusionCamera != null)
                    _illusionCamera.Lock = false;

                CreateCrosshair();
                Instance.enabled = true;
                Logger.LogDebug("VR Kanojo Desktop control component enabled.");

                SetMouseCaptured(true);
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _applicationHasFocus = hasFocus;
            if (_mouseCaptured && IsDesktopNoHmdMode())
                ApplyCursorCapture(IsMouseCaptureActive());
        }

        private void OnDisable()
        {
            if (_mouseCaptured && IsDesktopNoHmdMode())
                ApplyCursorCapture(false);
        }

        private void OnApplicationQuit()
        {
            OnDisable();
        }

        private void LateUpdate()
        {
            // Native Update handlers can change cursor state after our Update.
            RestoreMouseCapture();
        }

        private static CanvasGroup _cursorCg;
        private static Text _handDepthText;
        private float _inactiveTime = 5;

        private void Update()
        {
            // Capture recovery must not depend on the HUD surviving a scene change.
            UpdateMouseCapture();
            if (!_cursorCg)
                return;

            ResolveDesktopCamera();
            UpdateMouseLook();
            UpdateVirtualLeftHand();
            UpdateHandDepthIndicator();

            if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2))
                _inactiveTime = 0;

            _inactiveTime += Time.deltaTime;

            _cursorCg.alpha = Mathf.Clamp01(1f - (_inactiveTime - 4));
        }

        private static void UpdateMouseCapture()
        {
            if (!_applicationHasFocus || !IsDesktopNoHmdMode())
                return;

            if (Input.GetKeyDown(KeyCode.Tab))
                SetMouseCaptured(!_mouseCaptured);

            RestoreMouseCapture();
        }

        private static void SetMouseCaptured(bool captured)
        {
            _mouseCaptured = captured;
            if (IsDesktopNoHmdMode())
                ApplyCursorCapture(IsMouseCaptureActive());
            UpdateHandDepthIndicator();
        }

        private static bool IsMouseCaptureActive()
        {
            return _mouseCaptured && _applicationHasFocus && Instance &&
                   Instance.isActiveAndEnabled && IsDesktopNoHmdMode();
        }

        private static void RestoreMouseCapture()
        {
            // Do not fight manual Tab release, background windows, or a disabled plugin.
            if (IsMouseCaptureActive())
                ApplyCursorCapture(true);
        }

        private static void ApplyCursorCapture(bool captured)
        {
            var desiredLock = captured ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != desiredLock)
                Cursor.lockState = desiredLock;
            if (Cursor.visible == captured)
                Cursor.visible = !captured;
        }

        private static void UpdateMouseLook()
        {
            if (!IsMouseCaptureActive() || !OwnsCamera(_illusionCamera) || !Camera.main || IllusionCameraRotateMethod == null)
                return;

            var mouseX = Input.GetAxis("Mouse X");
            var mouseY = Input.GetAxis("Mouse Y");
            if (Mathf.Approximately(mouseX, 0f) && Mathf.Approximately(mouseY, 0f))
                return;

            var rotation = new Vector3(-mouseY * MouseLookSensitivity, mouseX * MouseLookSensitivity, 0f);
            var eye = Camera.main.transform;
            var eyePosition = eye.position;
            IllusionCameraRotateMethod.Invoke(_illusionCamera, new object[] { rotation });
            // CalcPos includes distance and center offset, and sets root rotation.
            // Measuring the actual eye includes its offset through the full hierarchy.
            var root = _illusionCamera.transform;
            root.position = (Vector3)CameraCalcPosMethod.Invoke(_illusionCamera, null);
            var correction = eyePosition - eye.position;
            CameraNowFocusField.SetValue(_illusionCamera, (Vector3)CameraNowFocusField.GetValue(_illusionCamera) + correction);
            CameraSetFocusField.SetValue(_illusionCamera, (Vector3)CameraSetFocusField.GetValue(_illusionCamera) + correction);
            CameraPrevFocusField.SetValue(_illusionCamera, (Vector3)CameraPrevFocusField.GetValue(_illusionCamera) + correction);
            root.position += correction;
        }

        private static void ResolveDesktopCamera()
        {
            if (!IsDesktopNoHmdMode())
                return;
            var eye = Camera.main;
            var rig = eye ? eye.GetComponentInParent<OVRCameraRig>() : null;
            if (rig == _cameraRig && _illusionCamera)
                return;
            _cameraRig = rig;
            _illusionCamera = rig ? rig.GetComponent<IllusionCamera>() : null;
            if (rig && !_illusionCamera && _mouseCaptured)
                _illusionCamera = rig.gameObject.AddComponent<IllusionCamera>();
            if (OwnsCamera(_illusionCamera))
                SynchronizeCameraPose();
        }

        private static void UpdateVirtualLeftHand()
        {
            if (!IsMouseCaptureActive())
            {
                SetVirtualLeftHandInteractionVolumes(false);
                return;
            }

            if (!ResolveVirtualLeftHand())
                return;

            var cameraTransform = GetVirtualHandCameraTransform();
            if (!cameraTransform)
                return;

            SetVirtualLeftHandInteractionVolumes(true);

            var wheel = Input.GetAxis("Mouse ScrollWheel");
            if (!Mathf.Approximately(wheel, 0f))
                _virtualHandDepth = Mathf.Clamp(_virtualHandDepth + wheel * VirtualHandWheelSensitivity, MinVirtualHandDepth, MaxVirtualHandDepth);

            _virtualLeftTrackTrans.position = cameraTransform.position + cameraTransform.forward * _virtualHandDepth;
            _virtualLeftTrackTrans.rotation = cameraTransform.rotation;
            SetVirtualLeftHandInteractionVolumes(_mouseCaptured);
        }

        private static void UpdateHandDepthIndicator()
        {
            if (!_handDepthText)
                return;

            _handDepthText.enabled = _mouseCaptured;
            _handDepthText.text = string.Format("Hand: {0:0.00} m", _virtualHandDepth);
        }

        private static Transform GetVirtualHandCameraTransform()
        {
            if (Camera.main)
                return Camera.main.transform;

            return null;
        }

        private static bool ResolveVirtualLeftHand()
        {
            if (_virtualLeftHand && _virtualLeftController && _virtualLeftTrackTrans)
                return true;

            _virtualLeftHand = null;
            _virtualLeftController = null;
            _virtualLeftTrackTrans = null;

            var hands = UnityEngine.Object.FindObjectsOfType(typeof(Hand));
            for (var i = 0; i < hands.Length; i++)
            {
                var hand = hands[i] as Hand;
                if (!hand || hand.Handedness != Hand.HANDEDNESS.L)
                    continue;

                var controller = HandTrackedControllerField != null
                    ? HandTrackedControllerField.GetValue(hand) as TrackedController
                    : null;
                if (!controller || !controller.TrackTrans)
                    continue;

                _virtualLeftHand = hand;
                _virtualLeftController = controller;
                _virtualLeftTrackTrans = controller.TrackTrans;
                SetVirtualLeftHandInteractionVolumes(IsMouseCaptureActive());
                Logger.LogDebug("Desktop virtual left hand using " + hand.name + " -> " + _virtualLeftTrackTrans.name + ".");
                return true;
            }

            return false;
        }

        private static bool ShouldRunVirtualLeftHandTrigger(Hand hand)
        {
            if (!IsDesktopVirtualLeftHand(hand))
                return true;

            return IsVirtualLeftTouchEnabled();
        }

        private static bool IsDesktopVirtualLeftHand(Hand hand)
        {
            return IsDesktopNoHmdMode() && hand && hand.Handedness == Hand.HANDEDNESS.L;
        }

        private static bool IsVirtualLeftTouchEnabled()
        {
            return IsMouseCaptureActive() && Input.GetMouseButton(0);
        }

        private static bool IsVirtualLeftGrabEnabled()
        {
            return IsMouseCaptureActive() && Input.GetMouseButton(1);
        }

        private static void SetVirtualLeftHandInteractionVolumes(bool enabled)
        {
            if (!_virtualLeftHand || HandGrabVolumesField == null)
                return;

            var grabVolumes = HandGrabVolumesField.GetValue(_virtualLeftHand) as Collider[];
            if (grabVolumes == null)
                return;

            for (var i = 0; i < grabVolumes.Length; i++)
            {
                var collider = grabVolumes[i];
                if (collider && collider.enabled != enabled)
                    collider.enabled = enabled;
            }
        }

        private static void TrackVirtualLeftGrabCandidate(Hand hand, Collider otherCollider)
        {
            if (!hand || !otherCollider || HandGrabCandidatesField == null)
                return;

            var grabbable = otherCollider.GetComponent<Grabbable>();
            if (!grabbable)
                grabbable = otherCollider.GetComponentInParent<Grabbable>();
            if (!grabbable)
                return;

            var grabCandidates =
                HandGrabCandidatesField.GetValue(hand) as System.Collections.Generic.Dictionary<Grabbable, int>;
            if (grabCandidates == null)
                return;

            grabbable.GrabEnter();

            int count;
            grabCandidates.TryGetValue(grabbable, out count);
            grabCandidates[grabbable] = count + 1;
        }

        private static bool IsDesktopNoHmdMode()
        {
            return !VRDevice.isPresent || GlobalVRKanojo.ForceMouse_;
        }

        private static void CreateCrosshair()
        {
            var managerObj = Chainloader.ManagerObject.transform;
            if (managerObj.FindChild("CrosshairCanvas")) return;

            var cursorData = ResourceUtils.GetEmbeddedResource("crosshair.png") ?? throw new Exception("crosshair.png resource not found");

            var crosshairRoot = new GameObject("CrosshairCanvas");
            crosshairRoot.transform.parent = managerObj;

            var canvas = crosshairRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            crosshairRoot.AddComponent<CanvasRenderer>();

            _cursorCg = crosshairRoot.AddComponent<CanvasGroup>();
            _cursorCg.interactable = false;
            _cursorCg.blocksRaycasts = false;
            _cursorCg.alpha = 1;

            var sc = crosshairRoot.AddComponent<CanvasScaler>();
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.uiScaleMode = CanvasScaler.ScaleMode.ConstantPhysicalSize;

            var imageTr = new GameObject("CrosshairImage");
            imageTr.transform.parent = crosshairRoot.transform;
            imageTr.transform.localPosition = Vector3.zero;
            imageTr.transform.localEulerAngles = Vector3.zero;
            imageTr.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

            var cursorTex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            cursorTex.LoadImage(cursorData);
            var imageComp = imageTr.AddComponent<Image>();
            imageComp.sprite = Sprite.Create(cursorTex, new Rect(0, 0, 300, 300), new Vector2(0.5f, 0.5f));

            var depthTextObj = new GameObject("HandDepthText");
            depthTextObj.transform.parent = crosshairRoot.transform;
            depthTextObj.transform.localPosition = new Vector3(0f, -46f, 0f);
            depthTextObj.transform.localEulerAngles = Vector3.zero;
            depthTextObj.transform.localScale = Vector3.one;

            _handDepthText = depthTextObj.AddComponent<Text>();
            _handDepthText.alignment = TextAnchor.MiddleCenter;
            _handDepthText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _handDepthText.fontSize = 14;
            _handDepthText.color = new Color(1f, 1f, 1f, 0.85f);
            _handDepthText.raycastTarget = false;
            _handDepthText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _handDepthText.verticalOverflow = VerticalWrapMode.Overflow;

            var depthRect = _handDepthText.rectTransform;
            depthRect.sizeDelta = new Vector2(160f, 24f);

            UpdateHandDepthIndicator();
        }
    }
}
