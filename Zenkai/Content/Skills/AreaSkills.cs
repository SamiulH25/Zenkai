using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Zenkai.Common.Players;
using Zenkai.Common.Skills;
using Zenkai.Content.Dusts;
using Zenkai.Content.Projectiles;

namespace Zenkai.Content.Skills;

/// <summary>Gather ki and let it burst outward in every direction, hitting everything close by once.</summary>
public class ExplosiveWave : ModSkill
{
	const float ChargeDamageBonus = 1f; // full charge doubles damage

	public override void SetStaticDefaults()
	{
		Kind = SkillKind.Burst;
		Tier = 3;
		KiCost = 45f;
		MinCharge = 20;
		MaxCharge = 70;
		Cooldown = 180;
		Recovery = 16;
		BaseDamage = 46;
		Knockback = 9f;
		ChargeMoveSpeed = 0.2f;
	}

	public override bool LearnCondition(SkillPlayer sp) => NPC.downedMechBossAny;

	public override void OnChargeTick(SkillPlayer sp)
	{
		// Motes pulled in while it builds.
		Vector2 off = Main.rand.NextVector2CircularEdge(60f, 60f);
		Dust.NewDustPerfect(sp.Player.Center + off, ModContent.DustType<KiSpark>(), -off * 0.08f);
		if (sp.StateTicks % 20 == 0)
			SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.4f + sp.ChargeFraction * 0.8f, Volume = 0.55f }, sp.Player.Center);
	}

	public override void OnRelease(SkillPlayer sp, float charge)
	{
		Spawn(sp, sp.Player.Center, Vector2.Zero, ModContent.ProjectileType<KiWave>(), 1f + ChargeDamageBonus * charge);
		SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.2f }, sp.Player.Center);
		SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.5f, Volume = 0.7f }, sp.Player.Center);
		sp.Shake(8f + 4f * charge, 22);
	}

	public override void PoseArms(SkillPlayer sp)
	{
		// Arms thrown out to the sides.
		float spread = sp.State == SkillState.Charging ? 0.5f : 1.4f;
		sp.Player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, -spread * sp.Player.direction);
		sp.Player.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, spread * sp.Player.direction);
	}
}

/// <summary>A blinding flash from the brow. Enemies nearby lose their bearings for a few seconds. No damage.</summary>
public class SolarFlash : ModSkill
{
	const float Radius = 360f;
	const int ConfuseTicks = 300;
	static readonly Color Tint = new(255, 246, 190);

	public override void SetStaticDefaults()
	{
		Kind = SkillKind.Utility;
		Tier = 1;
		KiCost = 25f;
		Cooldown = 600;
		Recovery = 20;
	}

	public override bool LearnCondition(SkillPlayer sp) => NPC.downedQueenBee || NPC.downedBoss3;

	public override void OnRelease(SkillPlayer sp, float charge)
	{
		Player player = sp.Player;
		Spawn(sp, player.Center, Vector2.Zero, ModContent.ProjectileType<FormBurst>(), 0f, 0f, 0f, FormBurst.Pack(Tint));
		for (int i = 0; i < 28; i++)
			Dust.NewDustPerfect(player.Center, ModContent.DustType<TintSpark>(), Main.rand.NextVector2CircularEdge(7f, 7f) * Main.rand.NextFloat(0.5f, 1f), 0, Tint);
		SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f, Volume = 0.8f }, player.Center);
		sp.Shake(3f, 12);

		if (Main.netMode == NetmodeID.MultiplayerClient)
			return;
		foreach (NPC npc in Main.ActiveNPCs) {
			if (npc.friendly || npc.boss || npc.dontTakeDamage || npc.buffImmune[BuffID.Confused])
				continue;
			if (Vector2.Distance(npc.Center, player.Center) <= Radius && Collision.CanHitLine(player.Center, 1, 1, npc.Center, 1, 1))
				npc.AddBuff(BuffID.Confused, ConfuseTicks);
		}
	}

	public override void PoseArms(SkillPlayer sp)
	{
		// Hands raised beside the face.
		sp.Player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Quarter, -2.6f * sp.Player.direction);
		sp.Player.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Quarter, -2.6f * sp.Player.direction);
	}
}

/// <summary>Hold to wrap yourself in a shell of ki: far less damage taken and no knockback, for as long as the ki lasts.</summary>
public class KiBarrier : ModSkill
{
	const float DamageTaken = 0.4f;
	const int MaxTicks = 60 * 60;

	public override void SetStaticDefaults()
	{
		Kind = SkillKind.Utility;
		Tier = 2;
		KiCost = 5f;
		SustainDrain = 14f;
		Cooldown = 30;
		Recovery = 8;
		HoldToSustain = true;
	}

	public override bool LearnCondition(SkillPlayer sp) => NPC.downedBoss3;

	public override int SustainTicks(SkillPlayer sp, float charge) => MaxTicks;

