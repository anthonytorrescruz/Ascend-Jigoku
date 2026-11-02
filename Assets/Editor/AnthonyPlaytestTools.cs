using System;
using System.IO;
using AscendJigoku.Gameplay;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AnthonyPlaytestTools
{
    public const string ScenePath = "Assets/Scenes/AnthonyScene.unity";
    public const string SettingsFolder = "Assets/Settings/Anthony";
    [MenuItem("Tools/Anthony/Build or Reset AnthonyScene")]
    public static void CreateScene()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before rebuilding the test scene.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save your current scene before rebuilding AnthonyScene.");
        if (!AssetDatabase.IsValidFolder(SettingsFolder)) AssetDatabase.CreateFolder("Assets/Settings", "Anthony");
        // Open a copy: all saved source scenes remain byte-for-byte unchanged.
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/EnemyPlayground.unity");
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
        var camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
        foreach (var root in scene.GetRootGameObjects())
            if (root != player.gameObject && root != camera.gameObject) UnityEngine.Object.DestroyImmediate(root);
        player.transform.position = new Vector3(-7, 0.6f, 0);
        if (player.GetComponent<AudioSource>() == null) player.gameObject.AddComponent<AudioSource>().playOnAwake = false;
        var lockControls = player.gameObject.AddComponent<PlayerControlLock>();
        var wallet = player.gameObject.AddComponent<PrototypeWallet>();
        player.gameObject.AddComponent<PlayerUpgrades>();
        player.gameObject.AddComponent<ShopMenu>().wallet = wallet;
        var death = player.gameObject.AddComponent<AnthonyDeathHandler>();
        var host = new GameObject("Anthony Test Run");
        var session = host.AddComponent<AnthonySession>();
        session.player = player; death.session = session;
        camera.orthographicSize = 6;
        camera.transform.position = new Vector3(-7, 2.5f, -10);
        camera.GetComponent<CameraFollow>().player = player.transform;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Prototypes/BossBlock.png");
        var material = player.GetComponent<SpriteRenderer>().sharedMaterial;
        Box("Blue Checkpoint - Shop Prototype", new Vector2(6, -0.5f), new Vector2(32, 1), new Color(0.2f, 0.45f, 0.8f), sprite, material, true);
        var arena = Box("Boss Arena", new Vector2(44, -0.75f), new Vector2(38, 1.5f), new Color(0.42f, 0.42f, 0.45f), sprite, material, true);
        var merchantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossShopMediaTools.ShopPrefab);
        if (merchantPrefab != null)
        {
            var merchant = (GameObject)PrefabUtility.InstantiatePrefab(merchantPrefab); merchant.transform.position = new Vector3(-1, .02f, 0);
        }
        else
        {
            var merchant = Box("Checkpoint Merchant", new Vector2(-1, .8f), new Vector2(.9f, 1.6f), new Color(.75f, .6f, .85f), sprite, material, false);
            merchant.AddComponent<Shopkeeper>().catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(SettingsFolder + "/ShopCatalog.asset");
        }
        var checkpoint = new GameObject("Healing Checkpoint"); checkpoint.transform.position = new Vector3(-1, 0.9f, 0);
        var trigger = checkpoint.AddComponent<BoxCollider2D>(); trigger.size = new Vector2(3, 1.8f); trigger.isTrigger = true;
        var rest = checkpoint.AddComponent<HealingCheckpoint>();
        rest.respawnPoint = new GameObject("Checkpoint Spawn").transform; rest.respawnPoint.SetParent(checkpoint.transform); rest.respawnPoint.position = new Vector3(-3, 0.7f, 0);
        var fall = new GameObject("Fall Damage Zone"); fall.transform.position = new Vector3(25, -8, 0);
        var fallShape = fall.AddComponent<BoxCollider2D>(); fallShape.size = new Vector2(120, 2); fallShape.isTrigger = true; fall.AddComponent<DamageZone>();
        var boss = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/FireWormBossAnimated.prefab"));
        boss.name = "Anthony Three Phase Fire Worm"; boss.transform.position = new Vector3(47.8f, 1.88f, 0);
        session.boss = boss.GetComponent<FireWormBoss>();
        session.boss.BindArena(arena.GetComponent<Collider2D>());
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = host;
        if (SceneView.lastActiveSceneView != null)
        { SceneView.lastActiveSceneView.in2DMode = true; SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(20, 1, 0), new Vector3(78, 20, 1)), false); }
    }
    private static GameObject Box(string name, Vector2 position, Vector2 size, Color color, Sprite sprite, Material material, bool solid)
    {
        var go = new GameObject(name); go.transform.position = position;
        var visual = go.AddComponent<SpriteRenderer>(); visual.sprite = sprite; visual.drawMode = SpriteDrawMode.Sliced; visual.size = size; visual.color = color; visual.sharedMaterial = material;
        if (solid) { go.layer = 3; go.tag = "Ground"; go.AddComponent<BoxCollider2D>().size = size; }
        return go;
    }
    [MenuItem("Tools/Anthony/Build Windows Playtest")]
    public static void Build()
    {
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds/AnthonyPlaytest");
        Directory.CreateDirectory(folder);
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath }, locationPathName = Path.Combine(folder, "AscendJigoku.exe"),
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
        File.WriteAllText(Path.Combine(folder, "build-result.txt"), result.summary.result + "\nErrors: " + result.summary.totalErrors + "\nWarnings: " + result.summary.totalWarnings);
        if (result.summary.result != BuildResult.Succeeded) throw new Exception("Anthony playtest build failed: " + result.summary.result);
    }
}
