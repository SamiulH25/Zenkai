using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Zenkai.Common.Players;
using Zenkai.Common.Skills;
using Zenkai.Common.UI;
using Zenkai.Common.Forms;
using Zenkai.Content.Forms;
using Zenkai.Content.Skills;

namespace Zenkai.Dev;

/// <summary>
/// Screenshot harness for the visual quality gate. Inactive unless the ZENKAI_SHOTS environment
/// variable names an output folder. When active it creates or loads a test character and world,
/// poses the player through a list of scenes, saves one PNG per scene and exits the game.
/// Run it through tools/shots.sh, which uses a separate save folder so real saves are never touched.
/// </summary>
public class ShotRunner : ModSystem
{
	record Scene(string Name, int WaitTicks, Action<Player, KiPlayer> Setup, Action<Ctx, int> Tick = null);
	record Ctx(Player Player, KiPlayer Ki, SkillPlayer Sp);

	static ModSkill Skill<T>() where T : ModSkill => ModContent.GetInstance<T>();

	static void ReleaseAll(SkillPlayer sp)
	{
		Array.Clear(sp.DevSlotHeld);
		Vanish.DevDirection = null;
	}

	/// <summary>Learns every skill and slots the four phase 2 skills in a fixed order.</summary>
	static void FullLoadout(SkillPlayer sp)
	{
		foreach (ModSkill s in SkillLoader.Skills)
			sp.Learn(s);
		sp.SetSlot(0, Skill<KiBlast>());
		sp.SetSlot(1, Skill<ChargedBeam>());
		sp.SetSlot(2, Skill<Rush>());
		sp.SetSlot(3, Skill<Vanish>());
	}

	static NPC FindDummy()
	{
		foreach (NPC npc in Main.ActiveNPCs) {
			if (npc.type == NPCID.TargetDummy)
				return npc;
		}
		return null;
	}

	/// <summary>Stands the player just left of the dummy, facing it.</summary>
	static void StandByDummy(Player p, float gap)
	{
		NPC dummy = FindDummy();
		if (dummy == null)
			return;
		p.Bottom = dummy.Bottom + new Vector2(-gap, 0f);
		p.velocity = Vector2.Zero;
		p.direction = 1;
	}

	static SkillPlayer Sp(Player p) => p.GetModPlayer<SkillPlayer>();

