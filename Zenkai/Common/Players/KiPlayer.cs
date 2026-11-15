using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Zenkai.Common.Systems;
using Zenkai.Common.Visuals;
using Zenkai.Content.Dusts;

namespace Zenkai.Common.Players;

/// <summary>
/// All per-player ki state: the ki pool, charging and flight.
/// Singleplayer only for now; nothing here is synced yet (see docs/PLAN.md, phase 8).
/// </summary>
public class KiPlayer : ModPlayer
{
	public const int StartingMaxKi = 100;

	// Tuning, per second unless noted.
	const float NaturalRegen = 1.5f;
	const float ChargeFractionOfMax = 0.25f; // a full charge from empty takes 4 seconds
	const float FlightDrainMoving = 5f;
	const float FlightDrainHover = 2f;
	const float FlightSpeed = 7f;
	const float FlightAccel = 0.14f;
	const float ChargeDamageTaken = 1.25f;
	const int RegenDelayAfterSpend = 90; // ticks
	// Flight pose: the body tilts toward the direction of travel, head first, so level flight
	// is nearly horizontal and rising straight up is upright. Descending stays feet first with
	// a smaller forward lean.
	const float FlightTiltScale = 0.9f;      // 1 would lie fully flat in level flight
	const float FlightDescentTilt = 0.5f;    // share of the tilt kept while descending
	const float FlightPoseMinSpeed = 1.5f;   // px/tick; slower than this is hovering, upright
	const float FlightTiltEase = 0.18f;
	const float FlightAuraOpacity = 0.85f;
	const int SparksPerTick = 1;

	/// <summary>Permanent max ki, saved with the character.</summary>
	public int MaxKiBase = StartingMaxKi;
	/// <summary>Max ki after this tick's bonuses. Rebuilt in ResetEffects.</summary>
	public int MaxKi = StartingMaxKi;
	public float Ki = StartingMaxKi;

	public bool Charging { get; private set; }
	public bool Flying { get; private set; }
	/// <summary>How the aura looks. Transformations will swap this for their own style.</summary>
	public AuraStyle AuraStyle = AuraStyle.Ki;
	/// <summary>True while flying head first; the aura then trails from the feet.</summary>
	public bool FlyingHeadFirst { get; private set; }
	/// <summary>0 to 1, eased. Drives the aura and the bar glow.</summary>
	public float AuraOpacity { get; private set; }

	// Test hooks used by Dev/ShotRunner to pose the player without real input.
	public bool DevHoldCharge;
	public Vector2? DevFlightInput;

	bool chargeHeld;
	int regenDelay;
	// Flight keeps its own velocity so vanilla gravity, which is added before PreUpdateMovement, never accumulates.
	Vector2 flightVelocity;
	float flightTilt;

	/// <summary>Multiplies flight speed this tick. Skills lower it while charging. Reset every tick.</summary>
	public float FlightSpeedMultiplier = 1f;

	/// <summary>Aura opacity when neither charging nor flying. Forms raise it so the aura stays up. Reset every tick.</summary>
	public float IdleAuraOpacity;
	/// <summary>Multiplies natural ki regen this tick (exhaustion lowers it). Reset every tick.</summary>
	public float NaturalRegenMultiplier = 1f;

	public override void ResetEffects()
	{
		MaxKi = MaxKiBase;
		FlightSpeedMultiplier = 1f;
		IdleAuraOpacity = 0f;
		NaturalRegenMultiplier = 1f;
	}

	/// <summary>Moves the player by a teleport-style skill without the flight velocity carrying the old motion.</summary>
	public void SetFlightVelocity(Vector2 velocity) => flightVelocity = velocity;

	/// <summary>Set by skills to drive the player's velocity for one tick, on the ground or in flight.
	/// Cleared after use. Skills own movement through this so the order of ModPlayer hooks does not matter.</summary>
	public Vector2? ForcedVelocity;

	public override void SaveData(TagCompound tag)
	{
		tag["maxKiBase"] = MaxKiBase;
		tag["ki"] = Ki;
	}

	public override void LoadData(TagCompound tag)
	{
		MaxKiBase = tag.ContainsKey("maxKiBase") ? tag.GetInt("maxKiBase") : StartingMaxKi;
		Ki = tag.ContainsKey("ki") ? tag.GetFloat("ki") : MaxKiBase;
	}

	public override void ProcessTriggers(TriggersSet triggersSet)
	{
		chargeHeld = ZenkaiKeybinds.ChargeKi.Current;
		if (ZenkaiKeybinds.ToggleFlight.JustPressed)
			SetFlying(!Flying);
	}

	public bool CanUseKi => !Player.dead && !Player.CCed && !Player.mount.Active && Player.grappling[0] < 0 && !Player.pulley;

	public void SetFlying(bool on)
	{
		if (on && (!CanUseKi || Ki <= 0f))
			return;
		if (on && !Flying)
			flightVelocity = Player.velocity;
		Flying = on;
		if (!on) {
			Player.fullRotation = 0f;
			flightTilt = 0f;
			FlyingHeadFirst = false;
		}
	}

	/// <summary>Spends ki if there is enough. Returns false and spends nothing otherwise.</summary>
	public bool TrySpendKi(float amount)
	{
		if (Ki < amount)
			return false;
		Ki -= amount;
		regenDelay = RegenDelayAfterSpend;
		return true;
	}

	public override void SetControls()
	{
		// Charging roots the player: no walking, jumping or item use.
		if (!WantsCharge || !CanUseKi)
			return;
		Player.controlLeft = Player.controlRight = Player.controlJump = false;
		Player.controlUseItem = Player.controlUseTile = false;
	}

