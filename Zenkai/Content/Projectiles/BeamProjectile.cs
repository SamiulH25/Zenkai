using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using Zenkai.Common.Players;
using Zenkai.Content.DamageClasses;
using Zenkai.Content.Dusts;
using Zenkai.Content.Skills;

namespace Zenkai.Content.Projectiles;

/// <summary>
/// The charged beam, start to finish. ai[0] is the state (charging, firing, fading), ai[1] the
/// charge 0..1, both kept on ai so they can be synced later. While firing it ray-casts from the
/// hands each tick to find its length, hits along that line, and draws a start cap, a tiled body
/// and an end cap. It follows the owner's <see cref="SkillPlayer"/> and fades once the skill ends.
/// </summary>
public class BeamProjectile : ModProjectile
{
	public const int StateCharging = 0;
	public const int StateFiring = 1;
	public const int StateFading = 2;

	const float MaxLength = 1100f;
	const float Step = 4f;
	const float HitWidth = 22f;
	const int FadeTicks = 9;
	const int AnimTicks = 4;
	const float HandReach = 18f;

	static Asset<Texture2D> orb, start, body, end;

	float Length { get => Projectile.localAI[0]; set => Projectile.localAI[0] = value; }
	int fade;

	public override string Texture => "Zenkai/Assets/Projectiles/BeamBody";

	public override void Load()
	{
		if (Main.dedServ)
			return;
		orb = ModContent.Request<Texture2D>("Zenkai/Assets/Projectiles/BeamOrb");
		start = ModContent.Request<Texture2D>("Zenkai/Assets/Projectiles/BeamStart");
		body = ModContent.Request<Texture2D>("Zenkai/Assets/Projectiles/BeamBody");
		end = ModContent.Request<Texture2D>("Zenkai/Assets/Projectiles/BeamEnd");
	}

	public override void Unload() => orb = start = body = end = null;

	public override void SetDefaults()
	{
		Projectile.width = Projectile.height = 16;
		Projectile.friendly = true;
		Projectile.DamageType = ModContent.GetInstance<KiDamageClass>();
		Projectile.penetrate = -1;
		Projectile.tileCollide = false;
		Projectile.ignoreWater = true;
		Projectile.usesLocalNPCImmunity = true;
		Projectile.localNPCHitCooldown = 10; // six hits a second
		Projectile.timeLeft = 2;
		Projectile.hide = false;
	}

	public override bool ShouldUpdatePosition() => false;
	public override bool? CanCutTiles() => false;

	Vector2 Direction => Projectile.velocity.SafeNormalize(Vector2.UnitX);

	public override void AI()
	{
		Player owner = Main.player[Projectile.owner];
		SkillPlayer sp = owner.GetModPlayer<SkillPlayer>();
		bool ours = sp.ActiveProjectile == Projectile.whoAmI && sp.Active is ChargedBeam && owner.active && !owner.dead;
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
		Projectile.velocity = sp.Aim;
		Projectile.Center = owner.MountedCenter + new Vector2(0f, -2f) + sp.Aim * HandReach;
		Projectile.frameCounter++;

		if (Projectile.ai[0] == StateCharging) {
			float c = Projectile.ai[1];
			Lighting.AddLight(Projectile.Center, new Vector3(0.2f, 0.55f, 1f) * (0.5f + c));
			// Motes drawn in toward the orb.
			if (Main.rand.NextFloat() < 0.35f + c * 0.4f) {
				Vector2 off = Main.rand.NextVector2CircularEdge(30f, 30f);
				Dust.NewDustPerfect(Projectile.Center + off, ModContent.DustType<KiSpark>(), -off * 0.1f);
			}
			return;
		}

		// Firing: find where the beam hits a solid tile.
		Vector2 dir = Direction;
		float length = 0f;
		while (length < MaxLength) {
			Vector2 p = Projectile.Center + dir * (length + Step);
			if (Collision.SolidCollision(p - new Vector2(2f), 4, 4))
				break;
			length += Step;
		}
		Length = length;

		Vector2 tip = Projectile.Center + dir * length;
		for (float d = 0f; d < length; d += 48f)
			Lighting.AddLight(Projectile.Center + dir * d, 0.25f, 0.6f, 1.1f);
		if (length < MaxLength && Main.rand.NextBool(2))
			Dust.NewDustPerfect(tip, ModContent.DustType<KiSpark>(), (-dir).RotatedByRandom(1.2) * Main.rand.NextFloat(2f, 5f));
	}

	public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
	{
		if (Projectile.ai[0] != StateFiring)
			return false;
		float point = 0f;
		Vector2 tip = Projectile.Center + Direction * Length;
		return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, tip, HitWidth, ref point);
	}

	public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
	{
		// Push away from the caster, not from the projectile's tiny hitbox at the hands.
		modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Main.player[Projectile.owner].Center.X);
	}

	public override bool PreDraw(ref Color lightColor)
	{
		int frame = (Projectile.frameCounter / AnimTicks) % 3;
		Vector2 origin = (Projectile.Center - Main.screenPosition).Floor();
		Color color = Color.White;
		if (Projectile.ai[0] == StateFading)
			color *= 1f - (fade / 3) / 3f; // three flat steps, not a smooth fade

		// Orb at the hands while charging, sized by charge.
		if (Projectile.ai[0] == StateCharging) {
			Texture2D tex = orb.Value;
			int cell = tex.Width / 3;
			int size = Projectile.ai[1] < 0.34f ? 0 : Projectile.ai[1] < 0.99f ? 1 : 2;
			var src = new Rectangle(cell * size, cell * (frame % 2), cell, cell);
			Main.EntitySpriteDraw(tex, origin, src, color, 0f, new Vector2(cell / 2f), 1f, SpriteEffects.None);
			return false;
		}

		float rotation = Direction.ToRotation();
		Vector2 dir = Direction;
		Texture2D bodyTex = body.Value;
		int bw = bodyTex.Width;
		int bh = bodyTex.Height / 3;
		Texture2D startTex = start.Value;
		int sw = startTex.Width;
		int sh = startTex.Height / 3;

		// Body: from the end of the start cap to the tip, tiled; the last tile is cut to fit.
		for (float d = sw / 2f; d < Length; d += bw) {
			int w = (int)Math.Min(bw, Length - d);
			if (w <= 0)
				break;
			var src = new Rectangle(0, bh * frame, w, bh);
			Main.EntitySpriteDraw(bodyTex, origin + dir * d, src, color, rotation, new Vector2(0f, bh / 2f), 1f, SpriteEffects.None);
		}

		// Start cap centred on the hands.
		Main.EntitySpriteDraw(startTex, origin, new Rectangle(0, sh * frame, sw, sh), color, rotation, new Vector2(sw / 2f, sh / 2f), 1f, SpriteEffects.None);

		// End cap: the splash where it hits, or a rounded head in open air.
		Texture2D endTex = end.Value;
		int eh = endTex.Height / 3;
		Main.EntitySpriteDraw(endTex, origin + dir * Length, new Rectangle(0, eh * frame, endTex.Width, eh), color, rotation,
			new Vector2(endTex.Width / 2f, eh / 2f), 1f, SpriteEffects.None);
		return false;
	}
}
