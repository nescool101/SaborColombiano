using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SaborColombiano.Input
{
    /// <summary>
    /// Unified input manager that abstracts mouse (editor) and touch (mobile) input
    /// into a single set of events. Detects single taps, double taps, long presses,
    /// drags, pinch-to-zoom, and two-finger panning.
    /// <para>
    /// Attach to a persistent GameObject in the scene. Other systems (e.g.
    /// <see cref="CameraController"/>, placement system) subscribe to the events
    /// they care about rather than polling <c>UnityEngine.Input</c> directly.
    /// </para>
    /// </summary>
    public class TouchInputManager : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Events
        // ------------------------------------------------------------------ //

        /// <summary>Raised on a single tap/click at the given screen position.</summary>
        public event Action<Vector2> OnTap;

        /// <summary>Raised on a double tap/click at the given screen position.</summary>
        public event Action<Vector2> OnDoubleTap;

        /// <summary>Raised when the pointer is held without moving for <see cref="_longPressDuration"/> seconds.</summary>
        public event Action<Vector2> OnLongPress;

        /// <summary>Raised when a drag gesture begins. Provides the screen position where the drag started.</summary>
        public event Action<Vector2> OnDragStart;

        /// <summary>Raised each frame during a drag. Provides the screen-space delta since the last frame.</summary>
        public event Action<Vector2> OnDrag;

        /// <summary>Raised when a drag gesture ends. Provides the final screen position.</summary>
        public event Action<Vector2> OnDragEnd;

        /// <summary>
        /// Raised during a pinch-to-zoom gesture. Provides the zoom delta
        /// (positive = zoom in, negative = zoom out). In the editor, this maps
        /// to the mouse scroll wheel.
        /// </summary>
        public event Action<float> OnPinchZoom;

        /// <summary>
        /// Raised during a two-finger pan gesture. Provides the screen-space
        /// delta of the midpoint between the two touches. In the editor, this
        /// maps to middle-mouse-button drag.
        /// </summary>
        public event Action<Vector2> OnPan;

        // ------------------------------------------------------------------ //
        //  Inspector fields
        // ------------------------------------------------------------------ //

        [Header("Tap Detection")]

        [SerializeField]
        [Tooltip("Maximum distance in pixels the pointer can move and still count as a tap (dead zone).")]
        [Range(1f, 50f)]
        private float _tapDeadZone = 10f;

        [SerializeField]
        [Tooltip("Maximum time in seconds between two taps to register as a double tap.")]
        [Range(0.1f, 1f)]
        private float _doubleTapMaxInterval = 0.3f;

        [Header("Long Press")]

        [SerializeField]
        [Tooltip("Duration in seconds the pointer must be held without moving to trigger a long press.")]
        [Range(0.1f, 2f)]
        private float _longPressDuration = 0.5f;

        [Header("Zoom (Editor)")]

        [SerializeField]
        [Tooltip("Multiplier applied to the mouse scroll wheel delta for zoom events.")]
        [Range(0.1f, 10f)]
        private float _scrollZoomSensitivity = 1f;

        // ------------------------------------------------------------------ //
        //  Private state -- single pointer (touch 0 / left mouse)
        // ------------------------------------------------------------------ //

        /// <summary>Whether the primary pointer is currently held down.</summary>
        private bool _isPointerDown;

        /// <summary>Screen position where the primary pointer went down.</summary>
        private Vector2 _pointerDownPosition;

        /// <summary>Time when the primary pointer went down.</summary>
        private float _pointerDownTime;

        /// <summary>Whether the current gesture has exceeded the dead zone and become a drag.</summary>
        private bool _isDragging;

        /// <summary>Whether a long press has already been fired for the current hold.</summary>
        private bool _longPressFired;

        /// <summary>Screen position of the pointer on the previous frame (for delta calculation).</summary>
        private Vector2 _previousPointerPosition;

        // ------------------------------------------------------------------ //
        //  Private state -- double tap
        // ------------------------------------------------------------------ //

        /// <summary>Time of the last completed single tap (for double-tap detection).</summary>
        private float _lastTapTime;

        /// <summary>Screen position of the last completed single tap.</summary>
        private Vector2 _lastTapPosition;

        /// <summary>Whether we are waiting to see if a second tap arrives to form a double tap.</summary>
        private bool _waitingForSecondTap;

        // ------------------------------------------------------------------ //
        //  Private state -- two-finger gestures (touch only)
        // ------------------------------------------------------------------ //

        /// <summary>Distance between two fingers on the previous frame (pinch detection).</summary>
        private float _previousPinchDistance;

        /// <summary>Midpoint between two fingers on the previous frame (pan detection).</summary>
        private Vector2 _previousPanMidpoint;

        /// <summary>Whether a two-finger gesture is currently active.</summary>
        private bool _isTwoFingerGestureActive;

        // ------------------------------------------------------------------ //
        //  Private state -- middle mouse pan (editor)
        // ------------------------------------------------------------------ //

        /// <summary>Whether the middle mouse button is currently held for panning.</summary>
        private bool _isMiddleMousePanning;

        /// <summary>Screen position of the middle mouse button on the previous frame.</summary>
        private Vector2 _previousMiddleMousePosition;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Update()
        {
            // Skip all input when the pointer is over a UI element.
            if (IsPointerOverUI())
            {
                // If we were in the middle of a gesture, cancel it gracefully.
                if (_isDragging)
                {
                    _isDragging = false;
                    OnDragEnd?.Invoke(GetPointerPosition());
                }
                _isPointerDown = false;
                _isMiddleMousePanning = false;
                _isTwoFingerGestureActive = false;
                return;
            }

            // Mobile touch input takes priority when touches are present.
            if (UnityEngine.Input.touchCount > 0)
            {
                HandleTouchInput();
            }
            else
            {
                HandleMouseInput();
            }
        }

        // ------------------------------------------------------------------ //
        //  Mouse input (editor / standalone)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Processes mouse input: left button for tap/drag, scroll wheel for zoom,
        /// and middle button for panning.
        /// </summary>
        private void HandleMouseInput()
        {
            // ---- Scroll wheel zoom ----
            float scroll = UnityEngine.Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                OnPinchZoom?.Invoke(scroll * _scrollZoomSensitivity);
            }

            // ---- Middle mouse button pan ----
            if (UnityEngine.Input.GetMouseButtonDown(2))
            {
                _isMiddleMousePanning = true;
                _previousMiddleMousePosition = UnityEngine.Input.mousePosition;
            }
            else if (UnityEngine.Input.GetMouseButton(2) && _isMiddleMousePanning)
            {
                Vector2 currentPos = UnityEngine.Input.mousePosition;
                Vector2 delta = currentPos - _previousMiddleMousePosition;
                if (delta.sqrMagnitude > 0.01f)
                {
                    OnPan?.Invoke(delta);
                }
                _previousMiddleMousePosition = currentPos;
            }
            if (UnityEngine.Input.GetMouseButtonUp(2))
            {
                _isMiddleMousePanning = false;
            }

            // ---- Left mouse button: tap / drag / long press ----
            Vector2 mousePos = UnityEngine.Input.mousePosition;

            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                HandlePointerDown(mousePos);
            }
            else if (UnityEngine.Input.GetMouseButton(0) && _isPointerDown)
            {
                HandlePointerHeld(mousePos);
            }

            if (UnityEngine.Input.GetMouseButtonUp(0) && _isPointerDown)
            {
                HandlePointerUp(mousePos);
            }
        }

        // ------------------------------------------------------------------ //
        //  Touch input (mobile)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Processes touch input. Handles single-finger tap/drag/long-press and
        /// two-finger pinch-zoom and pan gestures.
        /// </summary>
        private void HandleTouchInput()
        {
            int touchCount = UnityEngine.Input.touchCount;

            // ---- Two-finger gestures ----
            if (touchCount >= 2)
            {
                // If we were dragging with one finger, end the drag first.
                if (_isDragging)
                {
                    _isDragging = false;
                    OnDragEnd?.Invoke(UnityEngine.Input.GetTouch(0).position);
                }
                _isPointerDown = false;

                Touch touch0 = UnityEngine.Input.GetTouch(0);
                Touch touch1 = UnityEngine.Input.GetTouch(1);

                Vector2 midpoint = (touch0.position + touch1.position) * 0.5f;
                float currentDistance = Vector2.Distance(touch0.position, touch1.position);

                if (!_isTwoFingerGestureActive)
                {
                    // First frame of two-finger gesture: initialise baselines.
                    _isTwoFingerGestureActive = true;
                    _previousPinchDistance = currentDistance;
                    _previousPanMidpoint = midpoint;
                }
                else
                {
                    // Pinch zoom.
                    float pinchDelta = currentDistance - _previousPinchDistance;
                    if (Mathf.Abs(pinchDelta) > 0.5f)
                    {
                        // Normalise by screen height so the gesture feels consistent
                        // across different screen sizes.
                        float normalisedDelta = pinchDelta / Screen.height * 10f;
                        OnPinchZoom?.Invoke(normalisedDelta);
                    }

                    // Two-finger pan.
                    Vector2 panDelta = midpoint - _previousPanMidpoint;
                    if (panDelta.sqrMagnitude > 0.5f)
                    {
                        OnPan?.Invoke(panDelta);
                    }

                    _previousPinchDistance = currentDistance;
                    _previousPanMidpoint = midpoint;
                }

                return;
            }

            // Reset two-finger state when fewer than two fingers are touching.
            _isTwoFingerGestureActive = false;

            // ---- Single-finger gestures ----
            if (touchCount == 1)
            {
                Touch touch = UnityEngine.Input.GetTouch(0);

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        HandlePointerDown(touch.position);
                        break;

                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        HandlePointerHeld(touch.position);
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        HandlePointerUp(touch.position);
                        break;
                }
            }
        }

        // ------------------------------------------------------------------ //
        //  Unified pointer logic
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Called when the primary pointer goes down. Records the position and
        /// time for subsequent gesture detection.
        /// </summary>
        /// <param name="screenPos">Screen position of the pointer.</param>
        private void HandlePointerDown(Vector2 screenPos)
        {
            _isPointerDown = true;
            _pointerDownPosition = screenPos;
            _pointerDownTime = Time.unscaledTime;
            _isDragging = false;
            _longPressFired = false;
            _previousPointerPosition = screenPos;
        }

        /// <summary>
        /// Called each frame while the primary pointer is held. Detects
        /// transitions from "stationary" to "drag" and triggers long-press
        /// when the hold duration threshold is exceeded.
        /// </summary>
        /// <param name="screenPos">Current screen position of the pointer.</param>
        private void HandlePointerHeld(Vector2 screenPos)
        {
            if (!_isPointerDown)
                return;

            float distanceFromStart = Vector2.Distance(screenPos, _pointerDownPosition);

            if (!_isDragging)
            {
                // Check if we have exceeded the dead zone.
                if (distanceFromStart > _tapDeadZone)
                {
                    _isDragging = true;
                    OnDragStart?.Invoke(_pointerDownPosition);
                    _previousPointerPosition = screenPos;
                }
                else
                {
                    // Still within dead zone -- check for long press.
                    if (!_longPressFired &&
                        (Time.unscaledTime - _pointerDownTime) >= _longPressDuration)
                    {
                        _longPressFired = true;
                        OnLongPress?.Invoke(screenPos);
                    }
                }
            }
            else
            {
                // Already dragging -- emit drag delta.
                Vector2 delta = screenPos - _previousPointerPosition;
                if (delta.sqrMagnitude > 0.01f)
                {
                    OnDrag?.Invoke(delta);
                }
                _previousPointerPosition = screenPos;
            }
        }

        /// <summary>
        /// Called when the primary pointer is released. Finalises drags or
        /// resolves tap / double-tap.
        /// </summary>
        /// <param name="screenPos">Screen position where the pointer was released.</param>
        private void HandlePointerUp(Vector2 screenPos)
        {
            if (!_isPointerDown)
                return;

            _isPointerDown = false;

            if (_isDragging)
            {
                _isDragging = false;
                OnDragEnd?.Invoke(screenPos);
                return;
            }

            // If a long press was already fired, do not also fire a tap.
            if (_longPressFired)
                return;

            // ---- Tap / double-tap resolution ----
            float now = Time.unscaledTime;

            if (_waitingForSecondTap &&
                (now - _lastTapTime) <= _doubleTapMaxInterval &&
                Vector2.Distance(screenPos, _lastTapPosition) <= _tapDeadZone * 2f)
            {
                // Second tap arrived in time -- double tap.
                _waitingForSecondTap = false;
                OnDoubleTap?.Invoke(screenPos);
            }
            else
            {
                // First tap (or the second tap was too slow / too far away).
                _waitingForSecondTap = true;
                _lastTapTime = now;
                _lastTapPosition = screenPos;

                // Fire the single tap immediately. If a double-tap follows
                // we will fire that separately.
                OnTap?.Invoke(screenPos);
            }
        }

        // ------------------------------------------------------------------ //
        //  Public helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Converts a screen-space position to a world-space position by casting
        /// a ray from <c>Camera.main</c>. For 2D / isometric games this returns the
        /// point on the Z = 0 plane.
        /// </summary>
        /// <param name="screenPos">Screen-space position (e.g. from a touch or mouse).</param>
        /// <returns>
        /// World-space position on the Z = 0 plane, or <c>Vector3.zero</c> if the
        /// main camera is unavailable.
        /// </returns>
        public Vector3 GetWorldPosition(Vector2 screenPos)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[TouchInputManager] Camera.main is null. Cannot convert screen position.");
                return Vector3.zero;
            }

            if (cam.orthographic)
            {
                // Orthographic: direct conversion.
                Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, cam.nearClipPlane));
                worldPos.z = 0f;
                return worldPos;
            }
            else
            {
                // Perspective: raycast against the Z = 0 plane.
                Ray ray = cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
                Plane groundPlane = new Plane(Vector3.forward, Vector3.zero);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    return ray.GetPoint(enter);
                }

                Debug.LogWarning("[TouchInputManager] Screen ray did not hit the ground plane.");
                return Vector3.zero;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the primary pointer (first touch or mouse) is
        /// currently positioned over a UI element managed by the
        /// <see cref="EventSystem"/>. Input events are suppressed when this is the case.
        /// </summary>
        /// <returns><c>true</c> when the pointer is over UI.</returns>
        public bool IsPointerOverUI()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            // Touch input.
            if (UnityEngine.Input.touchCount > 0)
            {
                return eventSystem.IsPointerOverGameObject(UnityEngine.Input.GetTouch(0).fingerId);
            }

            // Mouse input.
            return eventSystem.IsPointerOverGameObject();
        }

        // ------------------------------------------------------------------ //
        //  Private helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns the current screen position of the primary pointer (first touch
        /// or mouse position).
        /// </summary>
        private Vector2 GetPointerPosition()
        {
            if (UnityEngine.Input.touchCount > 0)
                return UnityEngine.Input.GetTouch(0).position;
            return UnityEngine.Input.mousePosition;
        }
    }
}
