using UnityEditor;

public sealed class GarageVehicleAssetImporter : AssetPostprocessor
{
    private const string VehicleAssetRoot = "Assets/Resources/Vehicles/";

    private bool IsGarageVehicleAsset => assetPath.StartsWith(VehicleAssetRoot);

    private void OnPreprocessModel()
    {
        if (!IsGarageVehicleAsset)
        {
            return;
        }

        ModelImporter modelImporter = (ModelImporter)assetImporter;
        modelImporter.importAnimation = false;
        modelImporter.importCameras = false;
        modelImporter.importLights = false;
        modelImporter.addCollider = false;
        modelImporter.isReadable = false;
        modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
    }

    private void OnPreprocessTexture()
    {
        if (!IsGarageVehicleAsset)
        {
            return;
        }

        TextureImporter textureImporter = (TextureImporter)assetImporter;
        textureImporter.maxTextureSize = 2048;
        textureImporter.textureCompression = TextureImporterCompression.CompressedHQ;
        bool isNormalMap = assetPath.EndsWith("_normal.png");
        bool isLinearData = isNormalMap
            || assetPath.EndsWith("_metallic.png")
            || assetPath.EndsWith("_roughness.png");
        textureImporter.sRGBTexture = !isLinearData;
        if (isNormalMap)
        {
            textureImporter.textureType = TextureImporterType.NormalMap;
        }
    }
}
