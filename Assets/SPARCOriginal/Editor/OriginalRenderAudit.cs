using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using TMPro;
namespace Sparc.Original.Editor
{
    public static class OriginalRenderAudit
    {
        public static void Inspect()
        {
            var scene=EditorSceneManager.OpenScene(OriginalProjectBuilder.PlayableScene,OpenSceneMode.Single);var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
            var texts=all.SelectMany(t=>t.GetComponents<TMP_Text>()).Select(t=>new{path=PathOf(t.transform),t.text,material=t.fontSharedMaterial?t.fontSharedMaterial.name:null,shader=t.fontSharedMaterial&&t.fontSharedMaterial.shader?t.fontSharedMaterial.shader.name:null,shaderPath=t.fontSharedMaterial&&t.fontSharedMaterial.shader?AssetDatabase.GetAssetPath(t.fontSharedMaterial.shader):null,font=t.font?t.font.name:null,fontPath=AssetDatabase.GetAssetPath(t.font),materialPath=AssetDatabase.GetAssetPath(t.fontSharedMaterial),atlas=t.font&&t.font.atlasTexture?t.font.atlasTexture.name:null}).ToArray();
            var surfaces=all.SelectMany(t=>t.GetComponents<Unity.AI.Navigation.NavMeshSurface>()).Select(s=>new{path=PathOf(s.transform),data=AssetDatabase.GetAssetPath(s.navMeshData),s.enabled,s.agentTypeID}).ToArray();
            var renderers=all.SelectMany(t=>t.GetComponents<Renderer>()).SelectMany(r=>r.sharedMaterials.Where(m=>m).Select(m=>new{path=PathOf(r.transform),material=m.name,shader=m.shader?m.shader.name:null,materialPath=AssetDatabase.GetAssetPath(m),shaderPath=AssetDatabase.GetAssetPath(m.shader)})).Distinct().ToArray();
            Directory.CreateDirectory("Reports/OriginalScene");File.WriteAllText("Reports/OriginalScene/render-audit.json",JsonConvert.SerializeObject(new{texts,surfaces,renderers},Formatting.Indented));
            Debug.Log("ORIGINAL_FONT_SHADERS "+string.Join(";",texts.Select(t=>t.shader+" @ "+t.shaderPath).Distinct()));Debug.Log("NAV_SURFACES "+surfaces.Length);
        }
    }
}
