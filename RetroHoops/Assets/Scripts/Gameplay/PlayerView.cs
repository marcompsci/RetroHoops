using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// Draws one player from simulation state: body sprite (view, frame, flip), drop shadow,
    /// and the controlled-player ring. Reads state only; never changes it.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        private const float RunFps = 10f;
        private const float IdleFps = 2f;

        private PlayerRuntimeState _state;
        private Sprite[,] _frames;
        private SpriteRenderer _body;
        private SpriteRenderer _shadow;
        private SpriteRenderer _ring;
        private float _animTime;
        private Sprite[,] _heads;
        private SpriteRenderer _head;
        private const float BigHeadScale = 1.7f;
        private SpriteRenderer _reflection;

        /// <summary>
        /// G3 graphics: a faint upside-down copy of the player in a shiny floor (hardwood, neon glass, tiles). 0 = off.
        /// </summary>
        public void EnableReflection(float alpha)
        {
            if (alpha <= 0f || _reflection != null) return;
            _reflection = NewRenderer(transform, "Reflection", _body.sprite);
            _reflection.flipY = true;
            _reflection.color = new Color(1f, 1f, 1f, alpha);
        }

        /// <summary>Shadow, reflection and big head follow the body (G3: the shadow shrinks and fades in the air).</summary>
        private void SyncExtras(int view, int frame, bool flip, float liftWorld, float jump01, int order)
        {
            GameplayFx.ShadowForJump(jump01, out float scale, out float alpha);
            _shadow.transform.localScale = new Vector3(scale, scale, 1f);
            var sc = _shadow.color;
            if (!Mathf.Approximately(sc.a, alpha)) _shadow.color = new Color(sc.r, sc.g, sc.b, alpha);
            if (_reflection != null)
            {
                _reflection.sprite = _body.sprite;
                _reflection.flipX = flip;
                // Mirrored about the feet (the sprite's pivot is one art pixel above them): the higher the jump, the deeper the image.
                const float px = 1f / CourtSpace.PixelsPerUnit;
                _reflection.transform.localPosition = new Vector3(_body.transform.localPosition.x, -liftWorld - 2f * px, 0f);
                _reflection.sortingOrder = order - 3;
            }
            SyncHead(view, frame, flip);
        }

        /// <summary>BIG HEADS secret: draws an enlarged copy of the head above the body.</summary>
        public void EnableBigHead(Sprite[,] heads)
        {
            _heads = heads;
            _head = NewRenderer(transform, "BigHead", heads[(int)CharacterView.Back, 0]);
            _head.transform.localScale = new Vector3(BigHeadScale, BigHeadScale, 1f);
        }

        private void SyncHead(int view, int frame, bool flip)
        {
            if (_head == null) return;
            _head.sprite = _heads[view, frame];
            _head.flipX = flip;
            // The head sprite's pivot is its bottom row; the body's pivot is one pixel above its feet.
            int row = CharacterSpriteGenerator.HeadBottomRow(_state.Def.appearance, frame);
            var body = _body.transform.localPosition;
            _head.transform.localPosition = body + new Vector3(0f, (row - 1) / CourtSpace.PixelsPerUnit, 0f);
            _head.sortingOrder = _body.sortingOrder + 1;
        }

        /// <summary>Phase 36: follow the same slot in a rebuilt match (the replay theater jumping back).</summary>
        public void Rebind(PlayerRuntimeState state) => _state = state;

        public static PlayerView Create(Transform parent, PlayerRuntimeState state, Sprite[,] frames, MatchArt art, Color ringColor)
        {
            var go = new GameObject("Player " + state.Index + " " + state.Def.DisplayName);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<PlayerView>();
            view._state = state;
            view._frames = frames;

            view._shadow = NewRenderer(go.transform, "Shadow", art.Shadow);
            view._ring = NewRenderer(go.transform, "Ring", art.Ring);
            view._ring.color = ringColor;
            view._ring.enabled = false;
            view._body = NewRenderer(go.transform, "Body", frames[(int)CharacterView.Back, 0]);
            view.Sync(0f, false, false);
            return view;
        }

        private static SpriteRenderer NewRenderer(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            return sr;
        }

        /// <summary>A different player in this slot (Full Court substitution).</summary>
        public void SetFrames(Sprite[,] frames)
        {
            _frames = frames;
            if (_head != null)
            {
                Destroy(_head.gameObject);
                _head = null;
                _heads = null;
            }
        }

        /// <summary>Draws a recorded replay frame instead of live state (presentation only).</summary>
        public void SyncReplay(float dt, ReplayPlayer f, bool controlled)
        {
            transform.position = CourtSpace.ToWorldSnapped(f.Position);
            _animTime += dt;
            var view = CharacterSpriteGenerator.ViewFor(CourtSpace.Facing(f.Facing), out bool flip);
            int frame = f.ArmsUp || f.Jump01 > 0.05f ? CharacterSpriteGenerator.ShootFrame
                : f.Moving ? CharacterSpriteGenerator.IdleFrames + (int)(_animTime * RunFps) % CharacterSpriteGenerator.RunFrames
                : (int)(_animTime * IdleFps) % CharacterSpriteGenerator.IdleFrames;
            _body.sprite = _frames[(int)view, frame];
            _body.flipX = flip;
            float lift = Mathf.Round(f.Jump01 * 0.7f * CourtSpace.PixelsPerUnit) / CourtSpace.PixelsPerUnit;
            _body.transform.localPosition = new Vector3(0f, lift, 0f);
            int order = CourtSpace.SortingOrder(f.Position);
            _body.sortingOrder = order;
            _shadow.sortingOrder = order - 2;
            _ring.sortingOrder = order - 1;
            _ring.enabled = controlled;
            SyncExtras((int)view, frame, flip, lift, f.Jump01, order);
        }

        // Drawn-only position and lift for one frame (a dunk gliding to the rim); cleared every frame.
        private bool _override;
        private Vec2 _overrideGround;
        private int _overrideLiftPx;

        public void SetOverride(Vec2 ground, int liftPx)
        {
            _override = true;
            _overrideGround = ground;
            _overrideLiftPx = liftPx;
        }

        public void ClearOverride() => _override = false;

        /// <summary>
        /// 16 Oct 2026 (LEAVE COURT): a drawn-only walk or wave with the game stopped (the simulation isn't touched).
        /// </summary>
        public void SyncScripted(float dt, Vec2 ground, Facing8 facing, bool walking, bool wave)
        {
            transform.position = CourtSpace.ToWorldSnapped(ground);
            _animTime += dt;
            var view = CharacterSpriteGenerator.ViewFor(CourtSpace.Facing(facing), out bool flip);
            int frame = walking ? CharacterSpriteGenerator.IdleFrames + (int)(_animTime * RunFps * 0.7f) % CharacterSpriteGenerator.RunFrames
                : wave ? ((int)(_animTime * 4f) % 2 == 0 ? CharacterSpriteGenerator.ShootFrame : 0)
                : (int)(_animTime * IdleFps) % CharacterSpriteGenerator.IdleFrames;
            _body.sprite = _frames[(int)view, frame];
            _body.flipX = flip;
            _body.transform.localPosition = Vector3.zero;
            int order = CourtSpace.SortingOrder(ground);
            _body.sortingOrder = order;
            _shadow.sortingOrder = order - 2;
            _ring.sortingOrder = order - 1;
            _ring.enabled = false;
            SyncExtras((int)view, frame, flip, 0f, 0f, order);
        }

        /// <summary>Called by the match controller after each simulation update.</summary>
        public void Sync(float dt, bool controlled, bool shooting = false, float jump01 = 0f, FlairPose flair = default)
        {
            var motion = _state.Motion;
            var ground = _override ? _overrideGround : motion.position;
            transform.position = CourtSpace.ToWorldSnapped(ground);

            bool moving = motion.IsMoving;
            _animTime += dt;
            var view = CharacterSpriteGenerator.ViewFor(CourtSpace.Facing(motion.facing), out bool flip);
            if (flair.FlipOverride) flip = !flip;
            // The dunk frame (G2) is the one pose that holds in the air.
            int posed = (shooting || jump01 > 0.05f) && flair.Frame != PoseFrame.Dunk ? -1 : CharacterSpriteGenerator.FrameFor(flair.Frame);
            int frame = posed >= 0 ? posed
                : shooting || jump01 > 0.05f || flair.ArmsUp ? CharacterSpriteGenerator.ShootFrame
                : moving ? CharacterSpriteGenerator.IdleFrames + (int)(_animTime * RunFps) % CharacterSpriteGenerator.RunFrames
                : (int)(_animTime * IdleFps) % CharacterSpriteGenerator.IdleFrames;
            _body.sprite = _frames[(int)view, frame];
            _body.flipX = flip;
            // Jumping lifts the sprite off its shadow (0.7 m at the peak), snapped to art pixels.
            // Celebrations and dribble moves add a few art pixels on top (presentation only).
            const float px = 1f / CourtSpace.PixelsPerUnit;
            float lift = _override ? _overrideLiftPx * px : Mathf.Round(jump01 * 0.7f * CourtSpace.PixelsPerUnit) * px + flair.Lift * px;
            _body.transform.localPosition = new Vector3(flair.OffsetX * px, lift, 0f);

            int order = CourtSpace.SortingOrder(ground);
            _body.sortingOrder = order;
            _shadow.sortingOrder = order - 2;
            _ring.sortingOrder = order - 1;
            _ring.enabled = controlled;
            SyncExtras((int)view, frame, flip, lift, _override ? Mathf.Clamp01(_overrideLiftPx / 12f) : jump01, order);
        }
    }
}
