using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool to set up the Sanitizer Spray weapon on the Player.
/// </summary>
public class SanitizerSpraySetup : EditorWindow
{
    [MenuItem("Tools/BROcoli/Setup Sanitizer Spray on Player")]
    public static void SetupSprayOnPlayer()
    {
        // Find the player object
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            EditorUtility.DisplayDialog("Error", "No GameObject with tag 'Player' found in the scene!", "OK");
            return;
        }

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController == null)
        {
            EditorUtility.DisplayDialog("Error", "Player does not have a PlayerController component!", "OK");
            return;
        }

        // Check if spray already exists
        SanitizerSpray existingSpray = player.GetComponentInChildren<SanitizerSpray>();
        if (existingSpray != null)
        {
            if (!EditorUtility.DisplayDialog("Spray Already Exists", 
                "A SanitizerSpray component already exists on the player. Do you want to recreate it?", 
                "Recreate", "Cancel"))
            {
                return;
            }
            // Remove existing
            DestroyImmediate(existingSpray.gameObject);
        }

        // Create the spray weapon as a child of the player
        GameObject sprayObj = new GameObject("SanitizerSpray");
        sprayObj.transform.SetParent(player.transform);
        sprayObj.transform.localPosition = Vector3.zero;
        sprayObj.transform.localRotation = Quaternion.identity;

        // Add the SanitizerSpray component
        SanitizerSpray spray = sprayObj.AddComponent<SanitizerSpray>();

        // Add the audio component
        AudioSource audioSource = sprayObj.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D sound
        
        ProceduralSprayAudio sprayAudio = sprayObj.AddComponent<ProceduralSprayAudio>();

        // Link the spray to the player controller via SerializedObject
        SerializedObject serializedController = new SerializedObject(playerController);
        SerializedProperty sprayProperty = serializedController.FindProperty("sanitizerSpray");
        if (sprayProperty != null)
        {
            sprayProperty.objectReferenceValue = spray;
            serializedController.ApplyModifiedProperties();
        }

        // Set weapon type to SanitizerSpray
        SerializedProperty weaponTypeProperty = serializedController.FindProperty("currentWeapon");
        if (weaponTypeProperty != null)
        {
            weaponTypeProperty.enumValueIndex = 1; // SanitizerSpray = 1
            serializedController.ApplyModifiedProperties();
        }

        // Mark scene dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        // Select the spray object
        Selection.activeGameObject = sprayObj;

        EditorUtility.DisplayDialog("Success", 
            "Sanitizer Spray weapon has been set up on the Player!\n\n" +
            "The spray includes:\n" +
            "• Particle system for spray visual\n" +
            "• Procedural spray sound effect\n" +
            "• Hand sprite holding spray can\n" +
            "• Auto-aiming at enemies in range\n\n" +
            "You can customize the spray settings in the Inspector.", 
            "OK");

        Debug.Log("[SanitizerSpraySetup] Spray weapon set up successfully on " + player.name);
    }

    [MenuItem("Tools/BROcoli/Switch Weapon/Use Sanitizer Spray")]
    public static void SwitchToSpray()
    {
        SetPlayerWeapon(PlayerController.WeaponType.SanitizerSpray);
    }

    [MenuItem("Tools/BROcoli/Switch Weapon/Use Projectile")]
    public static void SwitchToProjectile()
    {
        SetPlayerWeapon(PlayerController.WeaponType.Projectile);
    }

    private static void SetPlayerWeapon(PlayerController.WeaponType weaponType)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            EditorUtility.DisplayDialog("Error", "No GameObject with tag 'Player' found!", "OK");
            return;
        }

        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Error", "Player does not have PlayerController!", "OK");
            return;
        }

        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty weaponTypeProperty = serializedController.FindProperty("currentWeapon");
        if (weaponTypeProperty != null)
        {
            weaponTypeProperty.enumValueIndex = (int)weaponType;
            serializedController.ApplyModifiedProperties();
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"[SanitizerSpraySetup] Switched weapon to {weaponType}");
    }
}
