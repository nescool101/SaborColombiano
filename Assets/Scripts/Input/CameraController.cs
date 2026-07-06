using UnityEngine;

namespace SaborColombiano.Input
{
    /// <summary>
    /// Controls the orthographic isometric camera for the restaurant view.
    /// Supports smooth panning (drag / two-finger pan), zooming (pinch / scroll wheel),
    /// boundary clamping, and programmatic focus transitions.
    /// <para>
    /// Requires a <see cref="TouchInputManager"/> in the scene to receive input events.
    /// Attach this component to the same GameObject as the main <see cref="Camera"/>,
    /// or assign the camera reference in the Inspector.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector fields
        // ------------------------------------------------------------------ //

        [Header("References")]

        [SerializeField]
        [Tooltip("Reference to the TouchInputManager in the scene. " +
                 "Auto-discovered at runtime if left empty.")]
        private TouchInputManager _inputManager;

        [Header("Pan")]

        [SerializeField]
        [Tooltip("Multiplier for pan speed. Higher values move the camera faster per pixel of drag.")]
        [Range(0.001f, 0.1f)]
        private float _panSpeed = 0.02f;

        [Header("Zoom")]

        [SerializeField]
        [Tooltip("Multiplier for zoom speed. Applied to the zoom delta from pinch/scroll events.")]
        [Range(0.1f, 5f)]
        private float _zoomSpeed = 1f;

        [SerializeField]
        [Tooltip("Minimum orthographic size (most zoomed in).")]
        [Range(1f, 10f)]
        private float _minZoom = 3f;

        [SerializeField]
        [Tooltip("Maximum orthographic size (most zoomed out).")]
        [Range(5f, 30f)]
        private float _maxZoom = 15f;

        [Header("Smoothing")]

        [SerializeField]
        [Tooltip("Smooth time for SmoothDamp-based position transitions. " +
                 "Lower values = snappier movement.")]
        [Range(0.01f, 1f)]
        private float _smoothTime = 0.15f;

        [SerializeField]
        [Tooltip("Smooth time for zoom transitions (orthographic size interpolation).")]
        [Range(0.01f, 1f)]
        private float _zoomSmoothTime = 0.1f;

        [Header("Bounds")]

        [SerializeField]
        [Tooltip("When true, camera position is clamped to the configured bounds rectangle.")]
        private bool _useBounds = true;

        [SerializeField]
        [Tooltip("World-space minimum corner of the camera bounds rectangle (bottom-left).")]
        private Vector2 _boundsMin = new Vector2(-15f, -10f);

        [SerializeField]
        [Tooltip("World-space maximum corner of the camera bounds rectangle (top-right).")]
        private Vector2 _boundsMax = new Vector2(15f, 10f);

        [Header("Defaults")]

        [SerializeField]
        [Tooltip("Default camera position used by ResetCamera().")]
        private Vector3 _defaultPosition = new Vector3(0f, 0f, -10f);

        [SerializeField]
        [Tooltip("Default orthographic size used by ResetCamera().")]
        [Range(1f, 30f)]
        private float _defaultZoom = 8f;

        // ------------------------------------------------------------------ //
        //  Public properties
        // ------------------------------------------------------------------ //

        /// <summary>Pan speed multiplier. Adjustable at runtime.</summary>
        public float PanSpeed
        {
            get => _panSpeed;
            set => _panSpeed = Mathf.Max(0.001f, value);
        }

        /// <summary>Zoom speed multiplier. Adjustable at runtime.</summary>
        public float ZoomSpeed
        {
            get => _zoomSpeed;
            set => _zoomSpeed = Mathf.Max(0.1f, value);
        }

        /// <summary>Minimum orthographic size (maximum zoom-in level).</summary>
        public float MinZoom
        {
            get => _minZoom;
            set => _minZoom = Mathf.Max(0.5f, value);
        }

        /// <summary>Maximum orthographic size (maximum zoom-out level).</summary>
        public float MaxZoom
        {
            get => _maxZoom;
            set => _maxZoom = Mathf.Max(_minZoom + 0.5f, value);
        }

        /// <summary>SmoothDamp time for position transitions.</summary>
        public float SmoothTime
        {
            get => _smoothTime;
            set => _smoothTime = Mathf.Max(0.01f, value);
        }

        // ------------------------------------------------------------------ //
        //  Private state
        // ------------------------------------------------------------------ //

        /// <summary>Cached camera reference.</summary>
        private Camera _camera;

        /// <summary>Desired camera position that we smooth-damp towards.</summary>
        private Vector3 _targetPosition;

