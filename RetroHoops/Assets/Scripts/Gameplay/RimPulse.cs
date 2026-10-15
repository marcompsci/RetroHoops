using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// 15 Oct 2026 (Omari): THE STREETS dunk pulse. When a dunk goes in, rings of energy burst out from the rim and
    /// fade (shapes and timing: <see cref="RimPulseShape"/>), with a quick flash on the rim. A SHOWSTOPPER dunk sends
    /// three bigger rings. Pixel-crisp: every ring size is its own small sprite, made once.
    /// </summary>
    public sealed class RimPulse : MonoBehaviour
    {
        private const int Pool = 6;
        public const int NormalRadius = 26;
        public const int BigRadius = 42;

        private struct Ring
        {
            public SpriteRenderer Renderer;
            public float Start;
            public bool Big, Active;
            public Color Color;
        }

        private readonly Ring[] _rings = new Ring[Pool];
        private Sprite[] _normal, _big;
        private SpriteRenderer _flash;
        private float _flashStart = -9f;
        private Vector3 _at;

        public static RimPulse Create(Transform parent)
        {
            var go = new GameObject("RimPulse");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<RimPulse>();
            fx._normal = Frames(NormalRadius, 2);
            fx._big = Frames(BigRadius, 3);
            for (int i = 0; i < Pool; i++)
            {
                var child = new GameObject("Ring");
                child.transform.SetParent(go.transform, false);
                var sr = child.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 31590; // over the court and players, under the sparks
                sr.enabled = false;
                fx._rings[i].Renderer = sr;
            }
            var flash = new GameObject("Flash");
            flash.transform.SetParent(go.transform, false);
            fx._flash = flash.AddComponent<SpriteRenderer>();
            fx._flash.sprite = Disc(6);
            fx._flash.sortingOrder = 31595;
            fx._flash.enabled = false;
            return fx;
        }

        private static Sprite[] Frames(int maxRadius, int thickness)
        {
            var list = new Sprite[RimPulseShape.Frames];
            for (int f = 0; f < list.Length; f++)
            {
                var grid = RimPulseShape.Ring(RimPulseShape.RadiusAt(f, maxRadius), thickness);
                list[f] = ToSprite(grid, "Ring" + maxRadius + "_" + f);
            }
            return list;
        }

        private static Sprite Disc(int r)
        {
            var grid = new bool[r * 2 + 1, r + 1 + r / 2];
            int ry = grid.GetLength(1) / 2;
            for (int y = 0; y < grid.GetLength(1); y++)
                for (int x = 0; x < grid.GetLength(0); x++)
                {
                    float dx = (x - r) / (float)r, dy = (y - ry) / (float)System.Math.Max(1, ry);
                    grid[x, y] = dx * dx + dy * dy <= 1.05f;
                }
            return ToSprite(grid, "Flash");
        }

        private static Sprite ToSprite(bool[,] grid, string name)
        {
            int w = grid.GetLength(0), h = grid.GetLength(1);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = grid[x, y] ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), CourtSpace.PixelsPerUnit);
        }

        /// <summary>Fires the pulse at the rim (<paramref name="rim"/> in world space).</summary>
        public void Fire(Vector3 rim, Color color, bool big)
        {
            _at = rim;
            int rings = big ? 3 : 2;
            float now = Time.unscaledTime;
            for (int k = 0; k < rings; k++)
            {
                int slot = FreeSlot();
                _rings[slot].Start = now + k * RimPulseShape.Stagger;
                _rings[slot].Big = big;
                _rings[slot].Active = true;
                // Rings after the first are a little paler.
                _rings[slot].Color = k == 0 ? color : Color.Lerp(color, Color.white, 0.45f);
                _rings[slot].Renderer.transform.position = rim;
            }
            _flashStart = now;
            _flash.transform.position = rim;
            _flash.color = Color.Lerp(color, Color.white, 0.6f);
            _flash.enabled = true;
        }

        private int FreeSlot()
        {
            int oldest = 0;
            for (int i = 0; i < Pool; i++)
            {
                if (!_rings[i].Active) return i;
                if (_rings[i].Start < _rings[oldest].Start) oldest = i;
            }
            return oldest;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < Pool; i++)
            {
                ref var r = ref _rings[i];
                if (!r.Active) continue;
                float t = now - r.Start;
                if (t < 0f) { r.Renderer.enabled = false; continue; }
                int frame = RimPulseShape.FrameAt(t);
                if (frame < 0)
                {
                    r.Active = false;
                    r.Renderer.enabled = false;
                    continue;
                }
                r.Renderer.enabled = true;
                r.Renderer.sprite = (r.Big ? _big : _normal)[frame];
                var c = r.Color;
                c.a = RimPulseShape.AlphaAt(t);
                r.Renderer.color = c;
            }
            if (_flash.enabled)
            {
                float t = now - _flashStart;
                if (t > 0.16f) _flash.enabled = false;
                else
                {
                    var c = _flash.color;
                    c.a = 1f - t / 0.16f;
                    _flash.color = c;
                }
            }
        }
    }
}
