#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Practice2RaceSetup
{
    private const string ScenePath = "Assets/Scenes/Practice 2.unity";
    private const string BoostPrefabPath = "Assets/Prefabs/Practice 2/Speed Boost.prefab";

    [MenuItem("Multiplayer/Setup Practice 2 Race")]
    public static void SetupRace()
    {
        EnsureTags();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        SetupGameManager();
        SetupSpawners();
        SetupConnectionUi();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Practice 2 race setup complete.");
    }

    private static void EnsureTags()
    {
        AddTag("Spawner");
        AddTag("BoostSpawner");
    }

    private static void AddTag(string tag)
    {
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tags = tagManager.FindProperty("tags");

        for (int i = 0; i < tags.arraySize; i++)
        {
            if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                return;
        }

        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedProperties();
    }

    private static void SetupGameManager()
    {
        GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
        if (!gameManager)
        {
            Debug.LogError("GameManager not found in scene.");
            return;
        }

        GameObject go = gameManager.gameObject;
        if (!go.GetComponent<RaceTrackSetup>()) go.AddComponent<RaceTrackSetup>();
        if (!go.GetComponent<RaceResetManager>()) go.AddComponent<RaceResetManager>();

        BoostPickupManager boostManager = go.GetComponent<BoostPickupManager>();
        if (!boostManager) boostManager = go.AddComponent<BoostPickupManager>();

        GameObject boostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoostPrefabPath);
        SerializedObject so = new SerializedObject(boostManager);
        so.FindProperty("speedBoostPrefab").objectReferenceValue = boostPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupSpawners()
    {
        PickupManager pickupManager = Object.FindFirstObjectByType<PickupManager>();
        if (!pickupManager) return;

        GameObject go = pickupManager.gameObject;
        PlayerSpawnPoints spawnPoints = go.GetComponent<PlayerSpawnPoints>();
        if (!spawnPoints) spawnPoints = go.AddComponent<PlayerSpawnPoints>();

        Transform[] children = new Transform[go.transform.childCount];
        int count = 0;
        for (int i = 0; i < go.transform.childCount; i++)
        {
            Transform child = go.transform.GetChild(i);
            if (!child.name.StartsWith("Spawner")) continue;
            child.tag = "Spawner";
            children[count++] = child;
        }

        SerializedObject so = new SerializedObject(spawnPoints);
        SerializedProperty array = so.FindProperty("spawnPoints");
        array.arraySize = count;
        for (int i = 0; i < count; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = children[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupConnectionUi()
    {
        ConnectionUI connectionUi = Object.FindFirstObjectByType<ConnectionUI>();
        if (!connectionUi) return;

        GameObject go = connectionUi.gameObject;
        if (!go.GetComponent<ExitGameUI>()) go.AddComponent<ExitGameUI>();

        Transform playerConnection = go.transform.Find("Image/PlayerConnection");
        if (!playerConnection)
        {
            foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "PlayerConnection")
                {
                    playerConnection = child;
                    break;
                }
            }
        }

        if (!playerConnection) return;

        SerializedObject so = new SerializedObject(connectionUi);
        so.FindProperty("loginPanelRoot").objectReferenceValue = playerConnection.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
