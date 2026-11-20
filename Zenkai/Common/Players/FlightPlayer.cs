using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ModLoader;
using Zenkai.Common.Systems;

namespace Zenkai.Common.Players;

/// <summary>
/// Ki flight, and the one place that overrides the player's velocity. Flight keeps its own velocity
/// so vanilla gravity never accumulates, drains ki through <see cref="KiPlayer"/>, and tilts the body
/// toward the direction of travel. Skills move the player through <see cref="ForcedVelocity"/> so
/// the order of ModPlayer hooks does not matter.
/// </summary>
public class FlightPlayer : ModPlayer
{
	// Tuning, per second unless noted.
	const float DrainMoving = 5f;
	const float DrainHover = 2f;
	const float Speed = 7f;        // px/tick
	const float Accel = 0.14f;
	// Flight pose: the body tilts toward the direction of travel, head first, so level flight
	// is nearly horizontal and rising straight up is upright. Descending stays feet first with
	// a smaller forward lean.
	const float TiltScale = 0.9f;      // 1 would lie fully flat in level flight
	const float DescentTilt = 0.5f;    // share of the tilt kept while descending
	const float PoseMinSpeed = 1.5f;   // px/tick; slower than this is hovering, upright
	const float TiltEase = 0.18f;
	const float HoverPeriod = 72f; // ticks per gentle hover/breathing cycle
	const float TravelPeriod = 40f;
	const float HoverBob = 2f;    // draw pixels only, never moves the hitbox
	const float TravelBob = 0.8f;

	public bool Flying { get; private set; }
	/// <summary>True while flying head first; the aura then trails from the feet.</summary>
	public bool FlyingHeadFirst { get; private set; }

	/// <summary>Multiplies flight speed this tick. Skills lower it while charging, forms raise it. Reset every tick.</summary>
	public float SpeedMultiplier = 1f;

	/// <summary>Set by skills to drive the player's velocity for one tick, on the ground or in flight. Cleared after use.</summary>
	public Vector2? ForcedVelocity;

	/// <summary>Shot runner only: the flight direction, in place of the movement keys.</summary>
	public Vector2? DevFlightInput;

	// Flight keeps its own velocity so vanilla gravity, which is added before PreUpdateMovement, never accumulates.
	Vector2 velocity;
	float tilt;
	float animationPhase;
	float travelBlend;
	bool jumpHeld;

	public override void ResetEffects() => SpeedMultiplier = 1f;

	public override void ProcessTriggers(TriggersSet triggersSet)
	{
		if (ZenkaiKeybinds.ToggleFlight.JustPressed)
			SetFlying(!Flying);
	}

	public void SetFlying(bool on)
	{
		KiPlayer ki = Player.GetModPlayer<KiPlayer>();
		if (on && (!ki.CanUseKi || ki.Ki <= 0f))
			return;
		if (on && !Flying)
			velocity = Player.velocity;
		Flying = on;
		if (!on) {
			Player.fullRotation = 0f;
			tilt = 0f;
			animationPhase = travelBlend = 0f;
			Player.legRotation = 0f;
			FlyingHeadFirst = false;
		}
	}

	/// <summary>Replaces the flight velocity, so a teleport-style skill does not carry the old motion.</summary>
	public void SetVelocity(Vector2 value) => velocity = value;

	public override void SetControls()
	{
		jumpHeld = Player.controlJump;
		// Jump means "up" in flight. Taking it away from vanilla stops wings, rocket boots and
		// extra jumps from firing (and burning their time) under the flight.
		if (Flying)
			Player.controlJump = false;
	}

	Vector2 Input()
	{
		if (DevFlightInput is Vector2 dev)
			return dev;
		if (Player.GetModPlayer<KiPlayer>().Charging)
			return Vector2.Zero;
		var dir = new Vector2(
			(Player.controlRight ? 1 : 0) - (Player.controlLeft ? 1 : 0),
			(Player.controlDown ? 1 : 0) - (Player.controlUp || jumpHeld ? 1 : 0));
		return dir == Vector2.Zero ? dir : Vector2.Normalize(dir);
	}

	public override void PostUpdateMiscEffects()
	{
		if (!Flying)
			return;
		KiPlayer ki = Player.GetModPlayer<KiPlayer>();
		if (!ki.CanUseKi || ki.Ki <= 0f) {
			SetFlying(false);
			return;
		}
		// Charging in the air costs nothing; it is already the price of standing still.
		if (!ki.Charging && !ki.TryDrain(Input() == Vector2.Zero ? DrainHover : DrainMoving))
			SetFlying(false);
	}

