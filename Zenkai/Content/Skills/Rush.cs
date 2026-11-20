using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Zenkai.Common.Players;
using Zenkai.Common.Skills;
using Zenkai.Content.Projectiles;

namespace Zenkai.Content.Skills;

/// <summary>
/// Bare-handed combo. Each tap is a short lunge with a strike in front: jab, jab, launcher.
/// The launcher knocks the target up and away; tapping again soon after dashes after it
/// (costs ki) for a heavy follow-up. The player ignores contact damage during lunges.
/// </summary>
public class Rush : ModSkill
{
	/// <summary>Combo state for one player. Step is 1-2 jab, 3 launcher, 4 chase hit.</summary>
	public class Combo
	{
		public int Step;
		public int Timer;
		public int ChaseTarget = -1;
		public int ChaseTimer;
	}

	public const int StepLauncher = 3;
	public const int StepChase = 4;
	const int ComboWindow = 32;   // ticks after a hit to press again and continue the combo
	public const int ChaseWindow = 45;
	const float ChaseCost = 10f;
	const float ChaseSpeed = 16f;
	const float ChaseReach = 40f;
	const int ChaseMaxTicks = 18;
	static readonly float[] LungeSpeed = { 0f, 5.5f, 6f, 8f };
	static readonly int[] LungeTicks = { 0, 7, 7, 9 };
	static readonly float[] DamageMult = { 0f, 1f, 1f, 1.5f, 2f };
	static readonly float[] KnockMult = { 0f, 0.5f, 0.5f, 3f, 2f };

	public override void SetStaticDefaults()
	{
		Kind = SkillKind.Melee;
		KnownFromStart = true;
		KiCost = 0f;
		Recovery = 7;
		BaseDamage = 10;
		Knockback = 4f;
	}

	public static Combo ComboOf(SkillPlayer sp) => sp.Data<Combo>(ModContent.GetInstance<Rush>());

	static bool ChaseReady(Combo c) =>
		c.ChaseTimer > 0 && c.ChaseTarget >= 0 && Main.npc[c.ChaseTarget].active && !Main.npc[c.ChaseTarget].friendly;

	public override float GetCost(SkillPlayer sp) => ChaseReady(ComboOf(sp)) ? ChaseCost : KiCost;

	public override void UpdatePassive(SkillPlayer sp)
	{
		Combo c = ComboOf(sp);
		if (c.Timer > 0 && --c.Timer == 0)
			c.Step = 0;
		if (c.ChaseTimer > 0 && --c.ChaseTimer == 0)
			c.ChaseTarget = -1;
	}

	public override void OnStart(SkillPlayer sp)
	{
		Combo c = ComboOf(sp);
		if (ChaseReady(c))
			c.Step = StepChase;
		else
			c.Step = c.Timer > 0 && c.Step < StepLauncher ? c.Step + 1 : 1;
		c.Timer = 0;
	}

	public override int SustainTicks(SkillPlayer sp, float charge)
	{
		int step = ComboOf(sp).Step;
		return step == StepChase ? ChaseMaxTicks : LungeTicks[step];
	}

