using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Zenkai.Common.Players;
using Zenkai.Common.Skills;
using Zenkai.Content.DamageClasses;
using Zenkai.Content.Dusts;

namespace Zenkai.Content.Projectiles;

/// <summary>
/// A charged beam, start to finish, for any <see cref="BeamSkill"/>. ai[0] is the state (charging,
/// firing, fading), ai[1] the charge 0..1, ai[2] the skill's type; all kept on ai so they can be
/// synced later. While firing it ray-casts from the hands each tick to find its length, hits along
/// that line, and draws a start cap, a tiled body and an end cap from the skill's sprite set.
/// It follows the owner's <see cref="SkillPlayer"/> and fades once the skill ends.
/// </summary>
public class BeamProjectile : ModProjectile
{
	public const int StateCharging = 0;
	public const int StateFiring = 1;
	public const int StateFading = 2;
	public const float HandReach = 18f;

	const float Step = 4f;
	const int FadeTicks = 9;
	const int AnimTicks = 4;
	const int Frames = 3;

	float Length { get => Projectile.localAI[0]; set => Projectile.localAI[0] = value; }
	int fade;

	public override string Texture => "Zenkai/Assets/Projectiles/BeamBody";

	BeamSkill Skill => SkillLoader.Get((int)Projectile.ai[2]) as BeamSkill;

	static Texture2D Piece(BeamSkill skill, string part) =>
		ModContent.Request<Texture2D>($"Zenkai/Assets/Projectiles/{skill.AssetPrefix}{part}").Value;

	public override void SetDefaults()
	{
		Projectile.width = Projectile.height = 16;
		Projectile.friendly = true;
		Projectile.DamageType = ModContent.GetInstance<KiDamageClass>();
		Projectile.penetrate = -1;
		Projectile.tileCollide = false;
		Projectile.ignoreWater = true;
		Projectile.usesLocalNPCImmunity = true;
		Projectile.localNPCHitCooldown = 10;
		Projectile.timeLeft = 2;
	}

	public override bool ShouldUpdatePosition() => false;
	public override bool? CanCutTiles() => false;

	Vector2 Direction => Projectile.velocity.SafeNormalize(Vector2.UnitX);

	void Spark(BeamSkill skill, Vector2 position, Vector2 velocity)
	{
		if (skill.AssetPrefix == "Beam")
			Dust.NewDustPerfect(position, ModContent.DustType<KiSpark>(), velocity);
		else
			Dust.NewDustPerfect(position, ModContent.DustType<TintSpark>(), velocity, 0, skill.LightColor);
	}

	public override void AI()
	{
		Player owner = Main.player[Projectile.owner];
		SkillPlayer sp = owner.GetModPlayer<SkillPlayer>();
		BeamSkill skill = Skill;
		bool ours = skill != null && sp.ActiveProjectile == Projectile.whoAmI && sp.Active == skill && owner.active && !owner.dead;
		if (!ours && Projectile.ai[0] != StateFading)
			Projectile.ai[0] = StateFading;

		if (Projectile.ai[0] == StateFading) {
			if (++fade >= FadeTicks) {
				Projectile.Kill();
				return;
			}
			Projectile.timeLeft = 2;
			return;
		}

		Projectile.timeLeft = 2;
		Projectile.localNPCHitCooldown = skill.HitCooldown;
		Projectile.velocity = sp.Aim;
		Projectile.Center = ModSkill.HandPosition(sp, HandReach);
		Projectile.frameCounter++;
		Vector3 light = skill.LightColor.ToVector3();

		if (Projectile.ai[0] == StateCharging) {
			float c = Projectile.ai[1];
			Lighting.AddLight(Projectile.Center, light * (0.5f + c));
			// Motes drawn in toward the orb.
			if (Main.rand.NextFloat() < 0.35f + c * 0.4f) {
				Vector2 off = Main.rand.NextVector2CircularEdge(30f, 30f);
				Spark(skill, Projectile.Center + off, -off * 0.1f);
			}
			return;
		}

		// Firing: find where the beam hits a solid tile.
		Vector2 dir = Direction;
		float length = 0f;
		if (skill.PiercesTiles) {
			length = skill.MaxLength;
		}
		else {
			while (length < skill.MaxLength) {
				Vector2 p = Projectile.Center + dir * (length + Step);
				if (Collision.SolidCollision(p - new Vector2(2f), 4, 4))
					break;
				length += Step;
			}
		}
		Length = length;

		Vector2 tip = Projectile.Center + dir * length;
		for (float d = 0f; d < length; d += 48f)
			Lighting.AddLight(Projectile.Center + dir * d, light * 1.1f);
		if (length < skill.MaxLength && Main.rand.NextBool(2))
			Spark(skill, tip, (-dir).RotatedByRandom(1.2) * Main.rand.NextFloat(2f, 5f));
	}

