using CallerRetroBall.Logic.PixelArt;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// Kit Studio live preview: your player in the kit you're editing, on a loop — idle, turning, running
    /// both ways, rising for a jumper. Drives a RawImage over the whole sprite sheet with uvRect, so a
    /// new kit only swaps one small texture.
    /// </summary>
    public sealed class KitPreview : MonoBehaviour
    {
        private struct Beat
        {
            public CharacterView View;
            public int Frame;
            public bool Flip;
            public float Seconds;
            public Beat(CharacterView v, int f, bool flip, float s) { View = v; Frame = f; Flip = flip; Seconds = s; }
        }

        private static readonly Beat[] Loop = Build();

        private static Beat[] Build()
        {
            var list = new System.Collections.Generic.List<Beat>();
            void Idle(CharacterView v, bool flip, int times) { for (int i = 0; i < times; i++) { list.Add(new Beat(v, 0, flip, 0.35f)); list.Add(new Beat(v, 1, flip, 0.35f)); } }
            void Run(CharacterView v, bool flip, int laps) { for (int l = 0; l < laps; l++) for (int f = 0; f < CharacterSpriteGenerator.RunFrames; f++) list.Add(new Beat(v, CharacterSpriteGenerator.IdleFrames + f, flip, 0.1f)); }
            Idle(CharacterView.Front, false, 2);
            Run(CharacterView.Side, false, 3);
            Idle(CharacterView.Back, false, 1);
            Run(CharacterView.Side, true, 3);
            list.Add(new Beat(CharacterView.Front, CharacterSpriteGenerator.ShootFrame, false, 0.9f));
            Idle(CharacterView.Front, false, 1);
            return list.ToArray();
        }

        private RawImage _image;
        private int _beat;
        private float _until;
        /// <summary>Freeze on one pose (e.g. while picking a colour) instead of looping.</summary>
        public bool Paused { get; set; }

        public static KitPreview Attach(RawImage image)
        {
            var p = image.gameObject.AddComponent<KitPreview>();
            p._image = image;
            return p;
        }

        public void SetSheet(Texture texture)
        {
            _image.texture = texture;
            Apply();
        }

        private void Update()
        {
            if (_image == null || _image.texture == null || Paused) return;
            if (Time.unscaledTime < _until) return;
            _beat = (_beat + 1) % Loop.Length;
            Apply();
        }

        private void Apply()
        {
            if (_image == null || _image.texture == null) return;
            var b = Loop[_beat];
            _until = Time.unscaledTime + b.Seconds;
            float w = (float)CharacterSpriteGenerator.SheetFrameWidth / _image.texture.width;
            float h = (float)CharacterSpriteGenerator.SheetFrameHeight / _image.texture.height;
            CharacterSpriteGenerator.FrameOrigin(b.View, b.Frame, out int fx, out int fy);
            float u = (float)fx / _image.texture.width, v = (float)fy / _image.texture.height;
            _image.uvRect = b.Flip ? new Rect(u + w, v, -w, h) : new Rect(u, v, w, h);
        }
    }
}
