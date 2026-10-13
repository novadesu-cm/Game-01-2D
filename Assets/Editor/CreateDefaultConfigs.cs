using System.Collections.Generic;
using GotA.Client;
using GotA.Simulation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu "GotA/Create Default Configs": creates the spec.md prototype ScriptableObjects
/// (weapons, traits, enemies, RunSetup) in Assets/Data/Configs. Existing assets are not overwritten.
/// </summary>
public static class CreateDefaultConfigs
{
    private const string Folder = "Assets/Data/Configs";

    [MenuItem("GotA/Create Default Configs")]
    public static void Create()
    {
        EnsureFolder("Assets/Data");
        EnsureFolder(Folder);

        WeaponConfigSO fists = Weapon("Weapon_Fists", "Fists", 8, 1.0f, 0.20f, 3.0f, 1.1f);
        WeaponConfigSO hammer = Weapon("Weapon_GiantHammer", "Giant Hammer", 24, 2.0f, 0.60f, 9.0f, 1.5f);
        WeaponConfigSO daggers = Weapon("Weapon_DualDaggers", "Dual Daggers", 5, 0.4f, 0.08f, 1.0f, 1.0f);
        WeaponConfigSO gruntAttack = Weapon("Weapon_GruntAttack", "Grunt Attack", 4, 1.2f, 0.30f, 2.5f, 1.1f);
        WeaponConfigSO bruteAttack = Weapon("Weapon_BruteAttack", "Brute Attack", 9, 2.0f, 0.70f, 7.0f, 1.4f);

        Trait("Trait_None", "None", TraitType.None, 0f, "No trait");
        Trait("Trait_Lifesteal", "Lifesteal", TraitType.Lifesteal, 0.25f, "Heal 25% of damage dealt");
        Trait("Trait_ThickSkin", "Thick Skin", TraitType.ThickSkin, 0.75f, "Take 25% less damage");

        EnemyConfigSO grunt = Enemy("Enemy_Grunt", "Grunt", 40, 2.4f, 0f, 0.40f, gruntAttack);
        EnemyConfigSO brute = Enemy("Enemy_Brute", "Brute", 100, 1.5f, 0.40f, 0.55f, bruteAttack);

        string setupPath = Folder + "/RunSetup.asset";
        if (AssetDatabase.LoadAssetAtPath<RunSetupSO>(setupPath) == null)
        {
            var setup = ScriptableObject.CreateInstance<RunSetupSO>();
            setup.waves = new List<WaveSetup>
            {
                Wave(30, (grunt, 3.0f, 1.0f), (grunt, 3.0f, -1.0f)),
                Wave(50, (grunt, 2.5f, 1.0f), (brute, 3.5f, -1.0f)),
                Wave(100, (grunt, 2.5f, 1.5f), (grunt, 2.5f, -1.5f), (brute, 3.5f, 0.0f))
            };
            AssetDatabase.CreateAsset(setup, setupPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("GotA: default configs are in " + Folder + ". Assign them to GameSessionManager " +
                  "(Weapons: Fists, Hammer, Daggers; Traits: None, Lifesteal, Thick Skin).");
    }

    private static WaveSetup Wave(int reward, params (EnemyConfigSO enemy, float x, float y)[] spawns)
    {
        var wave = new WaveSetup { potReward = reward };
        foreach (var s in spawns)
            wave.enemies.Add(new EnemySpawnSetup { enemy = s.enemy, position = new Vector2(s.x, s.y) });
        return wave;
    }

    private static WeaponConfigSO Weapon(string file, string label, float dmg, float interval,
                                         float windup, float knockback, float range)
    {
        return GetOrCreate<WeaponConfigSO>(file, so =>
        {
            so.displayName = label;
            so.baseDamage = dmg;
            so.attackInterval = interval;
            so.windup = windup;
            so.baseKnockback = knockback;
            so.range = range;
        });
    }

    private static TraitConfigSO Trait(string file, string label, TraitType type, float magnitude, string description)
    {
        return GetOrCreate<TraitConfigSO>(file, so =>
        {
            so.displayName = label;
            so.trait = type;
            so.magnitude = magnitude;
            so.description = description;
        });
    }

    private static EnemyConfigSO Enemy(string file, string label, int hp, float speed,
                                       float kbResist, float radius, WeaponConfigSO weapon)
    {
        return GetOrCreate<EnemyConfigSO>(file, so =>
        {
            so.displayName = label;
            so.stats = new UnitStatsData { maxHP = hp, moveSpeed = speed, kbResist = kbResist, bodyRadius = radius };
            so.weapon = weapon;
        });
    }

    private static T GetOrCreate<T>(string file, System.Action<T> init) where T : ScriptableObject
    {
        string path = Folder + "/" + file + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        var so = ScriptableObject.CreateInstance<T>();
        init(so);
        AssetDatabase.CreateAsset(so, path);
        return so;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
