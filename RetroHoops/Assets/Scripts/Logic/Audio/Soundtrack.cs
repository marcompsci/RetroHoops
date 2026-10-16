using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum SongScale { Major = 0, Minor = 1, Dorian = 2, Mixolydian = 3 }

    /// <summary>An original chiptune song: everything the composer needs to write it, deterministically.</summary>
    public sealed class SongDef
    {
        public string Title;
        public string Blurb;
        public float Bpm;
        /// <summary>MIDI note of the key's root (e.g. 57 = A3).</summary>
        public int Root;
        public SongScale Scale;
        /// <summary>Chord roots as scale degrees (0 = I), one per bar, for the A section; B uses <see cref="Bridge"/>.</summary>
        public int[] Verse;
        public int[] Bridge;
        /// <summary>Lead pulse width: 0.125, 0.25 or 0.5.</summary>
        public float Duty;
        /// <summary>0 = laid back (kick on 1 and 3), 1 = driving (four on the floor), 2 = breakbeat.</summary>
        public int Drums;
        public uint Seed;
        /// <summary>Notes per beat in the arpeggio (2 or 4).</summary>
        public int ArpRate = 2;
    }

    /// <summary>
    /// Retro Hoops's soundtrack. The first three tracks are the original loops from <see cref="AudioSynth"/>
    /// (menus, matches, Rise hub); the rest are full songs written here by a small composer: a pulse
    /// lead melody over chord tones, a triangle bass, a pulse arpeggio and noise drums, in an A-A-B-A
    /// form that loops seamlessly. Every note is generated from the song's numbers, so nothing is
    /// sampled or borrowed.
    /// </summary>
    public static class Soundtrack
    {
        public const int ClassicCount = AudioSynth.MusicTrackCount;
        public const int BeatsPerBar = 4;
        private const int SampleRate = AudioSynth.SampleRate;

        public static readonly string[] ClassicTitles = { "Sunset Cage", "Blacktop Bounce", "Gold Signal" };
        public static readonly string[] ClassicBlurbs = { "The menu theme.", "The match theme.", "The Rise Mode theme." };

        public static readonly SongDef[] Songs =
        {
            new SongDef { Title = "Neon Overpass", Blurb = "Late-night drive past the courts.", Bpm = 118f, Root = 57, Scale = SongScale.Minor,
                          Verse = new[] { 0, 5, 2, 6 }, Bridge = new[] { 3, 4, 0, 4 }, Duty = 0.25f, Drums = 1, Seed = 1101, ArpRate = 4 },
            new SongDef { Title = "Full Court Press", Blurb = "Up-tempo, no time to rest.", Bpm = 138f, Root = 52, Scale = SongScale.Dorian,
                          Verse = new[] { 0, 3, 0, 4 }, Bridge = new[] { 5, 3, 6, 4 }, Duty = 0.125f, Drums = 2, Seed = 2202, ArpRate = 2 },
            new SongDef { Title = "Pier Nine Breeze", Blurb = "Easy summer afternoon.", Bpm = 96f, Root = 55, Scale = SongScale.Major,
                          Verse = new[] { 0, 3, 4, 0 }, Bridge = new[] { 5, 3, 1, 4 }, Duty = 0.5f, Drums = 0, Seed = 3303, ArpRate = 2 },
            new SongDef { Title = "Crossover Dribble", Blurb = "Bouncy and a little cheeky.", Bpm = 124f, Root = 53, Scale = SongScale.Mixolydian,
                          Verse = new[] { 0, 6, 3, 0 }, Bridge = new[] { 4, 3, 6, 4 }, Duty = 0.25f, Drums = 2, Seed = 4404, ArpRate = 4 },
            new SongDef { Title = "Final Buzzer", Blurb = "Tense, for the last possession.", Bpm = 132f, Root = 50, Scale = SongScale.Minor,
                          Verse = new[] { 0, 0, 5, 4 }, Bridge = new[] { 3, 5, 6, 4 }, Duty = 0.125f, Drums = 1, Seed = 5505, ArpRate = 4 },
            new SongDef { Title = "Victory Lap", Blurb = "Bright, for the trophy ceremony.", Bpm = 112f, Root = 60, Scale = SongScale.Major,
                          Verse = new[] { 0, 4, 5, 3 }, Bridge = new[] { 1, 4, 0, 4 }, Duty = 0.5f, Drums = 1, Seed = 6606, ArpRate = 2 },
        };

        /// <summary>15 Oct 2026: the beat tape (<see cref="StreetBeats"/>) comes after the chiptune songs.</summary>
        public static int Count => ClassicCount + Songs.Length + StreetBeats.Beats.Length;

        /// <summary>The first beat-tape track.</summary>
        public static int FirstBeat => ClassicCount + Songs.Length;

        /// <summary>The soundtrack track of beat <paramref name="beat"/> (an index into <see cref="StreetBeats.Beats"/>).</summary>
        public static int BeatTrack(int beat) => FirstBeat + beat;

        /// <summary>The beat for a track, or null when it's a chiptune track.</summary>
        public static BeatDef BeatOf(int track) => track >= FirstBeat && track < Count ? StreetBeats.Beats[track - FirstBeat] : null;

        public static string Title(int track) =>
            track < ClassicCount ? ClassicTitles[Wrap(track)] : BeatOf(track)?.Title ?? Songs[track - ClassicCount].Title;
        public static string Blurb(int track) =>
            track < ClassicCount ? ClassicBlurbs[Wrap(track)] : BeatOf(track)?.Vibe ?? Songs[track - ClassicCount].Blurb;

        /// <summary>Tempo of a composed track (0 for the classic loops).</summary>
        public static float Bpm(int track) =>
            track < ClassicCount || track >= Count ? 0f : BeatOf(track)?.Bpm ?? Songs[track - ClassicCount].Bpm;

        private static int Wrap(int t) => ((t % ClassicCount) + ClassicCount) % ClassicCount;

        /// <summary>The samples for any track (classic loops or composed songs).</summary>
        public static float[] Render(int track)
        {
            if (track < 0 || track >= Count) track = 0;
            if (track < ClassicCount) return AudioSynth.MusicLoop(track);
            var beat = BeatOf(track);
            return beat != null ? StreetBeats.Render(beat) : Compose(Songs[track - ClassicCount]);
        }

        /// <summary>Length of a composed song's loop in seconds (A-A-B-A, four bars each).</summary>
        public static float Seconds(SongDef s) => 16 * BeatsPerBar * 60f / s.Bpm;

        private static readonly int[][] Intervals =
        {
            new[] { 0, 2, 4, 5, 7, 9, 11 },  // major
            new[] { 0, 2, 3, 5, 7, 8, 10 },  // natural minor
            new[] { 0, 2, 3, 5, 7, 9, 10 },  // dorian
            new[] { 0, 2, 4, 5, 7, 9, 10 },  // mixolydian
        };

        /// <summary>MIDI note of scale degree <paramref name="degree"/> (any integer; wraps into octaves).</summary>
        public static int Note(SongDef s, int degree)
        {
            var iv = Intervals[(int)s.Scale];
            int octave = (int)Math.Floor(degree / 7.0);
            int d = degree - octave * 7;
            return s.Root + octave * 12 + iv[d];
        }

        /// <summary>The 64 melody notes (one per beat, as scale degrees above the root; −99 = rest) for the whole loop.</summary>
        public static int[] Melody(SongDef s)
        {
            var rng = new SeededRandom(s.Seed);
            var phraseA = Phrase(s, s.Verse, rng);
            var phraseB = Phrase(s, s.Bridge, rng);
            var phraseA2 = (int[])phraseA.Clone();
            // The last A answers the first: its final bar resolves to the tonic.
            for (int i = 12; i < 16; i++) phraseA2[i] = i == 15 ? 7 : phraseA2[i];
            var all = new List<int>();
            all.AddRange(phraseA);
            all.AddRange(phraseA);
            all.AddRange(phraseB);
            all.AddRange(phraseA2);
            return all.ToArray();
        }

        /// <summary>A four-bar phrase: chord tones on beats 1 and 3, stepwise passing notes between, the odd rest.</summary>
        private static int[] Phrase(SongDef s, int[] chords, SeededRandom rng)
        {
            var notes = new int[16];
            int prev = 7 + chords[0];
            for (int bar = 0; bar < 4; bar++)
            {
                int chord = chords[bar % chords.Length];
                for (int beat = 0; beat < BeatsPerBar; beat++)
                {
                    int i = bar * BeatsPerBar + beat;
                    int n;
                    if (beat == 0 || beat == 2)
                    {
                        // Nearest chord tone (root, third or fifth, an octave up) to the previous note.
                        int best = 7 + chord, bestDist = int.MaxValue;
                        foreach (int tone in new[] { chord, chord + 2, chord + 4, chord + 7, chord + 9, chord + 11 })
                        {
                            int dist = Math.Abs(tone - prev) + (rng.NextFloat() < 0.25f ? 1 : 0);
                            if (dist < bestDist && tone >= 4 && tone <= 14) { best = tone; bestDist = dist; }
                        }
                        n = best;
                    }
                    else if (beat == 3 && bar % 2 == 1 && rng.NextFloat() < 0.35f) n = -99;
                    else n = prev + (rng.NextFloat() < 0.5f ? 1 : -1) * (rng.NextFloat() < 0.8f ? 1 : 2);
                    n = n == -99 ? n : Math.Max(3, Math.Min(15, n));
                    notes[i] = n;
                    if (n != -99) prev = n;
                }
            }
            return notes;
        }

        /// <summary>Writes the whole song: 16 bars (A-A-B-A), seamless when looped.</summary>
        public static float[] Compose(SongDef s)
        {
            float beat = 60f / s.Bpm;
            int beats = 16 * BeatsPerBar;
            int n = (int)Math.Round(beats * beat * SampleRate);
            var buf = new float[n];
            var melody = Melody(s);
            var rng = new SeededRandom(s.Seed ^ 0x5A5A5Au);
            int Start(float beatIndex) => (int)Math.Round(beatIndex * beat * SampleRate);

            for (int b = 0; b < beats; b++)
            {
                int bar = b / BeatsPerBar, inBar = b % BeatsPerBar;
                int section = bar / 4;
                var chords = section == 2 ? s.Bridge : s.Verse;
                int chord = chords[bar % 4 % chords.Length];
                int start = Start(b), len = Start(b + 1) - start;

                // Triangle bass: root on the beat, fifth on the "and" in driving songs.
                int bassNote = Note(s, chord) - 12;
                Tone(buf, start, (int)(len * 0.55f), Midi(bassNote), 0.22f, Wave.Triangle, 0f);
                if (s.Drums >= 1 && inBar % 2 == 1) Tone(buf, start + len / 2, (int)(len * 0.4f), Midi(Note(s, chord + 4) - 12), 0.16f, Wave.Triangle, 0f);

                // Arpeggio over the chord (quiet pulse).
                int steps = s.ArpRate;
                int[] shape = { 0, 2, 4, 7 };
                for (int k = 0; k < steps; k++)
                {
                    int deg = chord + shape[(b * steps + k) % shape.Length];
                    int at = start + k * len / steps;
                    Tone(buf, at, len / steps - 150, Midi(Note(s, deg) + 12), 0.045f, Wave.Pulse, 0.25f);
                }

                // Lead melody: a note per beat, held through the next rest, with a little vibrato on long notes.
                int m = melody[b];
                if (m != -99)
                {
                    int hold = 1;
                    while (b + hold < beats && melody[b + hold] == -99 && hold < 2) hold++;
                    int noteLen = Start(b + hold) - start - 300;
                    Tone(buf, start, noteLen, Midi(Note(s, m) + 12), 0.13f, Wave.Pulse, s.Duty, hold > 1 ? 0.012f : 0f);
                    // Echo an octave down, a sixteenth late, very quiet: a classic chip "chorus".
                    Tone(buf, start + len / 4, noteLen - len / 4, Midi(Note(s, m)), 0.035f, Wave.Pulse, 0.5f);
                }

                // Drums.
                bool kick = s.Drums == 1 || inBar == 0 || (s.Drums == 0 && inBar == 2) || (s.Drums == 2 && (inBar == 0 || (inBar == 2 && bar % 2 == 1)));
                if (s.Drums == 2 && inBar == 3) Kick(buf, start + len / 2, len / 3);
                if (kick) Kick(buf, start, len / 2);
                if (inBar == 1 || inBar == 3) Snare(buf, start, len / 2, rng);
                for (int h = 0; h < 2; h++) Hat(buf, start + h * len / 2, Math.Min(len / 4, 1500), rng, h == 1 ? 0.05f : 0.03f);
                // A fill into each new section.
                if (bar % 4 == 3 && inBar == 3)
                    for (int f = 0; f < 4; f++) Snare(buf, start + f * len / 4, len / 5, rng, 0.11f + 0.02f * f);
            }
            return AudioSynth.Normalize(buf, 0.8f);
        }

        // ------------------------------------------------------------------ instruments

        private enum Wave { Pulse, Triangle }

        private static float Midi(int note) => AudioSynth.Midi(note);

        private static void Tone(float[] s, int start, int length, float f, float amp, Wave wave, float duty, float vibrato = 0f)
        {
            if (length <= 0) return;
            double phase = 0;
            for (int i = 0; i < length && start + i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Math.Min(1f, i / 150f) * Math.Min(1f, (length - i) / 500f) * (1f - 0.25f * i / length);
                float freq = f * (1f + vibrato * (float)Math.Sin(2 * Math.PI * 5.5 * t) * Math.Min(1f, t * 4f));
                phase += freq / SampleRate;
                float p = (float)(phase - Math.Floor(phase));
                float v = wave == Wave.Pulse ? (p < duty ? 1f : -1f) : (p < 0.5f ? 4f * p - 1f : 3f - 4f * p);
                // Triangle is quantised to 16 steps, like the old sound chips.
                if (wave == Wave.Triangle) v = (float)Math.Round(v * 7.5f) / 7.5f;
                s[start + i] += v * env * amp;
            }
        }

        private static void Kick(float[] s, int start, int length)
        {
            double ph = 0;
            int kl = Math.Min(length, 3500);
            for (int i = 0; i < kl && start + i < s.Length; i++)
            {
                float t = i / (float)kl;
                ph += 2 * Math.PI * (120f - 80f * t) / SampleRate;
                s[start + i] += (float)Math.Sin(ph) * 0.28f * (1f - t);
            }
        }

        private static void Snare(float[] s, int start, int length, SeededRandom rng, float amp = 0.12f)
        {
            int sl = Math.Min(length, 4000);
            for (int i = 0; i < sl && start + i < s.Length; i++)
            {
                float t = i / (float)sl;
                s[start + i] += (rng.NextFloat() * 2f - 1f) * amp * (1f - t) * (1f - t);
            }
        }

        private static void Hat(float[] s, int start, int length, SeededRandom rng, float amp)
        {
            float prev = 0f;
            for (int i = 0; i < length && start + i < s.Length; i++)
            {
                float x = rng.NextFloat() * 2f - 1f;
                float hp = x - prev; // crude high-pass for a thin "tss"
                prev = x;
                s[start + i] += hp * amp * (1f - i / (float)length);
            }
        }
    }
}
