using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Zenkai.Common.Forms;
using Zenkai.Common.Players;
using Zenkai.Common.Skills;
using Zenkai.Content.Skills;

namespace Zenkai.Dev;

/// <summary>One-shot, opt-in save generator. Never runs during normal play or screenshot tests.</summary>
public class TestSaveRunner : ModSystem
{
	const string CharacterName = "Zenkai Tester";
	const string WorldName = "Zenkai Training Ground";
	const int TestKi = 5000;
	string output;
	bool verify, generating, entering, finished;
	int ticks;
	uint lastTick;

	public override void Load()
	{
		output = Environment.GetEnvironmentVariable("ZENKAI_TEST_SAVE");
		if (Main.dedServ || string.IsNullOrWhiteSpace(output))
			return;
		if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ZENKAI_SHOTS")))
			throw new InvalidOperationException("Do not combine save generation and screenshot runs.");
		verify = Environment.GetEnvironmentVariable("ZENKAI_TEST_VERIFY") == "1";
		Directory.CreateDirectory(output);
		Main.OnPostDraw += Drive;
	}

	public override void Unload() => Main.OnPostDraw -= Drive;

	void Drive(GameTime time)
	{
		if (finished)
			return;
		try {
			if (Main.gameMenu) {
				if (entering)
					return;
				if (generating) {
					if (WorldGen.generatingWorld || Main.menuMode == 10) {
						ticks = 0;
						return;
					}
					if (++ticks < 60)
						return;
					Main.LoadWorlds();
					EnterWorld();
					return;
				}
				if (++ticks < 90)
					return;
				if (Main.menuMode != 0) {
					Main.menuMode = 0;
					ticks = 30;
					return;
				}
				Main.LoadPlayers();
				Main.LoadWorlds();
				if (!verify && (Main.PlayerList.Count != 0 || Main.WorldList.Count != 0))
					throw new InvalidOperationException("Creation requires an empty, isolated save directory.");
				if (!verify && Main.PlayerList.Count == 0) {
					PlayerFileData.CreateAndSave(new Player { name = CharacterName });
					Main.LoadPlayers();
				}
				Main.PlayerList.Single(p => p.Player.name == CharacterName).SetAsActive();
				Main.LoadWorlds();
				if (!verify && Main.WorldList.Count == 0) {
					Main.maxTilesX = 4200;
					Main.maxTilesY = 1200;
					WorldGen.setWorldSize();
					Main.GameMode = 0;
					WorldGen.WorldGenParam_Evil = 0;
					Main.worldName = WorldName;
					Main.ActiveWorldFileData = WorldFile.CreateMetadata(WorldName, false, Main.GameMode);
					Main.ActiveWorldFileData.SetSeed("zenkai-training");
					Main.menuMode = 10;
					WorldGen.CreateNewWorld();
					generating = true;
					ticks = 0;
				}
				else {
					EnterWorld();
				}
				return;
			}

			if (Main.GameUpdateCount == lastTick)
				return;
			lastTick = Main.GameUpdateCount;
			if (++ticks < 120)
				return;
			if (verify) {
				Verify();
				File.WriteAllText(Path.Combine(output, "verified.txt"), "Character, mod progression, arena and dummies reloaded successfully.");
			}
			else {
				Prepare();
				Player.SavePlayer(Main.ActivePlayerFileData);
				WorldFile.SaveWorld();
				File.WriteAllText(Path.Combine(output, "created.json"), JsonSerializer.Serialize(new {
					player = Main.ActivePlayerFileData.Path,
					world = Main.ActiveWorldFileData.Path,
					characterName = CharacterName,
					worldName = WorldName,
					skills = SkillLoader.Skills.Count,
					forms = FormLoader.Ordered.Count(),
					maxKi = TestKi,
				}));
			}
			finished = true;
			Environment.Exit(0);
		}
		catch (Exception e) {
			File.WriteAllText(Path.Combine(output, "error.txt"), e.ToString());
			finished = true;
			Environment.Exit(1);
		}
	}

	void EnterWorld()
	{
		Main.WorldList.Single(w => w.Name == WorldName).SetAsActive();
		WorldGen.playWorld();
		entering = true;
		ticks = 0;
	}

