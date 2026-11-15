using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ModLoader;
using Zenkai.Common.Forms;
using Zenkai.Common.Skills;
using Zenkai.Common.Systems;

namespace Zenkai.Common.UI;

/// <summary>Draws one skill slot: frame, icon, cooldown cover, key label. Shared by the hotbar and the skill menu.
/// Everything is placed on the 2x art grid (even screen pixels) so it stays crisp.</summary>
public static class SkillSlotDrawer
{
	public const int Size = 48;      // 24 art px at 2x
	const int IconInset = 8;         // icon (16 art px = 32 screen px) sits 4 art px in
	const int WellRows = 16;         // art rows of the icon well

	static Asset<Texture2D> frame, frameActive;

	public static Color Outline => new(0x15, 0x12, 0x2b);
	public static Color Well => new(0x1d, 0x24, 0x47);
	public static Color KiFill => new(0x45, 0xc2, 0xfb);
	public static Color KiFillHi => new(0xd4, 0xfb, 0xff);
	public static Color Gold => new(0xf8, 0xdc, 0x7a);

	internal static void Load(Mod mod)
	{
		frame = mod.Assets.Request<Texture2D>("Assets/UI/SkillSlot");
		frameActive = mod.Assets.Request<Texture2D>("Assets/UI/SkillSlotActive");
	}

	internal static void Unload() => frame = frameActive = null;

	public static string KeyLabel(int slot)
	{
		var keys = ZenkaiKeybinds.SkillSlots[slot].GetAssignedKeys(InputMode.Keyboard);
		if (keys.Count == 0)
			return "-";
		string key = keys[0];
		return key.StartsWith("D") && key.Length == 2 ? key.Substring(1) : key; // D1 -> 1
	}

	public static void DrawFrame(SpriteBatch sb, Vector2 pos, bool active) =>
		sb.Draw((active ? frameActive : frame).Value, pos, Color.White);

	/// <summary>Icon in the well. Silhouette draws it as a dark shape for unlearned skills.</summary>
	public static void DrawIcon(SpriteBatch sb, Vector2 pos, ModSkill skill, bool dim, bool silhouette)
	{
		if (skill == null)
			return;
		Color color = silhouette ? new Color(10, 12, 28) : dim ? new Color(95, 95, 115) : Color.White;
		sb.Draw(skill.Icon.Value, pos + new Vector2(IconInset), color);
	}

	public static void DrawFormIcon(SpriteBatch sb, Vector2 pos, ModForm form, bool silhouette)
	{
		Color color = silhouette ? new Color(10, 12, 28) : Color.White;
		sb.Draw(form.Icon.Value, pos + new Vector2(IconInset), color);
	}

	/// <summary>A thin mastery bar under a slot, filled in the form's colour.</summary>
	public static void DrawMastery(SpriteBatch sb, Vector2 slotPos, float mastery, Color fill)
	{
		const int w = 22, h = 3; // art px
		var at = new Point((int)slotPos.X + (Size - w * 2) / 2, (int)slotPos.Y + Size + 2);
		Texture2D px = TextureAssets.MagicPixel.Value;
		sb.Draw(px, new Rectangle(at.X, at.Y, w * 2, h * 2), Outline);
		sb.Draw(px, new Rectangle(at.X + 2, at.Y + 2, (w - 2) * 2, (h - 2) * 2), Well);
		int n = (int)Math.Round((w - 2) * MathHelper.Clamp(mastery, 0f, 1f));
		if (n > 0)
			sb.Draw(px, new Rectangle(at.X + 2, at.Y + 2, n * 2, (h - 2) * 2), fill);
	}

	/// <summary>Dark cover over the top of the well that shrinks as the cooldown runs out.</summary>
	public static void DrawCooldown(SpriteBatch sb, Vector2 pos, float fraction)
	{
		if (fraction <= 0f)
			return;
		int rows = (int)Math.Ceiling(WellRows * fraction);
		sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)pos.X + IconInset, (int)pos.Y + IconInset, 32, rows * 2), Color.Black * 0.6f);
	}

	public static void DrawKey(SpriteBatch sb, Vector2 pos, string label)
	{
		var font = FontAssets.MouseText.Value;
		const float scale = 0.8f;
		Vector2 size = font.MeasureString(label) * scale;
		Vector2 at = (pos + new Vector2(Size - 6 - size.X, Size - 18)).Floor();
		Utils.DrawBorderString(sb, label, at, Color.White, scale);
	}

	public static void DrawCost(SpriteBatch sb, Vector2 pos, float cost)
	{
		Utils.DrawBorderString(sb, ((int)Math.Ceiling(cost)).ToString(), (pos + new Vector2(6, 4)).Floor(), new Color(255, 110, 110), 0.75f);
	}

	/// <summary>A small bar above the slot showing charge progress, white-flashing when full.</summary>
	public static void DrawChargeMeter(SpriteBatch sb, Vector2 slotPos, float fraction, bool full)
	{
		const int w = 20, h = 3; // art px
		var at = new Point((int)slotPos.X + (Size - w * 2) / 2, (int)slotPos.Y - h * 2 - 4);
		Texture2D px = TextureAssets.MagicPixel.Value;
		sb.Draw(px, new Rectangle(at.X, at.Y, w * 2, h * 2), Outline);
		sb.Draw(px, new Rectangle(at.X + 2, at.Y + 2, (w - 2) * 2, (h - 2) * 2), Well);
		int fill = (int)Math.Round((w - 2) * MathHelper.Clamp(fraction, 0f, 1f));
		bool flash = full && Main.GameUpdateCount / 6 % 2 == 0;
		if (fill > 0)
			sb.Draw(px, new Rectangle(at.X + 2, at.Y + 2, fill * 2, (h - 2) * 2), flash ? KiFillHi : KiFill);
	}
}
