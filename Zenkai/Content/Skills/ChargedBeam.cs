using Terraria;
using Zenkai.Common.Players;
using Zenkai.Common.Skills;

namespace Zenkai.Content.Skills;

/// <summary>The signature beam: a thick blue wave. Learned after the first boss.</summary>
public class ChargedBeam : BeamSkill
{
	public override void SetStaticDefaults()
	{
		BeamDefaults();
		Tier = 1;
		KiCost = 20f;
		SustainDrain = 15f;
		MinCharge = 30;
		MaxCharge = 150;
		BaseDamage = 14;
		Knockback = 1f;
	}

	public override bool LearnCondition(SkillPlayer sp) => NPC.downedBoss1 || NPC.downedSlimeKing || NPC.downedBoss2;
}

/// <summary>A thin drilling beam: long charge, short burst, ignores armour and goes through walls.</summary>
public class PiercingBeam : BeamSkill
{
	public override void SetStaticDefaults()
	{
		BeamDefaults();
		Tier = 3;
		KiCost = 50f;
		SustainDrain = 10f;
		MinCharge = 60;
		MaxCharge = 120;
		Cooldown = 150;
		BaseDamage = 55;
		Knockback = 0.5f;
		ArmorPenetration = 40;
		SustainMin = 24;
		SustainBonus = 24;
		MaxChargeDamageBonus = 1.5f;
		AssetPrefix = "PiercingBeam";
		HitWidth = 10f;
		HitCooldown = 8;
		PiercesTiles = true;
		MaxLength = 1300f;
		LightColor = new(190, 110, 255);
		SustainTurnRate = 0.012f;
	}

	public override bool LearnCondition(SkillPlayer sp) => NPC.downedPlantBoss;
}

/// <summary>The widest beam, thrown with both arms: slow to charge, enormous once it goes.</summary>
public class FinalBeam : BeamSkill
{
	public override void SetStaticDefaults()
	{
		BeamDefaults();
		Tier = 4;
		KiCost = 60f;
		SustainDrain = 30f;
		MinCharge = 45;
		MaxCharge = 180;
		Cooldown = 240;
		BaseDamage = 48;
		Knockback = 3f;
		SustainMin = 80;
		SustainBonus = 100;
		AssetPrefix = "FinalBeam";
		HitWidth = 36f;
		HitCooldown = 8;
		LightColor = new(255, 220, 90);
		FireShake = 7f;
		FireShakeBonus = 6f;
		SustainTurnRate = 0.02f;
		ChargeMoveSpeed = 0.2f;
	}

	public override bool LearnCondition(SkillPlayer sp) => NPC.downedGolemBoss;
}
