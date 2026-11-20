using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Zenkai.Common.Forms;
using Zenkai.Common.Players;
using Zenkai.Common.Visuals;

namespace Zenkai.Content.Forms;

/// <summary>
/// The Super Saiyan line. The character's own hair turns gold and bristles; the eyes turn teal.
/// Unlock of the first (user decision, 2026-11-14): near death only. Dropping below 10% life while
/// a boss is alive in Hardmode unlocks it and throws the player straight into it. The later stages
/// come from mastering the stage before and from the bosses that follow.
/// </summary>
public abstract class SuperSaiyanForm : ModForm
{
	protected const float MasteryToAscend = 0.75f;
	protected static readonly Color Gold = new(0xff, 0xe0, 0x50);
	protected static readonly Color Teal = new(0x3a, 0xd6, 0xc8);

	protected void SuperSaiyanDefaults(int stage)
	{
		Line = "SuperSaiyan";
		Tier = 4 + stage;
		Aura = stage == 1 ? AuraStyle.SuperSaiyan : AuraStyle.Gold(stage - 1);
		IdleAura = 0.7f;
		HairColor = Gold;
		HairBlend = 0.92f;
		HairLift = 2 * System.Math.Min(stage, 2);
		EyeColor = Teal;
	}
}

public class SuperSaiyan : SuperSaiyanForm
{
	const float NearDeath = 0.1f;

	public override void SetStaticDefaults()
	{
		SuperSaiyanDefaults(1);
		KiDamageMultiplier = 5f;
		SpeedBonus = 0.15f;
		DefenseBonus = 8;
		KiCritBonus = 0.10f;
		KiDrainAtZero = 4f;
		KiDrainAtFull = 1.5f;
	}

	public override void CheckUnlockOnHurt(FormPlayer fp, Player.HurtInfo info)
	{
		if (Main.hardMode && NearDeathAgainstBoss(fp.Player, NearDeath))
			fp.Unlock(this, dramatic: true);
	}
}

public class SuperSaiyan2 : SuperSaiyanForm
{
	public override void SetStaticDefaults()
	{
		SuperSaiyanDefaults(2);
		KiDamageMultiplier = 10f;
		SpeedBonus = 0.20f;
		DefenseBonus = 12;
		KiCritBonus = 0.15f;
		// The storm that gathers round the aura strikes foes on its own.
		LightningInterval = 90;
		KiDrainAtZero = 6f;
		KiDrainAtFull = 2.5f;
	}

	public override bool UnlockCondition(FormPlayer fp) =>
		NPC.downedMechBossAny && fp.Mastery(ModContent.GetInstance<SuperSaiyan>()) >= MasteryToAscend;
}

public class SuperSaiyan3 : SuperSaiyanForm
{
	public override void SetStaticDefaults()
	{
		SuperSaiyanDefaults(3);
		KiDamageMultiplier = 20f;
		SpeedBonus = 0.25f;
		DefenseBonus = 14;
		KiCritBonus = 0.20f;
		EnduranceBonus = 0.10f;
		// Enormous power, enormous appetite.
		KiDrainAtZero = 12f;
		KiDrainAtFull = 6f;
	}

	public override bool UnlockCondition(FormPlayer fp) =>
		NPC.downedPlantBoss && fp.Mastery(ModContent.GetInstance<SuperSaiyan2>()) >= MasteryToAscend;
}

/// <summary>Godly ki. The hair lies as it always did, turned crimson; the aura is a tall slow flame.</summary>
public class SuperSaiyanGod : ModForm
{
	public override void SetStaticDefaults()
	{
		Line = "God";
		Tier = 9;
		KiDamageMultiplier = 30f;
		SpeedBonus = 0.30f;
		DefenseBonus = 18;
		KiRegenMultiplier = 2f;
		MaxKiBonus = 50;
		KiDrainAtZero = 5f;
		KiDrainAtFull = 2f;
		SecondsToMaster = 1200f;
		Aura = AuraStyle.God;
		HairColor = new Color(0xf0, 0x38, 0x60);
		HairBlend = 0.9f;
		EyeColor = new Color(0xe8, 0x30, 0x50);
	}

	public override bool UnlockCondition(FormPlayer fp) =>
		NPC.downedAncientCultist && fp.Mastery(ModContent.GetInstance<SuperSaiyan3>()) >= 0.5f;
}

/// <summary>Godly ki inside a Super Saiyan: blue hair, a dense blue aura, the strongest steady form.</summary>
public class SuperSaiyanBlue : ModForm
{
	public override void SetStaticDefaults()
	{
		Line = "God";
		Tier = 10;
		KiDamageMultiplier = 50f;
		SpeedBonus = 0.35f;
		DefenseBonus = 24;
		KiCostMultiplier = 0.7f;
		EnduranceBonus = 0.12f;
		KiDrainAtZero = 8f;
		KiDrainAtFull = 3.5f;
		SecondsToMaster = 1200f;
		Aura = AuraStyle.Blue;
		HairColor = new Color(0x48, 0xc8, 0xff);
		HairBlend = 0.92f;
		HairLift = 2;
		EyeColor = new Color(0x40, 0xa0, 0xff);
	}

	public override bool UnlockCondition(FormPlayer fp) =>
		NPC.downedMoonlord && fp.Mastery(ModContent.GetInstance<SuperSaiyanGod>()) >= 0.75f;
}
