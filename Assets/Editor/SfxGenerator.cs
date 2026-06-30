using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SilverFang.EditorTools
{
    /// Procedurally synthesizes a set of combat sound effects (no external assets
    /// needed) and writes them as 16-bit PCM WAVs into Assets/Audio/SFX, then
    /// imports them as AudioClips. Run via SilverFang/Generate SFX or the
    /// GenerateAll method. Deterministic (seeded) so re-runs are stable.
    public static class SfxGenerator
    {
        private const int Rate = 44100;
        private const string OutDir = "Assets/Audio/SFX";
        private static System.Random rng = new System.Random(1234);

        [MenuItem("SilverFang/Generate SFX")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(OutDir);
            rng = new System.Random(1234);

            Write("sfx_sword_swing", Swing(0.28f, 1400f, 380f, 0.55f));
            Write("sfx_sword_heavy", Swing(0.40f, 950f, 220f, 0.85f));
            Write("sfx_hit_flesh",   FleshHit(0.30f));
            Write("sfx_hit_metal",   MetalHit(0.45f));
            Write("sfx_hit_crit",    CritHit(0.55f));
            Write("sfx_gunshot",     Gunshot(0.32f));
            Write("sfx_gun_charged", GunCharged(0.6f));
            Write("sfx_explosion",   Explosion(0.9f));
            Write("sfx_dash",        Dash(0.26f));
            Write("sfx_jump",        Sweep(0.22f, 320f, 720f, 0.5f, 0.25f));
            Write("sfx_land",        Thud(0.26f, 150f));
            Write("sfx_parry",       MetalHit(0.30f, bright: true));
            Write("sfx_ui_move",     Blip(0.07f, 660f));
            Write("sfx_ui_confirm",  Arp(0.32f, new[] { 523f, 784f, 1046f }));
            Write("sfx_level_up",    Arp(0.7f, new[] { 392f, 523f, 659f, 784f, 1046f }));
            Write("sfx_awaken",      Awaken(1.1f));
            Write("sfx_combo_tier",  Arp(0.25f, new[] { 880f, 1318f }));

            AssetDatabase.Refresh();
            foreach (var f in Directory.GetFiles(OutDir, "*.wav"))
            {
                var imp = AssetImporter.GetAtPath(f.Replace('\\', '/')) as AudioImporter;
                if (imp == null) continue;
                var s = imp.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad; // tiny one-shots
                imp.defaultSampleSettings = s;
                imp.forceToMono = true;
                imp.SaveAndReimport();
            }
            Debug.Log("SfxGenerator: generated combat SFX in " + OutDir);
        }

        // ---- synthesis voices ----
        private static float[] Swing(float dur, float f0, float f1, float amp)
        {
            int n = (int)(dur * Rate); var b = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Sin(t * Mathf.PI);          // soft in/out whoosh
                float cutoff = Mathf.Lerp(f0, f1, t);
                float noise = (float)(rng.NextDouble() * 2 - 1);
                b[i] = BandPass(noise, cutoff) * env * amp;
            }
            return b;
        }

        private static float[] FleshHit(float dur)
        {
            int n = (int)(dur * Rate); var b = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Exp(-7f * t);
                float thud = Mathf.Sin(2 * Mathf.PI * 110f * Mathf.Lerp(1f, 0.6f, t) * i / Rate);
                float splat = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-22f * t);
                b[i] = (thud * 0.7f + splat * 0.5f) * env;
            }
            return b;
        }

        private static float[] MetalHit(float dur, bool bright = false)
        {
            int n = (int)(dur * Rate); var b = new float[n];
            float[] partials = bright ? new[] { 1200f, 2700f, 4100f, 5600f }
                                      : new[] { 760f, 1830f, 2950f, 4200f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n; float s = 0f;
                foreach (var p in partials)
                    s += Mathf.Sin(2 * Mathf.PI * p * i / Rate) * Mathf.Exp(-9f * t);
                float click = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-60f * t);
                b[i] = (s / partials.Length * 0.8f + click * 0.6f);
            }
            return b;
        }

        private static float[] CritHit(float dur)
        {
            var a = MetalHit(dur, bright: true); var f = FleshHit(dur * 0.7f);
            for (int i = 0; i < f.Length && i < a.Length; i++) a[i] = Mathf.Clamp(a[i] + f[i], -1f, 1f);
            return a;
        }

        private static float[] Gunshot(float dur)
        {
            int n = (int)(dur * Rate); var b = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float crack = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-30f * t);
                float boom = Mathf.Sin(2 * Mathf.PI * 80f * i / Rate) * Mathf.Exp(-12f * t);
                b[i] = Mathf.Clamp(crack * 0.9f + boom * 0.7f, -1f, 1f);
            }
            return b;
        }

        private static float[] GunCharged(float dur)
        {
            int n = (int)(dur * Rate); var b = new float[n];
            int chargeN = (int)(n * 0.55f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                if (i < chargeN) // rising whine
                    b[i] = Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(200f, 1500f, i / (float)chargeN) * i / Rate) * 0.3f * (i / (float)chargeN);
                else
                {
                    float tt = (i - chargeN) / (float)(n - chargeN);
                    float crack = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-14f * tt);
                    float boom = Mathf.Sin(2 * Mathf.PI * 70f * i / Rate) * Mathf.Exp(-8f * tt);
                    b[i] = Mathf.Clamp(crack + boom, -1f, 1f);
                }
            }
            return b;
        }

        private static float[] Explosion(float dur)
        {
            int n = (int)(dur * Rate); var b = new float[n]; float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += (noise - lp) * 0.08f;                 // low-passed roar
                float rumble = Mathf.Sin(2 * Mathf.PI * 55f * i / Rate);
                b[i] = Mathf.Clamp((lp * 1.2f + rumble * 0.5f) * Mathf.Exp(-4.5f * t), -1f, 1f);
            }
            return b;
        }

        private static float[] Dash(float dur)
        {
            int n = (int)(dur * Rate); var b = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Sin(t * Mathf.PI);
                float noise = (float)(rng.NextDouble() * 2 - 1);
                b[i] = BandPass(noise, Mathf.Lerp(900f, 2600f, t)) * env * 0.5f;
            }
            return b;
        }

        private static float[] Sweep(float dur, float f0, float f1, float amp, float decay)
        {
            int n = (int)(dur * Rate); var b = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(f0, f1, t);
                b[i] = Mathf.Sin(2 * Mathf.PI * f * i / Rate) * amp * Mathf.Exp(-decay * t * 10f);
            }
            return b;
        }

        private static float[] Thud(float dur, float f)
        {
            int n = (int)(dur * Rate); var b = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                b[i] = Mathf.Sin(2 * Mathf.PI * f * Mathf.Lerp(1f, 0.5f, t) * i / Rate) * Mathf.Exp(-10f * t) * 0.8f;
            }
            return b;
        }

        private static float[] Blip(float dur, float f)
        {
            int n = (int)(dur * Rate); var b = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                b[i] = Mathf.Sin(2 * Mathf.PI * f * i / Rate) * Mathf.Exp(-8f * t) * 0.4f;
            }
            return b;
        }

        private static float[] Arp(float dur, float[] notes)
        {
            int n = (int)(dur * Rate); var b = new float[n];
            int per = n / notes.Length;
            for (int k = 0; k < notes.Length; k++)
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)per;
                    int idx = k * per + i;
                    if (idx >= n) break;
                    b[idx] = Mathf.Sin(2 * Mathf.PI * notes[k] * i / Rate) * Mathf.Exp(-5f * t) * 0.4f;
                }
            return b;
        }

        private static float[] Awaken(float dur)
        {
            int n = (int)(dur * Rate); var b = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(120f, 900f, t * t);
                float sweep = Mathf.Sin(2 * Mathf.PI * f * i / Rate);
                float shimmer = Mathf.Sin(2 * Mathf.PI * f * 2.01f * i / Rate) * 0.4f;
                float env = Mathf.Sin(t * Mathf.PI);
                b[i] = (sweep + shimmer) * env * 0.5f;
            }
            return b;
        }

        // crude one-pole bandpass for whooshy noise
        private static float bpLow, bpBand;
        private static float BandPass(float input, float cutoff)
        {
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Clamp(cutoff, 50f, 18000f) / Rate);
            bpLow += f * bpBand;
            float high = input - bpLow - 0.6f * bpBand;
            bpBand += f * high;
            return bpBand;
        }

        // ---- WAV writer (16-bit PCM mono) ----
        private static void Write(string name, float[] samples)
        {
            bpLow = bpBand = 0f;
            string path = $"{OutDir}/{name}.wav";
            using var fs = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(fs);
            int dataLen = samples.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataLen);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataLen);
            // peak-normalize to -1.5 dBFS so every effect is healthy and consistent
            float peak = 0.0001f;
            foreach (var s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
            float gain = 0.84f / peak;
            foreach (var s in samples)
                w.Write((short)(Mathf.Clamp(s * gain, -1f, 1f) * short.MaxValue));
        }
    }
}
