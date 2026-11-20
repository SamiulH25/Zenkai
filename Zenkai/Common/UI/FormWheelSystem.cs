using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Zenkai.Common.Forms;
using Zenkai.Common.Players;

namespace Zenkai.Common.UI;

/// <summary>
/// The form wheel: while the transform key is held, every unlocked form sits in a ring round the
/// middle of the screen, weakest at the top and clockwise from there, with "power down" at the
/// centre. The mouse direction picks one; letting go of the key takes it (see <see cref="FormPlayer"/>).
/// This class only draws the wheel and works out what the mouse is pointing at.
/// </summary>
public class FormWheelSystem : ModSystem
{
	const float Radius = 104f;
	const float RadiusCrowded = 132f; // past eight forms the ring widens so slots do not touch
	const int CrowdedCount = 8;
	const float CentreRadius = 36f;   // closer to the middle than this means "power down"

	static bool hasChoice;
	static ModForm choice;

	/// <summary>Shot runner: the form the wheel treats as pointed at, in place of the mouse.</summary>
	public static ModForm DevHover;

	/// <summary>What the mouse was pointing at when the wheel was last drawn: a form, or null for
	/// "power down". False when nothing was drawn yet.</summary>
	public static bool TryGetChoice(out ModForm form)
	{
		form = choice;
		bool had = hasChoice;
		hasChoice = false;
		return had;
	}

	public override void Unload()
	{
		choice = null;
		DevHover = null;
	}

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
		if (index < 0)
			return;
		layers.Insert(index, new LegacyGameInterfaceLayer("Zenkai: Form Wheel", Draw, InterfaceScaleType.UI));
	}

	static string T(string key, params object[] args) => Language.GetTextValue("Mods.Zenkai.UI.FormWheel." + key, args);

	static bool Draw()
	{
		Player player = Main.LocalPlayer;
		FormPlayer fp = player.GetModPlayer<FormPlayer>();
		if (!fp.WheelOpen || Main.gameMenu)
			return true;
		player.mouseInterface = true;

		List<ModForm> forms = fp.UnlockedForms();
		SpriteBatch sb = Main.spriteBatch;
		var centre = new Vector2(Main.screenWidth / 2 & ~1, Main.screenHeight / 2 & ~1);
		float radius = forms.Count > CrowdedCount ? RadiusCrowded : Radius;
		const int half = SkillSlotDrawer.Size / 2;

		// Which slot is the mouse pointing toward?
		Vector2 mouse = Main.MouseScreen - centre;
		int hover = -1; // -1 is the centre
		if (DevHover != null) {
			hover = forms.IndexOf(DevHover);
		}
		else if (mouse.Length() > CentreRadius && forms.Count > 0) {
			// Angle clockwise from straight up, so index 0 is at the top.
			float angle = MathHelper.WrapAngle((float)Math.Atan2(mouse.X, -mouse.Y));
			if (angle < 0f)
				angle += MathHelper.TwoPi;
			hover = (int)Math.Round(angle / MathHelper.TwoPi * forms.Count) % forms.Count;
		}
		hasChoice = true;
		choice = hover >= 0 ? forms[hover] : null;

		for (int i = 0; i < forms.Count; i++) {
			ModForm form = forms[i];
			float angle = MathHelper.TwoPi * i / forms.Count;
			Vector2 at = centre + new Vector2((float)Math.Sin(angle), -(float)Math.Cos(angle)) * radius;
			var pos = new Vector2((int)at.X - half & ~1, (int)at.Y - half & ~1);
			SkillSlotDrawer.DrawFrame(sb, pos, i == hover || form == fp.Active || form == fp.Overlay);
			SkillSlotDrawer.DrawFormIcon(sb, pos, form, false);
			SkillSlotDrawer.DrawMastery(sb, pos, fp.Mastery(form), form.Aura.Hot);
		}

		// Centre: power down.
		var centrePos = centre - new Vector2(half);
		SkillSlotDrawer.DrawFrame(sb, centrePos, hover < 0);
		string baseLabel = T("Base");
		Vector2 size = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(baseLabel) * 0.75f;
		Utils.DrawBorderString(sb, baseLabel, (centre - size / 2f + new Vector2(0f, 4f)).Floor(), hover < 0 ? SkillSlotDrawer.Gold : Color.White, 0.75f);

		// What is pointed at, under the ring.
		float textTop = centre.Y + radius + half + 14f;
		if (hover >= 0) {
			ModForm form = forms[hover];
			float m = fp.Mastery(form);
			string drain = form.LifeDrainAtZero > 0f
				? T("LifeDrain", form.LifeDrain(m).ToString("0.#"))
				: T("KiDrain", form.KiDrain(m).ToString("0.#"));
			DrawCentred(sb, form.DisplayName.Value, centre.X, textTop, form.Aura.Rim, 1.05f);
			DrawCentred(sb, T("Line", form.KiDamageMultiplier.ToString("0.#"), drain, (int)(m * 100f)), centre.X, textTop + 26f, Color.White, 0.85f);
		}
		else {
			DrawCentred(sb, T(fp.State == FormState.Normal ? "Hint" : "PowerDown"), centre.X, textTop, Color.White, 0.9f);
		}
		return true;
	}

	static void DrawCentred(SpriteBatch sb, string text, float x, float y, Color color, float scale)
	{
		Vector2 size = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(text) * scale;
		Utils.DrawBorderString(sb, text, new Vector2(x - size.X / 2f, y).Floor(), color, scale);
	}
}
