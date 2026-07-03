using System.IO;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using TargetsAuthoring = BovineLabs.Reaction.Authoring.Core.TargetsAuthoring;
using Target = BovineLabs.Reaction.Data.Core.Target;
using NavigationTrack = BovineLabs.Timeline.Traverse.Authoring.NavigationTrack;
using MoveToClip = BovineLabs.Timeline.Traverse.Authoring.MoveToClip;
using TimelineBeginAuthoring = BovineLabs.Timeline.Core.Authoring.TimelineBeginAuthoring;
using TimelineBeginMode = BovineLabs.Timeline.Core.Authoring.TimelineBeginMode;
using NavMeshSurfaceAuthoring = BovineLabs.NavMesh.Authoring.NavMeshSurfaceAuthoring;

public static class TraverseShowcaseBuilder
{
    private const string SampleFolder = "Assets/Samples/TraverseShowcase";
    private const string TimelineFolder = SampleFolder + "/Timelines";
    private const string ParentPath = SampleFolder + "/TraverseShowcase.unity";
    private const string SubPath = SampleFolder + "/TraverseShowcase_Sub.unity";
    private const string PlayablePath = TimelineFolder + "/TraverseShowcase.playable";

    private const string DestFolder = "Packages/BovineLabs.Timeline.Traverse/Sample~/Traverse Showcase";
    private const string DestTimelineFolder = DestFolder + "/Timelines";
    private const string DestParentPath = DestFolder + "/TraverseShowcase.unity";
    private const string DestSubPath = DestFolder + "/TraverseShowcase_Sub.unity";
    private const string DestPlayablePath = DestTimelineFolder + "/TraverseShowcase.playable";

    [MenuItem("Showcase/Build Traverse")]
    public static void Build()
    {
        EnsureFolders();
        ResetAssets();

        var parentScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(parentScene, ParentPath);

        var subScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(subScene);

        var requiredSub = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Required In Subscene.prefab");
        var requiredSubGo = (GameObject)PrefabUtility.InstantiatePrefab(requiredSub);
        requiredSubGo.name = "Required In Subscene";
        SceneManager.MoveGameObjectToScene(requiredSubGo, subScene);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(10f, 1f, 10f);
        SceneManager.MoveGameObjectToScene(floor, subScene);

        var navMeshGo = new GameObject("NavMesh");
        var nmsa = navMeshGo.AddComponent<NavMeshSurfaceAuthoring>();
        var bakedData = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.bovinelabs.traverse/Sample~/Scenes/NavMesh/BakedNavMeshData.bytes");
        nmsa.Baked = bakedData;
        nmsa.IsBaked = NavMeshSurfaceAuthoring.BakeMode.Scene;
        nmsa.StaticBake = true;
        SceneManager.MoveGameObjectToScene(navMeshGo, subScene);

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.vex.ee.baseplayer/Prefab/Player_XX.prefab");
        var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        playerGo.name = "Player";
        playerGo.transform.position = new Vector3(0f, 1f, 10f);
        SceneManager.MoveGameObjectToScene(playerGo, subScene);

        var agentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.bovinelabs.traverse/Sample~/Prefabs/Agent.prefab");
        var agentGo = (GameObject)PrefabUtility.InstantiatePrefab(agentPrefab);
        agentGo.name = "Agent";
        agentGo.transform.position = new Vector3(0f, 1f, -10f);
        SceneManager.MoveGameObjectToScene(agentGo, subScene);

        var targets = agentGo.GetComponent<TargetsAuthoring>();
        if (targets == null)
        {
            targets = agentGo.AddComponent<TargetsAuthoring>();
        }
        targets.Owner = agentGo;
        targets.Source = agentGo;
        targets.Custom = agentGo;
        targets.Target = playerGo;
        EditorUtility.SetDirty(targets);

        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timeline, PlayablePath);

        var navTrack = timeline.CreateTrack<NavigationTrack>(null, "Navigation");
        var clip = navTrack.CreateClip<MoveToClip>();
        clip.start = 0.0;
        clip.duration = 10.0;
        clip.displayName = "MoveTo Player";

