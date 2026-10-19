using UnityEditor;
using UnityEngine;

namespace Sparc.Original.Editor
{
    public sealed class OriginalGeneratedArtImporter:AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Resources/MeetingCellsArt/",System.StringComparison.Ordinal))return;
            var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Default;t.mipmapEnabled=false;t.filterMode=FilterMode.Point;t.wrapMode=TextureWrapMode.Clamp;t.npotScale=TextureImporterNPOTScale.None;t.maxTextureSize=2048;t.alphaIsTransparency=true;t.textureCompression=TextureImporterCompression.Uncompressed;
        }
    }
}