	public override void OnRelease(SkillPlayer sp, float charge)
	{
		Projectile bubble = Spawn(sp, sp.Player.Center, Vector2.Zero, ModContent.ProjectileType<KiBarrierBubble>(), 0f, 0f);
		sp.ActiveProjectile = bubble?.whoAmI ?? -1;
		SoundEngine.PlaySound(SoundID.Item28 with { Pitch = 0.3f, Volume = 0.6f }, sp.Player.Center);
	}

	public override bool OnSustainTick(SkillPlayer sp)
	{
		sp.DamageTakenMultiplier *= DamageTaken;
		sp.Player.noKnockback = true;
		return true;
	}

	public override void PoseArms(SkillPlayer sp)
	{
		// Arms crossed in front.
		sp.Player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters, -1.9f * sp.Player.direction);
		sp.Player.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters, -1.2f * sp.Player.direction);
	}
}

/// <summary>
/// The ultimate: hold to gather a great sphere of ki overhead, release to bring it down on the
/// cursor. It takes every bit of ki you have, and hits harder the more it took and the longer it grew.
/// </summary>
public class SpiritBomb : ModSkill
{
	const float MinKi = 50f;
	const float ChargeDamageBonus = 2f;      // full charge triples damage
	const float DamagePerKi = 0.01f;         // +1% damage per ki spent
	const float ThrowSpeed = 6.5f;

	public override void SetStaticDefaults()
	{
		Kind = SkillKind.Ultimate;
		Tier = 4;
		KiCost = MinKi;
		MinCharge = 120;
		MaxCharge = 360;
		Cooldown = 600;
		Recovery = 30;
		BaseDamage = 120;
		Knockback = 10f;
		ChargeMoveSpeed = 0f;
		UsableWhileFlying = true;
	}

	public override bool LearnCondition(SkillPlayer sp) => NPC.downedAncientCultist;

	/// <summary>The ki in the pool on the last tick of gathering, per player: what the throw costs.</summary>
	public class Gathered { public float Ki; }

	/// <summary>All the ki in the pool, and never less than the minimum. Divided by the player's
	/// cost multiplier so the final cost is exactly the pool whatever gear changes it.</summary>
	public override float GetCost(SkillPlayer sp) =>
		Math.Max(MinKi, sp.Player.GetModPlayer<KiPlayer>().Ki / Math.Max(sp.KiCostMultiplier, 0.01f));

	static Projectile Bomb(SkillPlayer sp)
	{
		if (sp.ActiveProjectile < 0)
			return null;
		Projectile p = Main.projectile[sp.ActiveProjectile];
		return p.active && p.ModProjectile is SpiritBombProjectile ? p : null;
	}

	public override void OnStart(SkillPlayer sp)
	{
		Projectile bomb = Spawn(sp, sp.Player.Top + new Vector2(0f, -SpiritBombProjectile.HoldHeight), Vector2.Zero,
			ModContent.ProjectileType<SpiritBombProjectile>());
		sp.ActiveProjectile = bomb?.whoAmI ?? -1;
	}

	public override void OnChargeTick(SkillPlayer sp)
	{
		if (Bomb(sp) is Projectile bomb)
			bomb.ai[1] = sp.ChargeTicks / (float)MaxCharge;
		sp.Data<Gathered>(this).Ki = sp.Player.GetModPlayer<KiPlayer>().Ki;
		if (sp.StateTicks % 30 == 0)
			SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.8f + sp.ChargeFraction, Volume = 0.6f }, sp.Player.Center);
		if (sp.ChargeTicks == MaxCharge && sp.StateTicks == MaxCharge)
			SoundEngine.PlaySound(SoundID.MaxMana, sp.Player.Center);
	}

	public override void OnRelease(SkillPlayer sp, float charge)
	{
		if (Bomb(sp) is not Projectile bomb)
			return;
		float paid = sp.Data<Gathered>(this).Ki;
		bomb.ai[0] = SpiritBombProjectile.StateThrown;
		bomb.ai[1] = Math.Max(bomb.ai[1], MinCharge / (float)MaxCharge);
		bomb.damage = DamageFor(sp.Player, (1f + ChargeDamageBonus * charge) * (1f + DamagePerKi * paid));
		bomb.knockBack = KnockbackFor(sp.Player);
		bomb.velocity = (sp.AimPoint - bomb.Center).SafeNormalize(Vector2.UnitY) * ThrowSpeed;
		sp.ActiveProjectile = -1;
		SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.7f }, sp.Player.Center);
		sp.Shake(6f, 20);
	}

	public override void OnEnd(SkillPlayer sp, bool cancelled)
	{
		if (cancelled && Bomb(sp) is Projectile bomb)
			bomb.Kill();
	}

	public override void PoseArms(SkillPlayer sp)
	{
		// Both arms raised while gathering; thrown down toward the target on release.
		float rotation = sp.State == SkillState.Charging ? MathHelper.Pi : sp.Aim.ToRotation() - MathHelper.PiOver2;
		sp.Player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rotation);
		sp.Player.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, rotation);
	}
}
