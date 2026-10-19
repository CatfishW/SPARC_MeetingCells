using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sparc.Original
{
    /// <summary>Selected built-in imagegen assets and code-native pixel UI frames.</summary>
    public static class OriginalGameArt
    {
        static readonly Dictionary<string,Sprite> art=new(),frames=new();
        public static readonly Color Ink=new(.10f,.19f,.24f),Paper=new(.97f,.96f,.91f),Blue=new(.13f,.45f,.58f),Mint=new(.43f,.77f,.68f);
        public static Sprite Get(string name)
        {
            if(art.TryGetValue(name,out var cached))return cached;
            var texture=Resources.Load<Texture2D>("MeetingCellsArt/"+name);if(!texture)return null;
            texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;
            var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);sprite.name="SPARC generated / "+name;art[name]=sprite;return sprite;
        }
        public static Sprite Frame(string kind)
        {
            if(frames.TryGetValue(kind,out var cached))return cached;
            var fill=kind switch{"primary"=>new Color(.16f,.37f,.40f),"secondary"=>new Color(.88f,.93f,.88f),"slot"=>new Color(.77f,.91f,.97f),"red-input"=>new Color(.98f,.85f,.82f),"dark"=>new Color(.07f,.16f,.20f),_=>Paper};
            const int size=24;var t=new Texture2D(size,size,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,name="SPARC pixel UI / "+kind};var p=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                bool edge=x<1||y<1||x>=size-1||y>=size-1,corner=(x<2||x>=size-2)&&(y<2||y>=size-2);
                bool dash=kind!="slot"||(x+y)%6<3;
                p[y*size+x]=corner?Color.clear:edge&&dash?new Color(Ink.r,Ink.g,Ink.b,.78f):fill;
            }
            t.SetPixels(p);t.Apply(false,true);var s=Sprite.Create(t,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(4,4,4,4));s.name="SPARC pixel UI / "+kind;frames[kind]=s;return s;
        }
        public static RectTransform Rect(string name,Transform parent)
        {var found=parent.Find(name) as RectTransform;if(found)return found;var go=new GameObject(name,typeof(RectTransform));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);return go.GetComponent<RectTransform>();}
        public static TMP_Text Text(string name,Transform parent,float size,Color color)
        {var rect=Rect(name,parent);var t=rect.GetComponent<TMP_Text>()??rect.gameObject.AddComponent<TextMeshProUGUI>();t.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");t.fontSize=size;t.color=color;t.raycastTarget=false;t.enableAutoSizing=false;t.textWrappingMode=TextWrappingModes.Normal;return t;}
        public static Image Image(string name,Transform parent,Sprite sprite,Color color)
        {var rect=Rect(name,parent);var image=rect.GetComponent<Image>()??rect.gameObject.AddComponent<Image>();image.sprite=sprite;image.type=sprite&&sprite.border!=Vector4.zero?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;image.color=color;image.raycastTarget=false;return image;}
        public static void Box(RectTransform r,float x,float y,float w,float h,float unit=1)
        {if(!r)return;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.localScale=Vector3.one;r.localRotation=Quaternion.identity;r.sizeDelta=new Vector2(w,h)*unit;r.anchoredPosition=new Vector2(x,-y)*unit;}
        public static void Stretch(RectTransform r,float left=0,float top=0,float right=0,float bottom=0)
        {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,.5f);r.offsetMin=new Vector2(left,bottom);r.offsetMax=new Vector2(-right,-top);r.localScale=Vector3.one;r.localRotation=Quaternion.identity;}
        public static void Style(Image image,string kind)
        {if(!image)return;image.sprite=Frame(kind);image.overrideSprite=null;image.type=UnityEngine.UI.Image.Type.Sliced;image.color=Color.white;image.material=null;var button=image.GetComponent<Button>();if(button){button.transition=Selectable.Transition.ColorTint;var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.90f,.97f,.95f);colors.pressedColor=new Color(.72f,.88f,.84f);colors.disabledColor=new Color(.7f,.7f,.7f,.55f);colors.colorMultiplier=1;button.colors=colors;}}
        public static void Entity(GameObject root,string name,bool aspect=true)
        {
            if(!root)return;var sprite=Get(name);if(!sprite)return;
            var image=root.GetComponent<Image>()??root.GetComponentInChildren<Image>(true);
            if(!image){var raw=root.GetComponent<RawImage>();if(raw)raw.enabled=false;image=OriginalComponents.Ensure<Image>(root);}
            image.enabled=false;image.sprite=sprite;image.overrideSprite=null;image.type=UnityEngine.UI.Image.Type.Simple;image.color=Color.white;image.material=null;image.preserveAspect=aspect;
            // Generated art owns the subject; source helper hitboxes/rigidbodies stay intact.
            foreach(var g in root.GetComponentsInChildren<Graphic>(true))if(g!=image&&!g.name.StartsWith("__SPARC_MINI_"))g.enabled=false;
            var visible=Image("__SPARC_MINI_Subject",root.transform,sprite,Color.white);visible.enabled=true;visible.preserveAspect=aspect;Stretch(visible.rectTransform);
            OriginalComponents.Ensure<OriginalMiniSpriteMotion>(root).Bind(visible,name);
        }
    }
}
