using System.Collections;
using UnityEngine;

namespace LudoGame
{
    /// <summary>One token's visual (a coloured disc with an outline) plus its move/capture animations.</summary>
    public sealed class TokenView : MonoBehaviour
    {
        public int BoardPos;
        public int TokenId;

        private float _baseScale = 0.3f;
        private bool _highlight;

        public void Init(int boardPos, int tokenId, Color color, float size)
        {
            BoardPos = boardPos; TokenId = tokenId; _baseScale = size;

            var outline = new GameObject("outline");
            outline.transform.SetParent(transform, false);
            outline.transform.localScale = Vector3.one * 1.2f;
            var osr = outline.AddComponent<SpriteRenderer>();
            osr.sprite = SpriteFactory.Circle(); osr.color = BoardColors.Outline; osr.sortingOrder = 9;

            var body = gameObject.AddComponent<SpriteRenderer>();
            body.sprite = SpriteFactory.Circle(); body.color = color; body.sortingOrder = 10;

            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.6f;

            transform.localScale = Vector3.one * _baseScale;
        }

        public void SetInstant(BoardLayout layout, int progress)
            => transform.position = layout.World(BoardPos, progress, TokenId);

        public void Highlight(bool on)
        {
            _highlight = on;
            if (!on) transform.localScale = Vector3.one * _baseScale;
        }

        private void Update()
        {
            if (_highlight)
                transform.localScale = Vector3.one * _baseScale * (1f + 0.14f * Mathf.PingPong(Time.time * 3f, 1f));
        }

        /// <summary>Hop cell-by-cell from one progress to another (the "walk").</summary>
        public IEnumerator MoveRoutine(BoardLayout layout, int fromProgress, int toProgress)
        {
            if (fromProgress < 0)
            {
                yield return Hop(transform.position, layout.World(BoardPos, 0, TokenId), 0.18f);
                yield break;
            }
            for (int p = fromProgress + 1; p <= toProgress; p++)
                yield return Hop(transform.position, layout.World(BoardPos, p, TokenId), 0.12f);
        }

        /// <summary>Fly back to base when captured (the "kick-off" knockback — refined later with the stickman).</summary>
        public IEnumerator ToBaseRoutine(BoardLayout layout)
        {
            Vector3 a = transform.position, b = layout.BaseSlot(BoardPos, TokenId);
            float t = 0f, dur = 0.45f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                Vector3 p = Vector3.Lerp(a, b, k);
                p.y += Mathf.Sin(k * Mathf.PI) * 1.2f;
                transform.position = p;
                transform.localScale = Vector3.one * _baseScale * (1f + 0.4f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            transform.position = b;
            transform.localScale = Vector3.one * _baseScale;
        }

        private IEnumerator Hop(Vector3 a, Vector3 b, float dur)
        {
            float t = 0f, arc = _baseScale * 0.8f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                Vector3 p = Vector3.Lerp(a, b, k);
                p.y += Mathf.Sin(k * Mathf.PI) * arc;
                transform.position = p;
                yield return null;
            }
            transform.position = b;
        }
    }
}
