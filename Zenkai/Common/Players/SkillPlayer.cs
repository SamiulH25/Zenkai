using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Zenkai.Common.Skills;
using Zenkai.Common.Systems;
using Zenkai.Common.UI;

namespace Zenkai.Common.Players;

public enum SkillState { Idle, Charging, Sustaining, Recovery }

/// <summary>
/// Learned skills, the loadout, cooldowns, the ki stance and the active skill's state machine
/// (Idle, Charging, Sustaining, Recovery). One skill is active at a time.
///
/// Controls: the ki stance key toggles the stance. In it the mouse belongs to skills: left button
/// uses the selected bar skill (hold to charge, sustain or repeat), right button uses the secondary
/// skill (melee by default), the hotbar keys 1-8 and the scroll wheel pick the bar skill, and a
/// double tap of a direction uses the dodge skill. Out of the stance vanilla items work as normal.
/// A press made while another skill is finishing is remembered for a moment and fires when it can.
///
/// Singleplayer only for now; the fields another client would need are plain values so they
/// can be synced in phase 8.
/// </summary>
public class SkillPlayer : ModPlayer
{
	public const int SlotCount = 8;
	/// <summary>Buttons a skill can be driven by.</summary>
	public const int Primary = 0, SecondaryButton = 1, Queued = 2;
	/// <summary>A hit taken while charging cancels the charge.</summary>
	const bool HitCancelsCharge = true;
	const int BufferTicks = 12;     // how long an early press is remembered
	const int DoubleTapWindow = 15; // ticks between two taps of a direction
	const int LearnCheckInterval = 60;

	/// <summary>Learned skills by full name. Unknown names (from a removed skill) are kept, not dropped.</summary>
	public readonly HashSet<string> Learned = new();
	/// <summary>Full names of the bar skills; null for an empty slot.</summary>
	public readonly string[] Loadout = new string[SlotCount];
	/// <summary>Full name of the right-button skill, or null.</summary>
	public string Secondary;
	/// <summary>The bar slot the left button uses.</summary>
	public int Selected { get; private set; }
	public bool InStance { get; private set; }

	public SkillState State { get; private set; }
	public ModSkill Active { get; private set; }
	/// <summary>The button driving the active skill, or -1.</summary>
	public int ActiveButton { get; private set; } = -1;
	/// <summary>The bar slot of the active skill, or -1 (idle, or driven by another button).</summary>
	public int ActiveSlot => ActiveButton == Primary ? Selected : -1;
	/// <summary>Ticks spent in the current state.</summary>
	public int StateTicks { get; private set; }
	public int ChargeTicks { get; private set; }
	/// <summary>Charge progress 0..1 between the active skill's min and max charge.</summary>
	public float ChargeFraction { get; private set; }
	int sustainLimit;
	int recoveryLeft;

	/// <summary>Unit vector from the player toward where the skill aims.</summary>
	public Vector2 Aim = Vector2.UnitX;
	/// <summary>The world position being aimed at (the cursor).</summary>
	public Vector2 AimPoint;
	/// <summary>Index of a projectile the active skill owns (beam, strike), or -1.</summary>
	public int ActiveProjectile = -1;
	/// <summary>Direction of the double tap that queued the active skill, if one did.</summary>
	public Vector2? TapDirection { get; private set; }

	// Per-tick modifiers, reset in ResetEffects. Skills, gear and forms raise or lower these.
	public float KiCostMultiplier = 1f;
	/// <summary>Set by a skill each tick it wants contact damage ignored (melee lunges).</summary>
	public bool ContactProof;
	/// <summary>Multiplies damage taken this tick (barriers).</summary>
	public float DamageTakenMultiplier = 1f;
	/// <summary>Ticks of hit pause left: the player freezes in place for a moment after a heavy hit.</summary>
	public int HitPause;

	int[] cooldowns;
	object[] data;

	// Input: the buttons this tick, edge-detected here so dev input behaves the same way.
	readonly bool[] keyHeld = new bool[2];
	readonly bool[] held = new bool[3];
	readonly bool[] wasHeld = new bool[3];
	readonly int[] buffer = new int[2];
	ModSkill queued;
	int queuedTicks;
	Vector2? queuedDirection;
	int stanceItem;
	bool hotbarKeyPressed;
	readonly bool[] directionWasHeld = new bool[4];
	int lastTap = -1;
	int tapTimer;

