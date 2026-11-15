using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using Zenkai.Common.Players;

namespace Zenkai.Common.UI;

/// <summary>Draws the ki bar to the left of the life display.</summary>
public class KiBarSystem : ModSystem
{
	// Where the fill sits inside Assets/UI/KiBarFrame.png, in texture pixels.
	const int FillLeft = 36;
	const int FillWidth = 144;

	// Vanilla draws life starting 300 px from the right edge.
	const int LifeDisplayWidth = 300;
	const int GapToLife = 28;
	// Centres the bar on the first row of hearts: at UI scale 1 their red fill spans
	// y 21..34 (measured in the 2026-11-14 shots), and the bar's centre is 16 px below its top.
	const int BarTop = 12;

	static Asset<Texture2D> frame;
	static Asset<Texture2D> fill;

	public override void Load()
	{
		if (Main.dedServ)
			return;
		frame = Mod.Assets.Request<Texture2D>("Assets/UI/KiBarFrame");
		fill = Mod.Assets.Request<Texture2D>("Assets/UI/KiBarFill");
	}

	public override void Unload()
	{
		frame = null;
		fill = null;
	}

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		int index = layers.FindIndex(layer => layer.Name == "Vanilla: Resource Bars");
		if (index < 0)
			return;
		layers.Insert(index + 1, new LegacyGameInterfaceLayer("Zenkai: Ki Bar", Draw, InterfaceScaleType.UI));
	}

	static bool Draw()
	{
		Player player = Main.LocalPlayer;
		if (player.dead || player.ghost)
			return true;

		KiPlayer ki = player.GetModPlayer<KiPlayer>();
		SpriteBatch spriteBatch = Main.spriteBatch;
		Texture2D frameTex = frame.Value;
		Texture2D fillTex = fill.Value;

		var pos = new Vector2(Main.screenWidth - LifeDisplayWidth - GapToLife - frameTex.Width, BarTop);
		spriteBatch.Draw(frameTex, pos, Color.White);

		float fraction = ki.MaxKi > 0 ? MathHelper.Clamp(ki.Ki / ki.MaxKi, 0f, 1f) : 0f;
		int width = (int)(FillWidth * fraction + 0.5f);
		if (width > 0) {
			var source = new Rectangle(FillLeft, 0, width, fillTex.Height);
			spriteBatch.Draw(fillTex, pos + new Vector2(FillLeft, 0f), source, Color.White);
			if (ki.Charging) {
				// Additive pulse over the fill while charging.
				float pulse = 0.25f + 0.2f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 14f);
				spriteBatch.Draw(fillTex, pos + new Vector2(FillLeft, 0f), source, new Color(255, 255, 255, 0) * pulse);
			}
		}

		// Current ki, right-aligned against the orb.
		string text = ((int)ki.Ki).ToString();
		const float TextScale = 1f;
		Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * TextScale;
		var textPos = new Vector2(pos.X - size.X - 8f, pos.Y + (frameTex.Height - size.Y) / 2f + 4f);
		Utils.DrawBorderString(spriteBatch, text, textPos, Color.White, TextScale);

		return true;
	}
}
