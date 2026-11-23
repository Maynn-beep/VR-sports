using System.Collections.Generic;
using UnityEngine;

namespace VRSports.Core
{
    /// <summary>
    /// Звуковые эффекты. Звуки сгенерированы скриптом Tools/Audio/generate_sounds.py и лежат в
    /// Resources/Sounds — поэтому их можно загрузить по имени: Sfx.Play("pin_hit", точка).
    ///
    /// Устройство:
    ///  • пул из 24 источников звука (AudioSource) — не создаём объект на каждый звук (меньше мусора, нет лагов);
    ///  • звук ПРОСТРАНСТВЕННЫЙ (3D): громкость падает с расстоянием, а слева/справа слышно по-разному —
    ///    в VR это важно, по звуку понимаешь, где упали кегли или куда воткнулась стрела;
    ///  • высота тона чуть случайна (±5%) — одинаковые удары не звучат «как из автомата»;
    ///  • ограничение частоты: один и тот же звук не чаще, чем раз в 30 мс (иначе при столкновении
    ///    десяти кеглей PhysX присылает десятки событий за кадр и звук «трещит»).
    /// </summary>
    public static class Sfx
    {
        private const int PoolSize = 24;
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static readonly Dictionary<string, float> LastPlayed = new Dictionary<string, float>();
        private static AudioSource[] _pool;
        private static int _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clips.Clear();
            LastPlayed.Clear();
            _pool = null;
            _next = 0;
        }

        public static AudioClip Clip(string name)
        {
            if (Clips.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>("Sounds/" + name);
            if (c == null) Debug.LogWarning($"[Sfx] Нет звука Resources/Sounds/{name}");
            Clips[name] = c;
            return c;
        }

        /// <summary>
        /// 3D-звук в точке. minDistance — до этого расстояния звук на полной громкости
        /// (для дальних событий, например кеглей в 19 м, берём больше).
        /// </summary>
        public static void Play(string name, Vector3 position, float volume = 1f, float minDistance = 1.5f,
            float pitchJitter = 0.05f)
        {
            if (volume <= 0.01f || !Allowed(name)) return;
            var clip = Clip(name);
            if (clip == null) return;
            var src = NextSource();
            src.transform.position = position;
            src.spatialBlend = 1f;
            src.minDistance = minDistance;
            src.maxDistance = Mathf.Max(30f, minDistance * 15f);
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        /// <summary>Звук «в голове» (без положения): отсчёт, гонг, итог партии.</summary>
        public static void Play2D(string name, float volume = 1f)
        {
            if (!Allowed(name)) return;
            var clip = Clip(name);
            if (clip == null) return;
            var src = NextSource();
            src.spatialBlend = 0f;
            src.pitch = 1f;
            src.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        /// <summary>Зацикленный 3D-звук на объекте (гул катящегося шара). Громкость меняет сам объект.</summary>
        public static AudioSource Loop(string name, GameObject owner, float minDistance = 1.5f)
        {
            var src = owner.AddComponent<AudioSource>();
            src.clip = Clip(name);
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.minDistance = minDistance;
            src.maxDistance = 30f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.volume = 0f;
            return src;
        }

        private static bool Allowed(string name)
        {
            float now = Time.unscaledTime;
            if (LastPlayed.TryGetValue(name, out float t) && now - t < 0.03f) return false;
            LastPlayed[name] = now;
            return true;
        }

        private static AudioSource NextSource()
        {
            if (_pool == null || _pool[0] == null)
            {
                var root = new GameObject("[Sfx]");
                Object.DontDestroyOnLoad(root);   // пул живёт между сценами
                _pool = new AudioSource[PoolSize];
                for (int i = 0; i < PoolSize; i++)
                {
                    var go = new GameObject("Voice_" + i);
                    go.transform.SetParent(root.transform);
                    var s = go.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.rolloffMode = AudioRolloffMode.Logarithmic;
                    s.dopplerLevel = 0f;
                    _pool[i] = s;
                }
            }
            var src = _pool[_next];
            _next = (_next + 1) % PoolSize;
            return src;
        }
    }
}