	bool WantsCharge => chargeHeld || DevHoldCharge;

	public override void PostUpdateMiscEffects()
	{
		Charging = WantsCharge && CanUseKi;

		if (Flying && (!CanUseKi || Ki <= 0f))
			SetFlying(false);

		if (Charging) {
			Ki += MaxKi * ChargeFractionOfMax / 60f;
			regenDelay = 0;
		}
		else if (Flying) {
			Ki -= (FlightInput() == Vector2.Zero ? FlightDrainHover : FlightDrainMoving) / 60f;
		}
		else if (regenDelay > 0) {
			regenDelay--;
		}
		else {
			Ki += NaturalRegen * NaturalRegenMultiplier / 60f;
		}

		Ki = Math.Clamp(Ki, 0f, MaxKi);

		float target = Charging ? 1f : Flying ? Math.Max(FlightAuraOpacity, IdleAuraOpacity) : IdleAuraOpacity;
		AuraOpacity = MathHelper.Lerp(AuraOpacity, target, 0.15f);
		if (AuraOpacity < 0.02f && target == 0f)
			AuraOpacity = 0f;
	}

	Vector2 FlightInput()
	{
		if (DevFlightInput is Vector2 dev)
			return dev;
		if (Charging)
			return Vector2.Zero;
		var dir = new Vector2(
			(Player.controlRight ? 1 : 0) - (Player.controlLeft ? 1 : 0),
			(Player.controlDown ? 1 : 0) - (Player.controlUp || Player.controlJump ? 1 : 0));
		return dir == Vector2.Zero ? dir : Vector2.Normalize(dir);
	}

	public override void PreUpdateMovement()
	{
		// A skill (lunge, dash, hit pause) owns movement this tick.
		if (ForcedVelocity is Vector2 forced) {
			ForcedVelocity = null;
			Player.velocity = forced;
			Player.fallStart = (int)(Player.position.Y / 16f);
			if (Flying) {
				flightVelocity = forced;
				UpdateFlightPose();
			}
			return;
		}

		if (Flying) {
			Vector2 input = FlightInput();
			flightVelocity = Vector2.Lerp(flightVelocity, input * FlightSpeed * FlightSpeedMultiplier, FlightAccel);
			if (input == Vector2.Zero && flightVelocity.LengthSquared() < 0.01f)
				flightVelocity = Vector2.Zero;
			Player.velocity = flightVelocity;
			if (input.X != 0f)
				Player.direction = Math.Sign(input.X);

			// No fall damage from flight.
			Player.fallStart = (int)(Player.position.Y / 16f);
			UpdateFlightPose();
		}
		else if (Charging) {
			Player.velocity.X *= 0.8f;
		}
	}

	void UpdateFlightPose()
	{
		float target = 0f;
		bool headFirst = false;
		if (!Charging && flightVelocity.Length() >= FlightPoseMinSpeed) {
			// Angle of travel measured from straight up, clockwise: right is +pi/2.
			float a = (float)Math.Atan2(flightVelocity.X, -flightVelocity.Y);
			if (Math.Abs(a) <= MathHelper.PiOver2) {
				target = a * FlightTiltScale;
				headFirst = true;
			}
			else {
				target = Math.Sign(a) * (MathHelper.Pi - Math.Abs(a)) * FlightTiltScale * FlightDescentTilt;
			}
		}
		flightTilt = MathHelper.Lerp(flightTilt, target, FlightTiltEase);
		FlyingHeadFirst = headFirst;
		Player.fullRotation = flightTilt;
		Player.fullRotationOrigin = Player.Size / 2f;
	}

	public override void ModifyHurt(ref Player.HurtModifiers modifiers)
	{
		if (Charging)
			modifiers.FinalDamage *= ChargeDamageTaken;
	}

	public override void UpdateDead()
	{
		Charging = false;
		Flying = false;
		AuraOpacity = 0f;
		Player.fullRotation = 0f;
	}

	public override void PostUpdate()
	{
		// Tile collision zeroes the blocked axis; carry that into the flight velocity.
		if (Flying) {
			if (Player.velocity.X == 0f)
				flightVelocity.X = 0f;
			if (Player.velocity.Y == 0f)
				flightVelocity.Y = 0f;
		}

		if (Main.dedServ || AuraOpacity <= 0f)
			return;

		Lighting.AddLight(Player.Center, 0.15f * AuraOpacity, 0.45f * AuraOpacity, 0.9f * AuraOpacity);

		// Flying pose: legs together (the standing frames) instead of vanilla's jump pose with the
		// arm thrown up and legs apart; the tilt comes from UpdateFlightPose. Item use keeps its arms.
		if (Flying) {
			Player.legFrame.Y = 0;
			if (Player.itemAnimation == 0)
				Player.bodyFrame.Y = 0;
		}

		// Sparks rising off the aura while charging.
		if (Charging) {
			for (int i = 0; i < SparksPerTick; i++) {
				// Off the flame's sides only: dust draws over the player, so sparks on the body hide the face.
				float side = Main.rand.NextBool() ? 1f : -1f;
				Vector2 pos = Player.Bottom + new Vector2(side * Main.rand.NextFloat(14f, 20f), Main.rand.NextFloat(-34f, -4f));
				Dust.NewDustPerfect(pos, ModContent.DustType<KiSpark>(), new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-4f, -2f)));
			}
		}
	}
}