	public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
	{
		if (Projectile.ai[0] != StateFiring || Skill is not BeamSkill skill)
			return false;
		float point = 0f;
		Vector2 tip = Projectile.Center + Direction * Length;
		return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, tip, skill.HitWidth, ref point);
	}

	public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
	{
		// Push away from the caster, not from the projectile's tiny hitbox at the hands.
		modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Main.player[Projectile.owner].Center.X);
	}

	public override bool PreDraw(ref Color lightColor)
	{
		if (Skill is not BeamSkill skill)
			return false;
		int frame = (Projectile.frameCounter / AnimTicks) % Frames;
		Vector2 origin = (Projectile.Center - Main.screenPosition).Floor();
		Color color = Color.White;
		if (Projectile.ai[0] == StateFading)
			color *= 1f - (fade / 3) / 3f; // three flat steps, not a smooth fade

		// Orb at the hands while charging, sized by charge.
		if (Projectile.ai[0] == StateCharging) {
			Texture2D tex = Piece(skill, "Orb");
			int cell = tex.Width / 3;
			int size = Projectile.ai[1] < 0.34f ? 0 : Projectile.ai[1] < 0.99f ? 1 : 2;
			var src = new Rectangle(cell * size, cell * (frame % 2), cell, cell);
			Main.EntitySpriteDraw(tex, origin, src, color, 0f, new Vector2(cell / 2f), 1f, SpriteEffects.None);
			return false;
		}

		float rotation = Direction.ToRotation();
		Vector2 dir = Direction;
		Texture2D bodyTex = Piece(skill, "Body");
		int bw = bodyTex.Width;
		int bh = bodyTex.Height / Frames;
		Texture2D startTex = Piece(skill, "Start");
		int sw = startTex.Width;
		int sh = startTex.Height / Frames;

		// Body: from the end of the start cap to the tip, tiled; the last tile is cut to fit.
		for (float d = sw / 2f; d < Length; d += bw) {
			// Off-screen tiles of a long beam are skipped.
			Vector2 at = origin + dir * d;
			if (at.X < -64f || at.Y < -64f || at.X > Main.screenWidth + 64f || at.Y > Main.screenHeight + 64f)
				continue;
			int w = (int)Math.Min(bw, Length - d);
			if (w <= 0)
				break;
			var src = new Rectangle(0, bh * frame, w, bh);
			Main.EntitySpriteDraw(bodyTex, at, src, color, rotation, new Vector2(0f, bh / 2f), 1f, SpriteEffects.None);
		}

		// Start cap centred on the hands.
		Main.EntitySpriteDraw(startTex, origin, new Rectangle(0, sh * frame, sw, sh), color, rotation, new Vector2(sw / 2f, sh / 2f), 1f, SpriteEffects.None);

		// End cap: the splash where it hits, or a rounded head in open air.
		Texture2D endTex = Piece(skill, "End");
		int eh = endTex.Height / Frames;
		Main.EntitySpriteDraw(endTex, origin + dir * Length, new Rectangle(0, eh * frame, endTex.Width, eh), color, rotation,
			new Vector2(endTex.Width / 2f, eh / 2f), 1f, SpriteEffects.None);
		return false;
	}
}
