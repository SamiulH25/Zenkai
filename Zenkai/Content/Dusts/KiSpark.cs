using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace Zenkai.Content.Dusts;

/// <summary>
/// A 2x2 art-pixel spark that rises off the charge aura. It steps through three frames,
/// bright to dim, instead of fading, and draws on whole pixels so it stays crisp.
/// </summary>
public class KiSpark : ModDust
{
	const int FrameSize = 4; // 2x2 art pixels at 2x
	const int TicksPerFrame = 8;
	const int Frames = 3;

	public override string Texture => "Zenkai/Assets/Dusts/KiSpark";

	public override void OnSpawn(Dust dust)
	{
		dust.frame = new Rectangle(0, 0, FrameSize, FrameSize);
		dust.noGravity = true;
		dust.noLight = true;
		dust.scale = 1f;
		dust.alpha = 0;
		dust.fadeIn = 0f; // used as the age in ticks
	}

	public override bool Update(Dust dust)
	{
		dust.position += dust.velocity;
		dust.velocity.X *= 0.9f;
		dust.velocity.Y *= 0.96f;
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
		Main.spriteBatch.Draw(Texture2D.Value, pos, dust.frame, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
		return false;
	}
}
