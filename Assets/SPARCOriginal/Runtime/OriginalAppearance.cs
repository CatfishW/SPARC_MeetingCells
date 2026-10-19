using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Sparc.Original
{
    /// <summary>Changes only the original avatar's modular meshes/materials. Never replaces its animator or bones.</summary>
    public sealed class OriginalAppearance:MonoBehaviour
    {
        public Texture2D[] skinAtlases=Array.Empty<Texture2D>();
        JObject catalog;Transform avatar;Material sourceMaterial;
        readonly Dictionary<string,SkinnedMeshRenderer> parts=new(StringComparer.Ordinal);
        readonly Dictionary<string,bool> originalActive=new(StringComparer.Ordinal);
        readonly Dictionary<string,Material[]> originalMaterials=new(StringComparer.Ordinal);
        Material[] tones;long revision=-1;string signature;
        public string Status {get;private set;}="Original scene appearance";
        public string AppliedPreset {get;private set;}="";
        public int AvailableParts=>parts.Count;
        void Start(){Initialize();}
        bool Initialize()
        {
            if(avatar)return true;var runtime=OriginalRuntime.Instance;if(!runtime||!runtime.player||!runtime.player.model)return false;
            var definitions=Resources.Load<TextAsset>("AppearanceCatalog");if(!definitions)return false;catalog=JObject.Parse(definitions.text);avatar=runtime.player.model.transform;
            foreach(var r in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {if(parts.ContainsKey(r.name))continue;parts[r.name]=r;originalActive[r.name]=r.gameObject.activeSelf;originalMaterials[r.name]=r.sharedMaterials;}
            sourceMaterial=parts.TryGetValue("Head",out var head)?head.sharedMaterial:null;
            tones=skinAtlases.Select((atlas,i)=>{if(!sourceMaterial||!atlas)return null;var material=new Material(sourceMaterial){name="Original avatar skin tone "+i};material.mainTexture=atlas;return material;}).ToArray();return true;
        }
        public void ReceiveAppearance(string payload)
        {try{ApplyEnvelope(payload);}catch(Exception){Status="Invalid appearance ignored";}}
        void ApplyEnvelope(string payload)
        {
            if(string.IsNullOrEmpty(payload)||payload.Length>32768||!Initialize())return;
            JObject message;try{message=JObject.Parse(payload);}catch{return;}
            string status=(string)message["status"];
            if(status=="signed_out")
            {foreach(var pair in parts){pair.Value.gameObject.SetActive(originalActive[pair.Key]);pair.Value.sharedMaterials=originalMaterials[pair.Key];}revision=-1;signature=null;AppliedPreset="";Status="Original scene appearance";return;}
            if(status!="ready"||message["revision"]?.Type!=JTokenType.Integer||message["appearance"] is not JObject appearance)return;
            long candidate;try{candidate=(long)message["revision"];}catch{return;}if(candidate<0||candidate<revision)return;
            string serialized=appearance.ToString(Newtonsoft.Json.Formatting.None);if(candidate==revision&&signature!=serialized)return;
            if((int?)appearance["schemaVersion"]!=1||(string)appearance["catalogVersion"]!=(string)catalog["catalogVersion"])return;
            string preset=(string)appearance["presetId"];if(!(catalog["presets"] as JArray??new JArray()).Any(p=>(string)p["id"]==preset))return;
            if(appearance["skinIndex"]?.Type!=JTokenType.Integer)return;int skin=(int)appearance["skinIndex"];if(skin<0||skin>=5||skin>=tones.Length||!tones[skin])return;
            if(appearance["parts"] is not JArray selection||selection.Count<5||selection.Count>40||selection.Any(p=>p.Type!=JTokenType.String))return;
            var chosen=selection.Values<string>().ToArray();var distinct=new HashSet<string>(chosen,StringComparer.Ordinal);if(distinct.Count!=chosen.Length)return;
            var canonical=(catalog["parts"] as JArray??new JArray()).ToDictionary(p=>(string)p["id"],p=>(string)p["slot"],StringComparer.Ordinal);
            var slots=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var id in chosen){if(!parts.ContainsKey(id)||!canonical.TryGetValue(id,out var slot))return;slots.TryGetValue(slot,out int count);count++;int limit=slot=="Body"?4:slot=="Headgear"||slot=="Mustache"?2:1;if(count>limit)return;slots[slot]=count;}
            if(new[]{"Head","Eye","lips"}.Any(s=>!slots.TryGetValue(s,out var count)||count!=1))return;
            if(!slots.ContainsKey("Top")&&!chosen.Any(s=>s.StartsWith("Body_Top_",StringComparison.Ordinal)))return;
            if(!slots.ContainsKey("Bottom")&&!(catalog["onePieceTops"] as JArray??new JArray()).Values<string>().Any(distinct.Contains))return;
            foreach(var pair in parts)
            {
                bool active=distinct.Contains(pair.Key);pair.Value.gameObject.SetActive(active);
                if(active){for(var parent=pair.Value.transform.parent;parent&&parent!=avatar;parent=parent.parent)parent.gameObject.SetActive(true);}
                pair.Value.sharedMaterials=originalMaterials[pair.Key].Select(material=>material&&(material==sourceMaterial||material.name.StartsWith("Skin",StringComparison.OrdinalIgnoreCase))?tones[skin]:material).ToArray();
            }
            revision=candidate;signature=serialized;AppliedPreset=preset;Status="Saved PING appearance";
        }
        void OnDestroy(){if(tones!=null)foreach(var material in tones)if(material)Destroy(material);}
    }
}
