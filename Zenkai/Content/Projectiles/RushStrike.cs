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
using Zenkai.Content.Skills;

namespace Zenkai.Content.Projectiles;

/// <summary>
/// The hitbox and swoosh of one melee strike. Rides along in front of the owner for a few ticks.
/// ai[0] is the strike step (1-2 jab, 3 launcher, 4 chase hit, 5 heavy strike); velocity holds the
/// strike direction.
/// </summary>
public class RushStrike : ModProjectile
{
	const int Life = 8;
	const int Frames = 3;
	const float Reach = 22f;
	const float LaunchLift = 9f;

	public override string Texture => "Zenkai/Assets/Projectiles/" + Name;

	public override void SetStaticDefaults()
	{
		Main.projFrames[Type] = Frames;
	}

	public override void SetDefaults()
	{
		Projectile.width = Projectile.height = 40;
		Projectile.friendly = true;
		Projectile.DamageType = ModContent.GetInstance<KiDamageClass>();
		Projectile.penetrate = -1;
		Projectile.tileCollide = false;
		Projectile.ignoreWater = true;
		Projectile.usesLocalNPCImmunity = true;
		Projectile.localNPCHitCooldown = -1; // each strike hits each enemy once
		Projectile.timeLeft = Life;
		Projectile.ownerHitCheck = true; // no hitting through walls
	}

	public override bool ShouldUpdatePosition() => false;
	public override bool? CanCutTiles() => false;

	int Step => (int)Projectile.ai[0];

	public override void AI()
	{
		Player owner = Main.player[Projectile.owner];
		Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX * owner.direction);
		Projectile.Center = owner.MountedCenter + dir * Reach;
		Projectile.rotation = dir.ToRotation();
		Projectile.frame = Math.Min(Frames - 1, (Life - Projectile.timeLeft) * Frames / Life);
	}

	public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
	{
		modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Main.player[Projectile.owner].Center.X);
	}

	public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
	{
		Player owner = Main.player[Projectile.owner];
		SkillPlayer sp = owner.GetModPlayer<SkillPlayer>();
		bool heavy = Step >= Rush.StepLauncher;
		sp.HitPause = heavy ? 5 : 3;
		sp.Shake(heavy ? 5f : 2f, heavy ? 14 : 8);
		if (heavy)
			SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.3f }, target.Center);

		if (Step == Rush.StepLauncher) {
			// Launcher: send it up as well as away, then open the chase window.
			if (!target.boss && target.knockBackResist > 0f) {
				target.velocity.Y = -LaunchLift * target.knockBackResist;
				target.netUpdate = true;
			}
			Rush.Combo combo = Rush.ComboOf(sp);
			combo.ChaseTarget = target.whoAmI;
			combo.ChaseTimer = Rush.ChaseWindow;
		}

		if (Projectile.owner == Main.myPlayer) {
			Vector2 at = Vector2.Lerp(Projectile.Center, target.Center, 0.5f);
			Projectile.NewProjectile(Projectile.GetSource_FromThis(), at, Vector2.Zero, ModContent.ProjectileType<StrikeSpark>(),
				0, 0f, Projectile.owner, Projectile.rotation);
		}
	}

	public override bool PreDraw(ref Color lightColor)
	{
		Texture2D tex = TextureAssets.Projectile[Type].Value;
		int h = tex.Height / Frames;
		var src = new Rectangle(0, h * Projectile.frame, tex.Width, h);
		bool flip = Math.Abs(Projectile.rotation) > MathHelper.PiOver2;
		// Second jab swings the other way.
		SpriteEffects fx = (flip ^ Step == 2) ? SpriteEffects.FlipVertically : SpriteEffects.None;
		Main.EntitySpriteDraw(tex, (Projectile.Center - Main.screenPosition).Floor(), src, Color.White,
			Projectile.rotation, new Vector2(tex.Width / 2f, h / 2f), 1f, fx);
		return false;
	}
}
