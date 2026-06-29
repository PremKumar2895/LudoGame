using System.Collections;
using UnityEngine;

namespace LudoGame
{
    /// <summary>
    /// One token, drawn as a small articulated STICKMAN (coloured head + dark torso/limbs). It walks
    /// (legs swing) while moving cell-to-cell, kicks off when leaving base, and tumbles home when captured.
    /// </summary>
    public sealed class TokenView : MonoBehaviour
    {
        public int BoardPos;
        public int TokenId;

        private float _baseScale = 0.3f;
        private bool _highlight;
        private Transform _figure, _legL, _legR, _armL, _armR;

        public void Init(int boardPos, int tokenId, Color color, float size)
        {
            BoardPos = boardPos; TokenId = tokenId; _baseScale = size;
            var dark = BoardColors.Outline;

            _figure = new GameObject("figure").transform;
            _figure.SetParent(transform, false);

            _legL = Limb(_figure, dark, 0.15f, 0.42f, new Vector2(-0.065f, -0.06f), 9);
            _legR = Limb(_figure, dark, 0.15f, 0.42f, new Vector2( 0.065f, -0.06f), 9);
            _armL = Limb(_figure, dark, 0.13f, 0.34f, new Vector2(-0.13f,  0.30f), 9);
            _armR = Limb(_figure, dark, 0.13f, 0.34f, new Vector2( 0.13f,  0.30f), 9);
            Bar(_figure, dark, 0.18f, 0.46f, new Vector2(0f, 0.15f), 10);   // torso
            Disc(_figure, color, dark, 0.50f, new Vector2(0f, 0.52f), 12);  // head

            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.55f; col.offset = new Vector2(0f, 0.18f);

            transform.localScale = Vector3.one * _baseScale;
            SetPose(0f);
        }

        // A limb whose PIVOT (returned transform) sits at the joint; the bar hangs below it → rotate to swing.
        private static Transform Limb(Transform parent, Color c, float w, float len, Vector2 joint, int order)
        {
            var pivot = new GameObject("joint").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = joint;
            var bar = new GameObject("bar").transform;
            bar.SetParent(pivot, false);
            bar.localPosition = new Vector3(0f, -len / 2f, 0f);
            bar.localScale = new Vector3(w, len, 1f);
            var sr = bar.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Square(); sr.color = c; sr.sortingOrder = order;
            return pivot;
        }

        private static void Bar(Transform parent, Color c, float w, float h, Vector2 pos, int order)
        {
            var go = new GameObject("bar"); go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = SpriteFactory.Square(); sr.color = c; sr.sortingOrder = order;
        }

        private static void Disc(Transform parent, Color fill, Color outline, float d, Vector2 pos, int order)
        {
            var o = new GameObject("headOutline"); o.transform.SetParent(parent, false);
            o.transform.localPosition = pos; o.transform.localScale = Vector3.one * (d * 1.28f);
            var os = o.AddComponent<SpriteRenderer>(); os.sprite = SpriteFactory.Circle(); os.color = outline; os.sortingOrder = order - 1;
            var go = new GameObject("head"); go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = Vector3.one * d;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = SpriteFactory.Circle(); sr.color = fill; sr.sortingOrder = order;
        }

        /// <summary>swing −1..1 → legs/arms counter-swing for a walk pose.</summary>
        private void SetPose(float swing)
        {
            float a = swing * 34f;
            if (_legL) _legL.localRotation = Quaternion.Euler(0, 0,  a);              // legs alternate = a step
            if (_legR) _legR.localRotation = Quaternion.Euler(0, 0, -a);
            if (_armL) _armL.localRotation = Quaternion.Euler(0, 0,  26f - a * 0.7f); // arms spread out + counter-swing
            if (_armR) _armR.localRotation = Quaternion.Euler(0, 0, -26f + a * 0.7f);
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
                transform.localScale = Vector3.one * _baseScale * (1f + 0.12f * Mathf.PingPong(Time.time * 3f, 1f));
        }

        /// <summary>Walk cell-by-cell from one progress to another; a kick-off jump when leaving base.</summary>
        public IEnumerator MoveRoutine(BoardLayout layout, int fromProgress, int toProgress)
        {
            if (fromProgress < 0) { yield return KickOff(layout); yield break; }
            for (int p = fromProgress + 1; p <= toProgress; p++)
                yield return Hop(transform.position, layout.World(BoardPos, p, TokenId), 0.14f);
            SetPose(0f);
        }

        private IEnumerator KickOff(BoardLayout layout)
        {
            Vector3 a = transform.position, b = layout.World(BoardPos, 0, TokenId);
            float t = 0f, dur = 0.34f;
            while (t < dur)
            {
                t += Time.deltaTime; float k = Mathf.Clamp01(t / dur);
                Vector3 p = Vector3.Lerp(a, b, k); p.y += Mathf.Sin(k * Mathf.PI) * _baseScale * 1.7f;
                transform.position = p;
                SetPose(Mathf.Sin(k * Mathf.PI)); // big leg kick at the apex
                yield return null;
            }
            transform.position = b; SetPose(0f);
        }

        /// <summary>Captured: a spinning tumble back to base (the "kick-off" knockback).</summary>
        public IEnumerator ToBaseRoutine(BoardLayout layout)
        {
            Vector3 a = transform.position, b = layout.BaseSlot(BoardPos, TokenId);
            float t = 0f, dur = 0.5f;
            while (t < dur)
            {
                t += Time.deltaTime; float k = Mathf.Clamp01(t / dur);
                Vector3 p = Vector3.Lerp(a, b, k); p.y += Mathf.Sin(k * Mathf.PI) * 1.3f;
                transform.position = p;
                if (_figure) _figure.localRotation = Quaternion.Euler(0, 0, -540f * k);
                transform.localScale = Vector3.one * _baseScale * (1f + 0.35f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            transform.position = b;
            if (_figure) _figure.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * _baseScale; SetPose(0f);
        }

        private IEnumerator Hop(Vector3 a, Vector3 b, float dur)
        {
            float t = 0f, arc = _baseScale * 0.6f;
            while (t < dur)
            {
                t += Time.deltaTime; float k = Mathf.Clamp01(t / dur);
                Vector3 p = Vector3.Lerp(a, b, k); p.y += Mathf.Sin(k * Mathf.PI) * arc;
                transform.position = p;
                SetPose(Mathf.Sin(k * Mathf.PI * 2f)); // one full leg swing per hop = a step
                yield return null;
            }
            transform.position = b;
        }
    }
}
