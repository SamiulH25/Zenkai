using Microsoft.Xna.Framework;

namespace Zenkai.Common.Visuals;

/// <summary>
/// Everything that decides how an aura looks. <see cref="AuraRenderer"/> draws any style
/// procedurally, so a transformation gets its own aura by defining one of these
/// (palette, size, how many tongues and how wild they are) instead of drawing a sheet.
/// All lengths are in art pixels; the aura is shown at 2x like every other sprite.
/// </summary>
public sealed class AuraStyle
{
	// Palette, outermost band first. Alpha is the band's opacity. Drawn with plain alpha
	// blending, so Edge works as a dark outline that holds over any background.
	public Color Edge;
	public Color Rim;
	public Color Hot;
	public Color Mid;
	public Color Inner;

	// Band widths measured inward from the edge, in art pixels: Edge is [0, EdgeWidth), and so on.
	public float EdgeWidth = 1f;
	public float RimWidth = 1f;
	public float HotWidth = 1f;
	public float MidWidth = 2f;

	/// <summary>Envelope: a capsule round the body's vertical axis. Radius must clear the hair (the
	/// player sprite is about 18 art px wide there) by a few pixels.</summary>
	public float Radius = 12f;
	public float Segment = 7f;
	/// <summary>Amplitude of the ripples that run along the envelope's edge.</summary>
	public float EdgeWave = 1.3f;

	public int TongueCount = 13;
	public float TongueLengthMin = 6f;
	public float TongueLengthMax = 22f;
	public float TongueWidthMin = 3.5f;
	public float TongueWidthMax = 6f;
	/// <summary>Tongue lifetime range in ticks at intensity 1 (calmer states live longer).</summary>
	public int TongueLifeMin = 14;
	public int TongueLifeMax = 28;
	/// <summary>Degrees either side of the flame axis where tongues can grow. Past 90 they also
	/// grow off the lower sides, so the whole envelope reads as flame.</summary>
	public float TongueSpread = 140f;
	/// <summary>How much shorter tongues at the edge of the spread are than those on the axis
	/// (0 = same length, 1 = none). Falls off with the square of the angle.</summary>
	public float SideShortening = 0.15f;
	/// <summary>Extra length for the tongue on the flame axis (0.5 = 50% longer).</summary>
	public float CrestBoost = 0.5f;
	/// <summary>How far tongues curl sideways toward their tips.</summary>
	public float TongueCurl = 3.5f;
	/// <summary>Extra tongue length at full flight speed (0.7 = 70% longer).</summary>
	public float SpeedStretch = 0.7f;
	/// <summary>Multiplies how fast ripples run and tongues live and die.</summary>
	public float Flicker = 1f;

	/// <summary>The base blue ki aura.</summary>
	public static readonly AuraStyle Ki = new() {
		Edge = new Color(0x14, 0x30, 0x7a, 235),
		Rim = new Color(0x8a, 0xe8, 0xff, 250),
		Hot = new Color(0x3c, 0xbc, 0xff, 215),
		Mid = new Color(0x24, 0x78, 0xe0, 175),
		Inner = new Color(0x1c, 0x52, 0xc4, 110),
	};

	/// <summary>Kaioken: red, short fast tongues, more of them.</summary>
	public static readonly AuraStyle Kaioken = new() {
		Edge = new Color(0x5a, 0x0a, 0x14, 235),
		Rim = new Color(0xff, 0xb4, 0xa0, 250),
		Hot = new Color(0xff, 0x5a, 0x3c, 215),
		Mid = new Color(0xe0, 0x24, 0x2c, 175),
		Inner = new Color(0xb0, 0x18, 0x28, 110),
		TongueCount = 16,
		TongueLengthMin = 5f,
		TongueLengthMax = 16f,
		TongueLifeMin = 9,
		TongueLifeMax = 18,
		Flicker = 1.6f,
		EdgeWave = 1.6f,
	};

	/// <summary>Stronger Kaioken ranks: the same red, larger and wilder with each rank (1 to 3).</summary>
	public static AuraStyle KaiokenRank(int rank) => new() {
		Edge = new Color(0x5a, 0x0a, 0x14, 235),
		Rim = new Color(0xff, 0xb4, 0xa0, 250),
		Hot = new Color(0xff, 0x5a, 0x3c, 215),
		Mid = new Color(0xe0, 0x24, 0x2c, 175),
		Inner = new Color(0xb0, 0x18, 0x28, 110),
		Radius = 12f + 0.5f * rank,
		TongueCount = 16,
		TongueLengthMin = 5f + rank,
		TongueLengthMax = 16f + 3f * rank,
		TongueLifeMin = 9,
		TongueLifeMax = 18,
		Flicker = 1.6f + 0.2f * rank,
		EdgeWave = 1.6f + 0.2f * rank,
	};

