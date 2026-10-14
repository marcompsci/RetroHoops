using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.Utilities;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// Builds every sprite a match needs from the procedural generators, once, at match start.
    /// Textures are point-filtered at 16 px per metre.
    /// </summary>
    public sealed class MatchArt
    {
        public Sprite Court { get; private set; }
        public Sprite Hoop { get; private set; }
        public Sprite Ball { get; private set; }
        public Sprite Shadow { get; private set; }
        public Sprite BallShadow { get; private set; }
        public Sprite Ring { get; private set; }
        /// <summary>The basket seen from the side (landscape Full Court).</summary>
        public Sprite HoopSide { get; private set; }
        /// <summary>Net frames (rest, stretched, flared) for the swish animation.</summary>
        public Sprite[] HoopFrames { get; private set; }
        public Sprite[] HoopSideFrames { get; private set; }
        /// <summary>Ball spin frames.</summary>
        public Sprite[] BallFrames { get; private set; }
        public Sprite MeterFrame { get; private set; }
        public Sprite Pixel { get; private set; }
        public Sprite Arrow { get; private set; }

        private readonly List<Object> _owned = new List<Object>();

        /// <param name="fullCourt">Draw both halves (Full Court); <paramref name="geometry"/> is then one half.</param>
        public static MatchArt Build(CourtDef court, CourtGeometry geometry, uint seed, RgbColor? bannerA = null, RgbColor? bannerB = null,
                                     bool staticCrowd = true, bool fullCourt = false)
        {
            var art = new MatchArt();
            const float ppu = CourtSpace.PixelsPerUnit;

            float px, py;
            PixelCanvas courtCanvas;
            if (fullCourt)
            {
                courtCanvas = CourtGenerator.GenerateFullCourt(court, geometry, seed, bannerA, bannerB, staticCrowd);
                CourtGenerator.FullCourtOriginPivot(geometry, out px, out py);
            }
            else
            {
                courtCanvas = CourtGenerator.Generate(court, geometry, seed, bannerA, bannerB, staticCrowd);
                CourtGenerator.OriginPivot(geometry, out px, out py);
            }
            art.Court = art.Make(courtCanvas, "court:" + court.id, new Vector2(px, py), ppu);

            // Art pass 2: props at 2x detail (two art pixels per court pixel); pivots stay in court pixels.
            const int k = PropSpriteGenerator.ArtScale;
            art.HoopFrames = new Sprite[PropSpriteGenerator.NetFrames];
            art.HoopSideFrames = new Sprite[PropSpriteGenerator.NetFrames];
            for (int f = 0; f < PropSpriteGenerator.NetFrames; f++)
            {
                var hoop = PropSpriteGenerator.Hoop(f);
                art.HoopFrames[f] = art.Make(hoop, "hoop" + f,
                    new Vector2((PropSpriteGenerator.HoopPivotX + 0.5f) * k / hoop.Width, (PropSpriteGenerator.HoopPivotY + 0.5f) * k / hoop.Height), ppu * k);
                var side = PropSpriteGenerator.HoopSide(f);
                art.HoopSideFrames[f] = art.Make(side, "hoop-side" + f,
                    new Vector2((PropSpriteGenerator.HoopSidePivotX + 0.5f) * k / side.Width, (PropSpriteGenerator.HoopSidePivotY + 0.5f) * k / side.Height), ppu * k);
            }
            art.Hoop = art.HoopFrames[0];
            art.HoopSide = art.HoopSideFrames[0];

            art.BallFrames = new Sprite[PropSpriteGenerator.BallFrames];
            for (int f = 0; f < PropSpriteGenerator.BallFrames; f++)
                art.BallFrames[f] = art.Make(PropSpriteGenerator.Ball(f), "ball" + f, new Vector2(0.5f, 0f), ppu * k);
            art.Ball = art.BallFrames[0];
            art.Shadow = art.Make(PropSpriteGenerator.Shadow(12, 4), "shadow", new Vector2(0.5f, 0.5f), ppu);
            art.BallShadow = art.Make(PropSpriteGenerator.Shadow(6, 2), "ball-shadow", new Vector2(0.5f, 0.5f), ppu);
            art.Ring = art.Make(PropSpriteGenerator.Ring(16, 6), "ring", new Vector2(0.5f, 0.5f), ppu);
            art.MeterFrame = art.Make(PropSpriteGenerator.MeterFrame(), "meter", new Vector2(0f, 0f), ppu);
            art.Pixel = art.Make(PropSpriteGenerator.WhitePixel(), "pixel", new Vector2(0f, 0f), ppu);
            art.Arrow = art.Make(PropSpriteGenerator.Arrow(), "arrow", new Vector2(0.5f, 0f), ppu);
            return art;
        }

        /// <summary>Slices a generated player sheet into [view, frame] sprites with the pivot at the feet.</summary>
        /// <summary>A crowd fan sprite (pivot bottom-left, one art pixel = one court pixel).</summary>
        /// <summary>Landscape sideline bleachers (pivot bottom-left).</summary>
        public Sprite SidelineStands(PixelCanvas canvas) => Make(canvas, "stands", Vector2.zero, CourtSpace.PixelsPerUnit);

        public Sprite CrowdFan(RgbColor shirt, RgbColor skin, bool armsUp, int variant = 0) =>
            Make(CrowdGenerator.Fan(shirt, skin, armsUp, variant), "fan", Vector2.zero, CourtSpace.PixelsPerUnit * CrowdGenerator.ArtScale);

        public Sprite[,] PlayerFrames(PlayerDef player, TeamDef team) => PlayerFrames(player, team.primary, team.secondary, team.accent, null, TeamPattern.Solid);

        /// <summary>Sheet with cosmetic overrides (jersey palette, shoes) and optional colourblind pattern.</summary>
        public Sprite[,] PlayerFrames(PlayerDef player, RgbColor jersey, RgbColor trim, RgbColor accent, RgbColor? shoes, TeamPattern pattern,
                                      RgbColor? shorts = null)
        {
            var sheet = CharacterSpriteGenerator.GenerateSheet(player.appearance, KitLook.Classic(jersey, trim, accent, shoes, pattern, shorts), player.jerseyNumber);
            return Slice(sheet, player.id);
        }

        private Sprite[,] Slice(PixelCanvas sheet, string id)
        {
            var tex = Own(TextureFactory.ToTexture(sheet, "sheet:" + id));
            var frames = new Sprite[CharacterSpriteGenerator.ViewCount, CharacterSpriteGenerator.FramesPerView];
            for (int v = 0; v < CharacterSpriteGenerator.ViewCount; v++)
                for (int f = 0; f < CharacterSpriteGenerator.FramesPerView; f++)
                {
                    CharacterSpriteGenerator.FrameOrigin((CharacterView)v, f, out int x, out int y);
                    var rect = new Rect(x, y, CharacterSpriteGenerator.SheetFrameWidth, CharacterSpriteGenerator.SheetFrameHeight);
                    // Pivot at the feet (bottom centre, one court pixel up to sit on the shadow). 2x detail: twice the pixels per unit.
                    var pivot = new Vector2(0.5f, 1f / CharacterSpriteGenerator.FrameHeight);
                    frames[v, f] = Own(Sprite.Create(tex, rect, pivot, CourtSpace.PixelsPerUnit * CharacterSpriteGenerator.ArtScale, 0, SpriteMeshType.FullRect));
                }
            return frames;
        }

        /// <summary>Sheet in a Kit Studio kit.</summary>
        public Sprite[,] PlayerFrames(PlayerDef player, KitLook kit) =>
            Slice(CharacterSpriteGenerator.GenerateSheet(player.appearance, kit, player.jerseyNumber), player.id);

        /// <summary>
        /// BIG HEADS secret: for each frame, a sprite of everything from the head row up, pivoted at
        /// its bottom centre, so it can be drawn scaled up on top of the body.
        /// </summary>
        public Sprite[,] HeadFrames(Sprite[,] frames, AppearanceDef look)
        {
            var heads = new Sprite[CharacterSpriteGenerator.ViewCount, CharacterSpriteGenerator.FramesPerView];
            for (int v = 0; v < CharacterSpriteGenerator.ViewCount; v++)
                for (int f = 0; f < CharacterSpriteGenerator.FramesPerView; f++)
                {
                    var body = frames[v, f];
                    int row = CharacterSpriteGenerator.HeadBottomRow(look, f) * CharacterSpriteGenerator.ArtScale;
                    var r = body.rect;
                    var rect = new Rect(r.x, r.y + row, r.width, r.height - row);
                    heads[v, f] = Own(Sprite.Create(body.texture, rect, new Vector2(0.5f, 0f), CourtSpace.PixelsPerUnit * CharacterSpriteGenerator.ArtScale, 0, SpriteMeshType.FullRect));
                }
            return heads;
        }

        public void Dispose()
        {
            foreach (var o in _owned)
                if (o != null) Object.Destroy(o);
            _owned.Clear();
        }

        private T Own<T>(T o) where T : Object
        {
            _owned.Add(o);
            return o;
        }

        private Sprite Make(PixelCanvas canvas, string name, Vector2 pivot, float ppu)
        {
            var tex = Own(TextureFactory.ToTexture(canvas, name));
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return Own(sprite);
        }
    }
}
