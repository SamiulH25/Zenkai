using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Zenkai.Common.Forms;

namespace Zenkai.Common.Players;

/// <summary>
/// Draws a form's hair over the head in place of the player's own (which <see cref="FormPlayer"/>
/// makes transparent). The sprite is one image, 20 art px wide like a vanilla hair frame and
/// <see cref="Pad"/> art rows taller so spikes can rise above the frame. It sits at vanilla's head
/// anchor and bobs with Main.OffsetsPlayerHeadgear, the offsets vanilla uses for one-frame headgear.
/// Hidden under helmets that hide hair, as vanilla hair is.
/// </summary>
public class FormHairLayer : PlayerDrawLayer
{
	/// <summary>Extra rows above the 56 px frame, in screen px (6 art px at 2x).</summary>
	const int Pad = 12;

	public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Head);

	/// <summary>True when vanilla would draw the player's hair (no helmet, or one that shows full hair).</summary>
	public static bool DrawsHair(ref PlayerDrawSet drawInfo) =>
		!drawInfo.drawPlayer.invis && (drawInfo.drawPlayer.head == -1 || drawInfo.fullHair);

	public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
	{
		ModForm look = drawInfo.drawPlayer.GetModPlayer<FormPlayer>().Visible;
		return look?.HairPath != null && DrawsHair(ref drawInfo);
	}

	protected override void Draw(ref PlayerDrawSet drawInfo)
	{
		Player p = drawInfo.drawPlayer;
		ModForm look = p.GetModPlayer<FormPlayer>().Visible;
		Texture2D tex = ModContent.Request<Texture2D>(look.HairPath).Value;

		Vector2 pos = new Vector2(
			(int)(drawInfo.Position.X - Main.screenPosition.X - p.bodyFrame.Width / 2 + p.width / 2),
			(int)(drawInfo.Position.Y - Main.screenPosition.Y + p.height - p.bodyFrame.Height + 4f))
			+ p.headPosition + drawInfo.headVect;
		int frame = p.bodyFrame.Height > 0 ? p.bodyFrame.Y / p.bodyFrame.Height : 0;
		if (frame >= 0 && frame < Main.OffsetsPlayerHeadgear.Length)
			pos += Main.OffsetsPlayerHeadgear[frame] * p.gravDir;

		// Lit like armour, nudged toward full bright so the hair reads as glowing.
		Color color = Color.Lerp(drawInfo.colorArmorHead, Color.White, 0.45f);
		color.A = drawInfo.colorArmorHead.A;
		Vector2 origin = drawInfo.headVect + new Vector2(0f, Pad);
		drawInfo.DrawDataCache.Add(new DrawData(tex, pos, null, color, p.headRotation, origin, 1f, drawInfo.playerEffect));
	}
}