	/// <summary>Super Saiyan: gold, taller tongues, more of them, a strong crest.</summary>
	public static readonly AuraStyle SuperSaiyan = Gold(0);

	/// <summary>The gold aura of the Super Saiyan line, growing with each stage (0 to 2).</summary>
	public static AuraStyle Gold(int stage) => new() {
		Edge = new Color(0x6a, 0x42, 0x08, 235),
		Rim = new Color(0xff, 0xf6, 0xb0, 250),
		Hot = new Color(0xff, 0xe0, 0x60, 215),
		Mid = new Color(0xf0, 0xb0, 0x20, 175),
		Inner = new Color(0xd0, 0x8a, 0x10, 110),
		Radius = 12.5f + 0.5f * stage,
		TongueCount = 17,
		TongueLengthMin = 8f + stage,
		TongueLengthMax = 24f + 2f * stage,
		TongueWidthMax = 6.5f,
		CrestBoost = 0.8f,
		TongueCurl = 3f,
		Flicker = 1f + 0.35f * stage,
		EdgeWave = 1.3f + 0.3f * stage,
	};

	/// <summary>Unleashed potential: a calm white aura with a pale blue edge.</summary>
	public static readonly AuraStyle Potential = new() {
		Edge = new Color(0x3c, 0x4a, 0x78, 225),
		Rim = new Color(0xff, 0xff, 0xff, 250),
		Hot = new Color(0xe6, 0xf0, 0xff, 215),
		Mid = new Color(0xb4, 0xc8, 0xf0, 165),
		Inner = new Color(0x8c, 0xa0, 0xd8, 100),
		TongueCount = 12,
		TongueLengthMin = 6f,
		TongueLengthMax = 18f,
		Flicker = 0.75f,
		EdgeWave = 1f,
	};

	/// <summary>Godly ki: a tall, slow, crimson flame.</summary>
	public static readonly AuraStyle God = new() {
		Edge = new Color(0x58, 0x08, 0x2a, 235),
		Rim = new Color(0xff, 0xc8, 0xd2, 250),
		Hot = new Color(0xff, 0x52, 0x7a, 215),
		Mid = new Color(0xe0, 0x20, 0x54, 175),
		Inner = new Color(0xa8, 0x14, 0x48, 110),
		Radius = 12.5f,
		TongueCount = 13,
		TongueLengthMin = 10f,
		TongueLengthMax = 26f,
		TongueWidthMin = 4f,
		TongueWidthMax = 7f,
		TongueLifeMin = 22,
		TongueLifeMax = 40,
		Flicker = 0.7f,
		CrestBoost = 0.9f,
	};

	/// <summary>Godly ki held under perfect control: deep blue with a white-cyan rim, dense and steady.</summary>
	public static readonly AuraStyle Blue = new() {
		Edge = new Color(0x08, 0x1e, 0x6e, 240),
		Rim = new Color(0xd8, 0xff, 0xff, 250),
		Hot = new Color(0x40, 0xd8, 0xff, 220),
		Mid = new Color(0x10, 0x8c, 0xf0, 185),
		Inner = new Color(0x0c, 0x50, 0xd0, 125),
		Radius = 13f,
		TongueCount = 17,
		TongueLengthMin = 9f,
		TongueLengthMax = 25f,
		TongueWidthMax = 6.5f,
		CrestBoost = 0.8f,
		Flicker = 1.1f,
	};

	/// <summary>Instinct: a thin silver shimmer edged in violet, quick and restless.</summary>
	public static readonly AuraStyle Instinct = new() {
		Edge = new Color(0x38, 0x30, 0x78, 225),
		Rim = new Color(0xff, 0xff, 0xff, 250),
		Hot = new Color(0xd8, 0xdc, 0xf4, 210),
		Mid = new Color(0xa0, 0xa4, 0xe0, 150),
		Inner = new Color(0x70, 0x6c, 0xc8, 90),
		Radius = 12f,
		TongueCount = 15,
		TongueLengthMin = 7f,
		TongueLengthMax = 22f,
		TongueWidthMin = 3f,
		TongueWidthMax = 5f,
		TongueLifeMin = 10,
		TongueLifeMax = 20,
		Flicker = 1.5f,
		TongueCurl = 4.5f,
	};
}