	// Test hooks used by Dev/ShotRunner.
	public readonly bool[] DevHeld = new bool[2];
	public Vector2? DevAimWorld;
	/// <summary>Shot runner: the direction for movement skills, in place of the movement keys.</summary>
	public Vector2? DevMoveDirection;

	static readonly Func<TriggersSet, bool>[] HotbarKeys = {
		t => t.Hotbar1, t => t.Hotbar2, t => t.Hotbar3, t => t.Hotbar4, t => t.Hotbar5,
		t => t.Hotbar6, t => t.Hotbar7, t => t.Hotbar8, t => t.Hotbar9, t => t.Hotbar10,
	};
	static readonly Vector2[] Directions = { -Vector2.UnitX, Vector2.UnitX, -Vector2.UnitY, Vector2.UnitY };

	public bool IsActive => State != SkillState.Idle;

	public override void Initialize()
	{
		Learned.Clear();
		Array.Clear(Loadout);
		Secondary = null;
		Selected = 0;
		InStance = false;
		foreach (ModSkill skill in SkillLoader.Ordered.Where(s => s.KnownFromStart))
			Learn(skill);
	}

	public override void SaveData(TagCompound tag)
	{
		tag["learned"] = Learned.ToList();
		tag["loadout"] = Loadout.Select(s => s ?? "").ToList();
		tag["secondary"] = Secondary ?? "";
		tag["selected"] = Selected;
	}

	public override void LoadData(TagCompound tag)
	{
		// Characters made before skills existed keep the defaults from Initialize.
		if (tag.ContainsKey("learned")) {
			Learned.Clear();
			foreach (string name in tag.GetList<string>("learned"))
				Learned.Add(name);
			// Skills that became known from the start since this character was saved.
			foreach (ModSkill skill in SkillLoader.Skills.Where(s => s.KnownFromStart))
				Learned.Add(skill.FullName);
		}
		if (tag.ContainsKey("loadout")) {
			IList<string> loadout = tag.GetList<string>("loadout");
			for (int i = 0; i < SlotCount; i++)
				Loadout[i] = i < loadout.Count && loadout[i] != "" ? loadout[i] : null;
		}
		if (tag.ContainsKey("secondary")) {
			string name = tag.GetString("secondary");
			Secondary = name == "" ? null : name;
		}
		else {
			// Saved before the right button had a skill: move the first melee skill there.
			ModSkill melee = SkillLoader.Ordered.FirstOrDefault(s => s.Kind == SkillKind.Melee && Knows(s));
			if (melee != null)
				SetSecondary(melee);
		}
		Selected = Math.Clamp(tag.GetInt("selected"), 0, SlotCount - 1);
	}

	public bool Knows(ModSkill skill) => skill != null && Learned.Contains(skill.FullName);

	/// <summary>Learns a skill and gives it a home: the right button for the first melee skill, else the first empty bar slot.</summary>
	public void Learn(ModSkill skill)
	{
		if (!Learned.Add(skill.FullName))
			return;
		if (Secondary == skill.FullName || Loadout.Contains(skill.FullName))
			return;
		if (Secondary == null && skill.Kind == SkillKind.Melee) {
			Secondary = skill.FullName;
			return;
		}
		int empty = Array.IndexOf(Loadout, null);
		if (empty >= 0)
			Loadout[empty] = skill.FullName;
	}

	public void Forget(ModSkill skill)
	{
		Learned.Remove(skill.FullName);
		Unslot(skill.FullName);
	}

	void Unslot(string fullName)
	{
		if (Active != null && Active.FullName == fullName)
			Cancel();
		for (int i = 0; i < SlotCount; i++) {
			if (Loadout[i] == fullName)
				Loadout[i] = null;
		}
		if (Secondary == fullName)
			Secondary = null;
	}

	/// <summary>Puts a learned skill in a bar slot. A skill has one home only; it moves if already placed.</summary>
	public void SetSlot(int slot, ModSkill skill)
	{
		if (slot < 0 || slot >= SlotCount || (skill != null && !Knows(skill)))
			return;
		if (skill != null)
			Unslot(skill.FullName);
		if (IsActive && ActiveButton == Primary && slot == Selected)
			Cancel();
		Loadout[slot] = skill?.FullName;
	}

	/// <summary>Puts a learned skill on the right button, or clears it with null.</summary>
	public void SetSecondary(ModSkill skill)
	{
		if (skill != null && !Knows(skill))
			return;
		if (skill != null)
			Unslot(skill.FullName);
		if (IsActive && ActiveButton == SecondaryButton)
			Cancel();
		Secondary = skill?.FullName;
	}

