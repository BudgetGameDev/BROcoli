using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Synth.Editor
{
    public static class SynthDemoBuilder
    {
        public const string ScenePath="Assets/ProceduralSynthDemo/MonoSynthDemo.unity";
        [MenuItem("Tools/Brocoli Synth/Create isolated demo scene")]
        private static void CreateDemoMenu() => CreateDemo();
        public static string CreateDemo()
        {
            if(EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Exit Play mode before updating the saved synth demo scene.");
            if(!AssetDatabase.IsValidFolder("Assets/ProceduralSynthDemo"))AssetDatabase.CreateFolder("Assets","ProceduralSynthDemo");
            var previous=SceneManager.GetActiveScene();
            var scene=SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded=scene.IsValid() && scene.isLoaded;
            if(!wasLoaded)
                scene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null
                    ? EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            bool wasDirty=wasLoaded && scene.isDirty;
            bool changed=false;
            try
            {
                SceneManager.SetActiveScene(scene);
                MonoSynthGenerator synth=null;
                GameObject namedInstrument=null;
                bool hasListener=false;
                foreach(var root in scene.GetRootGameObjects())
                {
                    if(synth==null)synth=root.GetComponentInChildren<MonoSynthGenerator>(true);
                    if(root.name=="Modular Mono")namedInstrument=root;
                    if(root.GetComponentInChildren<AudioListener>(true)!=null)hasListener=true;
                }
                if(!hasListener)
                {
                    var cameraObject=new GameObject("Audition listener",typeof(Camera),typeof(AudioListener));
                    Undo.RegisterCreatedObjectUndo(cameraObject,"Create synth demo listener");
                    var camera=cameraObject.GetComponent<Camera>();
                    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.047f,.039f);
                    changed=true;
                }
                bool newInstrument=synth==null && namedInstrument==null;
                var instrument=synth!=null?synth.gameObject:namedInstrument;
                if(newInstrument)
                {
                    instrument=new GameObject("Modular Mono");
                    Undo.RegisterCreatedObjectUndo(instrument,"Create synth demo instrument");
                    changed=true;
                }
                var source=instrument.GetComponent<AudioSource>();
                if(source==null) { source=Undo.AddComponent<AudioSource>(instrument); changed=true; }
                if(synth==null) { synth=Undo.AddComponent<MonoSynthGenerator>(instrument); changed=true; }
                // Existing instrument values and user routing are preserved on repeated runs.
                if(newInstrument)
                {
                    source.playOnAwake=false;source.loop=true;source.spatialBlend=0;source.volume=.75f;
                    synth.parameters=SynthParameters.HeavyBass;
                    // A package must also work without the BROcoli game's assets.
                    // Consumers can assign any mixer group on the AudioSource.
                    source.outputAudioMixerGroup=null;
                }
#if UNITY_6000_5_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)
                if(source.generator==null) { Undo.RecordObject(source,"Assign synth generator"); source.generator=synth; changed=true; }
#endif
                if(instrument.GetComponent<SynthAudition>()==null) { Undo.AddComponent<SynthAudition>(instrument); changed=true; }
                if(instrument.GetComponent<SynthAdaptation>()==null) { Undo.AddComponent<SynthAdaptation>(instrument); changed=true; }
                // Never save somebody else's pre-existing dirty work. On that path additions
                // remain in the open scene with Undo support for the user's normal save flow.
                if(changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if(!wasDirty && !EditorSceneManager.SaveScene(scene,ScenePath))
                        throw new System.InvalidOperationException("Demo scene save failed.");
                }
            }
            finally
            {
                if(previous.IsValid() && previous.isLoaded)SceneManager.SetActiveScene(previous);
                if(!wasLoaded)EditorSceneManager.CloseScene(scene,true);
            }
            return ScenePath;
        }
    }
}
