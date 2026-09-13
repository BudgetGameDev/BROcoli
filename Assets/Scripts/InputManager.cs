using UnityEngine;
using UnityEngine.InputSystem;
using System.Runtime.InteropServices;

[DefaultExecutionOrder(-1)]
public class InputManager : Singleton<InputManager>
{
    #if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int IsiOSMobile();
    
    [DllImport("__Internal")]
    private static extern int IsAndroidMobile();
    
    [DllImport("__Internal")]
    private static extern int IsMobileBrowser();
    #endif
    
    #region Events
    
    public delegate void StartTouch(Vector2 position, float time);
    public event StartTouch OnStartTouch;

    public delegate void EndTouch(Vector2 position, float time);
    public event EndTouch OnEndTouch;

    public delegate void SwipeDirection(Vector2 direction);
    public event SwipeDirection OnSwipeDirection;

    public delegate void UP(float axis);
    public event UP OnUP;

    public delegate void DOWN(float axis);
    public event DOWN OnDOWN;

    public delegate void LEFT(float axis);
    public event LEFT OnLEFT;

    public delegate void RIGHT(float axis);
    public event RIGHT OnRIGHT;

    #endregion

    private Camera mainCamera;

    private TouchAction touchAction;

    private void Awake() {
        touchAction = new TouchAction();
        mainCamera = Camera.main;
    }

    private void OnEnable() {
        if (touchAction != null)
            touchAction.Enable();
    }

    private void OnDisable() {
        if (touchAction != null)
            touchAction.Disable();
    }

    void Start()
    {
        touchAction.Touch.PrimaryContact.started += ctx => StartTouchPrimary(ctx);
        touchAction.Touch.PrimaryContact.canceled += ctx => EndTouchPrimary(ctx);

        touchAction.Touch.UP.performed += ctx => UPPrimary(ctx);
        touchAction.Touch.DOWN.performed += ctx => DOWNPrimary(ctx);
        touchAction.Touch.LEFT.performed += ctx => LEFTPrimary(ctx);
        touchAction.Touch.RIGHT.performed += ctx => RIGHTPrimary(ctx);
        
        // Activate VirtualController on mobile platforms
        ActivateVirtualControllerIfMobile();
    }
    
    private void ActivateVirtualControllerIfMobile()
    {
        bool isMobile = false;
        
        // For WebGL builds, use JavaScript-based detection (critical for iOS Safari)
        #if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            bool isMobileWebGL = IsMobileBrowser() == 1;
            bool isiOS = IsiOSMobile() == 1;
            bool isAndroid = IsAndroidMobile() == 1;
            
            Debug.Log($"[InputManager] WebGL JS detection - isMobile: {isMobileWebGL}, iOS: {isiOS}, Android: {isAndroid}");
            
            if (isMobileWebGL || isiOS || isAndroid)
            {
                isMobile = true;
                Debug.Log("[InputManager] Mobile browser detected via JavaScript");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[InputManager] JavaScript mobile detection failed: {e.Message}");
        }
        #endif
        
        // Preprocessor directives for native mobile builds
        #if UNITY_IOS && !UNITY_EDITOR
        isMobile = true;
        Debug.Log("[InputManager] iOS build detected via preprocessor");
        #elif UNITY_ANDROID && !UNITY_EDITOR
        isMobile = true;
        Debug.Log("[InputManager] Android build detected via preprocessor");
        #endif
        
        // Runtime checks as backup
        if (!isMobile)
        {
            isMobile = Application.platform == RuntimePlatform.IPhonePlayer ||
                       Application.platform == RuntimePlatform.Android ||
                       SystemInfo.deviceType == DeviceType.Handheld;
        }
        
        // For WebGL, also check touch support as fallback
        if (!isMobile && Application.platform == RuntimePlatform.WebGLPlayer)
        {
            if (Input.touchSupported)
            {
                isMobile = true;
                Debug.Log("[InputManager] WebGL with touch support - enabling mobile controls");
            }
        }
        
        #if UNITY_EDITOR
        if (UnityEngine.Device.SystemInfo.deviceType == DeviceType.Handheld || Input.touchSupported)
        {
            isMobile = true;
        }
        #endif
        
        Debug.Log($"[InputManager] Platform: {Application.platform}, DeviceType: {SystemInfo.deviceType}, isMobile: {isMobile}");
        
        if (isMobile)
        {
            // Find the VirtualController (it starts inactive in scene)
            var vc = FindObjectOfType<VirtualController>(true); // true = include inactive
            if (vc != null)
            {
                vc.gameObject.SetActive(true);
                Debug.Log($"[InputManager] VirtualController found and activated. Active: {vc.gameObject.activeInHierarchy}");
            }
            else
            {
                Debug.LogWarning("[InputManager] VirtualController not found in scene!");
            }
        }
    }
    
    // Also try activating in Update for first few frames in case of race conditions
    private int mobileCheckFrames = 10; // Increased for WebGL which may have delayed JS init
    private void Update()
    {
        if (mobileCheckFrames > 0)
        {
            mobileCheckFrames--;
            
            bool shouldActivate = false;
            
            #if UNITY_WEBGL && !UNITY_EDITOR
            // For WebGL, check via JavaScript
            try
            {
                shouldActivate = IsMobileBrowser() == 1 || IsiOSMobile() == 1 || IsAndroidMobile() == 1;
            }
            catch
            {
                // JavaScript not ready yet, try again next frame
            }
            #elif UNITY_IOS || UNITY_ANDROID
            shouldActivate = true;
            #endif
            
            if (shouldActivate)
            {
                var vc = FindObjectOfType<VirtualController>(true);
                if (vc != null && !vc.gameObject.activeInHierarchy)
                {
                    vc.gameObject.SetActive(true);
                    Debug.Log($"[InputManager] VirtualController re-activated in Update frame {10 - mobileCheckFrames}");
                }
            }
        }
    }

    private void StartTouchPrimary(InputAction.CallbackContext context) {
        if (OnStartTouch != null) OnStartTouch(Utils.ScreenToWorld(mainCamera, touchAction.Touch.PrimaryPosition.ReadValue<Vector2>()), (float)context.startTime);
    }

    private void EndTouchPrimary(InputAction.CallbackContext context) {
        if (OnEndTouch != null) OnEndTouch(Utils.ScreenToWorld(mainCamera, touchAction.Touch.PrimaryPosition.ReadValue<Vector2>()), (float)context.time);
    }

    private void UPPrimary(InputAction.CallbackContext context) {
        if (OnUP != null) OnUP(touchAction.Touch.UP.ReadValue<float>());
    }

    private void DOWNPrimary(InputAction.CallbackContext context) {
        if (OnDOWN != null) OnDOWN(touchAction.Touch.DOWN.ReadValue<float>());
    }

    private void LEFTPrimary(InputAction.CallbackContext context) {
        if (OnLEFT != null) OnLEFT(touchAction.Touch.LEFT.ReadValue<float>());
    }

    private void RIGHTPrimary(InputAction.CallbackContext context) {
        if (OnRIGHT != null) OnRIGHT(touchAction.Touch.RIGHT.ReadValue<float>());
    }

    public Vector2 PrimaryPosition() {
        return Utils.ScreenToWorld(mainCamera, touchAction.Touch.PrimaryPosition.ReadValue<Vector2>());
    }

    public void TriggerSwipeDirection(Vector2 direction) {
        if (OnSwipeDirection != null) OnSwipeDirection(direction);
    }
}