	public ModSkill SkillInSlot(int slot) => slot >= 0 && slot < SlotCount ? SkillLoader.Find(Loadout[slot]) : null;
	public ModSkill SecondarySkill => SkillLoader.Find(Secondary);
	ModSkill ButtonSkill(int button) => button == Primary ? SkillInSlot(Selected) : SecondarySkill;

	/// <summary>Picks the bar slot the left button uses. Ignored while that button's skill is running.</summary>
	public void Select(int slot)
	{
		if (slot < 0 || slot >= SlotCount || slot == Selected || (IsActive && ActiveButton == Primary))
			return;
		Selected = slot;
		if (Player.whoAmI == Main.myPlayer)
			SoundEngine.PlaySound(SoundID.MenuTick);
	}

	public void SetStance(bool on)
	{
		if (on == InStance)
			return;
		InStance = on;
		stanceItem = Player.selectedItem;
		if (!on && IsActive && State != SkillState.Recovery)
			Cancel();
		if (Player.whoAmI == Main.myPlayer)
			SoundEngine.PlaySound(on ? SoundID.Item4 with { Volume = 0.35f, Pitch = 0.6f } : SoundID.MenuClose);
	}

	int[] Cooldowns
	{
		get {
			if (cooldowns == null || cooldowns.Length != SkillLoader.Count)
				cooldowns = new int[SkillLoader.Count];
			return cooldowns;
		}
	}

	public int CooldownLeft(ModSkill skill) => skill == null ? 0 : Cooldowns[skill.Type];

	/// <summary>Shot runner only.</summary>
	public void DevSetCooldown(ModSkill skill, int ticks) => Cooldowns[skill.Type] = ticks;

	/// <summary>This player's state for a skill: created on first use, cleared on death.</summary>
	public T Data<T>(ModSkill skill) where T : class, new()
	{
		if (data == null || data.Length != SkillLoader.Count)
			data = new object[SkillLoader.Count];
		return (T)(data[skill.Type] ??= new T());
	}

	public float CostOf(ModSkill skill) => skill.GetCost(this) * KiCostMultiplier;

	public bool CanAfford(ModSkill skill) => Player.GetModPlayer<KiPlayer>().Ki >= CostOf(skill);

	public override void ResetEffects()
	{
		KiCostMultiplier = 1f;
		ContactProof = false;
		DamageTakenMultiplier = 1f;
	}

	public override void ProcessTriggers(TriggersSet triggersSet)
	{
		if (ZenkaiKeybinds.KiStance.JustPressed)
			SetStance(!InStance);
		if (ZenkaiKeybinds.SkillMenu.JustPressed)
			SkillMenuSystem.Toggle();

		// In the stance the hotbar keys pick a skill instead of an item.
		hotbarKeyPressed = false;
		TriggersSet pressed = PlayerInput.Triggers.JustPressed;
		for (int i = 0; i < HotbarKeys.Length; i++) {
			if (!HotbarKeys[i](pressed))
				continue;
			hotbarKeyPressed = true;
			if (InStance && !Main.playerInventory)
				Select(i);
		}
		ReclaimHotbar();
	}

	/// <summary>Vanilla moves its item selection on the hotbar keys and the scroll wheel. In the
	/// stance that selection is put back, and a scroll step moves the skill selection instead.</summary>
	void ReclaimHotbar()
	{
		if (!InStance || Main.playerInventory || Player.selectedItem == stanceItem) {
			if (Main.playerInventory)
				stanceItem = Player.selectedItem;
			return;
		}
		if (!hotbarKeyPressed && Player.selectedItem < 10 && stanceItem < 10) {
			int d = Player.selectedItem - stanceItem;
			int step = d is 1 or -9 ? 1 : d is -1 or 9 ? -1 : 0;
			if (step != 0)
				Select((Selected + step + SlotCount) % SlotCount);
		}
		Player.selectedItem = stanceItem;
	}

	bool UiBusy => Player.mouseInterface || Player.lastMouseInterface || Main.drawingPlayerChat || Main.mapFullscreen
		|| SkillMenuSystem.IsOpen || Player.GetModPlayer<FormPlayer>().WheelOpen;

