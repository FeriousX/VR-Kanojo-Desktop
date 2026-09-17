using System;
using BepInEx;
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
        public const string Version = "1.0.0";
        public const string GUID = "com.ferious.vrkanojodesktop";

        private static new ManualLogSource Logger;
        private static VRKanojoDesktopPlugin Instance;
        private static OVRCameraRig _cameraRig;
        private static IllusionCamera _illusionCamera;
        private static bool _mouseCaptured;
        private static Hand _virtualLeftHand;
        private static TrackedController _virtualLeftController;
        private static Transform _virtualLeftTrackTrans;
        private static float _virtualHandDepth = DefaultVirtualHandDepth;

        private static readonly System.Reflection.MethodInfo IllusionCameraRotateMethod =
            AccessTools.Method(typeof(IllusionCamera), "Rotate", new[] { typeof(Vector3) });
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
        private const float MaxVirtualHandDepth = 2f;
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
            if (!_mouseCaptured)
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
            if (!_mouseCaptured || IllusionCameraKeySensitivityField == null)
                return;

            var wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Approximately(wheel, 0f))
                return;

            var keySensitivity = (float)IllusionCameraKeySensitivityField.GetValue(__instance);
            IllusionCameraKeySensitivityField.SetValue(
                __instance,
                keySensitivity - wheel * IllusionCameraWheelKeySensitivityScale);
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
            if (!VRDevice.isPresent)
            {
                // Fix lockState getting reset after losing focus
                Cursor.lockState = hasFocus && _mouseCaptured ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !hasFocus || !_mouseCaptured;
            }
        }

        private static CanvasGroup _cursorCg;
        private static Text _handDepthText;
        private float _inactiveTime = 5;

        private void Update()
        {
            if (!_cursorCg)
                return;

            UpdateMouseCapture();
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
            if (Input.GetKeyDown(KeyCode.Tab))
                SetMouseCaptured(!_mouseCaptured);
        }

        private static void SetMouseCaptured(bool captured)
        {
            _mouseCaptured = captured;
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
            UpdateHandDepthIndicator();
        }

        private static void UpdateMouseLook()
        {
            if (!_mouseCaptured || !_illusionCamera || IllusionCameraRotateMethod == null)
                return;

            var mouseX = Input.GetAxis("Mouse X");
            var mouseY = Input.GetAxis("Mouse Y");
            if (Mathf.Approximately(mouseX, 0f) && Mathf.Approximately(mouseY, 0f))
                return;

            var rotation = new Vector3(-mouseY * MouseLookSensitivity, mouseX * MouseLookSensitivity, 0f);
            IllusionCameraRotateMethod.Invoke(_illusionCamera, new object[] { rotation });
        }

        private static void UpdateVirtualLeftHand()
        {
            if (!_mouseCaptured)
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
                SetVirtualLeftHandInteractionVolumes(_mouseCaptured);
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
            return _mouseCaptured && Input.GetMouseButton(0);
        }

        private static bool IsVirtualLeftGrabEnabled()
        {
            return _mouseCaptured && Input.GetMouseButton(1);
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
