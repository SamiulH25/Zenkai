using Microsoft.Xna.Framework;
using Terraria;
using Zenkai.Common.Forms;
using Zenkai.Common.Players;
using Zenkai.Common.Visuals;

namespace Zenkai.Content.Forms;

/// <summary>
/// Dormant power drawn out in full. Nothing about the character changes but a calm white aura;
/// strong, and far cheaper to hold than the golden forms.
/// </summary>
public class PotentialUnleashed : ModForm
{
	public override void SetStaticDefaults()
	{
		Line = "Potential";
		Tier = 8;
		KiDamageMultiplier = 12f;
		SpeedBonus = 0.20f;
		DefenseBonus = 16;
		MaxKiBonus = 100;
		LifeRegenBonus = 2f;
		KiDrainAtZero = 3f;
		KiDrainAtFull = 1f;
		SecondsToMaster = 1200f;
		Aura = AuraStyle.Potential;
		IdleAura = 0.6f;
	}

	public override bool UnlockCondition(FormPlayer fp) => NPC.downedGolemBoss;
}

/// <summary>
/// The body moving before the mind decides. Silver creeps into the hair and eyes, and many hits
/// simply miss. Awakened only at the edge of death against a boss once the Moon Lord has fallen;
/// it burns ki faster than anything else.
/// </summary>
public class UltraInstinct : ModForm
{
	const float NearDeath = 0.1f;

	public override void SetStaticDefaults()
	{
		Line = "Instinct";
		Tier = 11;
		KiDamageMultiplier = 40f;
		SpeedBonus = 0.45f;
		DefenseBonus = 10;
		DodgeChance = 0.35f;
		DodgeCounter = true;
		KiDrainAtZero = 14f;
		KiDrainAtFull = 8f;
		SecondsToMaster = 1500f;
		Aura = AuraStyle.Instinct;
		HairColor = new Color(0xe0, 0xe4, 0xf8);
		HairBlend = 0.45f;
		HairLift = 2;
		EyeColor = new Color(0xc8, 0xd0, 0xe8);
	}

	public override void CheckUnlockOnHurt(FormPlayer fp, Player.HurtInfo info)
	{
		if (NPC.downedMoonlord && NearDeathAgainstBoss(fp.Player, NearDeath))
			fp.Unlock(this, dramatic: true);
	}
}
