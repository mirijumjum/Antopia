using System.IO;
using UnityEditor;
using UnityEngine;

namespace Antopia.EditorTools
{
    // Configura solo los PNG de Assets/Resources/UI (sprites 9-slice) y Assets/Resources/Textures (repetibles),
    // para que basta con soltar la imagen en la carpeta.
    public class UiSpriteImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            if (assetPath.StartsWith("Assets/Resources/UI/"))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Bilinear;
                ti.spritePixelsPerUnit = 100f;
                string n = Path.GetFileNameWithoutExtension(assetPath);
                // Borde del 9-slice: izquierda, abajo, derecha, arriba.
                if (n.StartsWith("Button")) ti.spriteBorder = new Vector4(48f, 40f, 48f, 40f);
                else if (n.StartsWith("Msg")) ti.spriteBorder = new Vector4(48f, 48f, 48f, 52f);
            }
            else if (assetPath.StartsWith("Assets/Resources/Textures/"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.mipmapEnabled = true;
                ti.filterMode = FilterMode.Bilinear;
            }
        }
    }
}
