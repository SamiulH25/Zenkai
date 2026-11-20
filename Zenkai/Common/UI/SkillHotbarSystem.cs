using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Zenkai.Common.Players;
using Zenkai.Common.Skills;

namespace Zenkai.Common.UI;

/// <summary>
/// The skill bar. In the ki stance it takes the place of the item hotbar at the top left: eight
/// skill slots (the selected one lit, picked with 1-8 or the scroll wheel, used with the left
/// button) and, set apart, the right-button skill. Out of the stance it is not drawn and the
/// vanilla hotbar is back.
/// </summary>
public class SkillHotbarSystem : ModSystem
{
	const int Left = 20;
	const int Top = 24;      // leaves room above for the selected skill's name, as vanilla does for items
	const int Gap = 4;
	const int SecondaryGap = 16;

	public override void Load()
	{
		if (!Main.dedServ)
			SkillSlotDrawer.Load(Mod);
	}

	public override void Unload() => SkillSlotDrawer.Unload();

	static bool Showing
	{
		get {
			Player player = Main.LocalPlayer;
			return !Main.gameMenu && !player.dead && !player.ghost && !Main.playerInventory
				&& player.GetModPlayer<SkillPlayer>().InStance;
		}
	}

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		int index = layers.FindIndex(layer => layer.Name == "Vanilla: Hotbar");
		if (index < 0)
			index = layers.Count - 1;
		else if (Showing)
			layers[index].Active = false;
		layers.Insert(index + 1, new LegacyGameInterfaceLayer("Zenkai: Skill Bar", Draw, InterfaceScaleType.UI));
	}

	/// <summary>Top-left corner of a bar slot; slot <see cref="SkillPlayer.SlotCount"/> is the right-button slot.</summary>
	public static Vector2 SlotPosition(int slot)
	{
		int x = Left + slot * (SkillSlotDrawer.Size + Gap);
		if (slot >= SkillPlayer.SlotCount)
			x += SecondaryGap - Gap;
		return new Vector2(x, Top);
	}

	static bool Draw()
	{
		if (!Showing)
			return true;
		Player player = Main.LocalPlayer;
		SkillPlayer sp = player.GetModPlayer<SkillPlayer>();
		SpriteBatch sb = Main.spriteBatch;

		// The selected skill's name, where vanilla puts the held item's.
		string title = sp.SkillInSlot(sp.Selected)?.DisplayName.Value ?? Language.GetTextValue("Mods.Zenkai.UI.SkillBar.Empty");
		Utils.DrawBorderString(sb, title, new Vector2(Left, 2), Color.White, 0.9f);

		for (int i = 0; i <= SkillPlayer.SlotCount; i++) {
			bool secondary = i == SkillPlayer.SlotCount;
			Vector2 pos = SlotPosition(i);
			ModSkill skill = secondary ? sp.SecondarySkill : sp.SkillInSlot(i);
			bool running = sp.IsActive && (secondary ? sp.ActiveButton == SkillPlayer.SecondaryButton : sp.ActiveSlot == i);
			SkillSlotDrawer.DrawFrame(sb, pos, running || (!secondary && i == sp.Selected));
			if (skill != null) {
				bool affordable = sp.CanAfford(skill);
				SkillSlotDrawer.DrawIcon(sb, pos, skill, !affordable, false);
				int cd = sp.CooldownLeft(skill);
				if (cd > 0 && skill.Cooldown > 0)
					SkillSlotDrawer.DrawCooldown(sb, pos, cd / (float)skill.Cooldown);
				if (!affordable)
					SkillSlotDrawer.DrawCost(sb, pos, sp.CostOf(skill));
				if (running && sp.State == SkillState.Charging)
					SkillSlotDrawer.DrawChargeMeter(sb, pos, sp.ChargeTicks / (float)skill.MaxCharge, sp.ChargeTicks >= skill.MaxCharge);
			}
			SkillSlotDrawer.DrawKey(sb, pos, secondary ? Language.GetTextValue("Mods.Zenkai.UI.SkillBar.RightButton") : SkillSlotDrawer.KeyLabel(i));

			var rect = new Rectangle((int)pos.X, (int)pos.Y, SkillSlotDrawer.Size, SkillSlotDrawer.Size);
			if (skill != null && rect.Contains(Main.MouseScreen.ToPoint())) {
				player.mouseInterface = true;
				Main.hoverItemName = skill.DisplayName.Value + "\n" +
					Language.GetTextValue("Mods.Zenkai.UI.SkillMenu.Cost", (int)sp.CostOf(skill));
			}
		}
		return true;
	}
}
