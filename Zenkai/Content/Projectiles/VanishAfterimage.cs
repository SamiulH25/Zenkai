using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Zenkai.Content.Projectiles;

/// <summary>
/// The after-image Vanish leaves behind: the owner redrawn where they stood, in the pose they had,
/// tinted ki blue and fading out in a few flat steps. No sprite of its own; ai[0] is the facing at
/// the moment of the blink. The tint is applied by <see cref="AfterimageTintPlayer"/> while
/// <see cref="Tint"/> is set.
/// </summary>
public class VanishAfterimage : ModProjectile
{
	const int Life = 24;
	const int Steps = 3;
	static readonly Color[] StepTints = {
		new(110, 200, 255, 200),
		new(70, 150, 240, 140),
		new(40, 100, 220, 80),
	};

	/// <summary>Colour to draw the player in, or null when not drawing an after-image.</summary>
	internal static Color? Tint;

	public override string Texture => "Zenkai/Assets/Projectiles/KiFlash"; // never drawn

	public override void SetDefaults()
	{
		Projectile.width = Projectile.height = 8;
		Projectile.friendly = false;
		Projectile.tileCollide = false;
		Projectile.ignoreWater = true;
		Projectile.penetrate = -1;
		Projectile.timeLeft = Life;
	}

	public override bool ShouldUpdatePosition() => false;

	public override void AI()
	{
		// Remember the pose on the first tick, before the player's frame moves on.
		if (Projectile.localAI[2] == 0f) {
			Player owner = Main.player[Projectile.owner];
			Projectile.localAI[0] = owner.bodyFrame.Y;
			Projectile.localAI[1] = owner.legFrame.Y;
			Projectile.localAI[2] = 1f;
		}
	}

	public override bool PreDraw(ref Color lightColor)
	{
		Player owner = Main.player[Projectile.owner];
		if (!owner.active || owner.dead || Projectile.localAI[2] == 0f)
			return false;

		int step = System.Math.Min(Steps - 1, (Life - Projectile.timeLeft) * Steps / Life);
		Rectangle body = owner.bodyFrame, legs = owner.legFrame;
		int direction = owner.direction;
		float rotation = owner.fullRotation;
		var compositeFront = owner.compositeFrontArm;
		var compositeBack = owner.compositeBackArm;

		owner.bodyFrame.Y = (int)Projectile.localAI[0];
		owner.legFrame.Y = (int)Projectile.localAI[1];
		owner.direction = Projectile.ai[0] == 0f ? owner.direction : (int)Projectile.ai[0];
		owner.fullRotation = 0f;
		owner.compositeFrontArm.enabled = false;
		owner.compositeBackArm.enabled = false;
		Tint = StepTints[step];
		try {
			Main.PlayerRenderer.DrawPlayer(Main.Camera, owner, Projectile.Center - owner.Size / 2f, 0f, owner.Size / 2f);
		}
		finally {
			Tint = null;
			owner.bodyFrame = body;
			owner.legFrame = legs;
			owner.direction = direction;
			owner.fullRotation = rotation;
			owner.compositeFrontArm = compositeFront;
			owner.compositeBackArm = compositeBack;
		}
		return false;
	}
}

/// <summary>Recolours the whole player while an after-image is being drawn.</summary>
public class AfterimageTintPlayer : ModPlayer
{
	public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
	{
		if (VanishAfterimage.Tint is not Color c)
			return;
		drawInfo.colorHair = drawInfo.colorEyeWhites = drawInfo.colorEyes = drawInfo.colorHead = c;
		drawInfo.colorBodySkin = drawInfo.colorLegs = drawInfo.colorShirt = drawInfo.colorUnderShirt = c;
		drawInfo.colorPants = drawInfo.colorShoes = c;
		drawInfo.colorArmorHead = drawInfo.colorArmorBody = drawInfo.colorArmorLegs = c;
		drawInfo.headGlowColor = drawInfo.bodyGlowColor = drawInfo.armGlowColor = drawInfo.legsGlowColor = c;
	}
}
