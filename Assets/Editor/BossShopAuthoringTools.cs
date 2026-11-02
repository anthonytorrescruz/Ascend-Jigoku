using System;
using System.Linq;
using AscendJigoku.Gameplay;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Explicit menu command only. Never rebuilds authored content on import or during a game.
public static class BossShopAuthoringTools
{
    const string Folder = "Assets/Settings/Anthony";
    [MenuItem("Tools/Anthony/Configure Animated Boss and Shop Assets")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var art = Asset<FireWormPresentationSet>(Folder + "/FireWormPresentation.asset");
        art.idle = Slice("Worm/Idle.png", 90, 9, 12, new Vector2(.5f, 32f / 90));
        art.walk = Slice("Worm/Walk.png", 90, 9, 12, new Vector2(.5f, 32f / 90));
        art.attack = Slice("Worm/Attack.png", 90, 16, 12, new Vector2(.5f, 32f / 90));
        art.hit = Slice("Worm/Get Hit.png", 90, 3, 12, new Vector2(.5f, 32f / 90));
        art.death = Slice("Worm/Death.png", 90, 8, 12, new Vector2(.5f, 32f / 90));
        art.fireball = Slice("Fire Ball/Move.png", 46, 6, 46, Vector2.one * .5f);
        art.explosion = Slice("Fire Ball/Explosion.png", 46, 7, 46, Vector2.one * .5f);
        art.windupSound = Clip("boss-windup"); art.fireSound = Clip("fire-cast"); art.slamSound = Clip("boss-slam");
        art.hitSound = Clip("boss-hit"); art.deathSound = Clip("boss-death"); EditorUtility.SetDirty(art);
        var definition = Asset<EnemyDefinition>(Folder + "/FireWormDefinition.asset");
        var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/FireWormBoss.prefab");
        EditorUtility.CopySerialized(original.GetComponent<EnemyCombat>().Definition, definition);
        definition.name = "FireWormDefinition"; definition.maxHealth = 45; definition.moveSpeed = 3.5f;
        definition.aggroRadius = 100; definition.leashRadius = 100; definition.stoppingDistance = 1.4f;
        EditorUtility.SetDirty(definition);
        var attacks = Asset<FireWormBossSettings>(Folder + "/BossAttacks.asset");
        attacks.healthPhases = true; attacks.slamShockwaves = true; attacks.maximumFireballs = 16;
        attacks.attackCooldownSeconds = 1.2f; attacks.recoverySeconds = .65f;
        attacks.fireballWindupFrames = 36; attacks.volleyWindupFrames = 54; attacks.slamWindupFrames = 48;
        attacks.phaseTwoHealth = 2f / 3; attacks.phaseThreeHealth = 1f / 3;
        EditorUtility.SetDirty(attacks);
        var boss = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Enemies/FireWormBoss.prefab");
        try
        {
            var enemy = new SerializedObject(boss.GetComponent<EnemyCombat>());
            enemy.FindProperty("definition").objectReferenceValue = definition; enemy.ApplyModifiedPropertiesWithoutUndo();
            var ai = new SerializedObject(boss.GetComponent<FireWormBoss>());
            ai.FindProperty("settings").objectReferenceValue = attacks; ai.FindProperty("arena").objectReferenceValue = null; ai.ApplyModifiedPropertiesWithoutUndo();
            var hp = new SerializedObject(boss.GetComponent<EnemyHealth>());
            hp.FindProperty("despawnDelay").floatValue = 1.25f; hp.ApplyModifiedPropertiesWithoutUndo();
            boss.GetComponent<BoxCollider2D>().size = new Vector2(1.2f, 3.4f);
            var tail = boss.transform.Find("Tail"); tail.localPosition = new Vector3(1.45f, -1.175f, 0);
            tail.GetComponent<BoxCollider2D>().size = new Vector2(4, 1.05f);
            var presentation = boss.GetComponent<FireWormPresentation>() ?? boss.AddComponent<FireWormPresentation>(); presentation.presentation = art;
            var visualObject = new GameObject("Fire Worm Animated Sprite"); visualObject.transform.SetParent(boss.transform, false);
            visualObject.transform.localPosition = new Vector3(1.175f, -1.69f, 0);
            var visual = visualObject.AddComponent<SpriteRenderer>(); visual.sprite = art.idle[0]; visual.flipX = true;
            visual.sharedMaterial = boss.GetComponent<SpriteRenderer>().sharedMaterial; visual.sortingOrder = boss.GetComponent<SpriteRenderer>().sortingOrder;
            boss.GetComponent<SpriteRenderer>().enabled = false; tail.GetComponent<SpriteRenderer>().enabled = false;
            PrefabUtility.SaveAsPrefabAsset(boss, "Assets/Prefabs/Enemies/FireWormBossAnimated.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(boss); }
        var encounter = Asset<BossEncounterSettings>(Folder + "/BossEncounter.asset");
        encounter.bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/FireWormBossAnimated.prefab");
        encounter.barrierSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Prototypes/BossBlock.png");
        EditorUtility.SetDirty(encounter);
        var pillar = AssetDatabase.LoadAssetAtPath<VerticalGenerationSettings>("Assets/Settings/VerticalPillar.asset");
        pillar.bossEncounter = encounter; EditorUtility.SetDirty(pillar);
        var shop = Asset<ShopCatalog>(Folder + "/ShopCatalog.asset"); shop.purchaseSound = Clip("shop-purchase"); EditorUtility.SetDirty(shop);
        AssetDatabase.SaveAssets();
    }
    static T Asset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }
    static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Boss/" + name + ".ogg");
    static Sprite[] Slice(string suffix, int size, int count, float ppu, Vector2 pivot)
    {
        string path = "Assets/Art/Fire Worm/Sprites/" + suffix;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = ppu; importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var old = provider.GetSpriteRects();
        var frames = new SpriteRect[count];
        for (int i = 0; i < count; i++)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(suffix) + "_" + i.ToString("D2");
            var existing = old.FirstOrDefault(x => x.name == name);
            frames[i] = new SpriteRect { name = name, rect = new Rect(i * size, 0, size, size), pivot = pivot, alignment = SpriteAlignment.Custom,
                spriteID = existing != null ? existing.spriteID : GUID.Generate() };
        }
        provider.SetSpriteRects(frames);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(frames.Select(f => new SpriteNameFileIdPair(f.name, f.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
    }
}

