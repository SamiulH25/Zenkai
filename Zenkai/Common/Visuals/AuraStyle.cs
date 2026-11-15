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

	/// <summary>Super Saiyan: gold, taller tongues, more of them, a strong crest.</summary>
	public static readonly AuraStyle SuperSaiyan = new() {
		Edge = new Color(0x6a, 0x42, 0x08, 235),
		Rim = new Color(0xff, 0xf6, 0xb0, 250),
		Hot = new Color(0xff, 0xe0, 0x60, 215),
		Mid = new Color(0xf0, 0xb0, 0x20, 175),
		Inner = new Color(0xd0, 0x8a, 0x10, 110),
		Radius = 12.5f,
		TongueCount = 17,
		TongueLengthMin = 8f,
		TongueLengthMax = 24f,
		TongueWidthMax = 6.5f,
		CrestBoost = 0.8f,
		TongueCurl = 3f,
	};
}
