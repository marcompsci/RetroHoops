using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>How a street beat moves.</summary>
    public enum BeatGroove
    {
        /// <summary>Half-time: the clap on beat 3, fast hats with rolls, long 808s. Hazy, spacey.</summary>
        Halftime = 0,
        /// <summary>Four on the bounce: claps on 2 and 4, a skippy kick, swung hats. Playful.</summary>
        Bounce = 1,
        /// <summary>Half-time and laid back: soft hats, few kicks, warm chords.</summary>
        Smooth = 2,
        /// <summary>Up-tempo game music: claps on 2 and 4, busy hats, brass stabs.</summary>
        Hype = 3,
    }

    /// <summary>The instrument that carries a beat's loop.</summary>
    public enum BeatLead { Piano = 0, ElectricPiano = 1, Bell = 2, Pluck = 3, Brass = 4 }

    /// <summary>One original instrumental beat: everything the composer needs to write it, deterministically.</summary>
    public sealed class BeatDef
    {
        public string Key, Title, Vibe;
        public float Bpm;
        /// <summary>MIDI note of the key's root (the 808 plays it two octaves down).</summary>
        public int Root;
        public SongScale Scale;
        /// <summary>Chord roots as scale degrees, one per bar, repeating every four bars (the B section uses <see cref="ChordsB"/>).</summary>
        public int[] Chords, ChordsB;
        public BeatGroove Groove;
        public BeatLead Lead;
        /// <summary>A second, quieter layer: a pad and/or bells over the loop.</summary>
        public bool Pad, Bells;
        /// <summary>A little dust: quiet vinyl crackle.</summary>
        public bool Crackle;
        /// <summary>0 = straight, up to 0.25 = heavily swung sixteenths.</summary>
        public float Swing;
        public uint Seed;
        /// <summary>Cover art colours (hex).</summary>
        public string ColorA, ColorB;
    }

    /// <summary>
    /// 15 Oct 2026 (Omari: "take the songs off and generate a playlist from scratch with instrumental beats"): Retro Hoops's
    /// own beat tape. Instrumental rap beats in the styles the old playlist leaned on (Michigan street rap: off-kilter
    /// piano loops, hazy bells, smooth electric piano, bouncy 808s, fast hats with rolls) plus up-tempo game music.
    /// Every note and sound is generated here from the beat's numbers: no samples, no vocals, nothing borrowed from
    /// any recording. Each beat is a full song with an intro, two sections, a breakdown and an outro, and ends (the
    /// phone's playlist moves on to the next one).
    /// </summary>
    public static class StreetBeats
    {
        private const int SampleRate = AudioSynth.SampleRate;
        public const int StepsPerBar = 16;
        public const string Credit = "RETRO HOOPS ORIGINAL";

        public static readonly BeatDef[] Beats =
        {
            new BeatDef { Key = "beat.eastside_static", Title = "Eastside Static", Vibe = "Hazy bells, long 808s, half-time",
                          Bpm = 148f, Root = 61, Scale = SongScale.Minor, Chords = new[] { 0, 5, 3, 4 }, ChordsB = new[] { 5, 3, 0, 6 },
                          Groove = BeatGroove.Halftime, Lead = BeatLead.Bell, Pad = true, Crackle = true, Swing = 0.04f, Seed = 7101,
                          ColorA = "#3A0CA3", ColorB = "#4CC9F0" },
            new BeatDef { Key = "beat.jit_bounce", Title = "Jit Bounce", Vibe = "Off-kilter piano, skippy bounce",
                          Bpm = 101f, Root = 58, Scale = SongScale.Minor, Chords = new[] { 0, 0, 5, 4 }, ChordsB = new[] { 3, 4, 0, 6 },
                          Groove = BeatGroove.Bounce, Lead = BeatLead.Piano, Swing = 0.16f, Seed = 7202,
                          ColorA = "#F77F00", ColorB = "#1A1A1F" },
            new BeatDef { Key = "beat.velvet_dashboard", Title = "Velvet Dashboard", Vibe = "Smooth keys, late-night ride",
                          Bpm = 136f, Root = 57, Scale = SongScale.Dorian, Chords = new[] { 0, 3, 6, 4 }, ChordsB = new[] { 5, 3, 1, 4 },
                          Groove = BeatGroove.Smooth, Lead = BeatLead.ElectricPiano, Pad = true, Crackle = true, Swing = 0.08f, Seed = 7303,
                          ColorA = "#7209B7", ColorB = "#FFD166" },
            new BeatDef { Key = "beat.cold_keys", Title = "Cold Keys", Vibe = "Dark piano loop, hard 808 slides",
                          Bpm = 152f, Root = 60, Scale = SongScale.Minor, Chords = new[] { 0, 1, 0, 5 }, ChordsB = new[] { 5, 4, 1, 0 },
                          Groove = BeatGroove.Halftime, Lead = BeatLead.Piano, Bells = true, Swing = 0.02f, Seed = 7404,
                          ColorA = "#1D4ED8", ColorB = "#F2F2F2" },
            new BeatDef { Key = "beat.fast_break_flow", Title = "Fast Break Flow", Vibe = "Up-tempo brass, game day energy",
                          Bpm = 104f, Root = 55, Scale = SongScale.Dorian, Chords = new[] { 0, 3, 4, 3 }, ChordsB = new[] { 5, 4, 3, 4 },
                          Groove = BeatGroove.Hype, Lead = BeatLead.Brass, Bells = true, Swing = 0.1f, Seed = 7505,
                          ColorA = "#D62828", ColorB = "#FFD166" },
            new BeatDef { Key = "beat.glass_backboard", Title = "Glass Backboard", Vibe = "Spacey plucks, floating hats",
                          Bpm = 144f, Root = 62, Scale = SongScale.Minor, Chords = new[] { 5, 3, 0, 4 }, ChordsB = new[] { 3, 4, 5, 6 },
                          Groove = BeatGroove.Halftime, Lead = BeatLead.Pluck, Pad = true, Bells = true, Swing = 0.05f, Seed = 7606,
                          ColorA = "#06D6A0", ColorB = "#1A1A2E" },
            new BeatDef { Key = "beat.corner_store_run", Title = "Corner Store Run", Vibe = "Goofy keys, swung bounce",
                          Bpm = 97f, Root = 56, Scale = SongScale.Mixolydian, Chords = new[] { 0, 6, 3, 0 }, ChordsB = new[] { 4, 3, 6, 4 },
                          Groove = BeatGroove.Bounce, Lead = BeatLead.ElectricPiano, Bells = true, Swing = 0.2f, Seed = 7707,
                          ColorA = "#2EC4B6", ColorB = "#F72585" },
            new BeatDef { Key = "beat.midnight_blacktop", Title = "Midnight Blacktop", Vibe = "Soulful chords, slow cruise",
                          Bpm = 132f, Root = 53, Scale = SongScale.Minor, Chords = new[] { 0, 5, 2, 6 }, ChordsB = new[] { 3, 6, 2, 4 },
                          Groove = BeatGroove.Smooth, Lead = BeatLead.ElectricPiano, Pad = true, Crackle = true, Swing = 0.1f, Seed = 7808,
                          ColorA = "#1A1A1F", ColorB = "#C74DFF" },
            new BeatDef { Key = "beat.ankle_breaker_anthem", Title = "Ankle Breaker Anthem", Vibe = "Loud brass, hype for the highlights",
                          Bpm = 108f, Root = 57, Scale = SongScale.Minor, Chords = new[] { 0, 0, 5, 6 }, ChordsB = new[] { 3, 5, 6, 4 },
                          Groove = BeatGroove.Hype, Lead = BeatLead.Brass, Swing = 0.06f, Seed = 7909,
                          ColorA = "#FFD166", ColorB = "#D62828" },
            new BeatDef { Key = "beat.no_look_pass", Title = "No Look Pass", Vibe = "Sneaky piano, rolling hats",
                          Bpm = 150f, Root = 59, Scale = SongScale.Dorian, Chords = new[] { 0, 4, 3, 1 }, ChordsB = new[] { 5, 6, 4, 0 },
                          Groove = BeatGroove.Halftime, Lead = BeatLead.Piano, Pad = true, Swing = 0.03f, Seed = 8010,
                          ColorA = "#4CC9F0", ColorB = "#3A0CA3" },
        };

        public static BeatDef Find(string key) => Array.Find(Beats, b => b.Key == key);

        public static int IndexOf(string key) => Array.FindIndex(Beats, b => b.Key == key);

        /// <summary>
        /// Pixel cover art for a beat (<paramref name="size"/>², row 0 at the bottom): its two colours as a sky, a
        /// pixel basketball rising like a sun, and an equalizer skyline in front. Deterministic per beat.
        /// </summary>
        public static RgbColor[] Cover(BeatDef b, int size = 32)
        {
            var px = new RgbColor[size * size];
            var top = RgbColor.FromHex(b.ColorA);
            var bottom = RgbColor.FromHex(b.ColorB);
            var dark = RgbColor.FromHex("#0B0B16");
            var ball = RgbColor.FromHex("#F77F00");
            var rng = new SeededRandom(b.Seed ^ 0xC0FEEu);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = y / (float)(size - 1);
                    // Banded gradient (8 steps) for the pixel look.
                    float band = (float)Math.Floor(u * 8f) / 7f;
                    px[y * size + x] = RgbColor.Lerp(bottom, top, Math.Min(1f, band));
                }
            // The ball.
            float cx = size * 0.5f, cy = size * 0.55f, r = size * 0.26f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d > r) continue;
                    bool seam = Math.Abs(dx) < 0.7f || Math.Abs(dy) < 0.7f || Math.Abs(d - r * 0.62f) < 0.6f && Math.Abs(dx) > r * 0.3f;
                    px[y * size + x] = d > r - 1f || seam ? dark : ball;
                }
            // Equalizer skyline.
            int bars = size / 3;
            for (int i = 0; i < bars; i++)
            {
                int h = 2 + rng.Range(0, size / 3);
                for (int y = 0; y < h; y++)
                    for (int x = i * 3; x < i * 3 + 2 && x < size; x++)
                        px[y * size + x] = y == h - 1 ? RgbColor.Lerp(top, RgbColor.FromHex("#FFFFFF"), 0.5f) : dark;
            }
            // Frame.
            for (int k = 0; k < size; k++)
            {
                px[k] = px[(size - 1) * size + k] = dark;
                px[k * size] = px[k * size + size - 1] = dark;
            }
            return px;
        }

        /// <summary>Section names, in order. Each beat uses <see cref="Sections"/> for its length.</summary>
        public enum Part { Intro, A, B, Break, Outro }

        /// <summary>The song's form, four bars per entry: fast beats get a longer form so every song lasts about the same.</summary>
        public static Part[] Sections(BeatDef b) => b.Bpm >= 125f
            ? new[] { Part.Intro, Part.A, Part.A, Part.B, Part.B, Part.Break, Part.A, Part.A, Part.B, Part.B, Part.Outro, Part.Outro }
            : new[] { Part.Intro, Part.A, Part.A, Part.B, Part.B, Part.Break, Part.A, Part.B, Part.Outro };

        public static int Bars(BeatDef b) => Sections(b).Length * 4;

        /// <summary>Seconds of music (before the last reverb tail).</summary>
        public static float Seconds(BeatDef b) => Bars(b) * 4 * 60f / b.Bpm;

        /// <summary>A short tail after the last bar so the echoes die out.</summary>
        public const float TailSeconds = 1.6f;

        // ------------------------------------------------------------------ patterns

        /// <summary>Kick pattern for a bar (16 steps, 'x' = hit). Two alternating bars per groove.</summary>
        public static string KickPattern(BeatGroove g, int bar)
        {
            switch (g)
            {
                case BeatGroove.Halftime: return bar % 2 == 0 ? "x.....x...x....." : "x.......x.x...x.";
                case BeatGroove.Bounce: return bar % 2 == 0 ? "x..x..x...x.x..." : "x..x....x.x..x..";
                case BeatGroove.Smooth: return bar % 2 == 0 ? "x.........x....." : "x......x..x.....";
                default: return bar % 2 == 0 ? "x...x.x...x.x..." : "x.x...x...x...x.";
            }
        }

        /// <summary>Clap / snare steps.</summary>
        public static string ClapPattern(BeatGroove g, int bar) =>
            g == BeatGroove.Halftime || g == BeatGroove.Smooth ? "........x......." : "....x.......x...";

        // ------------------------------------------------------------------ the composer

        private static readonly int[][] Intervals =
        {
            new[] { 0, 2, 4, 5, 7, 9, 11 },
            new[] { 0, 2, 3, 5, 7, 8, 10 },
            new[] { 0, 2, 3, 5, 7, 9, 10 },
            new[] { 0, 2, 4, 5, 7, 9, 10 },
        };

        public static int Note(BeatDef b, int degree)
        {
            var iv = Intervals[(int)b.Scale];
            int octave = (int)Math.Floor(degree / 7.0);
            int d = degree - octave * 7;
            return b.Root + octave * 12 + iv[d];
        }

        /// <summary>One note of the loop: a sixteenth step in a two-bar motif, a scale degree (relative to the chord), its length in steps.</summary>
        public struct MotifNote
        {
            public int Step, Degree, Length;
            public bool Grace;
        }

        /// <summary>
        /// The beat's hook: a two-bar figure on the sixteenth grid, leaning on off-beats, with chord tones on the strong
        /// hits, a neighbour note here and there, and (for the playful grooves) a chromatic grace note into a hit.
        /// </summary>
        public static List<MotifNote> Motif(BeatDef b)
        {
            var rng = new SeededRandom(b.Seed);
            var notes = new List<MotifNote>();
            int hits = b.Groove == BeatGroove.Smooth ? 5 : b.Groove == BeatGroove.Hype ? 8 : 7;
            var used = new bool[32];
            // Anchor on the one; the rest fall mostly on off-beats.
            used[0] = true;
            int placed = 1;
            int guard = 0;
            while (placed < hits && guard++ < 400)
            {
                int s = rng.Range(1, 32);
                bool offbeat = s % 2 == 1 || s % 4 == 2;
                if (used[s] || (!offbeat && rng.NextFloat() < 0.6f)) continue;
                if ((s > 0 && used[s - 1]) && rng.NextFloat() < 0.7f) continue;
                used[s] = true;
                placed++;
            }
            int[] tones = { 0, 2, 4, 7, 9 };
            int prev = 4;
            for (int s = 0; s < 32; s++)
            {
                if (!used[s]) continue;
                int next = s + 1;
                while (next < 32 && !used[next]) next++;
                int len = Math.Max(1, Math.Min(next - s, b.Groove == BeatGroove.Smooth ? 6 : 3));
                int deg;
                bool strong = s % 4 == 0;
                if (strong || rng.NextFloat() < 0.6f)
                {
                    int best = tones[0], bestDist = 99;
                    foreach (int t in tones)
                    {
                        int dist = Math.Abs(t - prev) + (rng.NextFloat() < 0.3f ? 1 : 0);
                        if (dist < bestDist) { best = t; bestDist = dist; }
                    }
                    deg = best;
                    if (rng.NextFloat() < 0.25f) deg = tones[rng.Range(0, tones.Length)];
                }
                else deg = prev + (rng.NextFloat() < 0.5f ? 1 : -1);
                deg = Math.Max(-1, Math.Min(9, deg));
                bool grace = (b.Groove == BeatGroove.Bounce || b.Lead == BeatLead.Piano) && rng.NextFloat() < 0.22f && s > 0;
                notes.Add(new MotifNote { Step = s, Degree = deg, Length = len, Grace = grace });
                prev = deg;
            }
            return notes;
        }

        /// <summary>The 808's line for a bar: on the kicks, the chord root, sliding up to the fifth or octave now and then.</summary>
        private static int BassDegree(int chord, int step, SeededRandom rng) =>
            step >= 10 && rng.NextFloat() < 0.3f ? chord + (rng.NextFloat() < 0.5f ? 4 : 7) : chord;

        /// <summary>Writes the whole beat (mono, <see cref="AudioSynth.SampleRate"/>).</summary>
        public static float[] Render(BeatDef b)
        {
            var sections = Sections(b);
            int bars = sections.Length * 4;
            double stepSec = 60.0 / b.Bpm / 4.0;
            int total = (int)Math.Round(bars * StepsPerBar * stepSec * SampleRate + TailSeconds * SampleRate);
            var dry = new float[total];   // drums and 808
            var keys = new float[total];  // melodic instruments (sent through the echo and room)
            var rng = new SeededRandom(b.Seed ^ 0x2B2B2Bu);
            var motif = Motif(b);
            var counter = Motif(new BeatDef { Seed = b.Seed * 31 + 7, Groove = BeatGroove.Smooth, Lead = b.Lead, Scale = b.Scale, Root = b.Root });

            int At(int bar, int step)
            {
                // Swing pushes the off sixteenths late.
                double t = (bar * StepsPerBar + step) * stepSec;
                if (step % 2 == 1) t += b.Swing * stepSec;
                return (int)Math.Round(t * SampleRate);
            }
            int StepLen(int steps) => (int)Math.Round(steps * stepSec * SampleRate);

            int lastBassNote = -1;
            for (int bar = 0; bar < bars; bar++)
            {
                var part = sections[bar / 4];
                int inSection = bar % 4;
                bool sectionStart = bar % 4 == 0;
                var chords = part == Part.B ? b.ChordsB : b.Chords;
                int chord = chords[bar % chords.Length];
                bool lastBarOfSection = inSection == 3;
                bool outroEnd = part == Part.Outro && bar >= bars - 4;
                bool drums = part == Part.A || part == Part.B || (part == Part.Outro && !outroEnd);
                bool introDrums = part == Part.Intro && inSection >= 2;

                // --- drums
                if (drums)
                {
                    var kick = KickPattern(b.Groove, bar);
                    var clap = ClapPattern(b.Groove, bar);
                    for (int s = 0; s < StepsPerBar; s++)
                    {
                        if (kick[s] == 'x' && !(lastBarOfSection && s >= 12 && part != Part.Outro)) Kick(dry, At(bar, s), 0.8f);
                        if (clap[s] == 'x') Clap(dry, At(bar, s), 0.55f, rng);
                    }
                    // A ghost clap and a little rim for the bouncy grooves.
                    if ((b.Groove == BeatGroove.Bounce || b.Groove == BeatGroove.Hype) && bar % 2 == 1) Rim(dry, At(bar, 15), 0.22f);
                    if (b.Groove == BeatGroove.Halftime && bar % 4 == 3) Clap(dry, At(bar, 15), 0.25f, rng);
                }

                // --- hats (also in the intro's second half and the breakdown, quietly)
                if (drums || introDrums || part == Part.Break)
                {
                    float level = drums ? 1f : 0.55f;
                    bool rollBar = lastBarOfSection || (b.Groove == BeatGroove.Halftime && bar % 2 == 1);
                    for (int s = 0; s < StepsPerBar; s++)
                    {
                        bool eighths = s % 2 == 0;
                        bool hit = b.Groove == BeatGroove.Smooth ? eighths : b.Groove == BeatGroove.Halftime ? eighths || (s % 4 == 3 && rng.NextFloat() < 0.5f) : true;
                        if (!hit) continue;
                        float accent = s % 4 == 0 ? 1f : s % 2 == 0 ? 0.75f : 0.55f;
                        // Rolls: the second half of the bar breaks into triplets or thirty-seconds.
                        if (rollBar && s >= 12 && drums)
                        {
                            int sub = b.Groove == BeatGroove.Halftime ? (s >= 14 ? 4 : 3) : 2;
                            int len = StepLen(1);
                            for (int k = 0; k < sub; k++)
                                Hat(dry, At(bar, s) + k * len / sub, 0.16f * level * (0.7f + 0.3f * k / sub), false, rng);
                            continue;
                        }
                        Hat(dry, At(bar, s), 0.15f * level * accent, false, rng);
                    }
                    if (b.Groove != BeatGroove.Smooth && drums && bar % 2 == 1) Hat(dry, At(bar, 6), 0.11f, true, rng); // an open hat
                }

                // --- 808 (with the kicks; held through the breakdown on the roots)
                if (drums || part == Part.Break)
                {
                    var kick = KickPattern(b.Groove, bar);
                    for (int s = 0; s < StepsPerBar; s++)
                    {
                        bool on = part == Part.Break ? s == 0 || s == 10 : kick[s] == 'x';
                        if (!on || (lastBarOfSection && s >= 12 && part == Part.A)) continue;
                        int next = s + 1;
                        while (next < StepsPerBar && kick[next] != 'x') next++;
                        int len = StepLen(Math.Max(2, next - s)) - 120;
                        int note = Note(b, BassDegree(chord, s, rng)) - 24;
                        while (note > b.Root - 18) note -= 12;
                        while (note < b.Root - 30) note += 12;
                        Bass808(dry, At(bar, s), len, note, lastBassNote, part == Part.Break ? 0.28f : 0.34f);
                        lastBassNote = note;
                    }
                }

                // --- the hook (every section; muffled in the intro and the last outro bars)
                bool soft = part == Part.Intro || outroEnd;
                float leadLevel = soft ? 0.55f : part == Part.Break ? 0.75f : 1f;
                int motifBar = bar % 2;
                foreach (var n in motif)
                {
                    if (n.Step / 16 != motifBar) continue;
                    int step = n.Step % 16;
                    int deg = chord + n.Degree;
                    int midi = Note(b, deg) + LeadOctave(b.Lead);
                    int start = At(bar, step);
                    int len = StepLen(n.Length);
                    if (n.Grace) Lead(keys, b, start - StepLen(1) / 3, StepLen(1) / 3, midi - 1, 0.5f * leadLevel, rng);
                    Lead(keys, b, start, len, midi, leadLevel, rng);
                }

                // --- chords: a pad, or electric piano comping for the smooth beats
                if (b.Pad || b.Lead == BeatLead.ElectricPiano)
                {
                    int[] voicing = { 0, 2, 4, 6 };
                    if (b.Pad)
                        foreach (int v in voicing)
                            Pad(keys, At(bar, 0), StepLen(16), Note(b, chord + v) - 12, (part == Part.Break ? 0.09f : 0.06f) * (soft ? 0.7f : 1f));
                    if (b.Lead == BeatLead.ElectricPiano && part != Part.Intro)
                        foreach (int v in voicing)
                        {
                            ElectricPiano(keys, At(bar, 0), StepLen(6), Note(b, chord + v), 0.11f, rng);
                            ElectricPiano(keys, At(bar, 10), StepLen(4), Note(b, chord + v), 0.08f, rng);
                        }
                }

                // --- bells / counter-line in the B sections
                if (b.Bells && part == Part.B)
                    foreach (var n in counter)
                    {
                        if (n.Step / 16 != motifBar) continue;
                        int midi = Note(b, chord + n.Degree) + 24;
                        Bell(keys, At(bar, n.Step % 16), StepLen(Math.Max(2, n.Length)), midi, 0.12f);
                    }

                // A riser into each drop.
                if (sectionStart && (part == Part.A || part == Part.B) && bar > 0 && sections[(bar / 4) - 1] != part)
                    Crash(dry, At(bar, 0), 0.12f, rng);
            }

            // Echo and room on the keys, then the mix.
            Echo(keys, (int)Math.Round(stepSec * 3 * SampleRate), 0.32f, 0.28f);
            var room = Room(keys, 0.22f);
            var mix = new float[total];
            for (int i = 0; i < total; i++) mix[i] = dry[i] + keys[i] + room[i];
            if (b.Crackle) Crackle(mix, rng);
            // Glue: a soft clip, then the level the rest of the soundtrack uses.
            for (int i = 0; i < total; i++) mix[i] = (float)Math.Tanh(mix[i] * 1.2f);
            FadeTail(mix, (int)(TailSeconds * SampleRate));
            return AudioSynth.Normalize(mix, 0.82f);
        }

        private static int LeadOctave(BeatLead l) => l == BeatLead.Bell ? 12 : l == BeatLead.Brass ? 0 : 0;

        private static void Lead(float[] buf, BeatDef b, int start, int len, int midi, float level, SeededRandom rng)
        {
            switch (b.Lead)
            {
                case BeatLead.Piano: Piano(buf, start, len, midi, 0.3f * level, rng); break;
                case BeatLead.ElectricPiano: ElectricPiano(buf, start, len, midi + 12, 0.23f * level, rng); break;
                case BeatLead.Bell: Bell(buf, start, len, midi, 0.23f * level); break;
                case BeatLead.Pluck: Pluck(buf, start, len, midi + 12, 0.3f * level, rng); break;
                default: Brass(buf, start, len, midi, 0.22f * level); break;
            }
        }

        // ------------------------------------------------------------------ instruments (all synthesized)

        private static float Hz(int midi) => AudioSynth.Midi(midi);

        private static void Kick(float[] buf, int at, float amp)
        {
            int len = (int)(0.38f * SampleRate);
            double ph = 0;
            for (int i = 0; i < len; i++)
            {
                int k = at + i;
                if (k < 0) continue;
                if (k >= buf.Length) break;
                float t = i / (float)SampleRate;
                float f = 46f + 125f * (float)Math.Exp(-t * 38f);
                ph += 2 * Math.PI * f / SampleRate;
                float env = (float)Math.Exp(-t * 9f);
                float click = i < 90 ? (1f - i / 90f) * 0.35f : 0f;
                buf[k] += (float)Math.Tanh(Math.Sin(ph) * 1.6f) * env * amp * 0.6f + click * amp * 0.3f;
            }
        }

        private static void Bass808(float[] buf, int at, int len, int midi, int fromMidi, float amp)
        {
            if (len <= 0) return;
            float target = Hz(midi);
            float from = fromMidi > 0 && Math.Abs(fromMidi - midi) <= 12 ? Hz(fromMidi) : target;
            double ph = 0;
            int glide = (int)(0.06f * SampleRate);
            int release = (int)(0.03f * SampleRate);
            for (int i = 0; i < len + release; i++)
            {
                int k = at + i;
                if (k < 0) continue;
                if (k >= buf.Length) break;
                float t = i / (float)SampleRate;
                float f = i < glide ? from + (target - from) * (i / (float)glide) : target;
                ph += 2 * Math.PI * f / SampleRate;
                float env = Math.Min(1f, i / 120f) * (float)Math.Exp(-t * 0.9f);
                if (i >= len) env *= 1f - (i - len) / (float)release;
                // Driven so its harmonics carry on phone speakers (they can't play the sub itself).
                double v = Math.Sin(ph) + 0.3 * Math.Sin(2 * ph) + 0.1 * Math.Sin(3 * ph);
                buf[k] += (float)Math.Tanh(v * 2.6) * env * amp;
            }
        }

        private static void Clap(float[] buf, int at, float amp, SeededRandom rng)
        {
            var bp = new Svf(1500f, 0.9f);
            int len = (int)(0.22f * SampleRate);
            double ph = 0;
            for (int i = 0; i < len; i++)
            {
                int k = at + i;
                if (k < 0) continue;
                if (k >= buf.Length) break;
                float t = i / (float)SampleRate;
                // Three quick slaps, then the tail.
                float slap = 0f;
                for (int s = 0; s < 3; s++)
                {
                    float ts = t - s * 0.009f;
                    if (ts >= 0f && ts < 0.009f) slap = Math.Max(slap, 1f - ts / 0.009f);
                }
                float tail = t > 0.018f ? (float)Math.Exp(-(t - 0.018f) * 16f) : 0f;
                float noise = bp.BandPass(rng.NextFloat() * 2f - 1f);
                ph += 2 * Math.PI * 190f / SampleRate;
                float body = (float)Math.Sin(ph) * (float)Math.Exp(-t * 30f) * 0.4f;
                buf[k] += (noise * 2.2f * Math.Max(slap, tail) + body) * amp;
            }
        }

        private static void Rim(float[] buf, int at, float amp)
        {
            int len = (int)(0.04f * SampleRate);
            for (int i = 0; i < len; i++)
            {
                int k = at + i;
                if (k < 0) continue;
                if (k >= buf.Length) break;
                float t = i / (float)SampleRate;
                buf[k] += (float)Math.Sin(2 * Math.PI * 1750f * t) * (float)Math.Exp(-t * 90f) * amp;
            }
        }

        private static void Hat(float[] buf, int at, float amp, bool open, SeededRandom rng)
        {
            var hp = new Svf(7500f, 0.7f);
            int len = (int)((open ? 0.16f : 0.03f) * SampleRate);
            float decay = open ? 22f : 140f;
            for (int i = 0; i < len; i++)
            {
                int k = at + i;
                if (k < 0) continue;
                if (k >= buf.Length) break;
                float t = i / (float)SampleRate;
                buf[k] += hp.HighPass(rng.NextFloat() * 2f - 1f) * (float)Math.Exp(-t * decay) * amp;
            }
        }

        private static void Crash(float[] buf, int at, float amp, SeededRandom rng)
        {
            var hp = new Svf(5000f, 0.8f);
            int len = (int)(1.2f * SampleRate);
            for (int i = 0; i < len; i++)
            {
                int k = at + i;
                if (k < 0) continue;
                if (k >= buf.Length) break;
                float t = i / (float)SampleRate;
                buf[k] += hp.HighPass(rng.NextFloat() * 2f - 1f) * (float)Math.Exp(-t * 3.2f) * amp;
            }
        }

        /// <summary>Piano-ish: decaying harmonics (the high ones die first), slightly stretched, with a soft hammer.</summary>
        private static void Piano(float[] buf, int at, int len, int midi, float amp, SeededRandom rng)
        {
            float f = Hz(midi);
            int total = len + (int)(0.25f * SampleRate);
            for (int i = 0; i < total; i++)
            {
                int k = at + i;
                if (k < 0) continue;
                if (k >= buf.Length) break;
                float t = i / (float)SampleRate;
                double v = 0;
                for (int h = 1; h <= 7; h++)
                {
                    double fh = f * h * (1 + 0.0004 * h * h);
                    if (fh > SampleRate * 0.45) break;
                    v += Math.Sin(2 * Math.PI * fh * t) * Math.Exp(-t * (1.5 + 1.1 * h)) / Math.Pow(h, 1.25);
                }
                float env = Math.Min(1f, i / 40f);
                if (i > len) env *= Math.Max(0f, 1f - (i - len) / (0.25f * SampleRate)); // damper
                float hammer = i < 160 ? (rng.NextFloat() * 2f - 1f) * (1f - i / 160f) * 0.15f : 0f;
                buf[k] += ((float)v * env + hammer) * amp;
            }
        }

        /// <summary>Electric piano: two-operator FM whose brightness fades, with a little tremolo and a tine on top.</summary>
        private static void ElectricPiano(float[] buf, int at, int len, int midi, float amp, SeededRandom rng)
        {
            float f = Hz(midi);
            int total = len + (int)(0.3f * SampleRate);
            for (int i = 0; i < total; i++)
            {
                int k = at + i;
                if (k < 0) continue;
                if (k >= buf.Length) break;
                float t = i / (float)SampleRate;
                double index = 1.6 * Math.Exp(-t * 4.0) + 0.25;
                double mod = Math.Sin(2 * Math.PI * f * t);
                double v = Math.Sin(2 * Math.PI * f * t + index * mod);
                v += 0.06 * Math.Sin(2 * Math.PI * f * 14 * t) * Math.Exp(-t * 25);
                float env = Math.Min(1f, i / 60f) * (float)Math.Exp(-t * 1.6f) * (1f + 0.12f * (float)Math.Sin(2 * Math.PI * 4.5 * t));
                if (i > len) env *= Math.Max(0f, 1f - (i - len) / (0.3f * SampleRate));
                buf[k] += (float)v * env * amp;
            }
        }

        /// <summary>Bell: inharmonic FM (3.5 : 1) with a long ring.</summary>
        private static void Bell(float[] buf, int at, int len, int midi, float amp)
        {
            float f = Hz(midi);
            int total = Math.Max(len, (int)(0.9f * SampleRate));
            for (int i = 0; i < total; i++)
            {
                int k = at + i;
                if (k < 0) continue;
                if (k >= buf.Length) break;
                float t = i / (float)SampleRate;
                double index = 2.4 * Math.Exp(-t * 5.0);
                double v = Math.Sin(2 * Math.PI * f * t + index * Math.Sin(2 * Math.PI * f * 3.5 * t));
                float env = Math.Min(1f, i / 30f) * (float)Math.Exp(-t * 2.6f);
                buf[k] += (float)v * env * amp;
            }
        }

        /// <summary>Plucked string (Karplus-Strong).</summary>
        private static void Pluck(float[] buf, int at, int len, int midi, float amp, SeededRandom rng)
        {
            int period = Math.Max(2, (int)Math.Round(SampleRate / Hz(midi)));
            var line = new float[period];
            for (int i = 0; i < period; i++) line[i] = rng.NextFloat() * 2f - 1f;
            int total = len + (int)(0.4f * SampleRate);
            int p = 0;
            float prev = 0f;
            for (int i = 0; i < total; i++)
            {
                int k = at + i;
                float v = line[p];
                float next = 0.5f * (v + prev) * 0.996f;
                prev = v;
                line[p] = next;
                p = (p + 1) % period;
                if (k < 0) continue;
                if (k >= buf.Length) break;
                float env = i > len ? Math.Max(0f, 1f - (i - len) / (0.4f * SampleRate)) : 1f;
                buf[k] += v * env * amp;
            }
        }

        /// <summary>Brass stab: detuned saws through a filter that opens on the attack.</summary>
        private static void Brass(float[] buf, int at, int len, int midi, float amp)
        {
            float f = Hz(midi);
            var lp = new Svf(800f, 0.8f);
            int total = len + (int)(0.08f * SampleRate);
            double p1 = 0, p2 = 0, p3 = 0;
            for (int i = 0; i < total; i++)
            {
                int k = at + i;
                float t = i / (float)SampleRate;
                p1 += f * 0.997 / SampleRate; p2 += f * 1.003 / SampleRate; p3 += f * 0.5 / SampleRate;
                float saw = (float)((p1 - Math.Floor(p1)) * 2 - 1 + (p2 - Math.Floor(p2)) * 2 - 1 + 0.5 * ((p3 - Math.Floor(p3)) * 2 - 1));
                lp.SetCutoff(600f + 3800f * (float)Math.Exp(-t * 9f));
                float v = lp.LowPass(saw * 0.4f);
                if (k < 0) continue;
                if (k >= buf.Length) break;
                float env = Math.Min(1f, i / 90f) * (i > len ? Math.Max(0f, 1f - (i - len) / (0.08f * SampleRate)) : 1f) * (0.75f + 0.25f * (float)Math.Exp(-t * 6f));
                buf[k] += v * env * amp * 2.2f;
            }
        }

        /// <summary>Pad: three detuned saws, filtered and slow to swell.</summary>
        private static void Pad(float[] buf, int at, int len, int midi, float amp)
        {
            float f = Hz(midi);
            var lp = new Svf(900f, 0.7f);
            double p1 = 0, p2 = 0, p3 = 0;
            int attack = (int)(0.35f * SampleRate), release = (int)(0.25f * SampleRate);
            for (int i = 0; i < len + release; i++)
            {
                int k = at + i;
                p1 += f * 0.993 / SampleRate; p2 += f / SampleRate; p3 += f * 1.007 / SampleRate;
                float saw = (float)((p1 - Math.Floor(p1)) + (p2 - Math.Floor(p2)) + (p3 - Math.Floor(p3)) - 1.5);
                float v = lp.LowPass(saw);
                if (k < 0) continue;
                if (k >= buf.Length) break;
                float env = Math.Min(1f, i / (float)attack) * (i > len ? Math.Max(0f, 1f - (i - len) / (float)release) : 1f);
                buf[k] += v * env * amp;
            }
        }

        private static void Crackle(float[] buf, SeededRandom rng)
        {
            for (int i = 0; i < buf.Length; i++)
                if (rng.NextFloat() < 0.0004f) buf[i] += (rng.NextFloat() * 2f - 1f) * 0.05f;
        }

        // ------------------------------------------------------------------ effects

        /// <summary>A feedback echo (in place).</summary>
        private static void Echo(float[] buf, int delay, float feedback, float mix)
        {
            if (delay <= 0) return;
            var line = new float[delay];
            int p = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                float d = line[p];
                line[p] = buf[i] + d * feedback;
                buf[i] += d * mix;
                p = (p + 1) % delay;
            }
        }

        /// <summary>A small room (four combs into two all-passes): returns the wet signal.</summary>
        private static float[] Room(float[] input, float wet)
        {
            var output = new float[input.Length];
            int[] combs = { 1116, 1188, 1277, 1356 };
            foreach (int c in combs)
            {
                var line = new float[c];
                int p = 0;
                float store = 0f;
                for (int i = 0; i < input.Length; i++)
                {
                    float y = line[p];
                    store = y * 0.75f + store * 0.25f; // damping
                    line[p] = input[i] + store * 0.78f;
                    output[i] += y * 0.25f;
                    p = (p + 1) % c;
                }
            }
            foreach (int a in new[] { 556, 441 })
            {
                var line = new float[a];
                int p = 0;
                for (int i = 0; i < output.Length; i++)
                {
                    float buffered = line[p];
                    float x = output[i];
                    line[p] = x + buffered * 0.5f;
                    output[i] = buffered - x;
                    p = (p + 1) % a;
                }
            }
            for (int i = 0; i < output.Length; i++) output[i] *= wet;
            return output;
        }

        private static void FadeTail(float[] buf, int samples)
        {
            int start = Math.Max(0, buf.Length - samples);
            for (int i = start; i < buf.Length; i++) buf[i] *= 1f - (i - start) / (float)Math.Max(1, samples);
        }

        /// <summary>A Chamberlin state-variable filter.</summary>
        private sealed class Svf
        {
            private float _f, _q, _low, _band;

            public Svf(float cutoff, float q)
            {
                SetCutoff(cutoff);
                _q = 1f / Math.Max(0.5f, q * 1.4f);
            }

            public void SetCutoff(float cutoff) => _f = 2f * (float)Math.Sin(Math.PI * Math.Min(cutoff, SampleRate * 0.2f) / SampleRate);

            private float Run(float x)
            {
                _low += _f * _band;
                float high = x - _low - _q * _band;
                _band += _f * high;
                return high;
            }

            public float LowPass(float x) { Run(x); return _low; }
            public float HighPass(float x) => Run(x);
            public float BandPass(float x) { Run(x); return _band; }
        }
    }
}