	public override void SetControls()
	{
		if (Player.whoAmI == Main.myPlayer && InStance && !Main.playerInventory) {
			ReclaimHotbar();
			bool ui = UiBusy;
			keyHeld[Primary] = Player.controlUseItem && !ui;
			keyHeld[SecondaryButton] = Player.controlUseTile && !ui;
			// The mouse belongs to skills here: no item use, no tile interaction.
			Player.controlUseItem = false;
			Player.controlUseTile = false;
			DetectDoubleTap();
		}

		if (State == SkillState.Idle)
			return;
		Player.controlUseItem = false;
		Player.controlUseTile = false;
		if ((State == SkillState.Sustaining && Active.RootWhileSustaining) || HitPause > 0) {
			Player.controlLeft = Player.controlRight = Player.controlJump = false;
			Player.controlUp = Player.controlDown = false;
		}
	}

	/// <summary>Two quick taps of a direction queue the dodge skill that way.</summary>
	void DetectDoubleTap()
	{
		if (tapTimer > 0 && --tapTimer == 0)
			lastTap = -1;
		bool[] now = { Player.controlLeft, Player.controlRight, Player.controlUp, Player.controlDown };
		for (int i = 0; i < 4; i++) {
			bool pressed = now[i] && !directionWasHeld[i];
			directionWasHeld[i] = now[i];
			if (!pressed)
				continue;
			if (lastTap == i && tapTimer > 0) {
				lastTap = -1;
				if (DodgeSkill is ModSkill dodge)
					Queue(dodge, Directions[i]);
			}
			else {
				lastTap = i;
				tapTimer = DoubleTapWindow;
			}
		}
	}

	ModSkill DodgeSkill => SkillLoader.Ordered.FirstOrDefault(s => s.Kind == SkillKind.Movement && s.MaxCharge == 0 && Knows(s));

	/// <summary>Asks for a tap skill to fire as soon as it can, without a button (double tap, dev tools).</summary>
	public void Queue(ModSkill skill, Vector2? direction = null)
	{
		queued = skill;
		queuedDirection = direction;
		queuedTicks = BufferTicks;
	}

	public override void PostUpdateEquips()
	{
		// A double tap dodges with ki here, not with a dash accessory.
		if (InStance && DodgeSkill != null)
			Player.dashType = 0;
	}

	public override bool CanUseItem(Item item) => State == SkillState.Idle;

	public override void PostUpdateMiscEffects()
	{
		int[] cd = Cooldowns;
		for (int i = 0; i < cd.Length; i++) {
			if (cd[i] > 0)
				cd[i]--;
		}

		bool learnCheck = Player.whoAmI == Main.myPlayer && Main.GameUpdateCount % LearnCheckInterval == 0;
		foreach (ModSkill skill in SkillLoader.Skills) {
			if (Knows(skill))
				skill.UpdatePassive(this);
			else if (learnCheck && skill.LearnCondition(this))
				LearnWithMessage(skill);
		}

		for (int b = 0; b < 2; b++) {
			wasHeld[b] = held[b];
			held[b] = keyHeld[b] || DevHeld[b];
			keyHeld[b] = false; // SetControls sets it again next tick if the button is still down
			if (buffer[b] > 0)
				buffer[b]--;
			// A press that cannot act yet is remembered. A press that cuts a sustained skill short is not.
			bool pressed = held[b] && !wasHeld[b];
			if (pressed && State != SkillState.Idle && !(State == SkillState.Sustaining && ActiveButton == b))
				buffer[b] = BufferTicks;
		}
		if (queuedTicks > 0 && --queuedTicks == 0)
			queued = null;

		UpdateAim();
		UpdateState();

		// Hit pause wins over any movement a skill asked for this tick.
		if (HitPause > 0) {
			HitPause--;
			ForceVelocity(Vector2.Zero);
		}
	}

