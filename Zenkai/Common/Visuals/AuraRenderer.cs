using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace Zenkai.Common.Visuals;

/// <summary>What the aura is doing this tick, from the owner's gameplay state.</summary>
/// <param name="Intensity">0 to 1: hovering is calm, charging is full.</param>
/// <param name="TailDown">Tongues stream toward the feet (flying head first) instead of rising.</param>
/// <param name="Speed">Movement speed in px/tick; stretches the tongues.</param>
public readonly record struct AuraState(float Intensity, bool TailDown, float Speed);

/// <summary>
/// Generates one player's aura as pixel art every game tick: a flame-edged envelope round the
/// body plus flame tongues that are born, grow, curl and die on their own, so the aura never
/// repeats a loop. The art is computed at 1x in body space (upright, centred on the player),
/// expanded to 2x on the CPU so it stays crisp under any sampler, and uploaded to a texture.
/// Vanilla's player draw transform then rotates it with the body.
/// Visual state only; nothing here affects gameplay.
/// </summary>
public sealed class AuraRenderer : IDisposable
{
	/// <summary>Canvas size in art pixels. The texture is twice this.</summary>
	public const int Size = 64;
	const int Scale = 2;
	const int TexSize = Size * Scale;
	const int MaxTongues = 16;
	const float FullSpeed = 7f; // flight speed that gives the full SpeedStretch

	struct Tongue
	{
		public float Angle;     // radians from the flame axis
		public float Life;
		public float MaxLife;
		public float MaxLength;
		public float HalfWidth;
		public float Curl;
	}

	readonly Tongue[] tongues = new Tongue[MaxTongues];
	readonly float[] depth = new float[Size * Size];
	readonly Color[] pixels = new Color[TexSize * TexSize];
	readonly bool[] keep = new bool[Size * Size];
	readonly Random random;
	// Per tick, per tongue: base point, direction, length, half width, curl.
	readonly float[] tbx = new float[MaxTongues], tby = new float[MaxTongues], tdx = new float[MaxTongues],
		tdy = new float[MaxTongues], tlen = new float[MaxTongues], thw = new float[MaxTongues], tcurl = new float[MaxTongues];

	Texture2D texture;
	uint lastTick = uint.MaxValue;
	float time;
	bool seeded;

	public AuraRenderer(int seed) => random = new Random(seed);

	public Texture2D Texture => texture;

	float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

	/// <summary>Advances and redraws the aura once per game tick. Call from drawing code (main thread).</summary>
	public void Update(AuraStyle style, AuraState state)
	{
		if (Main.GameUpdateCount == lastTick && texture != null)
			return;
		lastTick = Main.GameUpdateCount;
		texture ??= new Texture2D(Main.instance.GraphicsDevice, TexSize, TexSize);

		float pace = style.Flicker * (0.6f + 0.6f * state.Intensity);
		time += pace;
		Simulate(style, state, pace);
		Render(style, state);
		texture.SetData(pixels);
	}

	void Simulate(AuraStyle style, AuraState state, float pace)
	{
		int count = Math.Min(style.TongueCount, MaxTongues);
		for (int i = 0; i < count; i++) {
			ref Tongue t = ref tongues[i];
			if (!seeded || t.MaxLife <= 0f) {
				Spawn(ref t, i, count, style, state);
				t.Life = Range(0f, t.MaxLife); // stagger the first generation
				continue;
			}
			t.Life += pace;
			if (t.Life >= t.MaxLife)
				Spawn(ref t, i, count, style, state);
		}
		seeded = true;
	}

	// Each tongue owns a slot across the spread and respawns at a jittered angle inside it,
	// so the sides always carry flame while the exact layout never repeats.
	void Spawn(ref Tongue t, int slot, int count, AuraStyle style, AuraState state)
	{
		float spread = MathHelper.ToRadians(style.TongueSpread);
		float width = 2f * spread / count;
		t.Angle = -spread + width * (slot + Range(0.1f, 0.9f));
		float side = Math.Abs(t.Angle) / spread;            // 0 on the axis, 1 at the edge of the spread
		float sideFactor = 1f - style.SideShortening * side * side;
		// The tongue nearest the axis is the tallest, so the top reads as one flame, not a crown.
		float crest = 1f + style.CrestBoost * (float)Math.Pow(1f - side, 6);
		t.MaxLength = Range(style.TongueLengthMin, style.TongueLengthMax) * (0.7f + 0.4f * state.Intensity) * sideFactor * crest;
		// Low tongues are slimmer licks.
		t.HalfWidth = Range(style.TongueWidthMin, style.TongueWidthMax) * (1f - 0.2f * side);
		// Low side tongues are short, so they flicker faster to show as much movement as the top.
		t.MaxLife = Range(style.TongueLifeMin, style.TongueLifeMax) * (1f - 0.4f * side);
		// Side tongues bend their tips back toward the flame axis, like flames licking upward;
		// tongues near the axis just wander a little either way.
		t.Curl = -Math.Sign(t.Angle) * side * style.TongueCurl + Range(-0.4f, 0.4f) * style.TongueCurl;
		t.Life = 0f;
	}

