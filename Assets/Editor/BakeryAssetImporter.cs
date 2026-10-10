using UnityEditor;
using UnityEngine;

// Import settings for every image in Assets/Art, so pixel art always stays crisp:
// point filtering, no compression, and a pixels-per-unit that makes one floor tile = one world unit.
public class BakeryAssetImporter : AssetPostprocessor {
    void OnPreprocessTexture() {
        if ( !assetPath.StartsWith( "Assets/Art/" ) ) return;

        TextureImporter ti = (TextureImporter) assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;

        TextureImporterSettings s = new TextureImporterSettings();
        ti.ReadTextureSettings( s );
        s.spriteMeshType = SpriteMeshType.FullRect;     // needed for tiled and 9-sliced sprites
        s.spriteAlignment = (int) SpriteAlignment.Center;
        s.spritePixelsPerUnit = 16;

        if ( assetPath.Contains( "/Food/" ) ) {
            s.spritePixelsPerUnit = 48;                 // the 32px food icons are drawn at 2/3 of a tile
        } else if ( assetPath.EndsWith( "Characters/cat.png" ) ) {
            s.spritePixelsPerUnit = 64;
            s.spriteAlignment = (int) SpriteAlignment.BottomCenter;
        } else if ( assetPath.Contains( "/Characters/" ) || assetPath.Contains( "/Furniture/" ) || assetPath.Contains( "/Decor/" ) ) {
            s.spriteAlignment = (int) SpriteAlignment.BottomCenter;
        } else if ( assetPath.Contains( "/Tiles/" ) ) {
            ti.wrapMode = TextureWrapMode.Repeat;       // floors and walls are drawn tiled
        }
        ti.SetTextureSettings( s );

        // 9-slice borders for the UI panels (left, bottom, right, top in pixels)
        if ( assetPath.EndsWith( "panel_cream.png" ) || assetPath.EndsWith( "panel_wood.png" ) || assetPath.EndsWith( "panel_cream_rivets.png" ) ) {
            ti.spriteBorder = new Vector4( 10, 10, 10, 10 );
        } else if ( assetPath.EndsWith( "button_cream.png" ) || assetPath.EndsWith( "button_wood.png" ) ) {
            ti.spriteBorder = new Vector4( 5, 5, 5, 5 );
        } else if ( assetPath.EndsWith( "ribbon.png" ) ) {
            ti.spriteBorder = new Vector4( 32, 0, 32, 0 );     // the two folded ends stay square, the middle stretches
        } else if ( assetPath.EndsWith( "bar_frame.png" ) ) {
            ti.spriteBorder = new Vector4( 2, 1, 2, 1 );
        }
    }
}
