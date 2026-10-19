using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Sparc.Original.Editor
{
    /// <summary>Restores authored prefab overrides whose old fields now live in the new data boundary.</summary>
    public static class OriginalOverrideRestorer
    {
        public static List<object> Apply(IEnumerable<OriginalBehaviour> all)
        {
            var result=new List<object>();
            foreach(var instance in all)
            {
                var root=PrefabUtility.GetOutermostPrefabInstanceRoot(instance.gameObject);if(!root)continue;
                var source=PrefabUtility.GetCorrespondingObjectFromSource(instance);if(!source)continue;
                var modifications=PrefabUtility.GetPropertyModifications(root);if(modifications==null)continue;
                var config=JObject.Parse(instance.configJson??"{}");var refs=(instance.sourceReferences??Array.Empty<OriginalReference>()).ToList();bool changed=false;
                foreach(var m in modifications)
                {
                    if(m.target!=source||string.IsNullOrEmpty(m.propertyPath)||m.propertyPath.StartsWith("m_",StringComparison.Ordinal))continue;
                    string first=m.propertyPath.Split('.')[0];if(config[first]==null)continue;
                    try
                    {
                        JToken value=null;
                        if(m.objectReference)
                        {
                            string key="override:"+m.propertyPath;value=new JObject{["fileID"]=key};
                            refs.RemoveAll(r=>r.key==":"+key);refs.Add(new OriginalReference{key=":"+key,target=m.objectReference});
                        }
                        Set(config,m.propertyPath,m.value,value);
                        changed=true;result.Add(new{instance=PathOf(instance.transform),instance.sourceType,field=m.propertyPath,value=m.objectReference?m.objectReference.name:m.value});
                    }
                    catch(Exception e){throw new InvalidOperationException("Cannot preserve prefab override "+instance.name+" "+m.propertyPath,e);}
                }
                if(changed){instance.configJson=config.ToString(Formatting.None);instance.sourceReferences=refs.ToArray();EditorUtility.SetDirty(instance);}
            }
            return result;
        }
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        static void Set(JObject root,string path,string text,JToken objectValue)
        {
            var keys=Regex.Replace(path,@"\.Array\.data\[(\d+)\]", ".$1").Split('.');JToken current=root;
            for(int i=0;i<keys.Length-1;i++)
            {
                if(keys[i]=="Array"&&keys[i+1]=="size")
                {var array=(JArray)current;int n=int.Parse(text);while(array.Count>n)array.RemoveAt(array.Count-1);while(array.Count<n)array.Add(JValue.CreateNull());return;}
                if(current is JArray a){int index=int.Parse(keys[i]);while(a.Count<=index)a.Add(new JObject());current=a[index];}
                else
                {if(current[keys[i]]==null||current[keys[i]].Type==JTokenType.Null)current[keys[i]]=int.TryParse(keys[i+1],out _)?new JArray():new JObject();current=current[keys[i]];}
            }
            string last=keys[^1];JToken old=current is JArray arr?arr[int.Parse(last)]:current[last];JToken val=objectValue;
            if(val==null)
            {
                if(old is JObject o&&o["fileID"]!=null)val=new JObject{["fileID"]=0};
                else if(old?.Type==JTokenType.Boolean)val=new JValue(text=="1"||text=="true");
                else if(old?.Type==JTokenType.Integer&&long.TryParse(text,out long integer))val=new JValue(integer);
                else if(old?.Type==JTokenType.Float&&double.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double number))val=new JValue(number);
                else val=new JValue(text??"");
            }
            if(current is JArray array2)array2[int.Parse(last)]=val;else current[last]=val;
        }
    }
}
