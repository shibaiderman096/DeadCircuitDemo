#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace DeadCircuit.Editor
{
    // Only this expansion owns these folders. Keep SVG pixels and PCM loop seams intact.
    sealed class ExpansionAssetImport : AssetPostprocessor
    {
        const string ArtRoot="Assets/DeadCircuit/Resources/Art/Expansion/";
        const string AudioRoot="Assets/DeadCircuit/Resources/Audio/Expansion/";
        public override int GetPostprocessOrder() => 10000;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot,StringComparison.Ordinal) || !assetPath.EndsWith(".png",StringComparison.OrdinalIgnoreCase)) return;
            var importer=(TextureImporter)assetImporter;
            // Runtime sprites use the JSON rectangles; automatic grid slicing is incorrect
            // for 32px art separated by 2px gutters, and NPOT rescaling invalidates every rect.
            importer.textureType=TextureImporterType.Default;
            importer.npotScale=TextureImporterNPOTScale.None;
            importer.filterMode=FilterMode.Point;
            importer.wrapMode=TextureWrapMode.Clamp;
            importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true;
            importer.sRGBTexture=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=2048;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioRoot,StringComparison.Ordinal) || !assetPath.EndsWith(".wav",StringComparison.OrdinalIgnoreCase)) return;
            var importer=(AudioImporter)assetImporter;
            var settings=importer.defaultSampleSettings;
            settings.compressionFormat=AudioCompressionFormat.PCM;
            settings.loadType=AudioClipLoadType.DecompressOnLoad;
            settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData=true;
            importer.defaultSampleSettings=settings;
            // Sources are authored mono already. Avoid a second normalization/conversion.
            importer.forceToMono=false;
            importer.loadInBackground=false;
        }
    }
}
#endif
