using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Zenkai.Common.Players;
using Zenkai.Common.Skills;
using Zenkai.Content.Projectiles;

namespace Zenkai.Content.Skills;

/// <summary>The basic attack: a fast ki bolt from the leading hand toward the cursor. Hold to keep firing.</summary>
public class KiBlast : ModSkill
{
	public const float Speed = 13f;

	public override void SetStaticDefaults()
	{
		Kind = SkillKind.Blast;
		KnownFromStart = true;
		KiCost = 5f;
		Cooldown = 12;
		Recovery = 10; // keeps the arm out after the shot
		BaseDamage = 12;
		Knockback = 2f;
		AutoRepeat = true;
	}

	public override void OnRelease(SkillPlayer sp, float charge) => Fire(this, sp, ModContent.ProjectileType<KiBlastProjectile>(), sp.Aim * Speed, 0.35f, 0.6f);

	/// <summary>Throws a bolt from the hand with a muzzle flash, a shot sound and a little recoil on the ground.</summary>
	public static void Fire(ModSkill skill, SkillPlayer sp, int type, Vector2 velocity, float pitch, float recoil)
	{
		Player player = sp.Player;
		Vector2 hand = HandPosition(sp);
		skill.Spawn(sp, hand, velocity, type);
		skill.Spawn(sp, hand, Vector2.Zero, ModContent.ProjectileType<KiFlash>(), 0f, 0f);
		SoundEngine.PlaySound(SoundID.Item12 with { Pitch = pitch, Volume = 0.6f, PitchVariance = 0.15f }, hand);
		if (!player.GetModPlayer<FlightPlayer>().Flying)
			player.velocity.X -= sp.Aim.X * recoil;
	}
}

/// <summary>A stream of small bolts, both hands alternating, loose but relentless. Hold to keep firing.</summary>
public class Barrage : ModSkill
{
	const float Speed = 15f;
	const float Spread = 0.13f; // radians either side of the aim

	/// <summary>Which hand throws next, per player.</summary>
	public class Hands { public bool Back; }

	public override void SetStaticDefaults()
	{
		Kind = SkillKind.Blast;
		Tier = 2;
		KiCost = 2.5f;
		Cooldown = 4;
		Recovery = 5;
		BaseDamage = 7;
		Knockback = 0.5f;
		AutoRepeat = true;
	}

	public override bool LearnCondition(SkillPlayer sp) => Main.hardMode;

	public override void OnRelease(SkillPlayer sp, float charge)
	{
		Hands hands = sp.Data<Hands>(this);
		hands.Back = !hands.Back;
		Vector2 velocity = sp.Aim.RotatedBy(Main.rand.NextFloat(-Spread, Spread)) * Speed;
		KiBlast.Fire(this, sp, ModContent.ProjectileType<BarrageBolt>(), velocity, 0.7f, 0.15f);
	}

	public override void PoseArms(SkillPlayer sp)
	{
		float rotation = sp.Aim.ToRotation() - MathHelper.PiOver2;
		if (sp.Data<Hands>(this).Back)
			sp.Player.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, rotation);
		else
			sp.Player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rotation);
	}
}

/// <summary>A slower bolt that bends toward the nearest enemy.</summary>
public class HomingBlast : ModSkill
{
	const float Speed = 9f;

	public override void SetStaticDefaults()
	{
		Kind = SkillKind.Blast;
		Tier = 1;
		KiCost = 9f;
		Cooldown = 30;
		Recovery = 10;
		BaseDamage = 20;
		Knockback = 3f;
		AutoRepeat = true;
	}

	public override bool LearnCondition(SkillPlayer sp) => NPC.downedBoss2;

	public override void OnRelease(SkillPlayer sp, float charge) => KiBlast.Fire(this, sp, ModContent.ProjectileType<HomingBolt>(), sp.Aim * Speed, 0f, 0.6f);
}

/// <summary>A spinning disc of ki that cuts through every enemy in its path and curves toward the next.</summary>
public class EnergyDisc : ModSkill
{
	const float Speed = 12f;

	public override void SetStaticDefaults()
	{
		Kind = SkillKind.Blast;
		Tier = 2;
		KiCost = 35f;
		Cooldown = 90;
		Recovery = 14;
		BaseDamage = 38;
		Knockback = 1f;
		ArmorPenetration = 15;
	}

	public override bool LearnCondition(SkillPlayer sp) => Main.hardMode;

	public override void OnRelease(SkillPlayer sp, float charge)
	{
		Spawn(sp, HandPosition(sp) + new Vector2(0f, -10f), sp.Aim * Speed, ModContent.ProjectileType<EnergyDiscProjectile>());
		SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.4f, Volume = 0.7f }, sp.Player.Center);
	}

	public override void PoseArms(SkillPlayer sp)
	{
		// Thrown from overhead.
		Vector2 aim = Vector2.Lerp(new Vector2(0f, -1f), sp.Aim, MathHelper.Clamp(sp.StateTicks / 6f, 0f, 1f));
		sp.Player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, aim.ToRotation() - MathHelper.PiOver2);
	}
}