	public override void PreUpdateMovement()
	{
		// A skill (lunge, dash, hit pause) owns movement this tick.
		if (ForcedVelocity is Vector2 forced) {
			ForcedVelocity = null;
			Player.velocity = forced;
			Player.fallStart = (int)(Player.position.Y / 16f);
			if (Flying) {
				velocity = forced;
				UpdatePose();
			}
			return;
		}

		if (!Flying)
			return;
		Vector2 input = Input();
		velocity = Vector2.Lerp(velocity, input * Speed * SpeedMultiplier, Accel);
		if (input == Vector2.Zero && velocity.LengthSquared() < 0.01f)
			velocity = Vector2.Zero;
		Player.velocity = velocity;
		if (input.X != 0f)
			Player.direction = Math.Sign(input.X);

		// No fall damage from flight.
		Player.fallStart = (int)(Player.position.Y / 16f);
		UpdatePose();
	}

	void UpdatePose()
	{
		float target = 0f;
		bool headFirst = false;
		if (!Player.GetModPlayer<KiPlayer>().Charging && velocity.Length() >= PoseMinSpeed) {
			// Angle of travel measured from straight up, clockwise: right is +pi/2.
			float a = (float)Math.Atan2(velocity.X, -velocity.Y);
			if (Math.Abs(a) <= MathHelper.PiOver2) {
				target = a * TiltScale;
				headFirst = true;
			}
			else {
				target = Math.Sign(a) * (MathHelper.Pi - Math.Abs(a)) * TiltScale * DescentTilt;
			}
		}
		tilt = MathHelper.Lerp(tilt, target, TiltEase);
		FlyingHeadFirst = headFirst;
		Player.fullRotation = tilt;
		Player.fullRotationOrigin = Player.Size / 2f;
	}

	public override void UpdateDead()
	{
		Flying = false;
		FlyingHeadFirst = false;
		ForcedVelocity = null;
		Player.fullRotation = Player.legRotation = 0f;
		tilt = animationPhase = travelBlend = 0f;
	}

	public override void PostUpdate()
	{
		if (!Flying)
			return;
		// Tile collision zeroes the blocked axis; carry that into the flight velocity.
		if (Player.velocity.X == 0f)
			velocity.X = 0f;
		if (Player.velocity.Y == 0f)
			velocity.Y = 0f;

		float moving = MathHelper.Clamp((velocity.Length() - PoseMinSpeed) / (Speed - PoseMinSpeed), 0f, 1f);
		travelBlend = MathHelper.Lerp(travelBlend, moving, 0.12f);
		animationPhase = (animationPhase + MathHelper.TwoPi / MathHelper.Lerp(HoverPeriod, TravelPeriod, travelBlend)) % MathHelper.TwoPi;
	}

	public override void FrameEffects()
	{
		if (!Flying || Player.dead)
			return;
		float breath = (float)Math.Sin(animationPhase);
		float follow = (float)Math.Sin(animationPhase - 0.8f);
		// Bent knees at rest, long legs in a glide; limbs lag the body's breathing/bank.
		Player.legFrame.Y = travelBlend < 0.5f ? Player.legFrame.Height * 5 : 0;
		Player.legRotation = follow * MathHelper.Lerp(0.10f, 0.06f, travelBlend) * Player.direction;
		Player.fullRotation = tilt + breath * 0.025f * Player.direction;

		// Skills and held items own their arms. Charging also keeps its normal pose.
		if (Player.itemAnimation != 0 || Player.GetModPlayer<KiPlayer>().Charging
			|| Player.GetModPlayer<SkillPlayer>().State != SkillState.Idle)
			return;
		Player.bodyFrame.Y = 0;
		float spread = MathHelper.Lerp(0.38f, 0.16f, travelBlend);
		Player.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters,
			(spread + follow * 0.14f) * Player.direction);
		Player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters,
			(-spread + breath * 0.14f) * Player.direction);
	}

	public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
	{
		if (Flying && !Player.dead)
			drawInfo.Position.Y += (float)Math.Sin(animationPhase) * MathHelper.Lerp(HoverBob, TravelBob, travelBlend);
	}
}
