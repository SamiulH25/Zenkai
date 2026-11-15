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
/// The signature beam. Hold to charge (slowed, can aim), release to fire; hold to keep it going
/// while ki lasts, rooted and turning slowly. One <see cref="BeamProjectile"/> owns the orb and the beam.
/// </summary>
public class ChargedBeam : ModSkill
{
	const int SustainMin = 60;   // ticks at no charge
	const int SustainBonus = 90; // extra ticks at full charge
	const float MaxChargeDamageBonus = 1f; // full charge doubles damage

	public override void SetStaticDefaults()
	{
		Kind = SkillKind.Beam;
		Tier = 1;
		KiCost = 20f;
		SustainDrain = 15f;
		MinCharge = 30;
		MaxCharge = 150;
		Cooldown = 60;
		Recovery = 14;
		BaseDamage = 14;
		Knockback = 1f;
		// Fires for a time set by the charge, while ki lasts; pressing the key again ends it early.
		HoldToSustain = false;
		ChargeMoveSpeed = 0.4f;
		RootWhileSustaining = true;
		SustainTurnRate = 0.035f;
	}

	public override int SustainTicks(SkillPlayer sp, float charge) => SustainMin + (int)(SustainBonus * charge);

	static Projectile Beam(SkillPlayer sp)
	{
		if (sp.ActiveProjectile < 0)
			return null;
		Projectile p = Main.projectile[sp.ActiveProjectile];
		return p.active && p.ModProjectile is BeamProjectile ? p : null;
	}

	public override void OnStart(SkillPlayer sp)
	{
		Projectile beam = Spawn(sp, KiBlast.HandPosition(sp), sp.Aim, ModContent.ProjectileType<BeamProjectile>());
		sp.ActiveProjectile = beam?.whoAmI ?? -1;
		SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.6f, Volume = 0.6f }, sp.Player.Center);
	}

	public override void OnChargeTick(SkillPlayer sp)
	{
		if (Beam(sp) is Projectile beam)
			beam.ai[1] = sp.ChargeFraction;
		// A hum that rises as the charge builds, and a ping when it is full.
		if (sp.StateTicks % 24 == 0 && sp.ChargeTicks < MaxCharge)
			SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.6f + sp.ChargeFraction * 1.1f, Volume = 0.55f }, sp.Player.Center);
		if (sp.ChargeTicks == MaxCharge && sp.StateTicks == MaxCharge)
			SoundEngine.PlaySound(SoundID.MaxMana, sp.Player.Center);
	}

	public override void OnRelease(SkillPlayer sp, float charge)
	{
		if (Beam(sp) is Projectile beam) {
			beam.ai[0] = BeamProjectile.StateFiring;
			beam.ai[1] = charge;
			beam.damage = DamageFor(sp.Player, 1f + MaxChargeDamageBonus * charge);
			beam.knockBack = KnockbackFor(sp.Player);
		}
		SoundEngine.PlaySound(SoundID.Item12 with { Pitch = -0.7f, Volume = 1f }, sp.Player.Center);
		SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f, Volume = 0.5f }, sp.Player.Center);
		sp.Shake(4f + 4f * charge, 18);
	}

	public override bool OnSustainTick(SkillPlayer sp)
	{
		if (Beam(sp) == null)
			return false;
		if (sp.StateTicks % 18 == 0)
			SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.2f, Volume = 0.45f }, sp.Player.Center);
		if (sp.StateTicks % 12 == 0)
			sp.Shake(1.5f, 10);
		return true;
	}

	public override void OnEnd(SkillPlayer sp, bool cancelled)
	{
		if (Beam(sp) is Projectile beam)
			beam.ai[0] = BeamProjectile.StateFading;
	}

	public override void PoseArms(SkillPlayer sp)
	{
		// Both hands forward, cupped together.
		float rotation = sp.Aim.ToRotation() - MathHelper.PiOver2;
		sp.Player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rotation);
		sp.Player.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, rotation);
	}
}
