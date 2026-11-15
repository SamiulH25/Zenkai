using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace Zenkai.Content.Dusts;

/// <summary>
/// Like <see cref="KiSpark"/> but drawn from a pale grey sprite tinted by the dust's colour, so
/// forms can use their own aura colours (pass the colour to Dust.NewDustPerfect).
/// </summary>
public class TintSpark : ModDust
{
	const int FrameSize = 4;
	const int TicksPerFrame = 7;
	const int Frames = 3;

	public override string Texture => "Zenkai/Assets/Dusts/TintSpark";

	public override void OnSpawn(Dust dust)
	{
		dust.frame = new Rectangle(0, 0, FrameSize, FrameSize);
		dust.noGravity = true;
		dust.noLight = true;
		dust.scale = 1f;
		dust.fadeIn = 0f;
		if (dust.color == default)
			dust.color = Color.White;
	}

	public override bool Update(Dust dust)
	{
		dust.position += dust.velocity;
		dust.velocity *= 0.95f;
		dust.fadeIn++;
		int frame = (int)dust.fadeIn / TicksPerFrame;
		if (frame >= Frames) {
			dust.active = false;
			return false;
		}
		dust.frame.Y = frame * FrameSize;
		return false;
	}

	public override bool PreDraw(Dust dust)
	{
		Vector2 pos = (dust.position - Main.screenPosition).Floor();
		Main.spriteBatch.Draw(Texture2D.Value, pos, dust.frame, dust.color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
		return false;
	}
}
