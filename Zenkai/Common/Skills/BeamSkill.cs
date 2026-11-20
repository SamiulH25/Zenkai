using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Zenkai.Common.Players;
using Zenkai.Content.Projectiles;

namespace Zenkai.Common.Skills;

/// <summary>
/// A charged beam: hold to gather ki at the hands (slowed, can aim), release to fire, rooted and
/// turning slowly while ki lasts. One <see cref="BeamProjectile"/> owns the orb and the beam and
/// reads its look and reach from the fields here, so a new beam is a subclass that sets numbers
/// plus four sprites named &lt;AssetPrefix&gt;Orb, Start, Body and End.
/// </summary>
public abstract class BeamSkill : ModSkill
{
	/// <summary>Sustain length in ticks at no charge, and the extra at full charge.</summary>
	public int SustainMin = 60, SustainBonus = 90;
	/// <summary>Extra damage at full charge (1 = double).</summary>
	public float MaxChargeDamageBonus = 1f;
	/// <summary>Sprite set under Assets/Projectiles.</summary>
	public string AssetPrefix = "Beam";
	public float MaxLength = 1100f;
	/// <summary>Width of the damaging line in px.</summary>
	public float HitWidth = 22f;
	/// <summary>Ticks between hits on the same enemy.</summary>
	public int HitCooldown = 10;
	/// <summary>Goes through solid tiles instead of stopping at them.</summary>
	public bool PiercesTiles;
	public Color LightColor = new(64, 153, 255);
	/// <summary>Screen shake when fired, at no charge and the extra at full charge.</summary>
	public float FireShake = 4f, FireShakeBonus = 4f;

	protected void BeamDefaults()
	{
		Kind = SkillKind.Beam;
		// Fires for a time set by the charge, while ki lasts; pressing the button again ends it early.
		HoldToSustain = false;
		ChargeMoveSpeed = 0.4f;
		RootWhileSustaining = true;
		SustainTurnRate = 0.035f;
		Recovery = 14;
		Cooldown = 60;
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
		Projectile beam = Spawn(sp, HandPosition(sp, BeamProjectile.HandReach), sp.Aim, ModContent.ProjectileType<BeamProjectile>(), ai2: Type);
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
		sp.Shake(FireShake + FireShakeBonus * charge, 18);
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
