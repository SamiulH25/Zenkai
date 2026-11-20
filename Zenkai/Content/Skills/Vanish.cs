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

/// <summary>
/// Blink a short way in the tapped or held direction (facing direction if none) with a moment of
/// invulnerability. Stops short of solid tiles. Leaves an after-image where you were.
/// In the ki stance a double tap of a direction uses it.
/// </summary>
public class Vanish : ModSkill
{
	const float Distance = 176f; // 11 tiles
	const float Step = 4f;
	const int ImmuneTicks = 30;

	public override void SetStaticDefaults()
	{
		Kind = SkillKind.Movement;
		KnownFromStart = true;
		KiCost = 15f;
		Cooldown = 60;
		Recovery = 4;
	}

	static Vector2 HeldDirection(Player player)
	{
		var dir = new Vector2(
			(player.controlRight ? 1 : 0) - (player.controlLeft ? 1 : 0),
			(player.controlDown ? 1 : 0) - (player.controlUp ? 1 : 0));
		return dir == Vector2.Zero ? new Vector2(player.direction, 0f) : Vector2.Normalize(dir);
	}

	public override void OnRelease(SkillPlayer sp, float charge)
	{
		Player player = sp.Player;
		Vector2 dir = sp.DevMoveDirection ?? sp.TapDirection ?? HeldDirection(player);
		Vector2 from = player.position;

		// Walk the path and stop at the last spot the hitbox fits.
		Vector2 to = from;
		for (float d = Step; d <= Distance; d += Step) {
			Vector2 next = from + dir * d;
			if (Collision.SolidCollision(next, player.width, player.height))
				break;
			to = next;
		}
		Blink(this, sp, to, ImmuneTicks, 0.5f);
	}

	/// <summary>Moves the player to a spot at once: after-image behind, a streak of sparks along the
	/// way, a moment of invulnerability on arrival. Shared by the teleport skills.</summary>
	public static void Blink(ModSkill skill, SkillPlayer sp, Vector2 to, int immuneTicks, float pitch)
	{
		Player player = sp.Player;
		Vector2 from = player.position;
		Vector2 path = to - from;
		Vector2 dir = path.SafeNormalize(Vector2.UnitX);

		skill.Spawn(sp, player.Center, Vector2.Zero, ModContent.ProjectileType<VanishAfterimage>(), 0f, 0f, player.direction);

		Vector2 half = player.Size / 2f;
		for (float d = 0f; d < path.Length(); d += 12f)
			Dust.NewDustPerfect(from + half + dir * d + Main.rand.NextVector2Circular(6f, 12f), ModContent.DustType<KiSpark>(), dir * 0.5f);

		player.position = to;
		player.GetModPlayer<FlightPlayer>().SetVelocity(Vector2.Zero);
		player.velocity *= 0.3f;
		player.fallStart = (int)(player.position.Y / 16f);
		player.immune = true;
		player.immuneNoBlink = true;
		player.immuneTime = immuneTicks;
		SoundEngine.PlaySound(SoundID.Item8 with { Pitch = pitch, Volume = 0.7f }, player.Center);
	}

	public override void PoseArms(SkillPlayer sp) { }
}

/// <summary>Step straight to the cursor from far away, through anything in between.</summary>
public class InstantMovement : ModSkill
{
	const float MaxRange = 640f; // 40 tiles
	const float Step = 8f;
	const int ImmuneTicks = 20;

	public override void SetStaticDefaults()
	{
		Kind = SkillKind.Movement;
		Tier = 3;
		KiCost = 40f;
		Cooldown = 180;
		Recovery = 6;
	}

	public override bool LearnCondition(SkillPlayer sp) => NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3;

	/// <summary>The free spot nearest the cursor on the line back toward the player, within range.</summary>
	static bool FindSpot(SkillPlayer sp, out Vector2 spot)
	{
		Player player = sp.Player;
		Vector2 offset = sp.AimPoint - player.Center;
		if (offset.Length() > MaxRange)
			offset = offset.SafeNormalize(Vector2.UnitX) * MaxRange;
		Vector2 dir = offset.SafeNormalize(Vector2.UnitX);
		for (float d = offset.Length(); d >= 32f; d -= Step) {
			spot = player.position + dir * d;
			if (!Collision.SolidCollision(spot, player.width, player.height))
				return true;
		}
		spot = player.position;
		return false;
	}

	public override bool CanUse(SkillPlayer sp) => FindSpot(sp, out _);

	public override void OnRelease(SkillPlayer sp, float charge)
	{
		if (FindSpot(sp, out Vector2 spot))
			Vanish.Blink(this, sp, spot, ImmuneTicks, 0.9f);
	}

	public override void PoseArms(SkillPlayer sp) { }
}
