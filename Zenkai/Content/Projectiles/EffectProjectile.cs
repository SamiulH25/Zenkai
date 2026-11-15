using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Zenkai.Content.Projectiles;

/// <summary>
/// A visual-only projectile that plays its sheet once and dies: muzzle flashes, impact bursts,
/// hit sparks. Frames are stacked vertically. Drawn on whole pixels so the 2x art stays crisp.
/// ai[0] is the rotation; ai[1] = 1 follows the owner's hand offset given at spawn.
/// </summary>
public abstract class EffectProjectile : ModProjectile
{
	protected abstract int FrameCount { get; }
	protected virtual int TicksPerFrame => 3;
	protected virtual Color Light => new(0.2f, 0.55f, 1f);
	/// <summary>Draw rotated to ai[0]. Off for round effects so they stay on the pixel grid.</summary>
	protected virtual bool Rotates => false;
	/// <summary>Tint for the sprite. Grey sprites take their colour from here.</summary>
	protected virtual Color DrawColor => Color.White;

	public override string Texture => "Zenkai/Assets/Projectiles/" + Name;

	public override void SetStaticDefaults()
	{
		Main.projFrames[Type] = FrameCount;
	}

	public override void SetDefaults()
	{
		Projectile.width = Projectile.height = 8;
		Projectile.friendly = false;
		Projectile.hostile = false;
		Projectile.tileCollide = false;
		Projectile.ignoreWater = true;
		Projectile.penetrate = -1;
		Projectile.timeLeft = FrameCount * TicksPerFrame;
	}

	public override bool ShouldUpdatePosition() => false;

	public override void AI()
	{
		Projectile.frame = System.Math.Min(FrameCount - 1, (FrameCount * TicksPerFrame - Projectile.timeLeft) / TicksPerFrame);
		float fade = Projectile.timeLeft / (float)(FrameCount * TicksPerFrame);
		Lighting.AddLight(Projectile.Center, Light.ToVector3() * fade);
	}

	public override bool PreDraw(ref Color lightColor)
	{
		Texture2D tex = TextureAssets.Projectile[Type].Value;
		int h = tex.Height / FrameCount;
		var source = new Rectangle(0, h * Projectile.frame, tex.Width, h);
		Vector2 origin = new Vector2(tex.Width, h) / 2f;
		Vector2 pos = (Projectile.Center - Main.screenPosition).Floor();
		Main.EntitySpriteDraw(tex, pos, source, DrawColor, Rotates ? Projectile.ai[0] : 0f, origin, 1f, SpriteEffects.None);
		return false;
	}
}

/// <summary>The ring that bursts out when a transformation completes. ai[1] is the form's type, for its colour.</summary>
public class FormBurst : EffectProjectile
{
	protected override int FrameCount => 4;
	protected override int TicksPerFrame => 4;

	Common.Forms.ModForm Form
	{
		get {
			int type = (int)Projectile.ai[1];
			var forms = Common.Forms.FormLoader.Forms;
			return type >= 0 && type < forms.Count ? forms[type] : null;
		}
	}

	protected override Color DrawColor => Form?.Aura.Hot ?? Color.White;
	protected override Color Light => Form?.Aura.Hot ?? Color.White;
}

/// <summary>Muzzle flash at the hand when a blast is fired.</summary>
public class KiFlash : EffectProjectile
{
	protected override int FrameCount => 3;
	protected override int TicksPerFrame => 2;
}

/// <summary>Burst where a ki blast hits.</summary>
public class KiBurst : EffectProjectile
{
	protected override int FrameCount => 4;
}

/// <summary>Spark where a melee strike lands, rotated toward the hit.</summary>
public class StrikeSpark : EffectProjectile
{
	protected override int FrameCount => 3;
	protected override bool Rotates => true;
	protected override Color Light => new(0.9f, 0.85f, 0.6f);
}
