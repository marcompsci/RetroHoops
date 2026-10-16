using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.Utilities;
using UnityEngine;

namespace CallerRetroBall.City
{
    /// <summary>
    /// 16 Oct 2026 (BALLER CITY): one person in the city (you or an NPC). Draws the game's own character sheet
    /// (CharacterSpriteGenerator, the same art as on court) with a drop shadow, depth-sorted by the feet.
    /// One unit = one city tile; the 2x-detail sheets show at 32 pixels per tile, so people are a tile wide.
    /// </summary>
    public sealed class CityActorView : MonoBehaviour
    {
        private Sprite[,] _frames;
        private SpriteRenderer _body, _shadow;
        private float _anim;
        private Texture2D _sheet;

        public static CityActorView Create(Transform parent, string name, PlayerDef player, KitLook kit, Sprite shadow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<CityActorView>();
            v._sheet = TextureFactory.ToTexture(CharacterSpriteGenerator.GenerateSheet(player.appearance, kit, player.jerseyNumber), "city.sheet." + player.id);
            v._frames = Slice(v._sheet);
            v._shadow = NewRenderer(go.transform, "Shadow", shadow);
            v._shadow.color = new Color(0f, 0f, 0f, 0.35f);
            v._body = NewRenderer(go.transform, "Body", v._frames[(int)CharacterView.Front, 0]);
            return v;
        }

        private static Sprite[,] Slice(Texture2D tex)
        {
            var frames = new Sprite[CharacterSpriteGenerator.ViewCount, CharacterSpriteGenerator.FramesPerView];
            var pivot = new Vector2(0.5f, 1f / CharacterSpriteGenerator.FrameHeight);
            float ppu = CityArtGenerator.Tile * CharacterSpriteGenerator.ArtScale;
            for (int v = 0; v < CharacterSpriteGenerator.ViewCount; v++)
                for (int f = 0; f < CharacterSpriteGenerator.FramesPerView; f++)
                {
                    CharacterSpriteGenerator.FrameOrigin((CharacterView)v, f, out int x, out int y);
                    var rect = new Rect(x, y, CharacterSpriteGenerator.SheetFrameWidth, CharacterSpriteGenerator.SheetFrameHeight);
                    frames[v, f] = Sprite.Create(tex, rect, pivot, ppu, 0, SpriteMeshType.FullRect);
                }
            return frames;
        }

        private static SpriteRenderer NewRenderer(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            return sr;
        }

        /// <summary>
        /// Draws a pose. <paramref name="facing"/>: 0 toward the camera, 1 away, 2 left, 3 right.
        /// <paramref name="moving"/> plays the run cycle (faster when sprinting); armsUp alternates the shot pose (a wave or a shot).
        /// </summary>
        public void Sync(float dt, Vec2 feet, int facing, bool moving, float speed, bool armsUp, bool reduceMotion)
        {
            transform.localPosition = new Vector3(Snap(feet.x), Snap(feet.y), 0f);
            _anim += dt * (moving ? Mathf.Max(0.6f, speed / CityMovement.WalkSpeed) : 1f);
            var view = facing == 0 ? CharacterView.Front : facing == 1 ? CharacterView.Back : CharacterView.Side;
            int frame = moving ? CharacterSpriteGenerator.IdleFrames + (int)(_anim * 9f) % CharacterSpriteGenerator.RunFrames
                : armsUp ? (reduceMotion || (int)(_anim * 3f) % 2 == 0 ? CharacterSpriteGenerator.ShootFrame : 0)
                : (reduceMotion ? 0 : (int)(_anim * 2f) % CharacterSpriteGenerator.IdleFrames);
            var sprite = _frames[(int)view, frame];
            if (_body.sprite != sprite) _body.sprite = sprite;
            bool flip = facing == 2;
            if (_body.flipX != flip) _body.flipX = flip;
            int order = CityWorld.Order(feet.y);
            _body.sortingOrder = order;
            _shadow.sortingOrder = order - 1;
        }

        public void SetVisible(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        private static float Snap(float v) => Mathf.Round(v * CityArtGenerator.Tile) / CityArtGenerator.Tile;

        private void OnDestroy()
        {
            if (_frames != null)
                foreach (var s in _frames) if (s != null) Destroy(s);
            if (_sheet != null) Destroy(_sheet);
        }
    }
}