	static void Prepare()
	{
		Player player = Main.LocalPlayer;
		player.ConsumedLifeCrystals = 15;
		player.ConsumedLifeFruit = 20;
		player.ConsumedManaCrystals = 9;
		player.statLifeMax = player.statLifeMax2 = player.statLife = 500;
		player.statManaMax = player.statManaMax2 = player.statMana = 200;
		KiPlayer ki = player.GetModPlayer<KiPlayer>();
		ki.MaxKiBase = ki.MaxKi = TestKi;
		ki.Ki = TestKi;
		SkillPlayer skills = player.GetModPlayer<SkillPlayer>();
		foreach (ModSkill skill in SkillLoader.Skills)
			skills.Learn(skill);
		ModSkill[] bar = {
			ModContent.GetInstance<KiBlast>(), ModContent.GetInstance<ChargedBeam>(),
			ModContent.GetInstance<Vanish>(), ModContent.GetInstance<HomingBlast>(),
			ModContent.GetInstance<Barrage>(), ModContent.GetInstance<EnergyDisc>(),
			ModContent.GetInstance<ExplosiveWave>(), ModContent.GetInstance<SpiritBomb>(),
		};
		for (int i = 0; i < bar.Length; i++)
			skills.SetSlot(i, bar[i]);
		skills.SetSecondary(ModContent.GetInstance<Rush>());
		skills.Select(0);
		FormPlayer forms = player.GetModPlayer<FormPlayer>();
		foreach (ModForm form in FormLoader.Ordered) {
			forms.Unlock(form, dramatic: false);
			forms.SetMastery(form, 1f);
		}
		(int Type, int Stack)[] items = {
			(ItemID.IronPickaxe, 1), (ItemID.IronAxe, 1), (ItemID.IronHammer, 1),
			(ItemID.MagicMirror, 1), (ItemID.GreaterHealingPotion, 999),
			(ItemID.Torch, 999), (ItemID.TargetDummy, 10), (ItemID.Wood, 9999),
			(ItemID.StoneBlock, 9999), (ItemID.WorkBench, 1),
		};
		for (int i = 0; i < items.Length; i++) {
			player.inventory[i].SetDefaults(items[i].Type);
			player.inventory[i].stack = items[i].Stack;
		}

		int cx = Main.spawnTileX, floor = Main.spawnTileY + 3;
		for (int x = cx - 100; x <= cx + 100; x++) {
			for (int y = Math.Max(10, floor - 100); y <= floor + 5; y++) {
				Tile tile = Main.tile[x, y];
				tile.ClearEverything();
				if (y >= floor) {
					tile.HasTile = true;
					tile.TileType = y == floor ? TileID.GrayBrick : TileID.Dirt;
				}
			}
		}
		Main.spawnTileY = floor;
		player.Bottom = new Vector2(cx * 16f, floor * 16f);
		player.velocity = Vector2.Zero;
		player.direction = 1;
		foreach (int distance in new[] { 12, 24, 36 }) {
			if (!SkillCommand.PlaceDummy(player, distance))
				throw new InvalidOperationException("Could not place a training dummy.");
		}
		foreach (NPC npc in Main.ActiveNPCs) {
			if (npc.townNPC)
				npc.Bottom = new Vector2((cx - 40) * 16f, floor * 16f);
		}
		Main.dayTime = true;
		Main.time = 27000;
		Main.raining = false;
		Main.maxRaining = 0f;
	}

	static void Verify()
	{
		Player player = Main.LocalPlayer;
		SkillPlayer skills = player.GetModPlayer<SkillPlayer>();
		FormPlayer forms = player.GetModPlayer<FormPlayer>();
		var checks = new (bool Ok, string Failure)[] {
			(player.name == CharacterName, $"character name: {player.name}"),
			(Main.worldName == WorldName, $"world name: {Main.worldName}"),
			(player.statLifeMax == 500, $"health: {player.statLifeMax}"),
			(player.statManaMax == 200, $"mana: {player.statManaMax}"),
			(player.GetModPlayer<KiPlayer>().MaxKiBase == TestKi, $"ki: {player.GetModPlayer<KiPlayer>().MaxKiBase}"),
			(!SkillLoader.Skills.Any(s => !skills.Knows(s)), "missing learned skills"),
			(!FormLoader.Ordered.Any(f => !forms.IsUnlocked(f) || forms.Mastery(f) < 0.999f), "missing forms/mastery"),
			(!Enumerable.Range(0, SkillPlayer.SlotCount).Any(i => skills.SkillInSlot(i) == null), "empty skill slots"),
			(skills.SecondarySkill == ModContent.GetInstance<Rush>(), "secondary skill is not Rush"),
			(player.inventory[6].type == ItemID.TargetDummy, $"dummy item: {player.inventory[6].type}"),
			(Main.tile[Main.spawnTileX, Main.spawnTileY].TileType == TileID.GrayBrick, $"arena floor: {Main.tile[Main.spawnTileX, Main.spawnTileY].TileType}"),
			(TileEntity.ByID.Values.OfType<TETrainingDummy>().Count() >= 3, $"dummy entities: {TileEntity.ByID.Values.OfType<TETrainingDummy>().Count()}"),
		};
		string[] failures = checks.Where(c => !c.Ok).Select(c => c.Failure).ToArray();
		if (failures.Length > 0)
			throw new InvalidOperationException("Persistent test-save validation failed: " + string.Join("; ", failures));
	}
}