	void Render(AuraStyle style, AuraState state)
	{
		const float Tau = MathHelper.TwoPi;
		float axY = state.TailDown ? 1f : -1f; // flame axis: up, or toward the feet
		float r = style.Radius;
		float seg = style.Segment;
		float reach = seg + r; // the envelope's extent along the axis
		float stretch = state.TailDown ? 1f + Math.Min(state.Speed / FullSpeed, 1f) * style.SpeedStretch : 1f;
		float waveAmp = style.EdgeWave * (0.7f + 0.5f * state.Intensity);
		int count = Math.Min(style.TongueCount, MaxTongues);

		// Tongue geometry for this tick.
		for (int i = 0; i < count; i++) {
			ref Tongue t = ref tongues[i];
			float sin = (float)Math.Sin(t.Angle), cos = (float)Math.Cos(t.Angle);
			// Outward normal: the axis (0, axY) turned by Angle.
			float nx = -axY * sin, ny = axY * cos;
			// Base just inside the capsule edge in that direction.
			tbx[i] = nx * (r - 2f);
			tby[i] = ny * (r - 2f) + ny * seg;
			// Tongues lean from the normal toward the axis, like flames rising.
			// Low on the sides they point further outward so they break the outline.
			float rise = MathHelper.Lerp(0.6f, 0.4f, Math.Abs(t.Angle) / MathHelper.Pi);
			float dx = nx * (1f - rise), dy = ny * (1f - rise) + axY * rise;
			float len = (float)Math.Sqrt(dx * dx + dy * dy);
			tdx[i] = dx / len;
			tdy[i] = dy / len;
			float grow = (float)Math.Sin(Math.PI * t.Life / t.MaxLife); // grows, then dies back
			tlen[i] = t.MaxLength * grow * stretch;
			thw[i] = t.HalfWidth * (0.6f + 0.4f * grow);
			tcurl[i] = t.Curl;
		}

		for (int y = 0; y < Size; y++) {
			float py = y + 0.5f - Size / 2f;
			for (int x = 0; x < Size; x++) {
				float px = x + 0.5f - Size / 2f;
				float s = py * axY;   // distance along the flame axis
				float l = px;          // across it

				// Envelope with ripples running toward the flame axis, stronger near it.
				float sy = MathHelper.Clamp(py, -seg, seg);
				float cap = (float)Math.Sqrt(px * px + (py - sy) * (py - sy));
				float toward = MathHelper.Clamp(s / reach, -1f, 1f);
				float wave = waveAmp * (0.6f * (float)Math.Sin(Tau * s / 10f - time * 0.35f + l * 0.15f)
				                      + 0.4f * (float)Math.Sin(Tau * s / 6f - time * 0.6f + l * 0.4f));
				float d = r + 0.5f - Math.Max(0f, toward) + wave * (0.5f + 0.5f * (toward + 1f) / 2f) - cap;

				for (int i = 0; i < count; i++) {
					if (tlen[i] < 1f)
						continue;
					float rx = px - tbx[i], ry = py - tby[i];
					float ts = rx * tdx[i] + ry * tdy[i];
					if (ts < -2f || ts > tlen[i])
						continue;
					float tl = rx * -tdy[i] + ry * tdx[i];
					float k = (ts + 2f) / (tlen[i] + 2f);
					// Full body that narrows late into a bent tip: flame, not a shard.
					float hw = thw[i] * (float)Math.Pow(1f - k, 0.6f);
					// Never thinner than 2 art px until the very tip, so no tongue thins to a 1 px stick.
					if (k < 0.85f)
						hw = Math.Max(hw, 1.1f);
					float bend = tcurl[i] * k * k * (1f + tlen[i] * 0.08f);
					d = Math.Max(d, hw - Math.Abs(tl - bend));
				}
				depth[y * Size + x] = d;
			}
		}

		// Drop specks: pixels with fewer than two filled neighbours (thin tongue tips break up).
		for (int y = 0; y < Size; y++) {
			for (int x = 0; x < Size; x++) {
				int i = y * Size + x;
				if (depth[i] < 0f) {
					keep[i] = false;
					continue;
				}
				int n = 0;
				if (x > 0 && depth[i - 1] >= 0f) n++;
				if (x < Size - 1 && depth[i + 1] >= 0f) n++;
				if (y > 0 && depth[i - Size] >= 0f) n++;
				if (y < Size - 1 && depth[i + Size] >= 0f) n++;
				keep[i] = n >= 2;
			}
		}

		Color edge = Premultiply(style.Edge), rim = Premultiply(style.Rim), hot = Premultiply(style.Hot),
			mid = Premultiply(style.Mid), inner = Premultiply(style.Inner);
		float b1 = style.EdgeWidth, b2 = b1 + style.RimWidth, b3 = b2 + style.HotWidth, b4 = b3 + style.MidWidth;

		for (int y = 0; y < Size; y++) {
			for (int x = 0; x < Size; x++) {
				int i = y * Size + x;
				Color c = Color.Transparent;
				if (keep[i]) {
					float d = depth[i];
					c = d < b1 ? edge : d < b2 ? rim : d < b3 ? hot : d < b4 ? mid : inner;
				}
				int o = y * Scale * TexSize + x * Scale;
				pixels[o] = c;
				pixels[o + 1] = c;
				pixels[o + TexSize] = c;
				pixels[o + TexSize + 1] = c;
			}
		}
	}

	// Textures in this game hold premultiplied colour.
	static Color Premultiply(Color c) => new(c.R * c.A / 255, c.G * c.A / 255, c.B * c.A / 255, c.A);

	public void Dispose()
	{
		texture?.Dispose();
		texture = null;
	}
}
