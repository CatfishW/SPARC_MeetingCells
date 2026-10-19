using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sparc.Original
{
    /// <summary>Standalone presentation pass. Keeps authored geometry, materials and camera tracks intact.</summary>
    public sealed class OriginalLighting : MonoBehaviour
    {
        VolumeProfile grade;

        void Start()
        {
            // The imported skybox/probes leave downward-facing characters almost black.
            // Supply soft, neutral bounced light without baking or additional shadow maps.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // The imported slot points at a translucent brick material, not a sky shader.
            RenderSettings.skybox = null;
            RenderSettings.ambientSkyColor = new Color(0.48f, 0.54f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.37f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.24f, 0.27f);
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(0.27f, 0.29f, 0.33f));
            RenderSettings.ambientProbe = ambient;
            RenderSettings.reflectionIntensity = 0.55f;
            RenderSettings.fogColor = new Color(0.56f, 0.39f, 0.40f);
            RenderSettings.fogDensity = 0.0025f;

            var lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(light => light.gameObject.scene == gameObject.scene && light.type == LightType.Directional).ToArray();
            var key = RenderSettings.sun;
            if (!key) key = lights.OrderByDescending(light => light.intensity).FirstOrDefault();
            foreach (var light in lights)
            {
                if (light == key)
                {
                    light.color = new Color(1f, 0.96f, 0.91f);
                    light.intensity = 0.95f;
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = 0.48f;
                    light.shadowBias = 0.05f;
                    light.shadowNormalBias = 0.4f;
                }
                else
                {
                    light.color = new Color(0.80f, 0.88f, 1f);
                    light.intensity = 0.24f;
                    light.shadows = LightShadows.None;
                }
            }

            // A restrained grade takes the edge off the vessel's primary red texture.
            // Use a private runtime profile; source materials and shared profiles stay original.
            grade = ScriptableObject.CreateInstance<VolumeProfile>();
            grade.name = "Meeting Cells / soft vessel light";
            var color = grade.Add<ColorAdjustments>(true);
            color.saturation.Override(-19f);
            color.contrast.Override(-7f);
            color.postExposure.Override(0.12f);
            var tone = grade.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            // Enabling the grade must not also enable the imported volume's strong
            // bloom and dark vignette on cameras that previously bypassed that stack.
            grade.Add<Bloom>(true).intensity.Override(0f);
            grade.Add<Vignette>(true).intensity.Override(0f);
            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 20;
            volume.sharedProfile = grade;
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(camera => camera.gameObject.scene == gameObject.scene))
            {
                var data = camera.GetUniversalAdditionalCameraData();
                if (data.renderType == CameraRenderType.Base) data.renderPostProcessing = true;
            }
        }

        void OnDestroy()
        {
            if (!grade) return;
            foreach (var component in grade.components) if (component) Destroy(component);
            Destroy(grade);
        }
    }
}
