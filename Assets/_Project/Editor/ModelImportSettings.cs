using UnityEditor;

namespace VRSports.EditorTools
{
    /// <summary>
    /// Настройки импорта для наших моделей из Blender (Assets/_Project/Models).
    /// Материалы из FBX не импортируем: сборщик сцен назначает свои по номеру слота.
    /// Так все модели используют общие лёгкие материалы URP Simple Lit.
    /// </summary>
    public class ModelImportSettings : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/_Project/Models")) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.globalScale = 1f;
            mi.useFileScale = true;
        }
    }
}
