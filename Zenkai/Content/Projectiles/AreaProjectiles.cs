using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Zenkai.Common.Players;
using Zenkai.Content.DamageClasses;
using Zenkai.Content.Dusts;
using Zenkai.Content.Skills;

namespace Zenkai.Content.Projectiles;

/// <summary>
/// An expanding shock ring that hits everything inside it once: the Explosive Wave, and the blast
/// of a Spirit Bomb. ai[0] is the size multiplier (0 means 1); the hit radius follows the ring.
/// </summary>
public class KiWave : ModProjectile
{
	const int Frames = 4;
	const int TicksPerFrame = 4;
	const int Life = Frames * TicksPerFrame;
	/// <summary>Ring radius in px at each frame, at size 1 (matches art/skills.lua).</summary>
	static readonly float[] Radius = { 36f, 60f, 80f, 92f };

	public override string Texture => "Zenkai/Assets/Projectiles/KiWave";

	public override void SetStaticDefaults()
	{
		Main.projFrames[Type] = Frames;
	}

	public override void SetDefaults()
	{
		Projectile.width = Projectile.height = 16;
		Projectile.friendly = true;
		Projectile.DamageType = ModContent.GetInstance<KiDamageClass>();
		Projectile.penetrate = -1;
		Projectile.tileCollide = false;
		Projectile.ignoreWater = true;
		Projectile.usesLocalNPCImmunity = true;
		Projectile.localNPCHitCooldown = -1;
		Projectile.timeLeft = Life;
	}

	float Size => Projectile.ai[0] <= 0f ? 1f : Projectile.ai[0];

	public override bool ShouldUpdatePosition() => false;
	public override bool? CanCutTiles() => false;

	public override void AI()
	{
		Projectile.frame = Math.Min(Frames - 1, (Life - Projectile.timeLeft) / TicksPerFrame);
		Lighting.AddLight(Projectile.Center, new Vector3(0.3f, 0.7f, 1.2f) * (Projectile.timeLeft / (float)Life) * Size);
	}

	public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
	{
		float r = Radius[Projectile.frame] * Size;
		Vector2 nearest = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
		return Vector2.DistanceSquared(nearest, Projectile.Center) <= r * r;
	}

	public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
	{
		modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);
	}

	public override bool PreDraw(ref Color lightColor)
	{
		Texture2D tex = TextureAssets.Projectile[Type].Value;
		int h = tex.Height / Frames;
		var source = new Rectangle(0, h * Projectile.frame, tex.Width, h);
		// Whole-number sizes keep the art on a pixel grid.
		Main.EntitySpriteDraw(tex, (Projectile.Center - Main.screenPosition).Floor(), source, Color.White, 0f,
			new Vector2(tex.Width, h) / 2f, MathF.Round(Size * 2f) / 2f, SpriteEffects.None);
		return false;
	}
}

/// <summary>
/// The Spirit Bomb. ai[0] is the state (gathering, thrown), ai[1] the size 0..1. While gathering it
/// hangs over the owner and grows; thrown, it drifts toward where it was aimed and bursts into a
/// large <see cref="KiWave"/> on the first enemy or tile it meets.
/// </summary>
public class SpiritBombProjectile : ModProjectile
{
	public const int StateGathering = 0;
	public const int StateThrown = 1;
	/// <summary>Height of the sphere's centre above the owner's head while gathering, in px.</summary>
	public const float HoldHeight = 56f;

	const int Sizes = 3;
	const int FlickerTicks = 5;
	const float BlastSize = 2f;
	const int ThrownLife = 240;

	public override string Texture => "Zenkai/Assets/Projectiles/SpiritBomb";

	public override void SetDefaults()
	{
		Projectile.width = Projectile.height = 40;
		Projectile.friendly = true;
		Projectile.DamageType = ModContent.GetInstance<KiDamageClass>();
		Projectile.penetrate = 1;
		Projectile.tileCollide = false;
		Projectile.ignoreWater = true;
		Projectile.timeLeft = 2;
	}

	bool Thrown => Projectile.ai[0] == StateThrown;
	int SizeIndex => Projectile.ai[1] < 0.4f ? 0 : Projectile.ai[1] < 0.8f ? 1 : 2;

	public override bool? CanDamage() => Thrown ? null : false;
	public override bool? CanCutTiles() => false;
	public override bool ShouldUpdatePosition() => Thrown;

