using System;
using System.Linq;
using AscendJigoku.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BossShopMediaTools
{
    const string Settings = "Assets/Settings/Anthony/";
    public const string ShopPrefab = "Assets/Prefabs/NPCs/MementoMoriShopkeeper.prefab";
    [MenuItem("Tools/Anthony/Import Boss and Skeleton Media")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before importing media.");
        var art = AssetDatabase.LoadAssetAtPath<FireWormPresentationSet>(Settings + "FireWormPresentation.asset");
        const string worm = "Assets/Art/Fire Worm/Sprites/";
        art.idle = Slice(worm + "Worm/Idle.png", 90, 90, 9, 1, 12, new Vector2(.5f, 32f / 90));
        art.walk = Slice(worm + "Worm/Walk.png", 90, 90, 9, 1, 12, new Vector2(.5f, 32f / 90));
        art.attack = Slice(worm + "Worm/Attack.png", 90, 90, 16, 1, 12, new Vector2(.5f, 32f / 90));
        art.hit = Slice(worm + "Worm/Get Hit.png", 90, 90, 3, 1, 12, new Vector2(.5f, 32f / 90));
        art.death = Slice(worm + "Worm/Death.png", 90, 90, 8, 1, 12, new Vector2(.5f, 32f / 90));
        art.fireball = Slice(worm + "Fire Ball/Move.png", 46, 46, 6, 1, 46, Vector2.one * .5f);
        art.explosion = Slice(worm + "Fire Ball/Explosion.png", 46, 46, 7, 1, 46, Vector2.one * .5f);
        art.fireballAttack = art.attack.ToArray();
        // Source pack has no slam sheet: use its non-breathing poses plus the presentation's lift/squash/impact motion.
        art.slamAttack = art.attack.Take(9).ToArray();
        art.battleMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Boss/FireWormBattleMusic.mp3");
        EditorUtility.SetDirty(art);
        var musicImporter = (AudioImporter)AssetImporter.GetAtPath("Assets/Audio/Boss/FireWormBattleMusic.mp3");
        var audioSettings = musicImporter.defaultSampleSettings; audioSettings.loadType = AudioClipLoadType.Streaming;
        musicImporter.defaultSampleSettings = audioSettings; musicImporter.SaveAndReimport();
        var boss = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Enemies/FireWormBossAnimated.prefab");
        try
        {
            boss.GetComponent<FireWormPresentation>().presentation = art;
            if (boss.GetComponent<BossEncounterMusic>() == null) boss.AddComponent<BossEncounterMusic>();
            PrefabUtility.SaveAsPrefabAsset(boss, "Assets/Prefabs/Enemies/FireWormBossAnimated.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(boss); }
        var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(Settings + "ShopCatalog.asset");
        catalog.openSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Shops/ShopWhisper.mp3"); EditorUtility.SetDirty(catalog);
        var frames = Slice("Assets/Art/Shops/MementoMoriSkeleton.png", 128, 96, 4, 4, 40, new Vector2(.5f, 0));
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/NPCs")) AssetDatabase.CreateFolder("Assets/Prefabs", "NPCs");
        var keeper = new GameObject("Memento Mori Skeleton Shopkeeper");
        try
        {
            ConfigureMerchant(keeper, frames, catalog);
            PrefabUtility.SaveAsPrefabAsset(keeper, ShopPrefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(keeper); }
        var generation = AssetDatabase.LoadAssetAtPath<VerticalGenerationSettings>("Assets/Settings/VerticalPillar.asset");
        generation.checkpointShopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefab).GetComponent<Shopkeeper>(); EditorUtility.SetDirty(generation);
        UpgradeAnthony(frames, catalog);
        AssetDatabase.SaveAssets();
    }
    static void ConfigureMerchant(GameObject go, Sprite[] frames, ShopCatalog catalog)
    {
        // The old rectangle used different X/Y scales. Preserve height while restoring the artwork's aspect ratio.
        Vector3 scale = go.transform.localScale; scale.x = Mathf.Abs(scale.y) * (scale.x < 0 ? -1 : 1); go.transform.localScale = scale;
        var visual = go.GetComponent<SpriteRenderer>(); if (visual == null) visual = go.AddComponent<SpriteRenderer>();
        visual.sprite = frames[0]; visual.drawMode = SpriteDrawMode.Simple; visual.color = Color.white; visual.sortingOrder = 2;
        var animation = go.GetComponent<ShopkeeperAnimation>(); if (animation == null) animation = go.AddComponent<ShopkeeperAnimation>(); animation.frames = frames; animation.framesPerSecond = 12.5f;
        var keeper = go.GetComponent<Shopkeeper>(); if (keeper == null) keeper = go.AddComponent<Shopkeeper>(); keeper.catalog = catalog; keeper.displayName = "Memento Mori";
    }
    static void UpgradeAnthony(Sprite[] frames, ShopCatalog catalog)
    {
        var previous = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(AnthonyPlaytestTools.ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(AnthonyPlaytestTools.ScenePath, OpenSceneMode.Additive);
        if (scene.isDirty) EditorSceneManager.SaveScene(scene);
        string project = System.IO.Directory.GetParent(Application.dataPath).FullName;
        string backup = System.IO.Path.Combine(project, "Temp/AnthonyScene-before-media.unity");
        if (!System.IO.File.Exists(backup)) System.IO.File.Copy(System.IO.Path.Combine(project, scene.path), backup);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var keeper in root.GetComponentsInChildren<Shopkeeper>(true))
            {
                bool wasAnimated = keeper.GetComponent<ShopkeeperAnimation>() != null;
                var visual = keeper.GetComponent<SpriteRenderer>(); float floor = visual != null ? visual.bounds.min.y : keeper.transform.position.y;
                ConfigureMerchant(keeper.gameObject, frames, catalog); keeper.name = "Memento Mori Skeleton Shopkeeper";
                if (!wasAnimated) { var position = keeper.transform.position; position.y = floor + .02f; keeper.transform.position = position; }
            }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        if (opened) { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(previous); }
    }
    public static Sprite[] Slice(string path, int width, int height, int columns, int rows, float ppu, Vector2 pivot)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = ppu; importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var old = provider.GetSpriteRects(); var frames = new SpriteRect[columns * rows];
        for (int i = 0; i < frames.Length; i++)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path) + "_" + i.ToString("D2");
            var existing = old.FirstOrDefault(x => x.name == name);
            frames[i] = new SpriteRect { name = name, rect = new Rect(i % columns * width, (rows - 1 - i / columns) * height, width, height), pivot = pivot,
                alignment = SpriteAlignment.Custom, spriteID = existing != null ? existing.spriteID : GUID.Generate() };
        }
        provider.SetSpriteRects(frames);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(frames.Select(f => new SpriteNameFileIdPair(f.name, f.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
    }
}
