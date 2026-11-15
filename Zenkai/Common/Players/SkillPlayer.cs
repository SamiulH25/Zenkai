using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.Graphics.CameraModifiers;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Zenkai.Common.Skills;
using Zenkai.Common.Systems;
using Zenkai.Common.UI;

namespace Zenkai.Common.Players;

public enum SkillState { Idle, Charging, Sustaining, Recovery }

/// <summary>
/// Learned skills, the four-slot loadout, cooldowns and the active skill's state machine
/// (Idle, Charging, Sustaining, Recovery). One skill is active at a time.
/// Singleplayer only for now; the fields another client would need are plain values so they
/// can be synced in phase 8.
/// </summary>
public class SkillPlayer : ModPlayer
{
	public const int SlotCount = 4;
	/// <summary>A hit taken while charging cancels the charge.</summary>
	const bool HitCancelsCharge = true;

	/// <summary>Learned skills by full name. Unknown names (from a removed skill) are kept, not dropped.</summary>
	public readonly HashSet<string> Learned = new();
	/// <summary>Full names of the slotted skills; null for an empty slot.</summary>
	public readonly string[] Loadout = new string[SlotCount];

	public SkillState State { get; private set; }
	public ModSkill Active { get; private set; }
	public int ActiveSlot { get; private set; } = -1;
	/// <summary>Ticks spent in the current state.</summary>
	public int StateTicks { get; private set; }
	public int ChargeTicks { get; private set; }
	/// <summary>Charge progress 0..1 between the active skill's min and max charge.</summary>
	public float ChargeFraction { get; private set; }
	int sustainLimit;
	int recoveryLeft;

	/// <summary>Unit vector from the player toward where the skill aims.</summary>
	public Vector2 Aim = Vector2.UnitX;
	/// <summary>Index of a projectile the active skill owns (beam, strike), or -1.</summary>
	public int ActiveProjectile = -1;

	// Per-tick modifiers, reset in ResetEffects. Gear and forms will raise or lower these.
	public float KiCostMultiplier = 1f;
	/// <summary>Set by a skill each tick it wants contact damage ignored (melee lunges).</summary>
	public bool ContactProof;
	/// <summary>Ticks of hit pause left: the player freezes in place for a moment after a heavy hit.</summary>
	public int HitPause;

	// Skill-owned scratch state that outlives one activation (combo step, chase target).
	public int ComboStep;
	public int ComboTimer;
	public int ChaseTarget = -1;
	public int ChaseTimer;

	readonly int[] cooldowns = new int[64];

	// Input: the slot keys this tick, edge-detected here so dev input behaves the same way.
	readonly bool[] keyHeld = new bool[SlotCount];
	readonly bool[] held = new bool[SlotCount];
	readonly bool[] wasHeld = new bool[SlotCount];

	// Test hooks used by Dev/ShotRunner.
	public readonly bool[] DevSlotHeld = new bool[SlotCount];
	public Vector2? DevAimWorld;

	public bool IsActive => State != SkillState.Idle;

	public override void Initialize()
	{
		Learned.Clear();
		Array.Clear(Loadout);
		int slot = 0;
		foreach (ModSkill skill in SkillLoader.Ordered.Where(s => s.KnownFromStart)) {
			Learned.Add(skill.FullName);
			if (slot < SlotCount)
				Loadout[slot++] = skill.FullName;
		}
	}

	public override void SaveData(TagCompound tag)
	{
		tag["learned"] = Learned.ToList();
		tag["loadout"] = Loadout.Select(s => s ?? "").ToList();
	}

	public override void LoadData(TagCompound tag)
	{
		// Characters made before skills existed keep the defaults from Initialize.
		if (tag.ContainsKey("learned")) {
			Learned.Clear();
			foreach (string name in tag.GetList<string>("learned"))
				Learned.Add(name);
		}
		if (tag.ContainsKey("loadout")) {
			IList<string> loadout = tag.GetList<string>("loadout");
			for (int i = 0; i < SlotCount; i++)
				Loadout[i] = i < loadout.Count && loadout[i] != "" ? loadout[i] : null;
		}
	}

	public bool Knows(ModSkill skill) => skill != null && Learned.Contains(skill.FullName);

	public void Learn(ModSkill skill)
	{
		if (!Learned.Add(skill.FullName))
			return;
		int empty = Array.IndexOf(Loadout, null);
		if (empty >= 0 && !Loadout.Contains(skill.FullName))
			Loadout[empty] = skill.FullName;
	}

	public void Forget(ModSkill skill)
	{
		Learned.Remove(skill.FullName);
		for (int i = 0; i < SlotCount; i++) {
			if (Loadout[i] == skill.FullName)
				Loadout[i] = null;
		}
	}

