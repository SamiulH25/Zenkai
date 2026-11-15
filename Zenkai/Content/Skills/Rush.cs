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
/// Combo state lives on <see cref="SkillPlayer"/> (ComboStep 1-3, 4 = chase).
/// </summary>
public class Rush : ModSkill
{
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

	static bool ChaseReady(SkillPlayer sp) =>
		sp.ChaseTimer > 0 && sp.ChaseTarget >= 0 && Main.npc[sp.ChaseTarget].active && !Main.npc[sp.ChaseTarget].friendly;

	public override float GetCost(SkillPlayer sp) => ChaseReady(sp) ? ChaseCost : KiCost;

	public override void OnStart(SkillPlayer sp)
	{
		if (ChaseReady(sp))
			sp.ComboStep = StepChase;
		else
			sp.ComboStep = sp.ComboTimer > 0 && sp.ComboStep < 3 ? sp.ComboStep + 1 : 1;
		sp.ComboTimer = 0;
	}

	public override int SustainTicks(SkillPlayer sp, float charge) =>
		sp.ComboStep == StepChase ? ChaseMaxTicks : LungeTicks[sp.ComboStep];

	public override void OnRelease(SkillPlayer sp, float charge)
	{
		int step = sp.ComboStep;
		SoundEngine.PlaySound(SoundID.Item1 with { Pitch = step == 3 ? -0.2f : 0.3f + step * 0.1f }, sp.Player.Center);
		if (step == StepChase) {
			sp.ChaseTimer = 0;
			SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.4f, Volume = 0.6f }, sp.Player.Center);
			return; // the strike comes when the dash arrives
		}
		Strike(sp, step);
	}

	void Strike(SkillPlayer sp, int step)
	{
		Projectile p = Spawn(sp, sp.Player.MountedCenter, LungeDirection(sp), ModContent.ProjectileType<RushStrike>(),
			DamageMult[step], KnockMult[step], step);
		sp.ActiveProjectile = p?.whoAmI ?? -1;
	}

	Vector2 LungeDirection(SkillPlayer sp)
	{
		if (sp.Player.GetModPlayer<KiPlayer>().Flying)
			return sp.Aim;
		return new Vector2(Math.Sign(sp.Aim.X == 0 ? sp.Player.direction : sp.Aim.X), 0f);
	}

	public override bool OnSustainTick(SkillPlayer sp)
	{
		sp.ContactProof = true;
		Player player = sp.Player;
		int step = sp.ComboStep;

		if (step == StepChase) {
			if (sp.ChaseTarget < 0 || !Main.npc[sp.ChaseTarget].active)
				return false;
			NPC target = Main.npc[sp.ChaseTarget];
			Vector2 to = target.Center - player.Center;
			if (to.Length() <= ChaseReach + target.width / 2f) {
				sp.Aim = to.SafeNormalize(sp.Aim);
				Strike(sp, StepChase);
				sp.ForceVelocity(Vector2.Zero);
				return false;
			}
			sp.Aim = to.SafeNormalize(sp.Aim);
			sp.ForceVelocity(sp.Aim * ChaseSpeed);
			if (Main.rand.NextBool())
				Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f), ModContent.DustType<Dusts.KiSpark>(), -sp.Aim * 2f);
			return true;
		}

		// Lunge: fast at first, easing to a stop.
		float t = sp.StateTicks / (float)LungeTicks[step];
		Vector2 dir = LungeDirection(sp);
		Vector2 v = dir * LungeSpeed[step] * (1f - t * t);
		if (dir.Y == 0f && !player.GetModPlayer<KiPlayer>().Flying)
			v.Y = player.velocity.Y; // keep gravity on the ground
		sp.ForceVelocity(v);
		return true;
	}

	public override void OnEnd(SkillPlayer sp, bool cancelled)
	{
		if (cancelled || sp.ComboStep >= 3) {
			sp.ComboStep = 0;
			sp.ComboTimer = 0;
			return;
		}
		sp.ComboTimer = ComboWindow;
	}

	public override void PoseArms(SkillPlayer sp)
	{
		// Jabs alternate arms; the launcher is an uppercut.
		Vector2 aim = sp.Aim;
		if (sp.ComboStep == 3)
			aim = new Vector2(Math.Sign(aim.X == 0 ? sp.Player.direction : aim.X) * 0.5f, -1f);
		float rotation = aim.ToRotation() - MathHelper.PiOver2;
		if (sp.ComboStep == 2)
			sp.Player.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, rotation);
		else
			sp.Player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rotation);
	}
}
