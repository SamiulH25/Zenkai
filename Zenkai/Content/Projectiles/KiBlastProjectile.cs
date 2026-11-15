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

/// <summary>The Ki Blast bolt: flies straight, bursts on the first thing it hits.</summary>
public class KiBlastProjectile : ModProjectile
{
	const int Frames = 3;
	const int TicksPerFrame = 3;

	public override string Texture => "Zenkai/Assets/Projectiles/" + Name;

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