	/// <summary>Puts a learned skill in a slot. A skill sits in one slot only; it moves if already slotted.</summary>
	public void SetSlot(int slot, ModSkill skill)
	{
		if (slot < 0 || slot >= SlotCount)
			return;
		if (skill != null) {
			if (!Knows(skill))
				return;
			for (int i = 0; i < SlotCount; i++) {
				if (Loadout[i] == skill.FullName)
					Loadout[i] = null;
			}
		}
		if (State != SkillState.Idle && slot == ActiveSlot)
			Cancel();
		Loadout[slot] = skill?.FullName;
	}

	public ModSkill SkillInSlot(int slot) => slot >= 0 && slot < SlotCount ? SkillLoader.Find(Loadout[slot]) : null;

	public int CooldownLeft(ModSkill skill) => skill == null ? 0 : cooldowns[skill.Type];

	/// <summary>Shot runner only.</summary>
	public void DevSetCooldown(ModSkill skill, int ticks) => cooldowns[skill.Type] = ticks;

	public float CostOf(ModSkill skill) => skill.GetCost(this) * KiCostMultiplier;

	public bool CanAfford(ModSkill skill) => Player.GetModPlayer<KiPlayer>().Ki >= CostOf(skill);

	public override void ResetEffects()
	{
		KiCostMultiplier = 1f;
		ContactProof = false;
	}

	public override void ProcessTriggers(TriggersSet triggersSet)
	{
		for (int i = 0; i < SlotCount; i++)
			keyHeld[i] = ZenkaiKeybinds.SkillSlots[i].Current;
		if (ZenkaiKeybinds.SkillMenu.JustPressed)
			SkillMenuSystem.Toggle();
	}

	public override void SetControls()
	{
		if (State == SkillState.Idle)
			return;
		Player.controlUseItem = false;
		Player.controlUseTile = false;
		if ((State == SkillState.Sustaining && Active.RootWhileSustaining) || HitPause > 0) {
			Player.controlLeft = Player.controlRight = Player.controlJump = false;
			Player.controlUp = Player.controlDown = false;
		}
	}

	public override bool CanUseItem(Item item) => State == SkillState.Idle;

	public override void PostUpdateMiscEffects()
	{
		for (int i = 0; i < cooldowns.Length; i++) {
			if (cooldowns[i] > 0)
				cooldowns[i]--;
		}
		if (ComboTimer > 0 && --ComboTimer == 0)
			ComboStep = 0;
		if (ChaseTimer > 0 && --ChaseTimer == 0)
			ChaseTarget = -1;

		for (int i = 0; i < SlotCount; i++) {
			wasHeld[i] = held[i];
			held[i] = keyHeld[i] || DevSlotHeld[i];
			keyHeld[i] = false; // ProcessTriggers sets it again next tick if the key is still down
		}

		UpdateAim();
		UpdateState();

		// Hit pause wins over any movement a skill asked for this tick.
		if (HitPause > 0) {
			HitPause--;
			ForceVelocity(Vector2.Zero);
		}
	}

	void UpdateAim()
	{
		if (Player.whoAmI != Main.myPlayer)
			return;
		Vector2 target = (DevAimWorld ?? Main.MouseWorld) - Player.MountedCenter;
		if (target.LengthSquared() < 1f)
			return;
		target.Normalize();
		float rate = Active?.SustainTurnRate ?? 0f;
		if (State == SkillState.Sustaining && rate > 0f) {
			float from = Aim.ToRotation();
			float delta = MathHelper.WrapAngle(target.ToRotation() - from);
			Aim = (from + MathHelper.Clamp(delta, -rate, rate)).ToRotationVector2();
		}
		else {
			Aim = target;
		}
	}

	bool Blocked
	{
		get {
			KiPlayer ki = Player.GetModPlayer<KiPlayer>();
			return !ki.CanUseKi || ki.Charging || Player.noItems;
		}
	}

	void UpdateState()
	{
		KiPlayer ki = Player.GetModPlayer<KiPlayer>();
		if (State != SkillState.Idle && State != SkillState.Recovery && Blocked) {
			Cancel();
			return;
		}

		StateTicks++;
		switch (State) {
			case SkillState.Idle:
				if (Blocked)
					return;
				for (int i = 0; i < SlotCount; i++) {
					ModSkill skill = SkillInSlot(i);
					if (skill == null)
						continue;
					bool pressed = held[i] && !wasHeld[i];
					if ((pressed || (held[i] && skill.AutoRepeat)) && TryStart(skill, i, ki))
						break;
				}
				break;

			case SkillState.Charging:
				ChargeTicks = Math.Min(ChargeTicks + 1, Active.MaxCharge);
				ChargeFraction = Active.MaxCharge <= Active.MinCharge ? 1f
					: MathHelper.Clamp((ChargeTicks - Active.MinCharge) / (float)(Active.MaxCharge - Active.MinCharge), 0f, 1f);
				Active.OnChargeTick(this);
				if (!held[ActiveSlot]) {
					if (ChargeTicks >= Active.MinCharge && ki.TrySpendKi(CostOf(Active)))
						Fire(ChargeFraction);
					else
						Cancel();
				}
				break;

			case SkillState.Sustaining:
				float drain = Active.SustainDrain * KiCostMultiplier / 60f;
				bool paid = drain <= 0f || ki.TrySpendKi(drain);
				// Pressing the key again cuts a sustained skill short.
				bool pressedAgain = held[ActiveSlot] && !wasHeld[ActiveSlot];
				bool keep = paid && StateTicks <= sustainLimit && !pressedAgain && (!Active.HoldToSustain || held[ActiveSlot]);
				if (keep)
					keep = Active.OnSustainTick(this);
				if (!keep)
					Finish();
				break;

			case SkillState.Recovery:
				if (--recoveryLeft <= 0)
					ToIdle();
				break;
		}
	}