        var moveToClip = (MoveToClip)clip.asset;
        moveToClip.destination = Target.Target;
        moveToClip.follow = true;
        moveToClip.stopOnExit = true;
        EditorUtility.SetDirty(moveToClip);

        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = 10.0;
        EditorUtility.SetDirty(timeline);
        EditorUtility.SetDirty(navTrack);
        AssetDatabase.SaveAssets();

        var directorGo = new GameObject("PlayableDirector");
        var director = directorGo.AddComponent<PlayableDirector>();
        director.playOnAwake = true;
        director.extrapolationMode = DirectorWrapMode.Loop;
        director.playableAsset = timeline;
        director.SetGenericBinding(navTrack, targets);

        var begin = directorGo.AddComponent<TimelineBeginAuthoring>();
        begin.Mode = TimelineBeginMode.OnLoad;
        begin.DelaySeconds = 0f;
        SceneManager.MoveGameObjectToScene(directorGo, subScene);

        EditorSceneManager.SaveScene(subScene, SubPath);
        SceneManager.SetActiveScene(parentScene);
        EditorSceneManager.CloseScene(subScene, true);

        var requiredScene = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Required In Scene.prefab");
        var requiredSceneGo = (GameObject)PrefabUtility.InstantiatePrefab(requiredScene);
        requiredSceneGo.name = "Required In Scene";
        SceneManager.MoveGameObjectToScene(requiredSceneGo, parentScene);

        var camTransform = requiredSceneGo.transform.Find("Main Camera");
        if (camTransform != null)
        {
            camTransform.position = new Vector3(0f, 20f, -30f);
            camTransform.rotation = Quaternion.Euler(30f, 0f, 0f);
            var cam = camTransform.GetComponent<Camera>();
            if (cam != null)
            {
                cam.fieldOfView = 60f;
                cam.farClipPlane = 500f;
                EditorUtility.SetDirty(cam);
            }
            EditorUtility.SetDirty(camTransform);
        }

        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SubPath);
        var subSceneGo = new GameObject("Showcase SubScene");
        var subSceneComp = subSceneGo.AddComponent<SubScene>();
        subSceneComp.SceneAsset = sceneAsset;
        subSceneComp.AutoLoadScene = true;
        EditorUtility.SetDirty(subSceneComp);
        SceneManager.MoveGameObjectToScene(subSceneGo, parentScene);

        EditorSceneManager.SaveScene(parentScene, ParentPath);

        AssetDatabase.SaveAssets();

        CopyAssetsToPackage();
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Samples"))
        {
            AssetDatabase.CreateFolder("Assets", "Samples");
        }
        if (!AssetDatabase.IsValidFolder(SampleFolder))
        {
            AssetDatabase.CreateFolder("Assets/Samples", "TraverseShowcase");
        }
        if (!AssetDatabase.IsValidFolder(TimelineFolder))
        {
            AssetDatabase.CreateFolder(SampleFolder, "Timelines");
        }
        if (!Directory.Exists(DestFolder))
        {
            Directory.CreateDirectory(DestFolder);
        }
        if (!Directory.Exists(DestTimelineFolder))
        {
            Directory.CreateDirectory(DestTimelineFolder);
        }
    }

    private static void ResetAssets()
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(TimelineFolder) != null)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:TimelineAsset", new[] { TimelineFolder }))
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            }
        }
        foreach (var p in new[] { ParentPath, SubPath })
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(p) != null)
            {
                AssetDatabase.DeleteAsset(p);
            }
        }
    }

    private static void CopyAssetsToPackage()
    {
        if (File.Exists(ParentPath))
        {
            File.Copy(ParentPath, DestParentPath, true);
        }
        if (File.Exists(SubPath))
        {
            File.Copy(SubPath, DestSubPath, true);
        }
        if (File.Exists(PlayablePath))
        {
            File.Copy(PlayablePath, DestPlayablePath, true);
        }
    }
}
