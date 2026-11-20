using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Zenkai.Content.DamageClasses;
using Zenkai.Content.Dusts;

namespace Zenkai.Content.Projectiles;

/// <summary>The Ki Blast bolt: flies straight, bursts on the first thing it hits. Base of the other bolts.</summary>
public class KiBlastProjectile : ModProjectile
{
	protected const int Frames = 3;
	const int TicksPerFrame = 3;

	public override string Texture => "Zenkai/Assets/Projectiles/KiBlastProjectile";

	public override void SetStaticDefaults()
	{
		Main.projFrames[Type] = Frames;
	}

	public override void SetDefaults()
	{
		Projectile.width = Projectile.height = 12;
		Projectile.friendly = true;
		Projectile.DamageType = ModContent.GetInstance<KiDamageClass>();
		Projectile.penetrate = 1;
		Projectile.timeLeft = 70;
		Projectile.tileCollide = true;
		Projectile.ignoreWater = true;
		Projectile.extraUpdates = 1;
	}

	public override bool? CanCutTiles() => false;

	public override void AI()
	{
		Projectile.rotation = Projectile.velocity.ToRotation();
		if (++Projectile.frameCounter >= TicksPerFrame * (1 + Projectile.extraUpdates)) {
			Projectile.frameCounter = 0;
			Projectile.frame = (Projectile.frame + 1) % Frames;
		}
		Lighting.AddLight(Projectile.Center, 0.2f, 0.55f, 1f);
	}

	public override void OnKill(int timeLeft)
	{
		SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);
		if (Projectile.owner == Main.myPlayer)
			Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<KiBurst>(), 0, 0f, Projectile.owner);
		for (int i = 0; i < 5; i++)
			Dust.NewDustPerfect(Projectile.Center, ModContent.DustType<KiSpark>(), Main.rand.NextVector2Circular(3f, 3f));
	}

	public override bool PreDraw(ref Color lightColor)
	{
		Texture2D tex = TextureAssets.Projectile[Type].Value;
		int h = tex.Height / Frames;
		var source = new Rectangle(0, h * Projectile.frame, tex.Width, h);
		// The bright head sits at the right of the sprite; the tail trails left.
		var origin = new Vector2(tex.Width - h / 2f, h / 2f);
		Main.EntitySpriteDraw(tex, (Projectile.Center - Main.screenPosition).Floor(), source, Color.White,
			Projectile.rotation, origin, 1f, SpriteEffects.None);
		return false;
	}
}

/// <summary>A Barrage bolt: the same bolt, short-lived.</summary>
public class BarrageBolt : KiBlastProjectile
{
	public override void SetDefaults()
	{
		base.SetDefaults();
		Projectile.timeLeft = 44;
	}
}

/// <summary>A bolt that turns toward the nearest enemy it can see.</summary>
public class HomingBolt : KiBlastProjectile
{
	const float SeekRange = 420f;
	const float TurnRate = 0.09f; // radians per update
	const int StraightTicks = 10;

	public override void SetDefaults()
	{
		base.SetDefaults();
		Projectile.timeLeft = 150;
	}

	public override void AI()
	{
		if (++Projectile.localAI[0] > StraightTicks && Seek.Nearest(Projectile, SeekRange) is NPC target)
			Seek.TurnToward(Projectile, target.Center, TurnRate);
		base.AI();
	}
}

/// <summary>Target finding and steering shared by the homing projectiles.</summary>
public static class Seek
{
	public static NPC Nearest(Projectile projectile, float range)
	{
		NPC best = null;
		float bestDistance = range;
		foreach (NPC npc in Main.ActiveNPCs) {
			if (!npc.CanBeChasedBy(projectile))
				continue;
			float d = Vector2.Distance(npc.Center, projectile.Center);
			if (d < bestDistance && Collision.CanHitLine(projectile.Center, 1, 1, npc.Center, 1, 1)) {
				best = npc;
				bestDistance = d;
			}
		}
		return best;
	}

	/// <summary>Turns the velocity toward a point by at most <paramref name="rate"/> radians, keeping speed.</summary>
	public static void TurnToward(Projectile projectile, Vector2 point, float rate)
	{
		float speed = projectile.velocity.Length();
		float from = projectile.velocity.ToRotation();
		float delta = MathHelper.WrapAngle((point - projectile.Center).ToRotation() - from);
		projectile.velocity = (from + MathHelper.Clamp(delta, -rate, rate)).ToRotationVector2() * speed;
	}
}

/// <summary>
/// The Energy Disc: a flat spinning blade that passes through enemies, hitting each a few times a
/// second, and curves gently toward the next one. Breaks on tiles.
/// </summary>
public class EnergyDiscProjectile : ModProjectile
{
	const int Frames = 3;
	const int TicksPerFrame = 2;
	const float SeekRange = 360f;
	const float TurnRate = 0.035f;

	public override string Texture => "Zenkai/Assets/Projectiles/EnergyDisc";

	public override void SetStaticDefaults()
	{
		Main.projFrames[Type] = Frames;
	}

	public override void SetDefaults()
	{
		Projectile.width = Projectile.height = 20;
		Projectile.friendly = true;
		Projectile.DamageType = ModContent.GetInstance<KiDamageClass>();
		Projectile.penetrate = 8;
		Projectile.timeLeft = 150;
		Projectile.tileCollide = true;
		Projectile.ignoreWater = true;
		Projectile.usesLocalNPCImmunity = true;
		Projectile.localNPCHitCooldown = 12;
	}

	public override void AI()
	{
		if (Seek.Nearest(Projectile, SeekRange) is NPC target)
			Seek.TurnToward(Projectile, target.Center, TurnRate);
		if (++Projectile.frameCounter >= TicksPerFrame) {
			Projectile.frameCounter = 0;
			Projectile.frame = (Projectile.frame + 1) % Frames;
		}
		Lighting.AddLight(Projectile.Center, 0.25f, 0.6f, 1f);
		if (Main.rand.NextBool(3))
			Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 4f), ModContent.DustType<KiSpark>(), -Projectile.velocity * 0.1f);
	}

	public override void OnKill(int timeLeft)
	{
		SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.6f, Pitch = 0.5f }, Projectile.Center);
		if (Projectile.owner == Main.myPlayer)
			Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<KiBurst>(), 0, 0f, Projectile.owner);
	}

	public override bool PreDraw(ref Color lightColor)
	{
		// Drawn flat and unrotated: the disc is a blade seen edge-on, and stays on the pixel grid.
		Texture2D tex = TextureAssets.Projectile[Type].Value;
		int h = tex.Height / Frames;
		var source = new Rectangle(0, h * Projectile.frame, tex.Width, h);
		SpriteEffects fx = Projectile.velocity.X < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
		Main.EntitySpriteDraw(tex, (Projectile.Center - Main.screenPosition).Floor(), source, Color.White, 0f,
			new Vector2(tex.Width / 2f, h / 2f), 1f, fx);
		return false;
	}
}