	static readonly List<Scene> Scenes = new() {
		new("ki_bar_full", 30, (p, ki) => ki.Ki = ki.MaxKi),
		new("ki_bar_partial", 20, (p, ki) => ki.Ki = ki.MaxKi * 0.45f),
		new("ki_charge", 47, (p, ki) => { ki.Ki = ki.MaxKi * 0.2f; ki.DevHoldCharge = true; }),
		// The aura is generated live; two more frames a few ticks apart show it moving.
		new("ki_charge_b", 6, (p, ki) => { }),
		new("ki_charge_c", 6, (p, ki) => { }),
		new("ki_flight", 45, (p, ki) => {
			ki.DevHoldCharge = false;
			ki.Ki = ki.MaxKi * 0.8f;
			ki.SetFlying(true);
			ki.DevFlightInput = Vector2.Normalize(new Vector2(1f, -0.8f));
		}),
		// Level flight to the left, to check the horizontal wisp and the flipped sheet.
		new("ki_flight_level", 40, (p, ki) => ki.DevFlightInput = new Vector2(-1f, 0f)),
		new("ki_flight_hover", 40, (p, ki) => ki.DevFlightInput = Vector2.Zero),

		// Phase 2: skills. Land first so the scenes start on the ground.
		new("skill_hotbar_empty", 50, (p, ki) => {
			ki.SetFlying(false);
			ki.DevFlightInput = null;
			ki.Ki = ki.MaxKi;
			for (int i = 0; i < SkillPlayer.SlotCount; i++)
				Sp(p).SetSlot(i, null);
		}),
		// Vanish on cooldown, the beam unaffordable at 17 ki.
		new("skill_hotbar_full", 10, (p, ki) => {
			FullLoadout(Sp(p));
			ki.Ki = 17f;
			Sp(p).DevSetCooldown(Skill<Vanish>(), 40);
		}),
		new("skill_menu", 15, (p, ki) => {
			Sp(p).Forget(Skill<Vanish>());
			SkillMenuSystem.SetOpen(true);
			SkillMenuSystem.DevSelect(Skill<ChargedBeam>());
		}),
		// Ki Blast held down, firing up and to the right into open air.
		new("blast_fire", 30, (p, ki) => {
			SkillMenuSystem.SetOpen(false);
			FullLoadout(Sp(p));
			ki.Ki = ki.MaxKi;
			p.direction = 1;
			SkillCommand.PlaceDummy(p, 9);
			Sp(p).DevAimWorld = p.Center + new Vector2(400f, -60f);
			Sp(p).DevSlotHeld[0] = true;
		}),
		// Then at the target dummy, to catch bursts on impact.
		new("blast_impact", 26, (p, ki) => {
			ki.Ki = ki.MaxKi;
			StandByDummy(p, 120f);
			if (FindDummy() is NPC dummy)
				Sp(p).DevAimWorld = dummy.Center;
		}),
		// Beam: let go of the blast, then hold the beam key.
		new("beam_charge_half", 105, (p, ki) => {
			ReleaseAll(Sp(p));
			ki.Ki = ki.MaxKi;
			p.position.X -= 160f;
			Sp(p).DevAimWorld = p.Center + new Vector2(400f, -40f);
		}, (c, t) => c.Sp.DevSlotHeld[1] = t >= 20),
		new("beam_charge_full", 90, (p, ki) => ki.Ki = ki.MaxKi, (c, t) => c.Sp.DevSlotHeld[1] = true),
		new("beam_fire", 24, (p, ki) => ReleaseAll(Sp(p))),
		// A second beam aimed down into the ground, to show it stopping at tiles.
		new("beam_wall", 125, (p, ki) => {
			Sp(p).Cancel();
			ki.Ki = ki.MaxKi;
			Sp(p).DevAimWorld = p.Center + new Vector2(400f, 70f);
		}, (c, t) => c.Sp.DevSlotHeld[1] = t >= 70 && t < 105),
		// Rush: first jab landing on the dummy.
		new("rush_hit", 18, (p, ki) => {
			Sp(p).Cancel();
			ReleaseAll(Sp(p));
			ki.Ki = ki.MaxKi;
			StandByDummy(p, 46f);
			Sp(p).DevAimWorld = p.Center + new Vector2(200f, 0f);
		}, (c, t) => c.Sp.DevSlotHeld[2] = t >= 12 && t < 16),
		// Full combo; capture as the launcher lands.
		new("rush_launcher", 59, (p, ki) => {
			StandByDummy(p, 54f);
			Sp(p).DevAimWorld = p.Center + new Vector2(200f, 0f);
		}, (c, t) => c.Sp.DevSlotHeld[2] = t is (>= 12 and < 15) or (>= 33 and < 36) or (>= 54 and < 57)),
		// Vanish to the left, away from the dummy.
		new("vanish_blink", 18, (p, ki) => {
			ReleaseAll(Sp(p));
			ki.Ki = ki.MaxKi;
			StandByDummy(p, 46f);
			Vanish.DevDirection = new Vector2(-1f, 0f);
		}, (c, t) => c.Sp.DevSlotHeld[3] = t >= 12 && t < 15),

		// Phase 3: forms. Stand clear of the dummy first.
		new("form_kaioken", 45, (p, ki) => {
			ReleaseAll(Sp(p));
			Vanish.DevDirection = null;
			ki.Ki = ki.MaxKi;
			StandByDummy(p, 140f);
			Fp(p).Unlock(Form<Kaioken>(), dramatic: false);
			Fp(p).DevEnter(Form<Kaioken>());
		}),
		// Super Saiyan powering up from Kaioken: motes pulled in, gold aura building.
		new("form_transform", 32, (p, ki) => {
			ki.Ki = ki.MaxKi;
			Fp(p).Unlock(Form<SuperSaiyan>(), dramatic: false);
			Fp(p).DevBeginTransform(Form<SuperSaiyan>());
		}),
		// The burst as it completes (the sequence is 50 ticks).
		new("form_burst", 21, (p, ki) => { }),
		new("form_ssj", 45, (p, ki) => ki.Ki = ki.MaxKi),
		new("form_ssj_left", 30, (p, ki) => Sp(p).DevAimWorld = p.Center - new Vector2(300f, 0f), (c, t) => c.Player.ChangeDir(-1)),
		new("form_ssj_flight", 40, (p, ki) => {
			Sp(p).DevAimWorld = null;
			ki.Ki = ki.MaxKi;
			ki.SetFlying(true);
			ki.DevFlightInput = new Vector2(1f, -0.25f);
		}),
		new("form_menu", 15, (p, ki) => {
			ki.DevFlightInput = Vector2.Zero;
			Fp(p).SetMastery(Form<SuperSaiyan>(), 0.4f);
			Fp(p).SetMastery(Form<Kaioken>(), 0.75f);
			SkillMenuSystem.SetOpen(true);
			SkillMenuSystem.DevSelectForm(Form<SuperSaiyan>());
		}),
	};

