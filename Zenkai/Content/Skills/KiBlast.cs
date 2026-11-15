using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Zenkai.Common.Players;
using Zenkai.Common.Skills;
using Zenkai.Content.Projectiles;

namespace Zenkai.Content.Skills;

/// <summary>The basic tap attack: a fast ki bolt from the leading hand toward the cursor. Hold to keep firing.</summary>
public class KiBlast : ModSkill
{
	const float Speed = 13f;
	const float HandReach = 16f;

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

	public override void OnRelease(SkillPlayer sp, float charge)
	{
		Player player = sp.Player;
		Vector2 hand = HandPosition(sp);
		Spawn(sp, hand, sp.Aim * Speed, ModContent.ProjectileType<KiBlastProjectile>());
		Spawn(sp, hand, Vector2.Zero, ModContent.ProjectileType<KiFlash>(), 0f, 0f);
		SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.35f, Volume = 0.6f, PitchVariance = 0.15f }, hand);
		if (player.GetModPlayer<KiPlayer>().Flying)
			return;
		// A little recoil on the ground.
		player.velocity.X -= sp.Aim.X * 0.6f;
	}

	public static Vector2 HandPosition(SkillPlayer sp) => sp.Player.MountedCenter + new Vector2(0f, -2f) + sp.Aim * HandReach;
}
