using Microsoft.Xna.Framework;
using Terraria;
using Zenkai.Common.Forms;
using Zenkai.Common.Players;
using Zenkai.Common.Visuals;

namespace Zenkai.Content.Forms;

/// <summary>
/// The Kaioken line: red aura, the body pushed past its limits. Every rank burns life instead of
/// ki and drops out on its own when life gets low. Each rank is earned by mastering the one before
/// it and by the world growing more dangerous. The trainer teaches the first in phase 5; until then
/// the first boss does.
/// </summary>
public abstract class KaiokenForm : ModForm
{
	/// <summary>Mastery of the rank below needed to unlock this one.</summary>
	protected const float MasteryToRankUp = 0.5f;
	static readonly Color HairTint = new(0xff, 0x70, 0x58);

	protected void KaiokenDefaults(int rank)
	{
		Line = "Kaioken";
		Stacks = true;
		Tier = rank;
		RevertBelowLife = 0.1f;
		IdleAura = 0.75f;
		// Pushing past the limit gathers ki faster, at the usual cost in life.
		ChargeRateMultiplier = 1.5f;
		SecondsToMaster = 600f;
		// The red glow reaches the hair, no more.
		HairColor = HairTint;
		HairBlend = 0.2f + 0.05f * rank;
		Aura = rank == 1 ? AuraStyle.Kaioken : AuraStyle.KaiokenRank(rank - 1);
	}
}

public class Kaioken : KaiokenForm
{
	public override void SetStaticDefaults()
	{
		KaiokenDefaults(1);
		KiDamageMultiplier = 2f;
		SpeedBonus = 0.10f;
		SurgeBonus = 0.30f;
		LifeDrainAtZero = 2f;
		LifeDrainAtFull = 1f;
	}

	public override bool UnlockCondition(FormPlayer fp) => NPC.downedBoss1 || NPC.downedSlimeKing || NPC.downedBoss2;
}

public class KaiokenX3 : KaiokenForm
{
	public override void SetStaticDefaults()
	{
		KaiokenDefaults(2);
		KiDamageMultiplier = 3f;
		SpeedBonus = 0.15f;
		SurgeBonus = 0.45f;
		LifeDrainAtZero = 4f;
		LifeDrainAtFull = 2f;
	}

	public override bool UnlockCondition(FormPlayer fp) =>
		NPC.downedBoss3 && fp.Mastery(Terraria.ModLoader.ModContent.GetInstance<Kaioken>()) >= MasteryToRankUp;
}

public class KaiokenX10 : KaiokenForm
{
	public override void SetStaticDefaults()
	{
		KaiokenDefaults(3);
		KiDamageMultiplier = 6f;
		SpeedBonus = 0.25f;
		SurgeBonus = 0.60f;
		LifeDrainAtZero = 8f;
		LifeDrainAtFull = 4f;
	}

	public override bool UnlockCondition(FormPlayer fp) =>
		Main.hardMode && fp.Mastery(Terraria.ModLoader.ModContent.GetInstance<KaiokenX3>()) >= MasteryToRankUp;
}

public class KaiokenX20 : KaiokenForm
{
	public override void SetStaticDefaults()
	{
		KaiokenDefaults(4);
		KiDamageMultiplier = 10f;
		SpeedBonus = 0.35f;
		SurgeBonus = 0.80f;
		LifeDrainAtZero = 14f;
		LifeDrainAtFull = 7f;
	}

	public override bool UnlockCondition(FormPlayer fp) =>
		NPC.downedPlantBoss && fp.Mastery(Terraria.ModLoader.ModContent.GetInstance<KaiokenX10>()) >= MasteryToRankUp;
}
