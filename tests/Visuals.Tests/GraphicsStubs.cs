// CPU-only stand-ins for texture upload. The production renderer/style are linked unchanged.
namespace Microsoft.Xna.Framework
{
    public struct Color
    {
        public byte R, G, B, A;
        public Color(int r, int g, int b, int a = 255) { R = (byte)r; G = (byte)g; B = (byte)b; A = (byte)a; }
        public static Color Transparent => default;
    }
    public static class MathHelper
    {
        public const float Pi = (float)System.Math.PI;
        public const float TwoPi = Pi * 2f;
        public static float ToRadians(float degrees) => degrees * Pi / 180f;
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Clamp(float value, float min, float max) => System.Math.Clamp(value, min, max);
    }
}
namespace Microsoft.Xna.Framework.Graphics
{
    public sealed class Texture2D : System.IDisposable
    {
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed { get; private set; }
        public Texture2D(object device, int width, int height) { Width = width; Height = height; }
        public void SetData(Microsoft.Xna.Framework.Color[] pixels)
        {
            if (IsDisposed || pixels.Length != Width * Height)
                throw new System.InvalidOperationException("Invalid texture upload");
        }
        public void Dispose() => IsDisposed = true;
    }
}
namespace Terraria
{
    public sealed class Main
    {
        public static uint GameUpdateCount;
        public static readonly Main instance = new();
        public object GraphicsDevice => null;
    }
}
