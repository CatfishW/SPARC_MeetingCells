using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinemachine;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.Video;

namespace Sparc.Original.Editor
{
    public static class OriginalProjectBuilder
    {
        public const string ImportedScene="Assets/SPARCOriginal/Scenes/MeetingCells_NewEnv.unity";
        public const string PlayableScene="Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity";
        static string PathOf(Transform t){return t.parent?PathOf(t.parent)+"/"+t.name:t.name;}
        public static void Prepare()
        {
            if(!File.Exists(ImportedScene))throw new InvalidOperationException("Run Tools/restore_original_assets.py first.");
            var pipeline=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Rendering/Default_PipelineAsset.asset");
            if(pipeline)
            {
                Directory.CreateDirectory("Assets/SPARCOriginal/Generated");
                string copyPath="Assets/SPARCOriginal/Generated/OriginalPipelineWeb.asset";
                if(!AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(copyPath))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(pipeline),copyPath);
                var compatible=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(copyPath);EditorUtility.CopySerialized(pipeline,compatible);compatible.name="OriginalPipelineWeb";
                var settings=new SerializedObject(compatible);var renderers=settings.FindProperty("m_RendererDataList");
                for(int i=0;i<renderers.arraySize;i++)
                {
                    var renderer=renderers.GetArrayElementAtIndex(i).objectReferenceValue as UnityEngine.Rendering.Universal.UniversalRendererData;if(!renderer)continue;
                    string rendererPath="Assets/SPARCOriginal/Generated/OriginalRendererWeb"+i+".asset";
                    if(!AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(rendererPath))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(renderer),rendererPath);
                    var fixedRenderer=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(rendererPath);
                    // The source decal feature in this Unity beta dereferences cameraColor when
                    // rendering directly to the backbuffer. Keep the decal feature and all source
                    // renderer settings; ensure the intermediate color attachment actually exists.
                    fixedRenderer.intermediateTextureMode=UnityEngine.Rendering.Universal.IntermediateTextureMode.Always;
                    // WebGL cannot use URP's integer rendering-layer attachment on this
                    // source renderer. Keep each authored decal and its material; none uses
                    // a non-default projector layer. Avoid the invalid mixed MRT draw path.
                    foreach(var feature in fixedRenderer.rendererFeatures.Where(f=>f&&f.GetType().Name=="DecalRendererFeature"))
                    {var featureData=new SerializedObject(feature);var layers=featureData.FindProperty("m_Settings.decalLayers");if(layers!=null){layers.boolValue=false;featureData.ApplyModifiedPropertiesWithoutUndo();}}

                    EditorUtility.SetDirty(fixedRenderer);renderers.GetArrayElementAtIndex(i).objectReferenceValue=fixedRenderer;
                }
                settings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(compatible);AssetDatabase.SaveAssets();
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline=compatible;QualitySettings.renderPipeline=compatible;
            }

