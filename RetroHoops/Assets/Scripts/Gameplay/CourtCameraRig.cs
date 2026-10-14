using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// Portrait court camera: integer pixel zoom for crisp art, the court anchored under the
    /// HUD at the top of the screen (thumb zone below), and smooth horizontal follow of the
    /// controlled player, clamped to the court.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CourtCameraRig : MonoBehaviour
    {
        /// <summary>Always keep at least this much court width visible (m).</summary>
        [SerializeField] private float minVisibleWidth = 11.5f;
        /// <summary>And at least this much top to bottom (m): limits zoom on iPad and Mac, where width alone would crop the court.</summary>
        [SerializeField] private float minVisibleHeight = 20f;
        [SerializeField] private float followStiffness = 5f;
        /// <summary>Share of the screen height reserved for the HUD above the court.</summary>
        [SerializeField] private float hudReserve = 0.1f;

        private Camera _camera;
        private CourtGeometry _court;
        private float _minX, _maxX;
        private int _lastWidth, _lastHeight;
        private Rect _lastSafe;
        private float _targetY;
        /// <summary>Full Court: the camera also scrolls up and down the floor.</summary>
        private bool _vertical;
        private float _minY;
        private float _y;

        public int Zoom { get; private set; } = 1;

        /// <summary>
        /// 14 Oct 2026 (Omari): FILL SCREEN WITH COURT (portrait half court, on by default). The camera comes in so the
        /// court runs from under the score bar to the bottom of the screen, behind the see-through buttons, and follows
        /// the play both ways.
        /// </summary>
        public bool Fill { get; private set; }

        /// <summary>Phase 35 replay theater CLOSE-UP: one zoom step closer, following the play up and down too (portrait only).</summary>
        private bool _closeUp;

        public void SetCloseUp(bool on)
        {
            if (_landscape) on = false;
            if (on == _closeUp || _camera == null) return;
            _closeUp = on;
            Recompute();
        }

        private float _shake;

        /// <summary>Brief camera shake (dunks, blocks). Ignored when Screen Shake is off in Settings.</summary>
        public void Shake(float amount)
        {
            var settings = Core.App.Career?.settings;
            if (settings != null && (!settings.screenShake || settings.reduceMotion)) return;
            _shake = Mathf.Max(_shake, amount);
        }

        /// <param name="court">Simulation geometry (Full Court: the whole floor).</param>
        /// <param name="fullCourt">Scroll vertically to follow play between the two baskets.</param>
        /// <summary>Full Court on a landscape screen: the court runs left to right and the camera pans sideways.</summary>
        private bool _landscape;
        /// <summary>Landscape: at least this much court across the screen's height (sidelines plus room for the HUD), m.</summary>
        [SerializeField] private float landscapeVisibleHeight = 18f;

        public void Init(CourtGeometry court, Color background, bool fullCourt = false, bool landscape = false)
        {
            _landscape = landscape;
            _vertical = fullCourt && !landscape;
            var settings = Core.App.Career?.settings;
            Fill = !fullCourt && !landscape && (settings == null || settings.fillScreen);
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = background;
            _court = court;
            _minX = -court.HalfWidth - CourtGenerator.SideMargin;
            _maxX = court.HalfWidth + CourtGenerator.SideMargin;
            Recompute();
            _y = _targetY;
            transform.position = new Vector3(0f, _targetY, -10f);
        }

        /// <summary>
        /// Follows a position on the court as drawn (x across, y down from the top baseline): usually the
        /// controlled player, or the ball in 2 Player and Full Court.
        /// </summary>
        public void Follow(Vec2 target, float dt, bool snap = false)
        {
            if (_court == null) return;
            if (Screen.width != _lastWidth || Screen.height != _lastHeight || Screen.safeArea != _lastSafe) Recompute();
            if (_landscape)
            {
                FollowLandscape(target, dt, snap);
                return;
            }

            float halfW = _camera.orthographicSize * _camera.aspect;
            float goalY = _targetY;
            bool vertical = _vertical || _closeUp || Fill;
            if (vertical)
            {
                // Keep the play a little above the middle (the thumbs cover the bottom of the screen),
                // between the top of the court (under the HUD) and the bottom stands.
                goalY = Mathf.Clamp(-target.y - _camera.orthographicSize * 0.12f, _minY, _targetY);
            }
            var current = new Vec2(transform.position.x, vertical ? _y : _targetY);
            var goal = new Vec2(target.x, goalY);
            var next = snap ? goal : CameraMath.Follow(current, goal, dt, followStiffness);
            next = CameraMath.ClampView(next, halfW, _camera.orthographicSize,
                                        new Vec2(_minX, -1000f), new Vec2(_maxX, 1000f));
            float camY = vertical ? Mathf.Clamp(next.y, _minY, _targetY) : _targetY;
            _y = camY;
            // Snap the camera to the pixel grid of the current zoom to avoid shimmering.
            float step = 1f / (CourtSpace.PixelsPerUnit * Zoom);
            float sx = 0f, sy = 0f;
            if (_shake > 0.001f)
            {
                sx = (Random.value * 2f - 1f) * _shake;
                sy = (Random.value * 2f - 1f) * _shake;
                _shake = Mathf.MoveTowards(_shake, 0f, dt * 1.2f);
            }
            transform.position = new Vector3(Mathf.Round((next.x + sx) / step) * step, Mathf.Round((camY + sy) / step) * step, -10f);
        }

        private float _landscapeX;
        /// <summary>Height of the landscape sideline stands (m), drawn above the far sideline.</summary>
        public static float LandscapeStandsHeight => (4 + Logic.PixelArt.CrowdGenerator.StandRows * Logic.PixelArt.CrowdGenerator.StandRowHeight + 3) / CourtSpace.PixelsPerUnit;

        /// <summary>Landscape Full Court: pan with the play left and right; the sidelines stay in view below the HUD.</summary>
        private void FollowLandscape(Vec2 target, float dt, bool snap)
        {
            float length = CourtSpace.LandscapeLength;
            float halfW = _camera.orthographicSize * _camera.aspect;
            float goal = target.y - length * 0.5f;
            float min = -length * 0.5f - CourtGenerator.BaselineMargin, max = length * 0.5f + CourtGenerator.BaselineMargin;
            float x = snap ? goal : Mathf.Lerp(_landscapeX, goal, 1f - Mathf.Exp(-followStiffness * dt));
            x = halfW * 2f >= max - min ? (min + max) * 0.5f : Mathf.Clamp(x, min + halfW, max - halfW);
            _landscapeX = x;
            float step = 1f / (CourtSpace.PixelsPerUnit * Zoom);
            float sx = 0f, sy = 0f;
            if (_shake > 0.001f)
            {
                sx = (Random.value * 2f - 1f) * _shake;
                sy = (Random.value * 2f - 1f) * _shake;
                _shake = Mathf.MoveTowards(_shake, 0f, dt * 1.2f);
            }
            transform.position = new Vector3(Mathf.Round((x + sx) / step) * step, Mathf.Round((_targetY + sy) / step) * step, -10f);
        }

        private void Recompute()
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _lastSafe = Screen.safeArea;
            if (_landscape)
            {
                // Integer zoom that fits the court's width (plus margins) top to bottom, and a fair stretch across.
                Zoom = CameraMath.IntegerZoom(Screen.width, Screen.height, minVisibleWidth, landscapeVisibleHeight, CourtSpace.PixelsPerUnit);
                _camera.orthographicSize = CameraMath.OrthographicSize(Screen.height, Zoom, CourtSpace.PixelsPerUnit);
                // Near sideline at the bottom of the screen; above the far sideline, as much of the stands as fits
                // (the landscape HUD is a compact bar in the middle, so the stands show either side of it).
                float bottom = -_court.HalfWidth - CourtGenerator.SideMargin * 0.4f;
                float top = _court.HalfWidth + CourtGenerator.SideMargin + LandscapeStandsHeight;
                float ortho = _camera.orthographicSize;
                _targetY = 2f * ortho >= top - bottom ? (top + bottom) * 0.5f : bottom + ortho;
                return;
            }

            Zoom = CameraMath.IntegerZoom(Screen.width, Screen.height, minVisibleWidth, minVisibleHeight, CourtSpace.PixelsPerUnit) + (_closeUp ? 1 : 0);
            if (Fill)
            {
                float artHeight = _court.depth + CourtGenerator.BaselineMargin + CourtGenerator.TopMargin;
                Zoom = System.Math.Max(Zoom, CameraMath.FillZoom(Screen.height, artHeight, hudReserve, CourtSpace.PixelsPerUnit)) + (_closeUp ? 1 : 0);
            }
            _camera.orthographicSize = CameraMath.OrthographicSize(Screen.height, Zoom, CourtSpace.PixelsPerUnit);

            // Top of the court art (crowd strip) sits just below the HUD and the notch.
            float unitsPerPixel = 1f / (CourtSpace.PixelsPerUnit * Zoom);
            float topInsetPx = (Screen.height - _lastSafe.yMax) + Screen.height * hudReserve;
            float courtTopWorld = CourtGenerator.BaselineMargin; // baseline is world y = 0
            _targetY = courtTopWorld + topInsetPx * unitsPerPixel - _camera.orthographicSize;
            // Lowest camera centre: the bottom stands sit just above the thumb area (lower 40% of the screen).
            float courtBottomWorld = -(_court.depth + CourtGenerator.BaselineMargin);
            _minY = Mathf.Min(_targetY, courtBottomWorld + _camera.orthographicSize * 0.2f);
            // Filling the screen: never scroll past the bottom of the court art (the near side shows only a little).
            if (Fill) _minY = Mathf.Min(_targetY, -(_court.depth + CourtGenerator.TopMargin) - 1.5f + _camera.orthographicSize);
        }
    }
}
