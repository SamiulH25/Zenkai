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
/// Blink a short way in the held direction (facing direction if none) with a moment of
/// invulnerability. Stops short of solid tiles. Leaves an after-image where you were.
/// </summary>
public class Vanish : ModSkill
{
	const float Distance = 176f; // 11 tiles
	const float Step = 4f;
	const int ImmuneTicks = 30;

	public override void SetStaticDefaults()
	{
		Kind = SkillKind.Movement;
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

	/// <summary>The direction override for the shot runner, which cannot hold movement keys.</summary>
	public static Vector2? DevDirection;

	public override void OnRelease(SkillPlayer sp, float charge)
	{
		Player player = sp.Player;
		Vector2 dir = DevDirection ?? HeldDirection(player);
		Vector2 from = player.position;

		// Walk the path and stop at the last spot the hitbox fits.
		Vector2 to = from;
		for (float d = Step; d <= Distance; d += Step) {
			Vector2 next = from + dir * d;
			if (Collision.SolidCollision(next, player.width, player.height))
				break;
			to = next;
		}

		Spawn(sp, player.Center, Vector2.Zero, ModContent.ProjectileType<VanishAfterimage>(), 0f, 0f, player.direction);

		// Streak of sparks along the path.
		Vector2 size = new(player.width, player.height);
		for (float d = 0f; d < (to - from).Length(); d += 12f)
			Dust.NewDustPerfect(from + size / 2f + dir * d + Main.rand.NextVector2Circular(6f, 12f), ModContent.DustType<KiSpark>(), dir * 0.5f);

		player.position = to;
		player.GetModPlayer<KiPlayer>().SetFlightVelocity(Vector2.Zero);
		player.velocity *= 0.3f;
		player.fallStart = (int)(player.position.Y / 16f);
		player.immune = true;
		player.immuneNoBlink = true;
		player.immuneTime = ImmuneTicks;
		SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.5f, Volume = 0.7f }, player.Center);
	}

	public override void PoseArms(SkillPlayer sp) { }
}