	static FormPlayer Fp(Player p) => p.GetModPlayer<FormPlayer>();
	static ModForm Form<T>() where T : ModForm => ModContent.GetInstance<T>();

	// Lets a run render only some scenes: ZENKAI_SHOT_FILTER=beam,rush matches by name prefix.
	// Scenes that are skipped still run their setup so later scenes start from the same state;
	// only the capture is skipped.
	static string[] filter;
	static bool Wanted(string name) => filter == null || Array.Exists(filter, f => name.StartsWith(f));

	const int SettleTicks = 240; // let the world finish loading in and the camera settle

	static string outputDir;
	enum Stage { WaitForMenu, Generating, Entering, InWorld, Done }
	Stage stage;
	int ticks;
	int sceneIndex = -1;
	uint lastUpdate;

	/// <summary>True while the shot runner drives the game; gameplay code may skip player-facing chat.</summary>
	public static bool Active { get; private set; }

	public override void Load()
	{
		outputDir = Environment.GetEnvironmentVariable("ZENKAI_SHOTS");
		if (Main.dedServ || string.IsNullOrEmpty(outputDir))
			return;
		Active = true;
		Directory.CreateDirectory(outputDir);
		string f = Environment.GetEnvironmentVariable("ZENKAI_SHOT_FILTER");
		filter = string.IsNullOrWhiteSpace(f) ? null : f.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		Main.OnPostDraw += OnPostDraw;
	}

	public override void Unload()
	{
		Active = false;
		Main.OnPostDraw -= OnPostDraw;
	}

	void OnPostDraw(GameTime gameTime)
	{
		try {
			if (Main.gameMenu)
				DriveMenu();
			else
				DriveWorld();
		}
		catch (Exception e) {
			File.WriteAllText(Path.Combine(outputDir, "error.txt"), e.ToString());
			Environment.Exit(1);
		}
	}

	void DriveMenu()
	{
		switch (stage) {
			case Stage.WaitForMenu:
				if (++ticks < 60)
					return;
				// Mods are loaded by the time this runs, so any other menu is a first-run prompt
				// (welcome message, update notes). Skip it.
				if (Main.menuMode != 0) {
					Main.menuMode = 0;
					ticks = 30;
					return;
				}
				ticks = 0;

				Main.LoadPlayers();
				if (Main.PlayerList.Count == 0) {
					PlayerFileData.CreateAndSave(new Player { name = "Shots" });
					Main.LoadPlayers();
				}
				Main.PlayerList[0].SetAsActive();

				Main.LoadWorlds();
				if (Main.WorldList.Count == 0) {
					Main.maxTilesX = 4200;
					Main.maxTilesY = 1200;
					WorldGen.setWorldSize();
					Main.GameMode = 0;
					WorldGen.WorldGenParam_Evil = 0;
					Main.worldName = "ZenkaiShots";
					Main.ActiveWorldFileData = WorldFile.CreateMetadata(Main.worldName, false, Main.GameMode);
					Main.ActiveWorldFileData.SetSeed("zenkai");
					Main.menuMode = 10;
					WorldGen.CreateNewWorld();
					stage = Stage.Generating;
				}
				else {
					EnterWorld();
				}
				break;

			case Stage.Generating:
				if (WorldGen.generatingWorld || Main.menuMode == 10) {
					ticks = 0;
					return;
				}
				if (++ticks < 60)
					return;
				Main.LoadWorlds();
				EnterWorld();
				break;
		}
	}

