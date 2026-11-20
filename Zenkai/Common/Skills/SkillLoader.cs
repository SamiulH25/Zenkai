using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader;

namespace Zenkai.Common.Skills;

/// <summary>Registry of every loaded <see cref="ModSkill"/>, indexed by <see cref="ModSkill.Type"/>.</summary>
public static class SkillLoader
{
	static readonly List<ModSkill> skills = new();
	static List<ModSkill> ordered;

	public static IReadOnlyList<ModSkill> Skills => skills;
	public static int Count => skills.Count;

	/// <summary>Skills in the order the skill menu shows them. Built once, after every skill has set its defaults.</summary>
	public static IReadOnlyList<ModSkill> Ordered => ordered ??= skills.OrderBy(s => s.Tier).ThenBy(s => s.Kind).ThenBy(s => s.Name).ToList();

	internal static int Add(ModSkill skill)
	{
		skills.Add(skill);
		ordered = null;
		return skills.Count - 1;
	}

	public static ModSkill Get(int type) => type >= 0 && type < skills.Count ? skills[type] : null;

	public static ModSkill Find(string fullName) =>
		!string.IsNullOrEmpty(fullName) && ModContent.TryFind(fullName, out ModSkill skill) ? skill : null;

	internal static void ResetOrder() => ordered = null;

	internal static void Unload()
	{
		skills.Clear();
		ordered = null;
	}
}

public class SkillLoaderSystem : ModSystem
{
	// Tiers are set in SetStaticDefaults, which runs after registration; drop any order cached before that.
	public override void PostSetupContent() => SkillLoader.ResetOrder();

	public override void Unload() => SkillLoader.Unload();
}