	public override void OnRelease(SkillPlayer sp, float charge)
	{
		Combo c = ComboOf(sp);
		SoundEngine.PlaySound(SoundID.Item1 with { Pitch = c.Step == StepLauncher ? -0.2f : 0.3f + c.Step * 0.1f }, sp.Player.Center);
		if (c.Step == StepChase) {
			c.ChaseTimer = 0;
			SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.4f, Volume = 0.6f }, sp.Player.Center);
			return; // the strike comes when the dash arrives
		}
		Strike(sp, c.Step);
	}

	void Strike(SkillPlayer sp, int step)
	{
		Projectile p = Spawn(sp, sp.Player.MountedCenter, LungeDirection(sp), ModContent.ProjectileType<RushStrike>(),
			DamageMult[step], KnockMult[step], step);
		sp.ActiveProjectile = p?.whoAmI ?? -1;
	}

	/// <summary>Along the aim in flight; straight ahead on the ground.</summary>
	public static Vector2 LungeDirection(SkillPlayer sp)
	{
		if (sp.Player.GetModPlayer<FlightPlayer>().Flying)
			return sp.Aim;
		return new Vector2(Math.Sign(sp.Aim.X == 0 ? sp.Player.direction : sp.Aim.X), 0f);
	}

	public override bool OnSustainTick(SkillPlayer sp)
	{
		sp.ContactProof = true;
		Player player = sp.Player;
		Combo c = ComboOf(sp);

		if (c.Step == StepChase) {
			if (c.ChaseTarget < 0 || !Main.npc[c.ChaseTarget].active)
				return false;
			NPC target = Main.npc[c.ChaseTarget];
			Vector2 to = target.Center - player.Center;
			sp.Aim = to.SafeNormalize(sp.Aim);
			if (to.Length() <= ChaseReach + target.width / 2f) {
				Strike(sp, StepChase);
				sp.ForceVelocity(Vector2.Zero);
				return false;
			}
			sp.ForceVelocity(sp.Aim * ChaseSpeed);
			if (Main.rand.NextBool())
				Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f), ModContent.DustType<Dusts.KiSpark>(), -sp.Aim * 2f);
			return true;
		}

		// Lunge: fast at first, easing to a stop.
		float t = sp.StateTicks / (float)LungeTicks[c.Step];
		Vector2 dir = LungeDirection(sp);
		Vector2 v = dir * LungeSpeed[c.Step] * (1f - t * t);
		if (dir.Y == 0f && !player.GetModPlayer<FlightPlayer>().Flying)
			v.Y = player.velocity.Y; // keep gravity on the ground
		sp.ForceVelocity(v);
		return true;
	}

	public override void OnEnd(SkillPlayer sp, bool cancelled)
	{
		Combo c = ComboOf(sp);
		if (cancelled || c.Step >= StepLauncher) {
			c.Step = 0;
			c.Timer = 0;
			return;
		}
		c.Timer = ComboWindow;
	}

	public override void PoseArms(SkillPlayer sp)
	{
		// Jabs alternate arms; the launcher is an uppercut.
		int step = ComboOf(sp).Step;
		Vector2 aim = sp.Aim;
		if (step == StepLauncher)
			aim = new Vector2(Math.Sign(aim.X == 0 ? sp.Player.direction : aim.X) * 0.5f, -1f);
		float rotation = aim.ToRotation() - MathHelper.PiOver2;
		if (step == 2)
			sp.Player.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, rotation);
		else
			sp.Player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rotation);
	}
}

/// <summary>
/// A wound-up punch: hold to charge, release to drive forward with one heavy blow that sends the
/// target flying. The longer the charge, the further the lunge and the harder the hit.
/// </summary>
public class HeavyStrike : ModSkill
{
	const int LungeTicks = 10;
	const float LungeSpeedMin = 9f, LungeSpeedBonus = 7f;
	const float ChargeDamageBonus = 2f; // full charge triples damage
	/// <summary>Strike step for <see cref="RushStrike"/>: heavy, no launch window.</summary>
	public const int StepHeavy = 5;

	public override void SetStaticDefaults()
	{
		Kind = SkillKind.Melee;
		Tier = 1;
		KiCost = 8f;
		MinCharge = 15;
		MaxCharge = 60;
		Cooldown = 45;
		Recovery = 12;
		BaseDamage = 22;
		Knockback = 9f;
		ChargeMoveSpeed = 0.5f;
	}

	public override bool LearnCondition(SkillPlayer sp) => NPC.downedBoss1 || NPC.downedSlimeKing || NPC.downedBoss2;

	public override int SustainTicks(SkillPlayer sp, float charge) => LungeTicks;

	public override void OnChargeTick(SkillPlayer sp)
	{
		if (sp.ChargeTicks == MaxCharge && sp.StateTicks == MaxCharge)
			SoundEngine.PlaySound(SoundID.MaxMana, sp.Player.Center);
	}

	public override void OnRelease(SkillPlayer sp, float charge)
	{
		Projectile p = Spawn(sp, sp.Player.MountedCenter, Rush.LungeDirection(sp), ModContent.ProjectileType<RushStrike>(),
			1f + ChargeDamageBonus * charge, 1f + charge, StepHeavy);
		sp.ActiveProjectile = p?.whoAmI ?? -1;
		SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.5f, Volume = 1f }, sp.Player.Center);
	}

	public override bool OnSustainTick(SkillPlayer sp)
	{
		sp.ContactProof = true;
		float t = sp.StateTicks / (float)LungeTicks;
		Vector2 dir = Rush.LungeDirection(sp);
		Vector2 v = dir * (LungeSpeedMin + LungeSpeedBonus * sp.ChargeFraction) * (1f - t * t);
		if (dir.Y == 0f && !sp.Player.GetModPlayer<FlightPlayer>().Flying)
			v.Y = sp.Player.velocity.Y;
		sp.ForceVelocity(v);
		return true;
	}

	public override void PoseArms(SkillPlayer sp)
	{
		// Drawn back while charging, thrown forward on release.
		Vector2 aim = sp.State == SkillState.Charging ? new Vector2(-Math.Sign(sp.Aim.X == 0 ? 1 : sp.Aim.X) * 0.6f, 0.4f) : sp.Aim;
		sp.Player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, aim.ToRotation() - MathHelper.PiOver2);
	}
}
