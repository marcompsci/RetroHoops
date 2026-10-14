using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    /// <summary>Draws the ball at its ground position lifted by its height, plus a ground shadow.</summary>
    public sealed class BallView : MonoBehaviour
    {
        private SpriteRenderer _ball;
        private SpriteRenderer _shadow;
        private Sprite[] _frames;

        public static BallView Create(Transform parent, MatchArt art)
        {
            var go = new GameObject("Ball");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BallView>();

            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(go.transform, false);
            view._shadow = shadowGo.AddComponent<SpriteRenderer>();
            view._shadow.sprite = art.BallShadow;

            var ballGo = new GameObject("Sprite");
            ballGo.transform.SetParent(go.transform, false);
            view._ball = ballGo.AddComponent<SpriteRenderer>();
            view._ball.sprite = art.Ball;
            view._frames = art.BallFrames;
            return view;
        }

        /// <summary>Ball colour (RAINBOW BALL secret, HEAT CHECK glow); white = normal.</summary>
        public void Tint(Color c)
        {
            if (_ball.color != c) _ball.color = c;
        }

        /// <summary>Draws the ball at a given spot instead (in a dunker's hands on the way to the rim).</summary>
        public void SyncInHands(Vector3 world, int sortingOrder)
        {
            _ball.transform.position = world;
            _ball.sortingOrder = sortingOrder;
            _shadow.color = new Color(1f, 1f, 1f, 0f);
        }

        /// <param name="heldOffsetPx">Dribble-move offset in art pixels, applied only while the ball is held.</param>
        public void Sync(BallState ball, int holderSortingOrder, Vector2Int heldOffsetPx = default)
        {
            _shadow.transform.position = CourtSpace.ToWorldSnapped(ball.Position);
            var offset = ball.IsHeld ? new Vector3(heldOffsetPx.x, heldOffsetPx.y, 0f) / CourtSpace.PixelsPerUnit : Vector3.zero;
            _ball.transform.position = CourtSpace.ToWorldSnapped(ball.Position, ball.Height) + offset;

            int order = ball.IsHeld ? holderSortingOrder + 1
                : ball.Phase == BallPhase.Shot || ball.Height > 2f ? 31000 // in the air: above players and the hoop
                : CourtSpace.SortingOrder(ball.Position, 1);
            _ball.sortingOrder = order;
            _shadow.sortingOrder = order - 3;
            // Spin while it flies or rolls loose; still in someone's hands.
            if (_frames != null && _frames.Length > 0)
            {
                var s = _frames[PropSpriteGenerator.BallSpinFrame(Time.time, !ball.IsHeld)];
                if (_ball.sprite != s) _ball.sprite = s;
            }
            // Shadow fades as the ball rises.
            float a = Mathf.Clamp01(1f - ball.Height / 4f);
            _shadow.color = new Color(1f, 1f, 1f, a);
        }
    }
}