	void LearnWithMessage(ModSkill skill)
	{
		Learn(skill);
		ZenkaiChat.Say(Language.GetTextValue("Mods.Zenkai.UI.Learned", skill.DisplayName.Value), SkillSlotDrawer.KiFill);
		SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.6f });
	}

	void UpdateAim()
	{
		if (Player.whoAmI != Main.myPlayer)
			return;
		AimPoint = DevAimWorld ?? Main.MouseWorld;
		Vector2 target = AimPoint - Player.MountedCenter;
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
			return !ki.CanUseKi || ki.Charging || Player.noItems
				|| Player.GetModPlayer<FormPlayer>().State == FormState.Transforming;
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
				if (queued != null) {
					ModSkill skill = queued;
					Vector2? direction = queuedDirection;
					if (TryStart(skill, Queued, ki, direction)) {
						queued = null;
						break;
					}
				}
				for (int b = 0; b < 2; b++) {
					ModSkill skill = ButtonSkill(b);
					if (skill == null)
						continue;
					bool pressed = held[b] && !wasHeld[b];
					bool buffered = buffer[b] > 0 && (held[b] || skill.MaxCharge == 0);
					if ((pressed || buffered || (held[b] && skill.AutoRepeat)) && TryStart(skill, b, ki, null)) {
						buffer[b] = 0;
						break;
					}
				}
				break;

			case SkillState.Charging:
				ChargeTicks = Math.Min(ChargeTicks + 1, Active.MaxCharge);
				ChargeFraction = Active.MaxCharge <= Active.MinCharge ? 1f
					: MathHelper.Clamp((ChargeTicks - Active.MinCharge) / (float)(Active.MaxCharge - Active.MinCharge), 0f, 1f);
				Active.OnChargeTick(this);
				if (!held[ActiveButton]) {
					if (ChargeTicks >= Active.MinCharge && ki.TrySpendKi(CostOf(Active)))
						Fire(ChargeFraction);
					else
						Cancel();
				}
				break;

			case SkillState.Sustaining:
				bool paid = ki.TryDrain(Active.SustainDrain * KiCostMultiplier);
				// Pressing the button again cuts a sustained skill short.
				bool pressedAgain = held[ActiveButton] && !wasHeld[ActiveButton];
				bool keep = paid && StateTicks <= sustainLimit && !pressedAgain && (!Active.HoldToSustain || held[ActiveButton]);
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

	bool TryStart(ModSkill skill, int button, KiPlayer ki, Vector2? direction)
	{
		if (Cooldowns[skill.Type] > 0 || (Player.GetModPlayer<FlightPlayer>().Flying && !skill.UsableWhileFlying) || !skill.CanUse(this))
			return false;
		bool charged = skill.MaxCharge > 0;
		if (charged && button == Queued)
			return false; // a charge needs a button to hold
		float cost = CostOf(skill);
		// Charged skills pay on release; tap skills pay now, before anything changes.
		if (charged ? ki.Ki < cost : !ki.TrySpendKi(cost))
			return false;

		Active = skill;
		ActiveButton = button;
		TapDirection = direction;
		ChargeTicks = 0;
		ChargeFraction = 0f;
		ActiveProjectile = -1;
		if (charged) {
			SetState(SkillState.Charging);
			skill.OnStart(this);
			return true;
		}
		skill.OnStart(this);
		Fire(1f);
		return true;
	}

	void Fire(float charge)
	{
		Cooldowns[Active.Type] = Active.Cooldown;
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
		ActiveButton = -1;
		ActiveProjectile = -1;
		TapDirection = null;
		ChargeTicks = 0;
		ChargeFraction = 0f;
	}

	void SetState(SkillState state)
	{
		State = state;
		StateTicks = 0;
	}

	/// <summary>Drives the player's velocity this tick (see <see cref="FlightPlayer.ForcedVelocity"/>).</summary>
	public void ForceVelocity(Vector2 velocity) => Player.GetModPlayer<FlightPlayer>().ForcedVelocity = velocity;

	public override void PostUpdateRunSpeeds()
	{
		if (State == SkillState.Charging && Active.ChargeMoveSpeed < 1f) {
			Player.maxRunSpeed *= Active.ChargeMoveSpeed;
			Player.accRunSpeed *= Active.ChargeMoveSpeed;
			Player.GetModPlayer<FlightPlayer>().SpeedMultiplier *= Active.ChargeMoveSpeed;
		}
	}

	public override void PostUpdate()
	{
		if (InStance)
			ReclaimHotbar();
		if (State == SkillState.Idle || Active == null)
			return;
		// Face the aim and pose the arms after vanilla has set the frame for this tick.
		if (Math.Abs(Aim.X) > 0.05f)
			Player.ChangeDir(Math.Sign(Aim.X));
		Active.PoseArms(this);
	}

	public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot) => !ContactProof;

	public override void ModifyHurt(ref Player.HurtModifiers modifiers)
	{
		if (DamageTakenMultiplier != 1f)
			modifiers.FinalDamage *= DamageTakenMultiplier;
	}

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
		data = null;
		queued = null;
		Array.Clear(buffer);
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