	bool TryStart(ModSkill skill, int slot, KiPlayer ki)
	{
		if (cooldowns[skill.Type] > 0 || (ki.Flying && !skill.UsableWhileFlying) || !skill.CanUse(this))
			return false;
		float cost = CostOf(skill);
		if (ki.Ki < cost)
			return false;

		Active = skill;
		ActiveSlot = slot;
		ChargeTicks = 0;
		ChargeFraction = 0f;
		ActiveProjectile = -1;
		if (skill.MaxCharge > 0) {
			SetState(SkillState.Charging);
			skill.OnStart(this);
			return true;
		}
		if (cost > 0f && !ki.TrySpendKi(cost))
			return false;
		skill.OnStart(this);
		Fire(1f);
		return true;
	}

	void Fire(float charge)
	{
		cooldowns[Active.Type] = Active.Cooldown;
		ChargeFraction = charge;
		sustainLimit = Active.SustainTicks(this, charge);
		Active.OnRelease(this, charge);
		if (Active == null)
			return; // OnRelease may cancel
		if (sustainLimit > 0)
			SetState(SkillState.Sustaining);
		else
			Finish();
	}

	void Finish()
	{
		ModSkill skill = Active;
		skill.OnEnd(this, false);
		recoveryLeft = skill.Recovery;
		if (recoveryLeft > 0)
			SetState(SkillState.Recovery);
		else
			ToIdle();
	}

	/// <summary>Stops the active skill without its recovery. Charged skills refund nothing because they had not paid yet.</summary>
	public void Cancel()
	{
		if (Active != null && State != SkillState.Recovery)
			Active.OnEnd(this, true);
		ToIdle();
	}

	void ToIdle()
	{
		SetState(SkillState.Idle);
		Active = null;
		ActiveSlot = -1;
		ActiveProjectile = -1;
		ChargeTicks = 0;
		ChargeFraction = 0f;
	}

	void SetState(SkillState state)
	{
		State = state;
		StateTicks = 0;
	}

	/// <summary>Drives the player's velocity this tick (see <see cref="KiPlayer.ForcedVelocity"/>).</summary>
	public void ForceVelocity(Vector2 velocity) => Player.GetModPlayer<KiPlayer>().ForcedVelocity = velocity;

	public override void PostUpdateRunSpeeds()
	{
		if (State == SkillState.Charging && Active.ChargeMoveSpeed < 1f) {
			Player.maxRunSpeed *= Active.ChargeMoveSpeed;
			Player.accRunSpeed *= Active.ChargeMoveSpeed;
			Player.GetModPlayer<KiPlayer>().FlightSpeedMultiplier *= Active.ChargeMoveSpeed;
		}
	}

	public override void PostUpdate()
	{
		if (State == SkillState.Idle || Active == null)
			return;
		// Face the aim and pose the arms after vanilla has set the frame for this tick.
		if (Math.Abs(Aim.X) > 0.05f)
			Player.ChangeDir(Math.Sign(Aim.X));
		Active.PoseArms(this);
	}

	public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot) => !ContactProof;

	public override void OnHurt(Player.HurtInfo info)
	{
		if (HitCancelsCharge && State == SkillState.Charging)
			Cancel();
	}

	public override void UpdateDead()
	{
		if (State != SkillState.Idle)
			Cancel();
		HitPause = 0;
		ComboStep = ComboTimer = ChaseTimer = 0;
		ChaseTarget = -1;
	}

	/// <summary>Screen shake for the local player only.</summary>
	public void Shake(float strength, int frames)
	{
		if (Player.whoAmI != Main.myPlayer || strength <= 0f)
			return;
		Main.instance.CameraModifiers.Add(new PunchCameraModifier(Player.Center, Main.rand.NextVector2Unit(),
			strength, 12f, frames, 1000f, "Zenkai"));
	}
}
