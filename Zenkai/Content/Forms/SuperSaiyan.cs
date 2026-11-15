using Microsoft.Xna.Framework;
using Terraria;
using Zenkai.Common.Forms;
using Zenkai.Common.Players;
using Zenkai.Common.Visuals;

namespace Zenkai.Content.Forms;

/// <summary>
/// The first true form, at early Hardmode. Gold aura, gold upright hair, teal eyes.
/// Unlock (user decision, 2026-11-14): near death only. Dropping below 10% life while a boss is
/// alive in Hardmode unlocks it and throws the player straight into it.
/// </summary>
public class SuperSaiyan : ModForm
{
	const float NearDeath = 0.1f;

	public override string HairPath => $"{Mod.Name}/Assets/Forms/SuperSaiyanHair";

	public override void SetStaticDefaults()
	{
		Tier = 2;
		KiDamageBonus = 0.35f;
		SpeedBonus = 0.15f;
		DefenseBonus = 8;
		KiDrainAtZero = 4f;
		KiDrainAtFull = 1.5f;
		SecondsToMaster = 900f;
		Aura = AuraStyle.SuperSaiyan;
		IdleAura = 0.7f;
		EyeColor = new Color(0x3a, 0xd6, 0xc8);
	}

	public override void CheckUnlockOnHurt(FormPlayer fp, Player.HurtInfo info)
	{
		Player player = fp.Player;
		if (!Main.hardMode || player.dead || player.statLife >= player.statLifeMax2 * NearDeath)
			return;
		foreach (NPC npc in Main.ActiveNPCs) {
			if (npc.boss) {
				fp.Unlock(this, dramatic: true);
				return;
			}
		}
	}
}
