// Lightweight transient toast notifications — connectivity, joins/leaves, turn timeouts, lag.
// Auto-creates itself on first Push (no scene wiring). Renders top-right, newest on top, auto-fades.
using System.Collections.Generic;
using UnityEngine;

namespace LudoGame.UI
{
    public enum NoticeKind { Info, Success, Warning, Error }

    public sealed class NotificationCenter : MonoBehaviour
    {
        public static NotificationCenter Instance { get; private set; }

        private struct Toast { public string Msg; public NoticeKind Kind; public float Born; public float Life; }
        private readonly List<Toast> _toasts = new List<Toast>();
        private GUIStyle _label;

        public static void Push(string msg, NoticeKind kind = NoticeKind.Info, float life = 4f)
        {
            if (Instance == null)
            {
                var go = new GameObject("NotificationCenter");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<NotificationCenter>();
            }
            Instance._toasts.Add(new Toast { Msg = msg, Kind = kind, Born = Time.unscaledTime, Life = life });
            if (Instance._toasts.Count > 6) Instance._toasts.RemoveAt(0);
            Debug.Log($"[Notice:{kind}] {msg}");
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
                if (Time.unscaledTime - _toasts[i].Born > _toasts[i].Life) _toasts.RemoveAt(i);
        }

        private static Color Accent(NoticeKind k)
        {
            switch (k)
            {
                case NoticeKind.Success: return new Color(0.30f, 0.74f, 0.43f);
                case NoticeKind.Warning: return new Color(0.95f, 0.66f, 0.22f);
                case NoticeKind.Error:   return new Color(0.88f, 0.32f, 0.32f);
                default:                 return new Color(0.36f, 0.49f, 0.86f);
            }
        }

        private void OnGUI()
        {
            if (_toasts.Count == 0) return;
            if (_label == null)
                _label = new GUIStyle(GUI.skin.label)
                { fontSize = 13, alignment = TextAnchor.MiddleLeft, wordWrap = true, padding = new RectOffset(2, 2, 2, 2) };

            const float w = 300f, h = 42f, gap = 8f;
            float x = Screen.width - w - 14f;
            float y = 14f;

            for (int i = _toasts.Count - 1; i >= 0; i--) // newest on top
            {
                var t = _toasts[i];
                float age = Time.unscaledTime - t.Born;
                float a = Mathf.Clamp01(Mathf.Min(age / 0.15f, (t.Life - age) / 0.6f)); // fade in/out
                var c = Accent(t.Kind);
                var prev = GUI.color;

                GUI.color = new Color(0.11f, 0.11f, 0.13f, 0.93f * a);
                GUI.Box(new Rect(x, y, w, h), GUIContent.none);
                GUI.color = new Color(c.r, c.g, c.b, a);
                GUI.Box(new Rect(x, y, 5f, h), GUIContent.none);

                _label.normal.textColor = new Color(0.97f, 0.97f, 0.99f, a);
                GUI.Label(new Rect(x + 14f, y, w - 22f, h), t.Msg, _label);
                GUI.color = prev;
                y += h + gap;
            }
        }
    }
}
