using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Zenkai.Common.Forms;
using Zenkai.Common.Players;
using Zenkai.Common.Skills;
using Zenkai.Common.Systems;
using Zenkai.Common.UI;
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
	static ModForm Form<T>() where T : ModForm => ModContent.GetInstance<T>();
	static SkillPlayer Sp(Player p) => p.GetModPlayer<SkillPlayer>();
	static FormPlayer Fp(Player p) => p.GetModPlayer<FormPlayer>();
	static FlightPlayer Fl(Player p) => p.GetModPlayer<FlightPlayer>();

	/// <summary>Lets go of both skill buttons and stops whatever skill is running.</summary>
	static void ReleaseAll(SkillPlayer sp)
	{
		Array.Clear(sp.DevHeld);
		sp.DevMoveDirection = null;
		sp.Cancel();
	}

	/// <summary>Learns every skill and fills the bar and the right button in a fixed order.</summary>
	static void FullLoadout(SkillPlayer sp)
	{
		foreach (ModSkill s in SkillLoader.Skills)
			sp.Learn(s);
		ModSkill[] bar = {
			Skill<KiBlast>(), Skill<ChargedBeam>(), Skill<Vanish>(), Skill<HomingBlast>(),
			Skill<Barrage>(), Skill<EnergyDisc>(), Skill<ExplosiveWave>(), Skill<SpiritBomb>(),
		};
		for (int i = 0; i < bar.Length; i++)
			sp.SetSlot(i, bar[i]);
		sp.SetSecondary(Skill<Rush>());
		sp.Select(0);
	}

	/// <summary>Stops any running skill, refills ki and puts a skill under the left button.</summary>
	static void Equip<T>(Player p) where T : ModSkill
	{
		SkillPlayer sp = Sp(p);
		ReleaseAll(sp);
		KiPlayer ki = p.GetModPlayer<KiPlayer>();
		ki.Ki = ki.MaxKi;
		sp.Learn(Skill<T>());
		sp.SetSlot(0, Skill<T>());
		sp.Select(0);
		sp.DevSetCooldown(Skill<T>(), 0);
	}

	static NPC FindDummy()
	{
		foreach (NPC npc in Main.ActiveNPCs) {
			if (npc.type == NPCID.TargetDummy)
				return npc;
		}
		return null;
	}

	/// <summary>Stands the player just left of the dummy, facing it, aiming at it.</summary>
	static void StandByDummy(Player p, float gap)
	{
		NPC dummy = FindDummy();
		if (dummy == null)
			return;
		p.Bottom = dummy.Bottom + new Vector2(-gap, 0f);
		p.velocity = Vector2.Zero;
		p.direction = 1;
		Sp(p).DevAimWorld = dummy.Center;
	}

	/// <summary>Drops any form, wheel and menu, and enters a form at once.</summary>
	static void Enter<T>(Player p) where T : ModForm
	{
		FormWheelSystem.DevHover = null;
		Fp(p).SetWheel(false);
		KiPlayer ki = p.GetModPlayer<KiPlayer>();
		ki.Ki = ki.MaxKi;
		p.statLife = p.statLifeMax2;
		Fp(p).DevEnter(Form<T>());
	}

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
			Fl(p).SetFlying(true);
			Fl(p).DevFlightInput = Vector2.Normalize(new Vector2(1f, -0.8f));
		}),
		// Level flight to the left, to check the horizontal wisp and the flipped sheet.
		new("ki_flight_level", 40, (p, ki) => Fl(p).DevFlightInput = new Vector2(-1f, 0f)),
		new("ki_flight_level_b", 10, (p, ki) => { }),
		new("ki_flight_level_c", 10, (p, ki) => { }),
		new("ki_flight_level_d", 10, (p, ki) => { }),
		new("ki_flight_hover", 40, (p, ki) => Fl(p).DevFlightInput = Vector2.Zero),
		new("ki_flight_hover_b", 12, (p, ki) => { }),
		new("ki_flight_hover_c", 12, (p, ki) => { }),
		new("ki_flight_hover_d", 12, (p, ki) => { }),
		new("ki_flight_hover_e", 12, (p, ki) => { }),
		new("ki_flight_hover_f", 12, (p, ki) => { }),
		new("ki_flight_up", 35, (p, ki) => Fl(p).DevFlightInput = new Vector2(0f, -1f)),
		new("ki_flight_down", 35, (p, ki) => Fl(p).DevFlightInput = new Vector2(0f, 1f)),
		new("ki_flight_land", 35, (p, ki) => { Fl(p).SetFlying(false); Fl(p).DevFlightInput = null; }),

		// Skills. Land first so the scenes start on the ground, and enter the ki stance.
		new("skill_bar_empty", 50, (p, ki) => {
			Fl(p).SetFlying(false);
			Fl(p).DevFlightInput = null;
			ki.Ki = ki.MaxKi;
			Sp(p).SetStance(true);
			for (int i = 0; i < SkillPlayer.SlotCount; i++)
				Sp(p).SetSlot(i, null);
			Sp(p).SetSecondary(null);
		}),
		// Vanish on cooldown, the beam and the dearer skills unaffordable at 17 ki.
		new("skill_bar_full", 10, (p, ki) => {
			FullLoadout(Sp(p));
			ki.Ki = 17f;
			Sp(p).DevSetCooldown(Skill<Vanish>(), 40);
		}),
		new("skill_menu", 15, (p, ki) => {
			Sp(p).Forget(Skill<FinalBeam>());
			Sp(p).Forget(Skill<InstantMovement>());
			SkillMenuSystem.SetOpen(true);
			SkillMenuSystem.DevSelect(Skill<ChargedBeam>());
		}),
		// Ki Blast held down, firing up and to the right into open air.
		new("blast_fire", 30, (p, ki) => {
			SkillMenuSystem.SetOpen(false);
			FullLoadout(Sp(p));
			Equip<KiBlast>(p);
			p.direction = 1;
			SkillCommand.PlaceDummy(p, 9);
			Sp(p).DevAimWorld = p.Center + new Vector2(400f, -60f);
			Sp(p).DevHeld[SkillPlayer.Primary] = true;
		}),
		// Then at the target dummy, to catch bursts on impact.
		new("blast_impact", 26, (p, ki) => {
			ki.Ki = ki.MaxKi;
			StandByDummy(p, 120f);
		}),
		new("barrage_fire", 30, (p, ki) => {
			Equip<Barrage>(p);
			StandByDummy(p, 200f);
			Sp(p).DevHeld[SkillPlayer.Primary] = true;
		}),
		// Homing bolts thrown up and away from the dummy, bending back toward it.
		new("homing_fire", 44, (p, ki) => {
			Equip<HomingBlast>(p);
			StandByDummy(p, 200f);
			Sp(p).DevAimWorld = p.Center + new Vector2(200f, -260f);
			Sp(p).DevHeld[SkillPlayer.Primary] = true;
		}),
		new("disc_fire", 16, (p, ki) => {
			Equip<EnergyDisc>(p);
			StandByDummy(p, 260f);
		}, (c, t) => c.Sp.DevHeld[SkillPlayer.Primary] = t is >= 2 and < 5),
		// Beam: hold the button to charge.
		new("beam_charge_half", 105, (p, ki) => {
			Equip<ChargedBeam>(p);
			p.position.X -= 160f;
			Sp(p).DevAimWorld = p.Center + new Vector2(400f, -40f);
		}, (c, t) => c.Sp.DevHeld[SkillPlayer.Primary] = t >= 20),
		new("beam_charge_full", 90, (p, ki) => ki.Ki = ki.MaxKi, (c, t) => c.Sp.DevHeld[SkillPlayer.Primary] = true),
		new("beam_fire", 24, (p, ki) => Array.Clear(Sp(p).DevHeld)),
		// A second beam aimed down into the ground, to show it stopping at tiles.
		new("beam_wall", 125, (p, ki) => {
			Equip<ChargedBeam>(p);
			Sp(p).DevAimWorld = p.Center + new Vector2(400f, 70f);
		}, (c, t) => c.Sp.DevHeld[SkillPlayer.Primary] = t >= 70 && t < 105),
		// The thin beam, through the ground.
		new("piercing_beam", 90, (p, ki) => {
			Equip<PiercingBeam>(p);
			Sp(p).DevAimWorld = p.Center + new Vector2(400f, 110f);
		}, (c, t) => c.Sp.DevHeld[SkillPlayer.Primary] = t < 72),
		// The wide beam, into open air.
		new("final_beam", 125, (p, ki) => {
			Equip<FinalBeam>(p);
			Sp(p).DevAimWorld = p.Center + new Vector2(400f, -90f);
		}, (c, t) => c.Sp.DevHeld[SkillPlayer.Primary] = t < 100),
		// Rush on the right button: first jab landing on the dummy.
		new("rush_hit", 18, (p, ki) => {
			ReleaseAll(Sp(p));
			ki.Ki = ki.MaxKi;
			Sp(p).SetSecondary(Skill<Rush>());
			StandByDummy(p, 46f);
			Sp(p).DevAimWorld = p.Center + new Vector2(200f, 0f);
		}, (c, t) => c.Sp.DevHeld[SkillPlayer.SecondaryButton] = t >= 12 && t < 16),
		// Full combo; capture as the launcher lands.
		new("rush_launcher", 59, (p, ki) => {
			StandByDummy(p, 54f);
			Sp(p).DevAimWorld = p.Center + new Vector2(200f, 0f);
		}, (c, t) => c.Sp.DevHeld[SkillPlayer.SecondaryButton] = t is (>= 12 and < 15) or (>= 33 and < 36) or (>= 54 and < 57)),
		// A charged punch landing.
		new("heavy_strike", 52, (p, ki) => {
			Equip<HeavyStrike>(p);
			StandByDummy(p, 76f);
			Sp(p).DevAimWorld = p.Center + new Vector2(200f, 0f);
		}, (c, t) => c.Sp.DevHeld[SkillPlayer.Primary] = t is >= 4 and < 44),
		// Vanish to the left, away from the dummy, as a double tap would queue it.
		new("vanish_blink", 18, (p, ki) => {
			ReleaseAll(Sp(p));
			ki.Ki = ki.MaxKi;
			StandByDummy(p, 46f);
			Sp(p).DevMoveDirection = new Vector2(-1f, 0f);
		}, (c, t) => {
			if (t == 12)
				c.Sp.Queue(Skill<Vanish>());
		}),
		new("wave_burst", 46, (p, ki) => {
			Equip<ExplosiveWave>(p);
			StandByDummy(p, 70f);
		}, (c, t) => c.Sp.DevHeld[SkillPlayer.Primary] = t < 40),
		new("flash", 12, (p, ki) => {
			Equip<SolarFlash>(p);
			StandByDummy(p, 120f);
		}, (c, t) => c.Sp.DevHeld[SkillPlayer.Primary] = t is >= 4 and < 7),
		new("barrier", 24, (p, ki) => {
			Equip<KiBarrier>(p);
			Sp(p).DevHeld[SkillPlayer.Primary] = true;
		}),
		// The Spirit Bomb gathered to full size, then on its way down, then bursting on the dummy.
		new("bomb_charge", 300, (p, ki) => {
			Equip<SpiritBomb>(p);
			StandByDummy(p, 220f);
			// The previous scene may have left the button down; the press must be a fresh one.
		}, (c, t) => c.Sp.DevHeld[SkillPlayer.Primary] = t >= 3),
		new("bomb_throw", 14, (p, ki) => Array.Clear(Sp(p).DevHeld)),
		new("bomb_blast", 26, (p, ki) => { }),

		// Forms. Stand clear of the dummy and unlock everything.
		new("form_kaioken", 45, (p, ki) => {
			ReleaseAll(Sp(p));
			Sp(p).DevAimWorld = null;
			StandByDummy(p, 160f);
			Sp(p).DevAimWorld = null;
			foreach (ModForm form in FormLoader.Forms)
				Fp(p).Unlock(form, dramatic: false);
			Enter<Kaioken>(p);
		}),
		// Super Saiyan powering up from Kaioken: motes pulled in, gold aura building.
		new("form_transform", 32, (p, ki) => {
			ki.Ki = ki.MaxKi;
			Fp(p).DevBeginTransform(Form<SuperSaiyan>());
		}),
		// The burst as it completes (the sequence is 50 ticks).
		new("form_burst", 21, (p, ki) => { }),
		new("form_ssj", 45, (p, ki) => ki.Ki = ki.MaxKi),
		new("form_ssj_left", 30, (p, ki) => ki.Ki = ki.MaxKi, (c, t) => c.Player.ChangeDir(-1)),
		new("form_ssj3", 45, (p, ki) => Enter<SuperSaiyan3>(p)),
		new("form_kaioken20", 45, (p, ki) => Enter<KaiokenX20>(p)),
		new("form_potential", 45, (p, ki) => Enter<PotentialUnleashed>(p)),
		new("form_god", 45, (p, ki) => Enter<SuperSaiyanGod>(p)),
		// Kaioken layered on Super Saiyan: gold hair, red aura.
		new("form_ssj_kaioken", 45, (p, ki) => { Enter<SuperSaiyan>(p); Fp(p).DevStack(Form<KaiokenX3>()); }),
		new("form_blue", 45, (p, ki) => Enter<SuperSaiyanBlue>(p)),
		new("form_instinct", 45, (p, ki) => Enter<UltraInstinct>(p)),
		// Super Saiyan 2 in combat: lightning arcing onto the dummy.
		new("form_ssj2_lightning", 36, (p, ki) => {
			Enter<SuperSaiyan2>(p);
			StandByDummy(p, 150f);
			Fp(p).DevInCombat = true;
		}),
		// Ultra Instinct slipping a hit from the dummy and answering with a bolt.
		new("form_instinct_counter", 30, (p, ki) => {
			Enter<UltraInstinct>(p);
			StandByDummy(p, 150f);
			Fp(p).DevForceDodge = true;
		}, (c, t) => {
			if (t == 10 && FindDummy() is NPC dummy)
				c.Player.Hurt(PlayerDeathReason.ByNPC(dummy.whoAmI), 1, 1);
		}),
		// The form wheel held open over Super Saiyan, pointing at Super Saiyan God.
		new("form_wheel", 20, (p, ki) => {
			Fp(p).DevInCombat = false;
			Fp(p).DevForceDodge = false;
			Enter<SuperSaiyan>(p);
			Fp(p).SetMastery(Form<SuperSaiyan>(), 0.4f);
			Fp(p).SetMastery(Form<Kaioken>(), 0.75f);
			Fp(p).SetMastery(Form<SuperSaiyanGod>(), 0.2f);
			Fp(p).SetWheel(true);
			FormWheelSystem.DevHover = Form<SuperSaiyanGod>();
		}),
		new("form_ssj_flight", 40, (p, ki) => {
			Enter<SuperSaiyan>(p);
			Fl(p).SetFlying(true);
			Fl(p).DevFlightInput = new Vector2(1f, -0.25f);
		}),
		new("form_ssj3_flight", 40, (p, ki) => {
			Enter<SuperSaiyan3>(p);
			Fl(p).SetFlying(true);
			Fl(p).DevFlightInput = new Vector2(1f, 0f);
		}),
		new("form_menu", 15, (p, ki) => {
			Fl(p).DevFlightInput = Vector2.Zero;
			Enter<SuperSaiyan>(p);
			Fp(p).Lock(Form<UltraInstinct>());
			SkillMenuSystem.SetOpen(true);
			SkillMenuSystem.DevSelectForm(Form<SuperSaiyan>());
		}),
	};

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

	/// <summary>True while the shot runner drives the game.</summary>
	public static bool Active { get; private set; }

	public override void Load()
	{
		outputDir = Environment.GetEnvironmentVariable("ZENKAI_SHOTS");
		if (Main.dedServ || string.IsNullOrEmpty(outputDir))
			return;
		Active = true;
		ZenkaiChat.Muted = true;
		Directory.CreateDirectory(outputDir);
		string f = Environment.GetEnvironmentVariable("ZENKAI_SHOT_FILTER");
		filter = string.IsNullOrWhiteSpace(f) ? null : f.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		Main.OnPostDraw += OnPostDraw;
	}

	public override void Unload()
	{
		Active = false;
		ZenkaiChat.Muted = false;
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
			// in the shot runner's own input profile (.shots/save).
			var status = Terraria.GameInput.PlayerInput.CurrentProfile.InputModes[Terraria.GameInput.InputMode.Keyboard].KeyStatus;
			foreach (var (name, key) in ZenkaiKeybinds.Defaults)
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
