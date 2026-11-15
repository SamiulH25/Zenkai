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

/// <summary>The four skill slots at the bottom centre of the screen.</summary>
public class SkillHotbarSystem : ModSystem
{
	const int Gap = 8;
	const int BottomMargin = 24;

	public override void Load()
	{
		if (!Main.dedServ)
			SkillSlotDrawer.Load(Mod);
	}

	public override void Unload() => SkillSlotDrawer.Unload();

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		int index = layers.FindIndex(layer => layer.Name == "Vanilla: Hotbar");
		if (index < 0)
			index = layers.Count - 1;
		layers.Insert(index + 1, new LegacyGameInterfaceLayer("Zenkai: Skill Hotbar", Draw, InterfaceScaleType.UI));
	}

	public static Vector2 SlotPosition(int slot)
	{
		int total = SkillPlayer.SlotCount * SkillSlotDrawer.Size + (SkillPlayer.SlotCount - 1) * Gap;
		int left = (Main.screenWidth - total) / 2 & ~1; // even, so 2x art stays on its grid
		int top = (Main.screenHeight - BottomMargin - SkillSlotDrawer.Size) & ~1;
		return new Vector2(left + slot * (SkillSlotDrawer.Size + Gap), top);
	}

	static bool Draw()
	{
		Player player = Main.LocalPlayer;
		if (player.dead || player.ghost || Main.gameMenu || SkillMenuSystem.IsOpen)
			return true;

		SkillPlayer sp = player.GetModPlayer<SkillPlayer>();
		SpriteBatch sb = Main.spriteBatch;
		for (int i = 0; i < SkillPlayer.SlotCount; i++) {
			Vector2 pos = SlotPosition(i);
			ModSkill skill = sp.SkillInSlot(i);
			bool active = sp.ActiveSlot == i && sp.IsActive;
			SkillSlotDrawer.DrawFrame(sb, pos, active);
			if (skill != null) {
				bool affordable = sp.CanAfford(skill);
				SkillSlotDrawer.DrawIcon(sb, pos, skill, !affordable, false);
				int cd = sp.CooldownLeft(skill);
				if (cd > 0 && skill.Cooldown > 0)
					SkillSlotDrawer.DrawCooldown(sb, pos, cd / (float)skill.Cooldown);
				if (!affordable)
					SkillSlotDrawer.DrawCost(sb, pos, sp.CostOf(skill));
				if (active && sp.State == SkillState.Charging)
					SkillSlotDrawer.DrawChargeMeter(sb, pos, sp.ChargeTicks / (float)skill.MaxCharge, sp.ChargeTicks >= skill.MaxCharge);
			}
			SkillSlotDrawer.DrawKey(sb, pos, SkillSlotDrawer.KeyLabel(i));

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
