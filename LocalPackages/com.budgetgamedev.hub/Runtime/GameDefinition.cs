using UnityEngine;

namespace BudgetGameDev.Hub
{
    /// <summary>
    /// A game's entry in the launcher. Each game package ships exactly one of
    /// these under <c>Resources/GameRegistry/</c>; that is the whole registration
    /// protocol. The hub never references a game's code, so adding or removing a
    /// game is a manifest edit rather than a change here.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameDefinition",
        menuName = "Budget GameDev/Game Definition",
        order = 0
    )]
    public sealed class GameDefinition : ScriptableObject
    {
        /// <summary>Folder, inside any Resources folder, that the catalog scans.</summary>
        public const string ResourceFolder = "GameRegistry";

        [Header("Identity")]
        [Tooltip("Stable, lowercase, unique. Used for save keys and the last-played record.")]
        [SerializeField]
        private string id;

        [SerializeField]
        private string displayName;

        [TextArea(2, 4)]
        [SerializeField]
        private string description;

        [Tooltip("Shown beside the name in the launcher list. Optional.")]
        [SerializeField]
        private Sprite icon;

        [Tooltip("Lower sorts first; ties fall back to display name.")]
        [SerializeField]
        private int sortOrder;

        [Header("Entry point")]
        [Tooltip("Scene the launcher loads when this game is selected: its own main menu.")]
        [SerializeField]
        private string mainMenuSceneName;

        [Tooltip("Every scene this game needs in the build, including the main menu.")]
        [SerializeField]
        private string[] sceneNames = new string[0];

        [Header("Audio")]
        [Tooltip("Resources path of this game's AudioMixer. Optional.")]
        [SerializeField]
        private string mixerResourcePath;

        public string Id => string.IsNullOrWhiteSpace(id) ? name : id;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;

        public string Description => description;

        public Sprite Icon => icon;

        public int SortOrder => sortOrder;

        public string MainMenuSceneName => mainMenuSceneName;

        public string MixerResourcePath => mixerResourcePath;

        public System.Collections.Generic.IReadOnlyList<string> SceneNames => sceneNames;

        /// <summary>
        /// True when this entry can actually be launched. An unplayable entry is
        /// listed but disabled, which is friendlier than hiding it: a game whose
        /// scenes are missing from the build is a setup mistake worth seeing.
        /// </summary>
        public bool IsPlayable => !string.IsNullOrWhiteSpace(mainMenuSceneName);

#if UNITY_EDITOR
        [Header("Editor wiring")]
        [Tooltip("Drag the main menu scene here; the scene names above are kept in sync.")]
        [SerializeField]
        private UnityEditor.SceneAsset mainMenuScene;

        [Tooltip("Every other scene this game needs in the build.")]
        [SerializeField]
        private UnityEditor.SceneAsset[] additionalScenes = new UnityEditor.SceneAsset[0];

        public UnityEditor.SceneAsset MainMenuScene => mainMenuScene;

        public UnityEditor.SceneAsset[] AdditionalScenes => additionalScenes;

        /// <summary>Keeps the runtime scene names matching the dragged assets.</summary>
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
                id = name.ToLowerInvariant();

            mainMenuSceneName = mainMenuScene != null ? mainMenuScene.name : string.Empty;

            var names = new System.Collections.Generic.List<string>();
            if (mainMenuScene != null)
                names.Add(mainMenuScene.name);
            foreach (UnityEditor.SceneAsset scene in additionalScenes)
                if (scene != null && !names.Contains(scene.name))
                    names.Add(scene.name);
            sceneNames = names.ToArray();
        }
#endif
    }
}
