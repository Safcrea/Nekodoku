using UnityEngine;

namespace Meowdoku
{
    public sealed partial class NekoGameController
    {
        private Sprite LoadTutorialHandSprite()
        {
            if (tutorialHandTexture == null)
            {
                return null;
            }

            float pixelsPerUnit = Mathf.Max(tutorialHandTexture.width, tutorialHandTexture.height);
            return Sprite.Create(tutorialHandTexture, new Rect(0f, 0f, tutorialHandTexture.width, tutorialHandTexture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private Sprite CreateFocusRingSprite()
        {
            const int textureSize = 128;
            const float center = (textureSize - 1) * 0.5f;
            Color32[] pixels = new Color32[textureSize * textureSize];
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 dark = FocusRingColor;
            Color32 glow = new Color(0.08f, 0.35f, 0.33f, 0.22f);

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    Color32 color = clear;
                    if (distance >= 48f && distance <= 56f)
                    {
                        color = dark;
                    }
                    else if (distance >= 39f && distance <= 61f)
                    {
                        color = glow;
                    }

                    pixels[(y * textureSize) + x] = color;
                }
            }

            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        }

        private Sprite CreateJuiceDotSprite()
        {
            const int textureSize = 48;
            const float center = (textureSize - 1) * 0.5f;
            Color32[] pixels = new Color32[textureSize * textureSize];
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 solid = new Color32(255, 255, 255, 255);

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    float alpha = Mathf.Clamp01(1f - ((distance - 16f) / 7f));
                    Color32 color = alpha <= 0f ? clear : new Color32(solid.r, solid.g, solid.b, (byte)Mathf.RoundToInt(alpha * 255f));
                    pixels[(y * textureSize) + x] = color;
                }
            }

            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        }

        private Sprite CreateNekoCatSprite()
        {
            const int textureSize = 128;
            Color32[] pixels = new Color32[textureSize * textureSize];
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 face = new Color32(255, 236, 205, 255);
            Color32 ear = new Color32(255, 151, 173, 255);
            Color32 ink = new Color32(45, 34, 31, 255);
            Color32 blush = new Color32(255, 118, 139, 180);
            Color32 shine = new Color32(255, 255, 255, 240);

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            FillTriangle(22, 70, 47, 118, 65, 74, face);
            FillTriangle(63, 74, 82, 118, 106, 70, face);
            FillTriangle(36, 78, 49, 104, 58, 78, ear);
            FillTriangle(72, 78, 82, 104, 95, 78, ear);
            FillCircle(64, 58, 43, face);
            FillCircle(48, 63, 5, ink);
            FillCircle(80, 63, 5, ink);
            FillCircle(50, 65, 2, shine);
            FillCircle(82, 65, 2, shine);
            FillCircle(64, 54, 3, ear);
            FillCircle(40, 50, 6, blush);
            FillCircle(88, 50, 6, blush);
            DrawLine(64, 52, 64, 47, ink);
            DrawLine(64, 47, 58, 43, ink);
            DrawLine(64, 47, 70, 43, ink);
            DrawLine(21, 61, 45, 58, ink);
            DrawLine(21, 51, 45, 51, ink);
            DrawLine(24, 41, 47, 47, ink);
            DrawLine(83, 58, 107, 61, ink);
            DrawLine(83, 51, 107, 51, ink);
            DrawLine(81, 47, 104, 41, ink);

            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);

            void SetPixel(int x, int y, Color32 color)
            {
                if (x < 0 || x >= textureSize || y < 0 || y >= textureSize)
                {
                    return;
                }

                pixels[(y * textureSize) + x] = color;
            }

            void FillCircle(int centerX, int centerY, int radius, Color32 color)
            {
                int radiusSquared = radius * radius;
                for (int y = centerY - radius; y <= centerY + radius; y++)
                {
                    for (int x = centerX - radius; x <= centerX + radius; x++)
                    {
                        int dx = x - centerX;
                        int dy = y - centerY;
                        if ((dx * dx) + (dy * dy) <= radiusSquared)
                        {
                            SetPixel(x, y, color);
                        }
                    }
                }
            }

            void FillTriangle(int ax, int ay, int bx, int by, int cx, int cy, Color32 color)
            {
                int minX = Mathf.Min(ax, Mathf.Min(bx, cx));
                int maxX = Mathf.Max(ax, Mathf.Max(bx, cx));
                int minY = Mathf.Min(ay, Mathf.Min(by, cy));
                int maxY = Mathf.Max(ay, Mathf.Max(by, cy));

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float w0 = Edge(bx, by, cx, cy, x, y);
                        float w1 = Edge(cx, cy, ax, ay, x, y);
                        float w2 = Edge(ax, ay, bx, by, x, y);
                        if ((w0 >= 0f && w1 >= 0f && w2 >= 0f) || (w0 <= 0f && w1 <= 0f && w2 <= 0f))
                        {
                            SetPixel(x, y, color);
                        }
                    }
                }
            }

            float Edge(int ax, int ay, int bx, int by, int px, int py)
            {
                return ((px - ax) * (by - ay)) - ((py - ay) * (bx - ax));
            }

            void DrawLine(int x0, int y0, int x1, int y1, Color32 color)
            {
                int dx = Mathf.Abs(x1 - x0);
                int dy = -Mathf.Abs(y1 - y0);
                int sx = x0 < x1 ? 1 : -1;
                int sy = y0 < y1 ? 1 : -1;
                int error = dx + dy;

                while (true)
                {
                    FillCircle(x0, y0, 1, color);
                    if (x0 == x1 && y0 == y1)
                    {
                        break;
                    }

                    int error2 = 2 * error;
                    if (error2 >= dy)
                    {
                        error += dy;
                        x0 += sx;
                    }

                    if (error2 <= dx)
                    {
                        error += dx;
                        y0 += sy;
                    }
                }
            }
        }
    }
}
