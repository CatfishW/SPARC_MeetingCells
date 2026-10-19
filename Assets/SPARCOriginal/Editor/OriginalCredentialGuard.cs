using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
namespace Sparc.Original.Editor
{
    public static class OriginalCredentialGuard
    {
        public static int Scrub(OriginalBehaviour[] records)
        {
            int count=0;
            foreach(var record in records)
            {
                var data=JObject.Parse(record.configJson??"{}");int removed=ScrubTree(data);
                if(removed==0)continue;record.configJson=data.ToString(Newtonsoft.Json.Formatting.None);EditorUtility.SetDirty(record);count+=removed;
            }
            return count;
        }
        static int ScrubTree(JToken value)
        {
            int count=0;
            if(value is JObject obj)foreach(var p in obj.Properties().ToArray())
            {
                string key=Regex.Replace(p.Name.ToLowerInvariant(),"[^a-z0-9]","");
                bool secret=new[]{"apikey","accesstoken","refreshtoken","password","secret","authorization"}.Any(key.EndsWith);
                if(secret&&p.Value.Type==JTokenType.String){if(!string.IsNullOrEmpty((string)p.Value)){p.Value="";count++;}}
                else count+=ScrubTree(p.Value);
            }
            else if(value is JArray array)foreach(var child in array)count+=ScrubTree(child);
            return count;
        }
    }
}