            foreach(var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w=>w.GetType().Name.Contains("TMP_PackageResourceImporter")))window.Close();
            var scene=EditorSceneManager.OpenScene(ImportedScene,OpenSceneMode.Single);
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            int missing=all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            var directors=all.SelectMany(t=>t.GetComponents<PlayableDirector>()).ToArray();
            var evidence=new Dictionary<string,object>();var timelineRows=new List<object>();
            var probes=all.SelectMany(t=>t.GetComponents<ReflectionProbe>()).Where(p=>p.mode==UnityEngine.Rendering.ReflectionProbeMode.Realtime).ToArray();
            evidence["reflectionScheduling"]=probes.Select(p=>new{path=PathOf(p.transform),resolution=p.resolution,originalRefresh=p.refreshMode.ToString(),originalShadowDistance=p.shadowDistance}).ToArray();
            foreach(var probe in probes){probe.refreshMode=UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;probe.timeSlicingMode=UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;probe.shadowDistance=0;}
            var scheduler=new GameObject("SPARC / bounded original reflections").AddComponent<OriginalReflectionScheduler>();scheduler.probes=probes;
            foreach(var particles in all.SelectMany(t=>t.GetComponents<ParticleSystemRenderer>())){particles.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;if(particles.renderMode==ParticleSystemRenderMode.Mesh)particles.enableGPUInstancing=true;}
            Directory.CreateDirectory("Assets/StreamingAssets/OriginalMedia");var media=new List<object>();
            foreach(var video in all.SelectMany(t=>t.GetComponents<VideoPlayer>()))
            {
                var clipPath=AssetDatabase.GetAssetPath(video.clip);if(string.IsNullOrEmpty(clipPath))continue;string guid=AssetDatabase.AssetPathToGUID(clipPath);string filename=guid+Path.GetExtension(clipPath);
                string destination="Assets/StreamingAssets/OriginalMedia/"+filename;File.Copy(clipPath,destination,true);
                var originalVideo=video.gameObject.AddComponent<OriginalVideo>();originalVideo.mediaFile=filename;originalVideo.authoredPlayOnAwake=video.playOnAwake;video.playOnAwake=false;
                video.clip=null;video.source=VideoSource.Url;video.url="StreamingAssets/OriginalMedia/"+filename;
                media.Add(new{original=clipPath,streaming=destination});
            }
            evidence["originalVideos"]=media;
            foreach(var d in directors)
            {
                var guard=d.gameObject.GetComponent<OriginalDirector>()??d.gameObject.AddComponent<OriginalDirector>();guard.director=d;guard.authoredPlayOnAwake=d.playOnAwake;d.playOnAwake=false;
                var outputs=d.playableAsset?d.playableAsset.outputs.ToArray():Array.Empty<PlayableBinding>();
                timelineRows.Add(new{path=PathOf(d.transform),asset=AssetDatabase.GetAssetPath(d.playableAsset),duration=d.playableAsset?d.playableAsset.duration:0,authoredPlayOnAwake=guard.authoredPlayOnAwake,active=d.gameObject.activeInHierarchy,bindings=outputs.Select(b=>new{track=b.streamName,type=b.outputTargetType?.FullName,target=d.GetGenericBinding(b.sourceObject)?d.GetGenericBinding(b.sourceObject).name:null}).ToArray()});
            }
            var original=all.SelectMany(t=>t.GetComponents<OriginalBehaviour>()).ToArray();
            foreach(var oldInput in all.SelectMany(t=>t.GetComponents<UnityEngine.EventSystems.StandaloneInputModule>()))
            {oldInput.enabled=false;var input=oldInput.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>()??oldInput.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();input.AssignDefaultActions();}
            foreach(var oldReceiver in all.SelectMany(t=>t.GetComponents<UnityEngine.InputSystem.PlayerInput>()))oldReceiver.enabled=false;

            evidence["restoredPrefabOverrides"]=OriginalOverrideRestorer.Apply(original);
            evidence["providerCredentialsRemoved"]=OriginalCredentialGuard.Scrub(original);
            evidence["reboundNativeEventTypeNames"]=RebindEventTypes(all);
            foreach(var component in original)
            {
                if(component.sourceType=="CutoutCircleAnimationControl")
                {var adapter=component.gameObject.GetComponent<CutoutCircleAnimationControl>()??component.gameObject.AddComponent<CutoutCircleAnimationControl>();adapter.cutoutSize=component.Number("cutoutSize",0.5f);adapter.cutoutCircleObject=component.Object("cutoutCircleObject");}
                if(component.sourceType=="DistortionEffectControl")
                {var adapter=component.gameObject.GetComponent<DistortionEffectControl>()??component.gameObject.AddComponent<DistortionEffectControl>();adapter.distortionAmount=component.Number("distortionAmount",0.1f);adapter.distortionSpeed=component.Number("distortionSpeed",1);adapter.materialSource=component.Object("materialSource");}
            }
            var system=new GameObject("SPARC / original-scene runtime");var runtime=system.AddComponent<OriginalRuntime>();
            runtime.originalWhiteWave=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resource/SM_Updated_WBC/ANIMATIONS/UPDATED_WBC_Wave/Updated Wave.anim");
            var appearance=new GameObject("SPARC Original Appearance").AddComponent<OriginalAppearance>();appearance.skinAtlases=Enumerable.Range(1,5).Select(i=>AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SPARC/Generated/Materials/SkinAtlas"+i+".asset")).ToArray();
            // Never rebuild the source environment, move its objects or replace its authored camera tracks.
            Directory.CreateDirectory("Assets/SPARCOriginal/Generated");
            var surfaces=all.SelectMany(t=>t.GetComponents<Unity.AI.Navigation.NavMeshSurface>()).ToArray();
            foreach(var surface in surfaces)
            {
                surface.RemoveData();surface.BuildNavMesh();
                if(surface.navMeshData){string navigation="Assets/SPARCOriginal/Generated/OriginalNavigation_"+surface.GetInstanceID()+".asset";AssetDatabase.CreateAsset(surface.navMeshData,navigation);surface.AddData();}
            }
            evidence["navigationRebuiltFromOriginalGeometry"]=surfaces.Select(s=>new{path=PathOf(s.transform),data=AssetDatabase.GetAssetPath(s.navMeshData)}).ToArray();

            EditorSceneManager.SaveScene(scene,PlayableScene);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(PlayableScene,true)};
            var lighting=Lightmapping.lightingDataAsset;
            if(lighting)
            {
                string sourceLighting=AssetDatabase.GetAssetPath(lighting);string copy="Assets/SPARCOriginal/Generated/OriginalLightingData.asset";
                if(!AssetDatabase.LoadAssetAtPath<LightingDataAsset>(copy))AssetDatabase.CopyAsset(sourceLighting,copy);
                var newLighting=AssetDatabase.LoadAssetAtPath<LightingDataAsset>(copy);
                if(newLighting){var serialized=new SerializedObject(newLighting);var targetScene=serialized.FindProperty("m_Scene");if(targetScene!=null&&targetScene.propertyType==SerializedPropertyType.ObjectReference){targetScene.objectReferenceValue=AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayableScene);serialized.ApplyModifiedPropertiesWithoutUndo();}Lightmapping.lightingDataAsset=newLighting;EditorSceneManager.SaveScene(scene);}
                evidence["lightingRelink"]=new{originalAsset=sourceLighting,copy};
            }

            evidence["scene"]=PlayableScene;evidence["originalSceneObjects"]=all.Length;evidence["missingScripts"]=missing;evidence["missingScriptPaths"]=all.Where(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0).Select(t=>PathOf(t)).ToArray();evidence["originalDirectors"]=directors.Length;evidence["timelines"]=timelineRows;
            evidence["sourceTypes"]=original.GroupBy(c=>c.sourceType).ToDictionary(g=>g.Key,g=>g.Count());
            evidence["animators"]=all.SelectMany(t=>t.GetComponents<Animator>()).Select(a=>new{path=PathOf(a.transform),enabled=a.enabled,controller=AssetDatabase.GetAssetPath(a.runtimeAnimatorController),avatar=AssetDatabase.GetAssetPath(a.avatar),clips=a.runtimeAnimatorController?a.runtimeAnimatorController.animationClips.Where(c=>c).Select(c=>new{name=c.name,length=c.length}).ToArray():null,parameters=a.runtimeAnimatorController?a.parameters.Select(p=>new{name=p.name,type=p.type.ToString()}).ToArray():null}).ToArray();
            evidence["cameras"]=all.SelectMany(t=>t.GetComponents<Camera>()).Select(c=>new{path=PathOf(c.transform),active=c.gameObject.activeInHierarchy,enabled=c.enabled,depth=c.depth}).ToArray();
            evidence["proxyConfiguration"]=original.Where(c=>new[]{"MeetingCellsPlayerMoveController","NPCLookAtManager","SayDialog","Teleport","AssignTargetGroupFollow","NPCMove2GivenDestination","WhiteBloodCellGameManager"}.Contains(c.sourceType)).Select(c=>new{path=PathOf(c.transform),c.sourceType,c.sourceId,c.configJson,refs=c.sourceReferences.Select(r=>new{r.key,target=r.target?r.target.name:null}).ToArray()}).ToArray();
            Directory.CreateDirectory("Reports/OriginalScene");File.WriteAllText("Reports/OriginalScene/imported-scene.json",JsonConvert.SerializeObject(evidence,Formatting.Indented));AssetDatabase.SaveAssets();
            Debug.Log("ORIGINAL_SCENE_IMPORTED objects="+all.Length+" directors="+directors.Length+" missing="+missing);
            if(missing>0)throw new InvalidOperationException("Source scene contains unresolved scripts; see imported-scene.json");
        }
        static int RebindEventTypes(IEnumerable<Transform> transforms)
        {
            int changed=0;
            foreach(var component in transforms.SelectMany(t=>t.GetComponents<Component>()).Where(c=>c&&c is not OriginalBehaviour))
            {
                var serialized=new SerializedObject(component);var property=serialized.GetIterator();bool dirty=false;
                while(property.Next(true))
                {
                    if(property.name!="m_TargetAssemblyTypeName"||property.propertyType!=SerializedPropertyType.String)continue;
                    string path=property.propertyPath;var target=serialized.FindProperty(path.Substring(0,path.Length-"m_TargetAssemblyTypeName".Length)+"m_Target");
                    if(target?.objectReferenceValue is OriginalBehaviour owner)
                    {property.stringValue=owner.GetType().AssemblyQualifiedName;dirty=true;changed++;}
                }
                if(dirty)serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return changed;
        }
        public static void AuditConfiguredScene()
        {
            var scene=EditorSceneManager.OpenScene(PlayableScene,OpenSceneMode.Single);
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var referenceRows=all.SelectMany(t=>t.GetComponents<OriginalBehaviour>()).Select(c=>new{path=PathOf(c.transform),c.sourceType,c.sourceId,active=c.isActiveAndEnabled,config=c.configJson,refs=c.sourceReferences.Select(r=>new{r.key,name=r.target?r.target.name:null,type=r.target?r.target.GetType().Name:null}).ToArray()}).ToArray();
            Directory.CreateDirectory("Reports/OriginalScene");File.WriteAllText("Reports/OriginalScene/configured-components.json",JsonConvert.SerializeObject(referenceRows,Formatting.Indented));
        }
        public static void BuildWebGL(){BuildBrowser(false);}
        public static void BuildWebGLRelease(){BuildBrowser(true);}
        static void BuildBrowser(bool release)
        {
            PlayerSettings.WebGL.template="PROJECT:SPARCOriginal";PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;PlayerSettings.WebGL.decompressionFallback=true;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{PlayableScene},locationPathName=release?"Builds/WebGLOriginalRelease":"Builds/WebGLOriginal",target=BuildTarget.WebGL,options=release?BuildOptions.None:BuildOptions.Development});
            Directory.CreateDirectory("Reports/OriginalScene");File.WriteAllText(release?"Reports/OriginalScene/webgl-release-build.json":"Reports/OriginalScene/webgl-build.json",JsonConvert.SerializeObject(new{result=report.summary.result.ToString(),builtUtc=DateTime.UtcNow.ToString("O"),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize,messages=report.steps.SelectMany(step=>step.messages.Select(message=>new{step=step.name,type=message.type.ToString(),text=message.content})).Where(message=>message.type=="Error"||message.type=="Warning"||message.type=="Exception").ToArray()},Formatting.Indented));
            if(report.summary.result!=BuildResult.Succeeded||report.summary.totalErrors!=0)throw new InvalidOperationException("Original-scene WebGL build is not clean; inspect recorded build messages");
        }
    }
}