	public override void AI()
	{
		Projectile.frameCounter++;
		Lighting.AddLight(Projectile.Center, new Vector3(0.5f, 0.85f, 1.2f) * (0.6f + Projectile.ai[1]));

		if (!Thrown) {
			Player owner = Main.player[Projectile.owner];
			SkillPlayer sp = owner.GetModPlayer<SkillPlayer>();
			if (!owner.active || owner.dead || sp.ActiveProjectile != Projectile.whoAmI || sp.Active is not SpiritBomb) {
				Projectile.Kill();
				return;
			}
			Projectile.timeLeft = 2;
			Projectile.Center = owner.Top + new Vector2(0f, -HoldHeight);
			// Ki drifting up into the sphere from all around.
			Vector2 off = Main.rand.NextVector2CircularEdge(150f, 110f);
			off.Y = Math.Abs(off.Y);
			Dust.NewDustPerfect(Projectile.Center + off, ModContent.DustType<KiSpark>(), -off * 0.035f);
			return;
		}

		if (Projectile.localAI[0] == 0f) {
			Projectile.localAI[0] = 1f;
			Projectile.timeLeft = ThrownLife;
			Projectile.tileCollide = true;
		}
	}

	public override void OnKill(int timeLeft)
	{
		if (!Thrown)
			return;
		SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.5f, Volume = 1f }, Projectile.Center);
		SoundEngine.PlaySound(SoundID.Item62 with { Pitch = -0.3f }, Projectile.Center);
		if (Projectile.owner == Main.myPlayer) {
			int blast = Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<KiWave>(),
				Projectile.damage, Projectile.knockBack, Projectile.owner, BlastSize);
			Main.projectile[blast].CritChance = Projectile.CritChance;
			Main.player[Projectile.owner].GetModPlayer<SkillPlayer>().Shake(12f, 30);
		}
		for (int i = 0; i < 40; i++)
			Dust.NewDustPerfect(Projectile.Center, ModContent.DustType<KiSpark>(), Main.rand.NextVector2Circular(9f, 9f));
	}

	public override bool PreDraw(ref Color lightColor)
	{
		Texture2D tex = TextureAssets.Projectile[Type].Value;
		int cell = tex.Width / Sizes;
		var source = new Rectangle(cell * SizeIndex, cell * (Projectile.frameCounter / FlickerTicks % 2), cell, cell);
		Main.EntitySpriteDraw(tex, (Projectile.Center - Main.screenPosition).Floor(), source, Color.White, 0f,
			new Vector2(cell / 2f), 1f, SpriteEffects.None);
		return false;
	}
}

/// <summary>The shell of the Ki Barrier: rides on the owner while the skill is sustained. Visual only.</summary>
public class KiBarrierBubble : ModProjectile
{
	const int Frames = 3;
	const int TicksPerFrame = 5;

	public override string Texture => "Zenkai/Assets/Projectiles/KiBarrier";

	public override void SetStaticDefaults()
	{
		Main.projFrames[Type] = Frames;
	}

	public override void SetDefaults()
	{
		Projectile.width = Projectile.height = 8;
		Projectile.friendly = false;
		Projectile.tileCollide = false;
		Projectile.ignoreWater = true;
		Projectile.penetrate = -1;
		Projectile.timeLeft = 2;
	}

	public override bool ShouldUpdatePosition() => false;

	public override void AI()
	{
		Player owner = Main.player[Projectile.owner];
		SkillPlayer sp = owner.GetModPlayer<SkillPlayer>();
		if (!owner.active || owner.dead || sp.ActiveProjectile != Projectile.whoAmI || sp.Active is not KiBarrier) {
			Projectile.Kill();
			return;
		}
		Projectile.timeLeft = 2;
		Projectile.Center = owner.Center;
		if (++Projectile.frameCounter >= TicksPerFrame) {
			Projectile.frameCounter = 0;
			Projectile.frame = (Projectile.frame + 1) % Frames;
		}
		Lighting.AddLight(Projectile.Center, 0.2f, 0.5f, 0.9f);
	}

	public override bool PreDraw(ref Color lightColor)
	{
		Texture2D tex = TextureAssets.Projectile[Type].Value;
		int h = tex.Height / Frames;
		Main.EntitySpriteDraw(tex, (Projectile.Center - Main.screenPosition).Floor(), new Rectangle(0, h * Projectile.frame, tex.Width, h),
			Color.White, 0f, new Vector2(tex.Width, h) / 2f, 1f, SpriteEffects.None);
		return false;
	}
}