        /// <summary>Desired orthographic size that we smooth-damp towards.</summary>
        private float _targetZoom;

        /// <summary>Velocity vector used by <see cref="Vector3.SmoothDamp"/>.</summary>
        private Vector3 _positionVelocity;

        /// <summary>Velocity scalar used by <see cref="Mathf.SmoothDamp"/> for zoom.</summary>
        private float _zoomVelocity;

        /// <summary>Whether a programmatic focus transition is currently in progress.</summary>
        private bool _isFocusing;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                Debug.LogError("[CameraController] No Camera component found on this GameObject.");
                enabled = false;
                return;
            }

            // Ensure the camera is orthographic for isometric rendering.
            _camera.orthographic = true;

            _targetPosition = transform.position;
            _targetZoom = _camera.orthographicSize;
        }

        private void OnEnable()
        {
            if (_inputManager == null)
            {
                _inputManager = FindObjectOfType<TouchInputManager>();
            }

            if (_inputManager != null)
            {
                SubscribeToInput();
            }
            else
            {
                Debug.LogWarning("[CameraController] No TouchInputManager found. " +
                                 "Camera will not respond to input events.");
            }
        }

        private void OnDisable()
        {
            if (_inputManager != null)
            {
                UnsubscribeFromInput();
            }
        }

        private void LateUpdate()
        {
            // Smooth position.
            transform.position = Vector3.SmoothDamp(
                transform.position,
                _targetPosition,
                ref _positionVelocity,
                _smoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );

            // Smooth zoom.
            _camera.orthographicSize = Mathf.SmoothDamp(
                _camera.orthographicSize,
                _targetZoom,
                ref _zoomVelocity,
                _zoomSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );

            // Check if focus transition has settled.
            if (_isFocusing)
            {
                float distSq = (transform.position - _targetPosition).sqrMagnitude;
                float zoomDiff = Mathf.Abs(_camera.orthographicSize - _targetZoom);
                if (distSq < 0.001f && zoomDiff < 0.01f)
                {
                    _isFocusing = false;
                }
            }
        }

        // ------------------------------------------------------------------ //
        //  Event subscriptions
        // ------------------------------------------------------------------ //

        /// <summary>Subscribes to all relevant <see cref="TouchInputManager"/> events.</summary>
        private void SubscribeToInput()
        {
            _inputManager.OnDrag += HandleDrag;
            _inputManager.OnPan += HandlePan;
            _inputManager.OnPinchZoom += HandleZoom;
        }

        /// <summary>Unsubscribes from all <see cref="TouchInputManager"/> events.</summary>
        private void UnsubscribeFromInput()
        {
            _inputManager.OnDrag -= HandleDrag;
            _inputManager.OnPan -= HandlePan;
            _inputManager.OnPinchZoom -= HandleZoom;
        }

        // ------------------------------------------------------------------ //
        //  Input handlers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Handles single-finger drag input to pan the camera. The camera moves
        /// in the opposite direction of the drag so the world appears to follow
        /// the finger.
        /// </summary>
        /// <param name="screenDelta">Screen-space delta of the drag gesture.</param>
        private void HandleDrag(Vector2 screenDelta)
        {
            ApplyPan(screenDelta);
        }

        /// <summary>
        /// Handles two-finger pan input. Behaviour is identical to single-finger
        /// drag but sourced from the two-finger midpoint delta.
        /// </summary>
        /// <param name="screenDelta">Screen-space delta of the two-finger midpoint.</param>
        private void HandlePan(Vector2 screenDelta)
        {
            ApplyPan(screenDelta);
        }

        /// <summary>
        /// Applies a pan offset to the camera's target position. Converts the
        /// screen-space delta into world-space units and moves the camera in the
        /// opposite direction of the gesture so the scene follows the pointer.
        /// </summary>
        /// <param name="screenDelta">Screen-space delta to convert and apply.</param>
        private void ApplyPan(Vector2 screenDelta)
        {
            // Cancel any programmatic focus when the player manually pans.
            _isFocusing = false;

            // Convert screen delta to world delta using the current orthographic size.
            float worldUnitsPerPixel = (_camera.orthographicSize * 2f) / Screen.height;
            Vector3 worldDelta = new Vector3(
                -screenDelta.x * worldUnitsPerPixel * _panSpeed * 10f,
                -screenDelta.y * worldUnitsPerPixel * _panSpeed * 10f,
                0f
            );

            _targetPosition += worldDelta;
            ClampTargetPosition();
        }

        /// <summary>
        /// Handles zoom input from pinch gestures or scroll wheel. Adjusts the
        /// target orthographic size within the configured min/max range.
        /// </summary>
        /// <param name="zoomDelta">
        /// Zoom delta: positive to zoom in (decrease ortho size),
        /// negative to zoom out (increase ortho size).
        /// </param>
        private void HandleZoom(float zoomDelta)
        {
            _targetZoom -= zoomDelta * _zoomSpeed;
            _targetZoom = Mathf.Clamp(_targetZoom, _minZoom, _maxZoom);

            // Re-clamp position since the visible area changed.
            ClampTargetPosition();
        }

        // ------------------------------------------------------------------ //
        //  Bounds clamping
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Clamps <see cref="_targetPosition"/> so the camera does not move beyond
        /// the configured bounds rectangle. Takes the current orthographic size and
        /// aspect ratio into account so the visible area never exceeds the bounds.
        /// </summary>
        private void ClampTargetPosition()
        {
            if (!_useBounds)
                return;

            float halfHeight = _targetZoom;
            float halfWidth = halfHeight * _camera.aspect;

            float minX = _boundsMin.x + halfWidth;
            float maxX = _boundsMax.x - halfWidth;
            float minY = _boundsMin.y + halfHeight;
            float maxY = _boundsMax.y - halfHeight;

            // If the bounds are smaller than the camera view, centre the camera.
            if (minX > maxX) minX = maxX = (_boundsMin.x + _boundsMax.x) * 0.5f;
            if (minY > maxY) minY = maxY = (_boundsMin.y + _boundsMax.y) * 0.5f;

            _targetPosition.x = Mathf.Clamp(_targetPosition.x, minX, maxX);
            _targetPosition.y = Mathf.Clamp(_targetPosition.y, minY, maxY);
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Smoothly moves the camera to centre on the given world position.
        /// The transition uses the same SmoothDamp parameters as regular movement.
        /// Any ongoing manual pan will cancel the focus.
        /// </summary>
        /// <param name="worldPosition">World position to focus on.</param>
        public void FocusOn(Vector3 worldPosition)
        {
            _targetPosition = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            ClampTargetPosition();
            _isFocusing = true;
        }

        /// <summary>
        /// Smoothly returns the camera to the default position and zoom level
        /// configured in the Inspector.
        /// </summary>
        public void ResetCamera()
        {
            _targetPosition = _defaultPosition;
            _targetZoom = _defaultZoom;
            ClampTargetPosition();
            _isFocusing = true;
        }

        /// <summary>
        /// Configures the camera bounds to match a <c>GridManager</c>'s restaurant
        /// dimensions. Call this after the grid is initialised or resized.
        /// </summary>
        /// <param name="worldMin">World-space bottom-left corner of the restaurant area.</param>
        /// <param name="worldMax">World-space top-right corner of the restaurant area.</param>
        /// <param name="padding">
        /// Extra world-space padding around the restaurant to allow slight overshoot.
        /// </param>
        public void SetBounds(Vector2 worldMin, Vector2 worldMax, float padding = 2f)
        {
            _boundsMin = worldMin - Vector2.one * padding;
            _boundsMax = worldMax + Vector2.one * padding;
            _useBounds = true;
            ClampTargetPosition();
        }

        /// <summary>
        /// Immediately snaps the camera to the given position and zoom level
        /// without any smooth interpolation.
        /// </summary>
        /// <param name="position">World position (Z is preserved from the current transform).</param>
        /// <param name="zoom">Orthographic size to apply.</param>
        public void SnapTo(Vector3 position, float zoom)
        {
            _targetPosition = new Vector3(position.x, position.y, transform.position.z);
            _targetZoom = Mathf.Clamp(zoom, _minZoom, _maxZoom);

            transform.position = _targetPosition;
            _camera.orthographicSize = _targetZoom;

            _positionVelocity = Vector3.zero;
            _zoomVelocity = 0f;
            _isFocusing = false;

            ClampTargetPosition();
        }

        // ------------------------------------------------------------------ //
        //  Editor gizmos
        // ------------------------------------------------------------------ //

#if UNITY_EDITOR
        /// <summary>
        /// Draws the camera bounds rectangle in the Scene view for easy tuning.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!_useBounds)
                return;

            Gizmos.color = new Color(0f, 1f, 0.5f, 0.4f);
            Vector3 centre = new Vector3(
                (_boundsMin.x + _boundsMax.x) * 0.5f,
                (_boundsMin.y + _boundsMax.y) * 0.5f,
                0f
            );
            Vector3 size = new Vector3(
                _boundsMax.x - _boundsMin.x,
                _boundsMax.y - _boundsMin.y,
                0.1f
            );
            Gizmos.DrawWireCube(centre, size);
        }
#endif
    }
}
