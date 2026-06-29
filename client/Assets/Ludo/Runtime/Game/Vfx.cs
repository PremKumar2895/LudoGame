using UnityEngine;

namespace LudoGame
{
    /// <summary>One-shot expanding-fading sprite burst (capture flash, win confetti). Self-destroys.</summary>
    public sealed class Vfx : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private float _maxSize, _life, _t;

        public static void Burst(Vector3 pos, Color color, float size = 0.9f, float life = 0.45f)
        {
            var go = new GameObject("vfx_burst");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * (size * 0.15f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle(); sr.color = color; sr.sortingOrder = 20;
            var v = go.AddComponent<Vfx>();
            v._sr = sr; v._maxSize = size; v._life = life;
        }

        /// <summary>A spray of coloured confetti bursts around a point (win celebration).</summary>
        public static void Confetti(Vector3 center, int count = 16)
        {
            for (int i = 0; i < count; i++)
            {
                var c = BoardColors.For(Random.Range(0, 10));
                Vector3 p = center + (Vector3)(Random.insideUnitCircle * Random.Range(0.4f, 2.6f));
                Burst(p, c, Random.Range(0.25f, 0.6f), Random.Range(0.5f, 1.1f));
            }
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = _t / _life;
            if (k >= 1f) { Destroy(gameObject); return; }
            transform.localScale = Vector3.one * Mathf.Lerp(_maxSize * 0.15f, _maxSize, k);
            var col = _sr.color; col.a = 1f - k; _sr.color = col;
        }
    }
}
