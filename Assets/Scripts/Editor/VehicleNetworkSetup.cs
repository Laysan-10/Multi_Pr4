#if UNITY_EDITOR
using FishNet.Component.Transforming;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VehicleNetworkSetup
{
    private const string Practice2ScenePath = "Assets/Scenes/Practice 2.unity";
    private const string MenuPath = "Multiplayer/Setup Scene Vehicles";
    private const string Practice2MenuPath = "Multiplayer/Setup Practice 2 Vehicles";

    [MenuItem(Practice2MenuPath)]
    public static void SetupPractice2Vehicles()
    {
        Scene scene = EditorSceneManager.OpenScene(Practice2ScenePath, OpenSceneMode.Single);
        SetupSceneVehicles();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("VehicleNetworkSetup: Practice 2 scene saved with networked vehicles.");
    }

    [MenuItem(MenuPath)]
    public static void SetupSceneVehicles()
    {
        PrometeoCarController[] cars = Object.FindObjectsByType<PrometeoCarController>(FindObjectsSortMode.None);
        if (cars.Length == 0)
        {
            Debug.LogWarning("VehicleNetworkSetup: no PrometeoCarController found in the active scene.");
            return;
        }

        int configured = 0;
        foreach (PrometeoCarController carController in cars)
        {
            if (ConfigureVehicle(carController.gameObject))
                configured++;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"VehicleNetworkSetup: configured {configured} vehicle(s) in scene '{SceneManager.GetActiveScene().name}'.");
    }

    private static bool ConfigureVehicle(GameObject vehicleRoot)
    {
        NetworkObject networkObject = vehicleRoot.GetComponent<NetworkObject>();
        if (!networkObject)
            networkObject = Undo.AddComponent<NetworkObject>(vehicleRoot);

        NetworkTransform networkTransform = vehicleRoot.GetComponent<NetworkTransform>();
        if (!networkTransform)
            networkTransform = Undo.AddComponent<NetworkTransform>(vehicleRoot);

        NetworkVehicle networkVehicle = vehicleRoot.GetComponent<NetworkVehicle>();
        if (!networkVehicle)
            networkVehicle = Undo.AddComponent<NetworkVehicle>(vehicleRoot);

        SerializedObject nt = new SerializedObject(networkTransform);
        nt.FindProperty("_componentConfiguration").enumValueIndex = 2;
        nt.FindProperty("_clientAuthoritative").boolValue = true;
        nt.FindProperty("_synchronizePosition").boolValue = true;
        nt.FindProperty("_synchronizeRotation").boolValue = true;
        nt.ApplyModifiedPropertiesWithoutUndo();

        PrometeoCarController carController = vehicleRoot.GetComponent<PrometeoCarController>();
        if (carController)
            carController.enabled = false;

        EditorUtility.SetDirty(vehicleRoot);
        return true;
    }
}
#endif
