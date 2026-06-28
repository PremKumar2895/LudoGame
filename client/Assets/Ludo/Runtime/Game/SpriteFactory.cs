using UnityEngine;

namespace LudoGame
{
    /// <summary>Generates simple 1-unit sprites at runtime so we need no imported art for the slice.</summary>
    public static class SpriteFactory
    {
        private static Sprite _square;
        private static Sprite _circle;

        public static Sprite Square()
        {
            if (_square == null)
            {
                var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                var px = new Color[64];
                for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                tex.SetPixels(px); tex.Apply();
                _square = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f); // 1 world unit
            }
            return _square;
        }

        public static Sprite Circle(int size = 64)
        {
            if (_circle == null)
            {
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                var px = new Color[size * size];
                float c = size / 2f, r = size / 2f - 1f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - c, dy = y + 0.5f - c;
                        float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.75f);
                        px[y * size + x] = new Color(1, 1, 1, a);
                    }
                tex.SetPixels(px); tex.Apply();
                _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size); // 1 world unit
            }
            return _circle;
        }
    }
}
