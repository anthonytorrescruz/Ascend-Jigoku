using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GrimDeathAuthoringTools
{
    public const string SettingsPath="Assets/Settings/GrimDeath.asset";
    public const string PracticePath="Assets/Scenes/GrimDeathPlayground.unity";
    const string Art="Assets/Art/Grim Death/png/";
    const string Audio="Assets/Audio/Boss/Grim Death/";
    [MenuItem("Tools/Anthony/Open Grim Death Practice")]
    public static void OpenPractice(){if(!EditorApplication.isPlayingOrWillChangePlaymode && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(PracticePath);}
    [MenuItem("Tools/Anthony/Configure Grim Death Boss")]
    public static void Configure(){
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        if(SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save your current scene before authoring the boss.");
        var s=AssetDatabase.LoadAssetAtPath<GrimDeathSettings>(SettingsPath);
        if(s==null){s=ScriptableObject.CreateInstance<GrimDeathSettings>();AssetDatabase.CreateAsset(s,SettingsPath);}
        s.idle=Slice(Art+"idle.png",100,100,5,1,4,20,new Vector2(.42f,.17f));
        s.idle2=Slice(Art+"idle2.png",100,100,4,2,8,20,new Vector2(.42f,.17f));
        s.scythe=Slice(Art+"attacking.png",100,100,6,3,13,20,new Vector2(.42f,.17f));
        s.skill=Slice(Art+"skill1.png",100,100,6,2,12,20,new Vector2(.42f,.17f));
        s.summon=Slice(Art+"summon.png",100,100,4,2,5,20,new Vector2(.42f,.17f));
        s.death=Slice(Art+"death.png",100,100,10,2,20,20,new Vector2(.42f,.17f));
        s.spiritAppear=Slice(Art+"summonAppear.png",50,50,3,2,6,14,new Vector2(.48f,.25f));
        s.spiritIdle=Slice(Art+"summonIdle.png",50,50,4,1,4,14,new Vector2(.48f,.25f));
        s.spiritDeath=Slice(Art+"summonDeath.png",50,50,3,2,6,14,new Vector2(.48f,.25f));
        s.music=Clip("GrimDeathBattleMusic.mp3");s.scytheSound=Clip("Scythe.wav");s.stepSound=Clip("ShadowStep.wav");
        s.castSound=Clip("ShadowCast.wav");s.spiritSound=Clip("SpiritAppear.wav");s.spiritDeathSound=Clip("SpiritDeath.wav");
        s.hitSound=Clip("GrimHit.wav");s.deathSound=Clip("GrimDeath.wav");s.pillarSound=Clip("Pillar.wav");s.burstSound=Clip("Burst.wav");
        var importer=(AudioImporter)AssetImporter.GetAtPath(Audio+"GrimDeathBattleMusic.mp3");
        var audio=importer.defaultSampleSettings;audio.loadType=AudioClipLoadType.Streaming;importer.defaultSampleSettings=audio;importer.SaveAndReimport();
        // Existing balance is preserved if this importer is run again.
        var spirit=new GameObject("Grim Death Spirit");
        try{
            var sr=spirit.AddComponent<SpriteRenderer>();sr.sprite=s.spiritIdle[0];sr.sortingOrder=8;
            var rb=spirit.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;rb.gravityScale=0;rb.useFullKinematicContacts=true;
            var c=spirit.AddComponent<BoxCollider2D>();c.size=s.spiritHurtboxSize;c.offset=s.spiritHurtboxOffset;
            var hp=spirit.AddComponent<EnemyHealth>();SetHealth(hp,1,.65f,0);
            spirit.AddComponent<Pogoable>();var attack=spirit.AddComponent<CombatAttack>();attack.SetTeam(CombatTeam.Enemy);
            spirit.AddComponent<GrimDeathSpirit>();
            s.spiritPrefab=PrefabUtility.SaveAsPrefabAsset(spirit,"Assets/Prefabs/Enemies/GrimDeathSpirit.prefab");
        }finally{UnityEngine.Object.DestroyImmediate(spirit);}
        var boss=new GameObject("Grim Death");
        try{
            var sr=boss.AddComponent<SpriteRenderer>();sr.sprite=s.idle[0];sr.sortingOrder=7;
            var rb=boss.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;rb.gravityScale=0;rb.useFullKinematicContacts=true;rb.interpolation=RigidbodyInterpolation2D.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            var c=boss.AddComponent<BoxCollider2D>();c.size=new Vector2(.8f,2.65f);c.offset=new Vector2(0,1.375f);
            var hp=boss.AddComponent<EnemyHealth>();SetHealth(hp,s.health,s.death.Length/s.framesPerSecond+.35f,1);
            boss.AddComponent<Pogoable>();var attack=boss.AddComponent<CombatAttack>();attack.SetTeam(CombatTeam.Enemy);
            var feedback=boss.AddComponent<CombatFeedback>();var so=new SerializedObject(feedback);so.FindProperty("skipDeathBurst").boolValue=true;so.FindProperty("volume").floatValue=0;so.ApplyModifiedPropertiesWithoutUndo();
            boss.AddComponent<GrimDeathBoss>().settings=s;
            s.bossPrefab=PrefabUtility.SaveAsPrefabAsset(boss,"Assets/Prefabs/Enemies/GrimDeath.prefab");
        }finally{UnityEngine.Object.DestroyImmediate(boss);}
        var generation=AssetDatabase.LoadAssetAtPath<VerticalGenerationSettings>("Assets/Settings/VerticalPillar.asset");generation.finalBossEncounter=s;EditorUtility.SetDirty(generation);EditorUtility.SetDirty(s);AssetDatabase.SaveAssets();
        BuildPractice(s,generation);
        if(!EditorBuildSettings.scenes.Any(scene=>scene.path==PracticePath))
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(PracticePath,true)}).ToArray();
        AssetDatabase.SaveAssets();
    }
    static AudioClip Clip(string name)=>AssetDatabase.LoadAssetAtPath<AudioClip>(Audio+name);
    static void SetHealth(EnemyHealth hp,int max,float delay,int cap){var so=new SerializedObject(hp);so.FindProperty("maxHealth").intValue=max;so.FindProperty("despawnDelay").floatValue=delay;so.FindProperty("damagePerHitCap").intValue=cap;so.ApplyModifiedPropertiesWithoutUndo();}
    static Sprite[] Slice(string path,int width,int height,int columns,int rows,int count,float ppu,Vector2 pivot){
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=ppu;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
        var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();var old=provider.GetSpriteRects();
        var rects=new SpriteRect[count];for(int i=0;i<count;i++){
            string name=System.IO.Path.GetFileNameWithoutExtension(path)+"_"+i.ToString("D2");var prior=old.FirstOrDefault(x=>x.name==name);
            rects[i]=new SpriteRect{name=name,rect=new Rect(i%columns*width,(rows-1-i/columns)*height,width,height),pivot=pivot,alignment=SpriteAlignment.Custom,spriteID=prior!=null?prior.spriteID:GUID.Generate()};
        }
        provider.SetSpriteRects(rects);provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));provider.Apply();importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x=>x.name).ToArray();
    }
    static void BuildPractice(GrimDeathSettings s,VerticalGenerationSettings generation){
        if(System.IO.File.Exists(PracticePath))return; // preserve later hand edits
        var previous=SceneManager.GetActiveScene();var donor=SceneManager.GetSceneByPath("Assets/Scenes/ProceduralPillar.unity");bool opened=!donor.IsValid() || !donor.isLoaded;
        if(opened)donor=EditorSceneManager.OpenScene("Assets/Scenes/ProceduralPillar.unity",OpenSceneMode.Additive);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        var roots=donor.GetRootGameObjects();var source=roots.Select(r=>r.GetComponent<PlayerController>()).First(p=>p!=null);
        var actor=UnityEngine.Object.Instantiate(source.gameObject);SceneManager.MoveGameObjectToScene(actor,scene);actor.name="Player";actor.transform.position=new Vector3(-12,1.5f,0);var player=actor.GetComponent<PlayerController>();
        foreach(var root in roots){if(root.GetComponent<Camera>()!=null){var camera=UnityEngine.Object.Instantiate(root);SceneManager.MoveGameObjectToScene(camera,scene);camera.name="Main Camera";var follow=camera.GetComponent<CameraFollow>();if(follow!=null)follow.player=player.transform;camera.GetComponent<Camera>().orthographicSize=6;}else if(root.name.Contains("Global Light")){var light=UnityEngine.Object.Instantiate(root);SceneManager.MoveGameObjectToScene(light,scene);}}
        var platform=UnityEngine.Object.Instantiate(generation.platformPrefab);SceneManager.MoveGameObjectToScene(platform,scene);platform.name="Grim Death full-width practice platform";platform.transform.position=new Vector3(0,-.6f,0);platform.transform.localScale=new Vector3(generation.pillarWidth,1.2f,1);platform.GetComponent<SpriteRenderer>().color=new Color(.28f,.23f,.34f);
        var setup=new GameObject("Grim Death Practice Setup").AddComponent<GrimDeathArenaSetup>();setup.settings=s;setup.floor=platform.GetComponent<Collider2D>();setup.player=player;
        EditorSceneManager.SaveScene(scene,PracticePath);EditorSceneManager.CloseScene(scene,true);if(opened)EditorSceneManager.CloseScene(donor,true);SceneManager.SetActiveScene(previous);
    }
}
