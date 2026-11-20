using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;
using Zenkai.Common.Forms;

namespace Zenkai.Common.Players;

/// <summary>
/// Makes a form's hair stand up a little: the character's own hair, in its form colour, drawn once
/// more a few pixels higher behind the head. Only the tips show above the normal hair, so the
/// hairstyle stays the player's and simply bristles. The colour shift itself is done by
/// <see cref="FormPlayer.ModifyDrawInfo"/>. Hidden under helmets that hide hair, as vanilla hair is.
/// </summary>
public class FormHairLayer : PlayerDrawLayer
{
	/// <summary>How far a form's hair is pushed toward full bright, so it reads as glowing.</summary>
	const float Glow = 0.4f;

	public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.Head);

	/// <summary>True when vanilla would draw the player's hair (no helmet, or one that shows full hair).</summary>
	public static bool DrawsHair(ref PlayerDrawSet drawInfo) =>
		!drawInfo.drawPlayer.invis && (drawInfo.drawPlayer.head == -1 || drawInfo.fullHair);

	/// <summary>A form's hair colour under the given light, nudged toward full bright.</summary>
	public static Color Lit(Color hair, Color light)
	{
		Color lit = hair.MultiplyRGBA(Color.Lerp(light, Color.White, Glow));
		lit.A = light.A;
		return lit;
	}

	public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
	{
		ModForm look = drawInfo.drawPlayer.GetModPlayer<FormPlayer>().BaseLook;
		return look != null && look.HairLift > 0 && drawInfo.shadow == 0f && DrawsHair(ref drawInfo);
	}

	protected override void Draw(ref PlayerDrawSet drawInfo)
	{
		Player p = drawInfo.drawPlayer;
		ModForm look = p.GetModPlayer<FormPlayer>().BaseLook;
		Main.instance.LoadHair(p.hair);
		Texture2D tex = TextureAssets.PlayerHair[p.hair].Value;

		// Vanilla's hair position, raised by the lift (down when gravity is flipped).
		Vector2 pos = new Vector2(
			(int)(drawInfo.Position.X - Main.screenPosition.X - p.bodyFrame.Width / 2 + p.width / 2),
			(int)(drawInfo.Position.Y - Main.screenPosition.Y + p.height - p.bodyFrame.Height + 4f))
			+ p.headPosition + drawInfo.headVect + drawInfo.hairOffset;
		pos.Y -= look.HairLift * p.gravDir;

		// colorHair already carries the form's colour (FormPlayer.ModifyDrawInfo runs first).
		var data = new DrawData(tex, pos, drawInfo.hairFrontFrame, drawInfo.colorHair, p.headRotation, drawInfo.headVect, 1f, drawInfo.playerEffect) {
			shader = drawInfo.hairDyePacked,
		};
		drawInfo.DrawDataCache.Add(data);
	}
}
