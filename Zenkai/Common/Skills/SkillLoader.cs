using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader;

namespace Zenkai.Common.Skills;

/// <summary>Registry of every loaded <see cref="ModSkill"/>, indexed by <see cref="ModSkill.Type"/>.</summary>
public static class SkillLoader
{
	static readonly List<ModSkill> skills = new();

	public static IReadOnlyList<ModSkill> Skills => skills;

	/// <summary>Skills in the order the skill menu shows them.</summary>
	public static IEnumerable<ModSkill> Ordered => skills.OrderBy(s => s.Tier).ThenBy(s => s.Kind).ThenBy(s => s.Name);

	internal static int Add(ModSkill skill)
	{
		skills.Add(skill);
		return skills.Count - 1;
	}

	public static ModSkill Get(int type) => type >= 0 && type < skills.Count ? skills[type] : null;

	public static ModSkill Find(string fullName) =>
		!string.IsNullOrEmpty(fullName) && ModContent.TryFind(fullName, out ModSkill skill) ? skill : null;

	internal static void Unload() => skills.Clear();
}

public class SkillLoaderSystem : ModSystem
{
	public override void Unload() => SkillLoader.Unload();
}