	void EnterWorld()
	{
		Main.WorldList[0].SetAsActive();
		WorldGen.playWorld();
		stage = Stage.Entering;
		ticks = 0;
	}

	void DriveWorld()
	{
		if (stage == Stage.Done)
			return;
		stage = Stage.InWorld;

		// Count game updates, not draws, so waits match in-game time.
		if (Main.GameUpdateCount == lastUpdate)
			return;
		lastUpdate = Main.GameUpdateCount;

		// A fixed, calm scene: midday, clear weather, no enemies.
		Main.dayTime = true;
		Main.time = 27000;
		Main.raining = false;
		Main.maxRaining = 0f;
		// Judge the HUD at 100%. Terraria picks its own default scale on a fresh config.
		Main.UIScale = 1f;
		foreach (NPC npc in Main.ActiveNPCs) {
			if (!npc.townNPC && npc.type != NPCID.TargetDummy)
				npc.active = false;
		}
		// Plants broken while placing the dummy drop items whose hover labels clutter the shots.
		foreach (Item item in Main.ActiveItems)
			item.active = false;

		Player player = Main.LocalPlayer;
		KiPlayer ki = player.GetModPlayer<KiPlayer>();
		// Keep mana full: vanilla bursts sparkle dust over the player when mana refills.
		player.statMana = player.statManaMax2;
		ticks++;

		if (sceneIndex < 0) {
			if (ticks < SettleTicks)
				return;
			NextScene(player, ki);
			return;
		}

		Scenes[sceneIndex].Tick?.Invoke(new Ctx(player, ki, player.GetModPlayer<SkillPlayer>()), ticks);
		if (ticks < Scenes[sceneIndex].WaitTicks)
			return;

		if (Wanted(Scenes[sceneIndex].Name))
			Capture(Scenes[sceneIndex].Name);
		NextScene(player, ki);
	}

	void NextScene(Player player, KiPlayer ki)
	{
		sceneIndex++;
		ticks = 0;
		if (sceneIndex >= Scenes.Count) {
			stage = Stage.Done;
			File.WriteAllText(Path.Combine(outputDir, "done.txt"), $"{Scenes.Count} shots");
			Environment.Exit(0);
		}
		if (sceneIndex == 0) {
			// tModLoader leaves mod keybinds unbound until the player binds them, so bind the defaults
			// in the shot runner's own input profile (.shots/save) for the hotbar's key labels.
			var status = Terraria.GameInput.PlayerInput.CurrentProfile.InputModes[Terraria.GameInput.InputMode.Keyboard].KeyStatus;
			foreach (var (name, key) in Common.Systems.ZenkaiKeybinds.Defaults)
				status["Zenkai/" + name] = new List<string> { key };
		}
		Scenes[sceneIndex].Setup(player, ki);
	}

	static void Capture(string name)
	{
		GraphicsDevice device = Main.instance.GraphicsDevice;
		int width = device.PresentationParameters.BackBufferWidth;
		int height = device.PresentationParameters.BackBufferHeight;
		var pixels = new Color[width * height];
		device.GetBackBufferData(pixels);
		for (int i = 0; i < pixels.Length; i++)
			pixels[i].A = 255;

		using var texture = new Texture2D(device, width, height);
		texture.SetData(pixels);
		using FileStream stream = File.Create(Path.Combine(outputDir, name + ".png"));
		texture.SaveAsPng(stream, width, height);
	}
}
