using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Builds the whole game out of the sprites in Assets/Art and the sounds in Assets/Audio:
// the fonts, the prefabs, and Assets/Scenes/Bakery.unity with the bakery, the kitchen, all the UI,
// and every Inspector reference wired up.
//
// Building it from code means the layout is written down in one readable place, and the scene can be
// rebuilt at any time from the menu bar: Enzo's Bakery > Rebuild Scene.
// (Tweaking values on the GameManager / ItemBook in the Inspector afterward works as usual.)
[InitializeOnLoad]
static public class BakeryBuilder {
    const string SCENE_PATH  = "Assets/Scenes/Bakery.unity";
    const string PREFAB_DIR  = "Assets/Prefabs";
    const string FONT_DIR    = "Assets/Fonts";
    const string BUILD_PATH  = "Builds/Mac/Enzos Bakery.app";

    // Kitchen layout, in world units (1 unit = one 16px tile)
    const float STATION_ROW_Y = -5f;    // the counters along the bottom
    const float ISLAND_Y      = -2f;    // the island in the middle of the kitchen
    const float COUNTER_Y     = 0f;     // the service counter between the kitchen and the shop
    const float STAND_Y       = 0.85f;  // where customers stand
    const float WALL_Y        = 3f;     // bottom of the back wall
    static readonly float[] SPOT_X = { -6.5f, -2.5f, 2.5f, 6.5f };
    const float DOOR_X        = 7.5f;

    static TMP_FontAsset    fontBody;
    static TMP_FontAsset    fontTitle;
    static Material         outlineMaterial;    // body font with a dark outline, for text over the game
    static Material         titleOutlineMaterial;

    static readonly Color INK    = new Color( 0.29f, 0.18f, 0.12f );   // dark brown text
    static readonly Color CREAM  = new Color( 1f, 0.95f, 0.82f );

    static BakeryBuilder() {
        // Build automatically the first time the project is opened in the editor
        EditorApplication.delayCall += () => {
            if ( Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ) return;
            if ( !File.Exists( SCENE_PATH ) ) Build();
        };
    }

    [MenuItem( "Enzo's Bakery/Rebuild Scene" )]
    static void RebuildFromMenu() {
        if ( !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() ) return;
        Build();
    }

    [MenuItem( "Enzo's Bakery/Build Mac App" )]
    static void BuildMacFromMenu() {
        BuildMac();
    }

    // Unity -batchmode -projectPath . -executeMethod BakeryBuilder.BuildFromCommandLine
    static public void BuildFromCommandLine() {
        try {
            Build();
            EditorApplication.Exit( 0 );
        } catch ( System.Exception e ) {
            Debug.LogException( e );
            EditorApplication.Exit( 1 );
        }
    }

    // Unity -batchmode -projectPath . -executeMethod BakeryBuilder.BuildMacFromCommandLine
    static public void BuildMacFromCommandLine() {
        try {
            if ( !File.Exists( SCENE_PATH ) ) Build();
            bool ok = BuildMac();
            EditorApplication.Exit( ok ? 0 : 1 );
        } catch ( System.Exception e ) {
            Debug.LogException( e );
            EditorApplication.Exit( 1 );
        }
    }

    static bool BuildMac() {
        BuildPlayerOptions options = new BuildPlayerOptions {
            scenes = new[] { SCENE_PATH },
            locationPathName = BUILD_PATH,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer( options );
        Debug.Log( "Enzo's Bakery build: " + report.summary.result + " " + report.summary.totalSize + " bytes" );
        return report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
    }

    // ================================================================== Build

    static void Build() {
        AssetDatabase.ImportAsset( "Assets/Art", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate );
        EnsureFolder( "Assets/Scenes" );
        EnsureFolder( PREFAB_DIR );

        BuildFonts();
        GameObject itemPrefab = BuildItemPrefab();
        GameObject textPrefab = BuildFloatingTextPrefab();
        GameObject customerPrefab = BuildCustomerPrefab();

        EditorSceneManager.NewScene( NewSceneSetup.EmptyScene, NewSceneMode.Single );
        BuildCamera();
        Transform world = new GameObject( "Bakery" ).transform;
        BuildRoom( world, out SpriteRenderer[] skies );

        Kitchen kitchen = BuildKitchen( world );
        Baker baker = BuildBaker( world );
        BakeryCat cat = BuildCat( world );
        CustomerSpawner spawner = BuildSpawner( world, customerPrefab, kitchen.spots );

        UI ui = BuildUI();
        BuildManagers( itemPrefab, textPrefab, baker, cat, spawner, kitchen, ui, skies );

        EditorSceneManager.SaveScene( EditorSceneManager.GetActiveScene(), SCENE_PATH );
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene( SCENE_PATH, true ) };
        AssetDatabase.SaveAssets();
        Debug.Log( "Enzo's Bakery: scene built at " + SCENE_PATH );
    }

    // ================================================================== Fonts

    static void BuildFonts() {
        EnsureFolder( FONT_DIR );
        fontBody = MakeFontAsset( "Kenney Mini", "KenneyMini SDF" );
        fontTitle = MakeFontAsset( "Kenney Blocks", "KenneyBlocks SDF" );
        outlineMaterial = MakeOutlineMaterial( fontBody, "KenneyMini Outline", 0.22f );
        titleOutlineMaterial = MakeOutlineMaterial( fontTitle, "KenneyBlocks Outline", 0.18f );
    }

    static TMP_FontAsset MakeFontAsset( string ttfName, string assetName ) {
        string path = FONT_DIR + "/" + assetName + ".asset";
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>( path );
        if ( existing != null ) return existing;

        Font ttf = AssetDatabase.LoadAssetAtPath<Font>( FONT_DIR + "/" + ttfName + ".ttf" );
        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset( ttf, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false );
        fa.name = assetName;
        AssetDatabase.CreateAsset( fa, path );
        fa.atlasTextures[0].name = assetName + " Atlas";
        AssetDatabase.AddObjectToAsset( fa.atlasTextures[0], fa );
        fa.material.name = assetName + " Material";
        AssetDatabase.AddObjectToAsset( fa.material, fa );
        // Bake the printable ASCII characters into the atlas up front
        string ascii = "";
        for ( char c = ' '; c <= '~'; c++ ) ascii += c;
        fa.TryAddCharacters( ascii, out string _ );
        EditorUtility.SetDirty( fa );
        AssetDatabase.SaveAssets();
        return fa;
    }

    static Material MakeOutlineMaterial( TMP_FontAsset fa, string name, float width ) {
        string path = FONT_DIR + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>( path );
        if ( m == null ) {
            m = new Material( fa.material );
            AssetDatabase.CreateAsset( m, path );
        }
        m.SetFloat( ShaderUtilities.ID_OutlineWidth, width );
        m.SetColor( ShaderUtilities.ID_OutlineColor, new Color( 0.18f, 0.1f, 0.07f ) );
        m.SetFloat( ShaderUtilities.ID_FaceDilate, width * 0.5f );
        EditorUtility.SetDirty( m );
        return m;
    }

    // ================================================================== Prefabs

    static GameObject BuildItemPrefab() {
        GameObject root = new GameObject( "Item" );
        Item item = root.AddComponent<Item>();
        item.sRend = MakeSprite( "Sprite", root.transform, Vector3.zero, "Food/bread", 5 );
        item.sparkle = MakeSprite( "Sparkle", root.transform, new Vector3( 0.22f, 0.2f, 0 ), "FX/sparkle", 6 );
        item.sparkle.enabled = false;
        return SavePrefab( root, "Item" );
    }

    static GameObject BuildFloatingTextPrefab() {
        GameObject root = new GameObject( "FloatingText" );
        TextMeshPro tmp = root.AddComponent<TextMeshPro>();
        tmp.font = fontBody;
        tmp.fontSharedMaterial = outlineMaterial;
        tmp.fontSize = 3.2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2( 6, 1 );
        tmp.text = "+$10";
        MeshRenderer mr = root.GetComponent<MeshRenderer>();
        mr.sortingLayerName = "WorldUI";
        mr.sortingOrder = 30;
        return SavePrefab( root, "FloatingText" );
    }

    static GameObject BuildCustomerPrefab() {
        GameObject root = new GameObject( "Customer" );
        Customer c = root.AddComponent<Customer>();

        MakeSprite( "Shadow", root.transform, new Vector3( 0, 0.02f, 0 ), "FX/shadow", -5, "Default", new Color( 0, 0, 0, 0.25f ) );

        // The paper doll, sorted as one piece against the rest of the world
        GameObject body = new GameObject( "Body" );
        body.transform.SetParent( root.transform, false );
        body.AddComponent<SortingGroup>();
        CustomerLook look = body.AddComponent<CustomerLook>();
        look.body  = MakeSprite( "Skin",  body.transform, Vector3.zero, "Characters/body/body_00", 0 );
        look.pants = MakeSprite( "Pants", body.transform, Vector3.zero, "Characters/pants/pants_00", 1 );
        look.shoes = MakeSprite( "Shoes", body.transform, Vector3.zero, "Characters/shoes/shoes_00", 2 );
        look.shirt = MakeSprite( "Shirt", body.transform, Vector3.zero, "Characters/shirt/shirt_00", 3 );
        look.beard = MakeSprite( "Beard", body.transform, Vector3.zero, null, 4 );
        look.hair  = MakeSprite( "Hair",  body.transform, Vector3.zero, "Characters/hair/hair_00", 5 );
        look.hat   = MakeSprite( "Hat",   body.transform, Vector3.zero, null, 6 );
        look.bodies       = LoadSprites( "Characters/body" );
        look.pantsOptions = LoadSprites( "Characters/pants" );
        look.shoeOptions  = LoadSprites( "Characters/shoes" );
        look.shirts       = LoadSprites( "Characters/shirt" );
        look.hairs        = LoadSprites( "Characters/hair" );
        look.beards       = LoadSprites( "Characters/beard" );
        look.hats         = LoadSprites( "Characters/hat" );
        c.look = look;
        c.bodyVisual = body.transform;

        // The order bubble floats above their head, in front of everything
        GameObject bubbleGO = new GameObject( "Bubble" );
        bubbleGO.transform.SetParent( root.transform, false );
        bubbleGO.transform.localPosition = new Vector3( 0, 1.75f, 0 );
        OrderBubble bubble = bubbleGO.AddComponent<OrderBubble>();
        bubble.bubble = MakeSprite( "Balloon", bubbleGO.transform, Vector3.zero, "UI/bubble", 0, "WorldUI" );
        bubble.bubble.transform.localScale = Vector3.one * 0.8f;
        bubble.alertIcon = MakeSprite( "Alert", bubbleGO.transform, new Vector3( 0, 0.05f, -0.01f ), "UI/alert", 2, "WorldUI" );
        bubble.alertIcon.transform.localScale = Vector3.one * 0.55f;
        bubble.itemIcons = new SpriteRenderer[2];
        bubble.checkMarks = new SpriteRenderer[2];
        for ( int i = 0; i < 2; i++ ) {
            bubble.itemIcons[i] = MakeSprite( "Item" + i, bubbleGO.transform, Vector3.zero, "Food/bread", 2, "WorldUI" );
            bubble.itemIcons[i].transform.localScale = Vector3.one * 1.0f;
            bubble.checkMarks[i] = MakeSprite( "Check", bubble.itemIcons[i].transform, new Vector3( 0.18f, -0.14f, -0.01f ), "FX/check", 3, "WorldUI" );
        }
        bubble.badge = MakeSprite( "Badge", bubbleGO.transform, new Vector3( -0.62f, 0.32f, -0.01f ), null, 3, "WorldUI" );
        bubble.patienceBar = MakeBar( "Patience", bubbleGO.transform, new Vector3( 0, 0.62f, 0 ), 0.8f, 0.1f, 1 );
        c.bubble = bubble;
        c.criticBadge = Spr( "FX/star" );
        c.hurriedBadge = Spr( "FX/clock" );

        return SavePrefab( root, "Customer" );
    }

    static GameObject SavePrefab( GameObject root, string name ) {
        string path = PREFAB_DIR + "/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset( root, path );
        Object.DestroyImmediate( root );
        return prefab;
    }

    // ================================================================== Camera & room

    static void BuildCamera() {
        GameObject go = new GameObject( "Main Camera" );
        go.tag = "MainCamera";
        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        // 11.25 tiles tall: exactly 6 screen pixels per art pixel at 1080p
        cam.orthographicSize = 5.625f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color( 0.16f, 0.1f, 0.08f );
        cam.transparencySortMode = TransparencySortMode.CustomAxis;
        cam.transparencySortAxis = Vector3.up;
        go.transform.position = new Vector3( 0, 0.375f, -10 );
        go.AddComponent<AudioListener>();
    }

    static void BuildRoom( Transform world, out SpriteRenderer[] skies ) {
        Transform room = Child( "Room", world );

        // Floors and walls are single tiled sprites
        MakeTiled( "Kitchen Floor", room, new Vector2( 0, -3f ), new Vector2( 20, 6 ), "Tiles/floor_kitchen", 0 );
        MakeTiled( "Shop Floor", room, new Vector2( 0, 1.5f ), new Vector2( 20, 3 ), "Tiles/floor_wood", 0 );
        MakeTiled( "Wainscot", room, new Vector2( 0, WALL_Y + 0.5f ), new Vector2( 20, 1 ), "Tiles/wall_face_low", 1 );
        MakeTiled( "Wall", room, new Vector2( 0, WALL_Y + 1.5f ), new Vector2( 20, 1 ), "Tiles/wall_face", 1 );
        MakeTiled( "Wall Top", room, new Vector2( 0, WALL_Y + 2.75f ), new Vector2( 20, 1.5f ), "Tiles/wall_top", 1 );

        // Windows with the sky behind them (BakeryAmbience colors the sky by time of day)
        List<SpriteRenderer> skyList = new List<SpriteRenderer>();
        foreach ( float x in new[] { -7.5f, -3.5f, 3.5f } ) {
            SpriteRenderer sky = MakeSprite( "Sky", room, new Vector3( x, WALL_Y + 1.0f, 0 ), "FX/sky", 2, "Background" );
            sky.transform.localScale = new Vector3( 3.4f, 7f, 1 );
            skyList.Add( sky );
            MakeSprite( "Window", room, new Vector3( x, WALL_Y, 0 ), "Furniture/window", 3, "Background" );
        }
        skies = skyList.ToArray();

        MakeSprite( "Door", room, new Vector3( DOOR_X, WALL_Y, 0 ), "Furniture/door_open", 3, "Background" );
        MakeSprite( "Cuckoo Clock", room, new Vector3( 0, WALL_Y + 0.55f, 0 ), "Decor/cuckoo_clock", 4, "Background" );
        MakeSprite( "Painting", room, new Vector3( 5.5f, WALL_Y + 0.7f, 0 ), "Decor/painting_a", 4, "Background" );

        // A chalkboard-style menu on the wall
        SpriteRenderer board = MakeSprite( "Menu Board", room, new Vector3( -5.5f, WALL_Y + 1.1f, 0 ), "UI/panel_wood", 4, "Background" );
        board.drawMode = SpriteDrawMode.Sliced;
        board.size = new Vector2( 2.6f, 1.5f );
        TextMeshPro menuText = MakeWorldText( "Menu Text", board.transform, Vector3.zero,
            "MENU\nBread $10\nCookies $6\nCupcake $12", 1.9f, CREAM, "Background", 5 );
        menuText.lineSpacing = -20;

        // Bread shelves on either side
        foreach ( float x in new[] { -9.4f, 9.4f } ) {
            MakeSprite( "Shelf", room, new Vector3( x, WALL_Y, 0 ), "Decor/shelf", 4, "Background" );
            MakeSprite( "Shelf Bread", room, new Vector3( x - 0.18f, WALL_Y + 0.62f, 0 ), "Food/bread", 5, "Background" ).transform.localScale = Vector3.one * 0.6f;
            MakeSprite( "Shelf Cookies", room, new Vector3( x + 0.2f, WALL_Y + 0.62f, 0 ), "Food/cookies", 5, "Background" ).transform.localScale = Vector3.one * 0.55f;
            MakeSprite( "Shelf Cupcake", room, new Vector3( x, WALL_Y + 0.25f, 0 ), "Food/cupcake_frosted", 5, "Background" ).transform.localScale = Vector3.one * 0.55f;
        }

        // Shop floor decoration
        SpriteRenderer rug = MakeSprite( "Rug", room, new Vector3( DOOR_X, 2.05f, 0 ), "Decor/rug", 1, "Background" );
        rug.transform.localScale = new Vector3( 0.7f, 0.7f, 1 );
        MakeSprite( "Plant", room, new Vector3( -9.5f, 1.2f, 0 ), "Decor/plant_a", 0 );
        MakeSprite( "Plant", room, new Vector3( 9.5f, 1.2f, 0 ), "Decor/plant_b", 0 );

        // The service counter, with a till and a tip jar
        // (furniture sprites have their pivot at the bottom, so this sits on y = COUNTER_Y)
        SpriteRenderer counter = MakeTiled( "Service Counter", room, new Vector2( 0, COUNTER_Y ), new Vector2( 20, 1 ), "Furniture/service_counter", 0, "Default" );
        BoxCollider2D col = counter.gameObject.AddComponent<BoxCollider2D>();
        col.size = new Vector2( 20, 1 );
        col.offset = new Vector2( 0, 0.5f );
        MakeSprite( "Coins", room, new Vector3( 8.6f, COUNTER_Y + 0.6f, 0 ), "Decor/coins", 1 );
        MakeSprite( "Counter Plant", room, new Vector3( -8.6f, COUNTER_Y + 0.55f, 0 ), "Decor/plant_b", 1 );

        // Invisible walls at the screen edges
        MakeWall( "Left Wall", room, new Vector2( -10.5f, -2.5f ), new Vector2( 1, 7 ) );
        MakeWall( "Right Wall", room, new Vector2( 10.5f, -2.5f ), new Vector2( 1, 7 ) );
        MakeWall( "Bottom Wall", room, new Vector2( 0, -6.5f ), new Vector2( 22, 1 ) );
    }

    static void MakeWall( string name, Transform parent, Vector2 center, Vector2 size ) {
        GameObject go = new GameObject( name );
        go.transform.SetParent( parent, false );
        go.transform.position = center;
        go.AddComponent<BoxCollider2D>().size = size;
    }

    // ================================================================== Kitchen

    class Kitchen {
        public List<MixingBowl>     bowls = new List<MixingBowl>();
        public List<Oven>           ovens = new List<Oven>();
        public IngredientCrate      berryCrate;
        public ServiceSpot[]        spots;
    }

    static Kitchen BuildKitchen( Transform world ) {
        Kitchen k = new Kitchen();
        Transform kitchen = Child( "Kitchen", world );

        // The back row of the kitchen, left to right: ingredients, mixing, ovens, frosting, trash
        string[] row = {
            "counterL", "flour", "chocolate", "berries", "counter", "bowl", "bowl", "counter", "oven", "oven",
            "oven3", "counter", "counter", "frosting", "counter", "counter", "sink", "trash", "counter", "counterR"
        };
        for ( int i = 0; i < row.Length; i++ ) {
            Vector3 pos = new Vector3( -10 + i + 0.5f, STATION_ROW_Y, 0 );
            switch ( row[i] ) {
            case "flour":     MakeCrate( kitchen, pos, eItem.flour, "Food/flour" ); break;
            case "chocolate": MakeCrate( kitchen, pos, eItem.chocolate, "Food/chocolate" ); break;
            case "berries":   k.berryCrate = MakeCrate( kitchen, pos, eItem.berries, "Food/berries" ); break;
            case "bowl":      k.bowls.Add( MakeBowl( kitchen, pos ) ); break;
            case "oven":      k.ovens.Add( MakeOven( kitchen, pos, false ) ); break;
            case "oven3":     k.ovens.Add( MakeOven( kitchen, pos, true ) ); break;
            case "frosting":  MakeFrosting( kitchen, pos ); break;
            case "trash":     MakeTrash( kitchen, pos ); break;
            case "sink":      MakeCounter( kitchen, pos, "Furniture/counter_sink", "Sink Counter" ); break;
            case "counterL":  MakeCounter( kitchen, pos, "Furniture/counter_left", "Counter" ); break;
            case "counterR":  MakeCounter( kitchen, pos, "Furniture/counter_right", "Counter" ); break;
            default:          MakeCounter( kitchen, pos, "Furniture/counter", "Counter" ); break;
            }
        }

        // Potted plants in the two top corners of the kitchen
        foreach ( float x in new[] { -9.5f, 9.5f } ) {
            SpriteRenderer plant = MakeSprite( "Kitchen Plant", kitchen, new Vector3( x, -1f, 0 ), "Decor/plant_a", 0 );
            plant.transform.localScale = new Vector3( 1.2f, 1.2f, 1 );
            plant.gameObject.AddComponent<SortingGroup>();
            BoxCollider2D pc = plant.gameObject.AddComponent<BoxCollider2D>();
            pc.size = new Vector2( 0.6f, 0.5f );
            pc.offset = new Vector2( 0, 0.25f );
        }

        // The island in the middle: extra counter space close to the customers
        string[] island = { "Furniture/counter_left", "Furniture/counter", "Furniture/counter", "Furniture/counter_right" };
        for ( int i = 0; i < island.Length; i++ ) {
            MakeCounter( kitchen, new Vector3( -2 + i + 0.5f, ISLAND_Y, 0 ), island[i], "Island Counter" );
        }

        // The spots on the service counter where customers are served
        k.spots = new ServiceSpot[SPOT_X.Length];
        for ( int i = 0; i < SPOT_X.Length; i++ ) {
            GameObject go = new GameObject( "Service Spot " + ( i + 1 ) );
            go.transform.SetParent( kitchen, false );
            go.transform.position = new Vector3( SPOT_X[i], COUNTER_Y, 0 );
            ServiceSpot spot = go.AddComponent<ServiceSpot>();
            Transform stand = Child( "Stand Point", go.transform );
            stand.position = new Vector3( SPOT_X[i], STAND_Y, 0 );
            spot.standPoint = stand;
            k.spots[i] = spot;
        }
        return k;
    }

    // Every station: a root with a SortingGroup and a collider, a "Visual" child that can pop, and an item anchor
    static T MakeStation<T>( Transform parent, Vector3 pos, string name, string counterSprite ) where T : Component {
        GameObject go = new GameObject( name );
        go.transform.SetParent( parent, false );
        go.transform.position = pos;
        go.AddComponent<SortingGroup>();
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2( 0.98f, 0.92f );
        col.offset = new Vector2( 0, 0.5f );
        Transform visual = Child( "Visual", go.transform );
        if ( counterSprite != null ) MakeSprite( "Counter", visual, Vector3.zero, counterSprite, 0 );
        return go.AddComponent<T>();
    }

    static void SetupStation( Station s, float anchorY = 0.95f ) {
        s.visual = s.transform.Find( "Visual" );
        Transform anchor = Child( "Item Anchor", s.transform );
        anchor.localPosition = new Vector3( 0, anchorY, 0 );
        s.itemAnchor = anchor;
    }

    static Counter MakeCounter( Transform parent, Vector3 pos, string sprite, string name ) {
        Counter c = MakeStation<Counter>( parent, pos, name, sprite );
        SetupStation( c );
        return c;
    }

    static IngredientCrate MakeCrate( Transform parent, Vector3 pos, eItem ingredient, string iconSprite ) {
        IngredientCrate crate = MakeStation<IngredientCrate>( parent, pos, IngredientName( ingredient ) + " Crate", "Furniture/counter" );
        crate.ingredient = ingredient;
        crate.visual = crate.transform.Find( "Visual" );
        SpriteRenderer box = MakeSprite( "Crate", crate.visual, new Vector3( 0, 0.4f, 0 ), "Furniture/crate", 1 );
        box.transform.localScale = new Vector3( 1.2f, 1.2f, 1 );
        crate.icon = MakeSprite( "Icon", crate.visual, new Vector3( 0, 1.15f, 0 ), iconSprite, 2 );
        crate.icon.transform.localScale = Vector3.one * 1.15f;
        crate.lockIcon = MakeSprite( "Lock", crate.transform, new Vector3( 0.28f, 0.75f, 0 ), "FX/lock", 3 );
        crate.lockIcon.transform.localScale = Vector3.one * 1.3f;
        crate.lockIcon.enabled = false;
        return crate;
    }

    static string IngredientName( eItem e ) {
        switch ( e ) {
        case eItem.flour: return "Flour";
        case eItem.chocolate: return "Chocolate";
        case eItem.berries: return "Berries";
        }
        return e.ToString();
    }

    static MixingBowl MakeBowl( Transform parent, Vector3 pos ) {
        MixingBowl bowl = MakeStation<MixingBowl>( parent, pos, "Mixing Bowl", "Furniture/counter" );
        SetupStation( bowl, 1.0f );
        SpriteRenderer b = MakeSprite( "Bowl", bowl.visual, new Vector3( 0, 0.55f, 0 ), "Furniture/bowl", 1 );
        b.transform.localScale = new Vector3( 1.9f, 1.9f, 1 );
        bowl.flourIcon = MakeSprite( "Flour In Bowl", bowl.transform, new Vector3( -0.2f, 1.25f, 0 ), "Food/flour", 3 );
        bowl.flourIcon.transform.localScale = Vector3.one * 0.6f;
        bowl.toppingIcon = MakeSprite( "Topping In Bowl", bowl.transform, new Vector3( 0.2f, 1.25f, 0 ), "Food/chocolate", 3 );
        bowl.toppingIcon.transform.localScale = Vector3.one * 0.6f;
        bowl.progressBar = MakeBar( "Mix Bar", bowl.transform, new Vector3( 0, 1.65f, 0 ), 0.8f, 0.12f, 0 );
        return bowl;
    }

    static Oven MakeOven( Transform parent, Vector3 pos, bool isThird ) {
        Oven oven = MakeStation<Oven>( parent, pos, isThird ? "Oven 3" : "Oven", "Furniture/counter" );
        SetupStation( oven, 0.9f );
        oven.ovenRend = MakeSprite( "Oven", oven.visual, new Vector3( 0, 0.35f, 0 ), "Furniture/oven_off", 1 );
        oven.ovenRend.transform.localScale = new Vector3( 1.1f, 1.1f, 1 );
        oven.spriteOff = Spr( "Furniture/oven_off" );
        oven.spriteOnA = Spr( "Furniture/oven_on_a" );
        oven.spriteOnB = Spr( "Furniture/oven_on_b" );
        oven.contentIcon = MakeSprite( "Inside", oven.transform, new Vector3( 0.32f, 1.38f, 0 ), "Food/bread", 3, "WorldUI" );
        oven.contentIcon.transform.localScale = Vector3.one * 0.55f;
        oven.contentIcon.enabled = false;
        oven.bar = MakeBar( "Bake Bar", oven.transform, new Vector3( 0, 1.7f, 0 ), 0.85f, 0.13f, 0 );
        oven.lockIcon = MakeSprite( "Lock", oven.transform, new Vector3( 0, 0.85f, 0 ), "FX/lock", 3 );
        oven.lockIcon.transform.localScale = Vector3.one * 1.4f;
        oven.lockIcon.enabled = false;
        return oven;
    }

    static FrostingStation MakeFrosting( Transform parent, Vector3 pos ) {
        FrostingStation fs = MakeStation<FrostingStation>( parent, pos, "Frosting Station", "Furniture/counter_drawers" );
        SetupStation( fs );
        fs.itemAnchor.localPosition = new Vector3( 0.12f, 0.95f, 0 );
        MakeSprite( "Frosting Tub", fs.visual, new Vector3( -0.22f, 0.55f, 0 ), "Furniture/frosting_tub", 1 );
        fs.progressBar = MakeBar( "Frost Bar", fs.transform, new Vector3( 0, 1.65f, 0 ), 0.8f, 0.12f, 0 );
        return fs;
    }

    static TrashBin MakeTrash( Transform parent, Vector3 pos ) {
        TrashBin t = MakeStation<TrashBin>( parent, pos, "Trash", null );
        t.visual = t.transform.Find( "Visual" );
        MakeSprite( "Shadow", t.visual, new Vector3( 0, 0.12f, 0 ), "FX/shadow", 0, "Default", new Color( 0, 0, 0, 0.3f ) );
        MakeSprite( "Can", t.visual, new Vector3( 0, 0.55f, 0 ), "FX/trash_can", 1 );
        return t;
    }

    // ================================================================== Characters

    static Baker BuildBaker( Transform world ) {
        GameObject go = new GameObject( "Baker (Enzo)" );
        go.transform.SetParent( world, false );
        go.transform.position = new Vector3( -0.5f, -3.5f, 0 );
        go.AddComponent<SortingGroup>();
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.3f;
        col.offset = new Vector2( 0, 0.2f );

        Baker baker = go.AddComponent<Baker>();
        MakeSprite( "Shadow", go.transform, new Vector3( 0, 0.02f, 0 ), "FX/shadow", -5, "Default", new Color( 0, 0, 0, 0.25f ) );
        Transform visual = Child( "Visual", go.transform );
        SpriteRenderer body = MakeSprite( "Body", visual, Vector3.zero, "Characters/baker", 0 );
        SpriteRenderer hat = MakeSprite( "Chef Hat", visual, new Vector3( 0, 0.44f, 0 ), "Characters/chef_hat", 1 );
        Transform hold = Child( "Hold Anchor", visual );
        hold.localPosition = new Vector3( 0, 0.42f, 0 );
        hold.localScale = new Vector3( 0.8f, 0.8f, 1 );     // carried food is drawn a bit smaller than Enzo
        baker.visual = visual;
        baker.flipRenderers = new[] { body, hat };
        baker.holdAnchor = hold;

        // The corner brackets that show what the baker will interact with
        SpriteRenderer sel = MakeSprite( "Selector", world, Vector3.zero, "FX/selector", 10, "WorldUI" );
        sel.enabled = false;
        baker.selector = sel.transform;
        baker.selectorRend = sel;

        Transform start = Child( "Baker Start", world );
        start.position = go.transform.position;
        return baker;
    }

    static BakeryCat BuildCat( Transform world ) {
        GameObject go = new GameObject( "Biscuit (cat)" );
        go.transform.SetParent( world, false );
        go.transform.position = new Vector3( 4.5f, -3.6f, 0 );
        go.AddComponent<SortingGroup>();
        BakeryCat cat = go.AddComponent<BakeryCat>();
        cat.centerOffset = new Vector2( 0, 0.35f );
        cat.size = new Vector2( 0.9f, 0.7f );
        MakeSprite( "Shadow", go.transform, new Vector3( 0, 0.02f, 0 ), "FX/shadow", -5, "Default", new Color( 0, 0, 0, 0.25f ) );
        Transform visual = Child( "Visual", go.transform );
        cat.visual = visual;
        cat.sRend = MakeSprite( "Cat", visual, Vector3.zero, "Characters/cat", 0 );
        return cat;
    }

    static CustomerSpawner BuildSpawner( Transform world, GameObject customerPrefab, ServiceSpot[] spots ) {
        GameObject go = new GameObject( "Customers" );
        go.transform.SetParent( world, false );
        CustomerSpawner sp = go.AddComponent<CustomerSpawner>();
        sp.customerPrefab = customerPrefab;
        sp.spots = spots;
        Transform door = Child( "Door Point", go.transform );
        door.position = new Vector3( DOOR_X, WALL_Y + 0.25f, 0 );
        sp.door = door;
        sp.walkwayY = 2.1f;
        return sp;
    }

    // ================================================================== Managers

    static void BuildManagers( GameObject itemPrefab, GameObject textPrefab, Baker baker, BakeryCat cat,
                               CustomerSpawner spawner, Kitchen kitchen, UI ui, SpriteRenderer[] skies ) {
        GameObject go = new GameObject( "_Managers" );

        ItemBook book = go.AddComponent<ItemBook>();
        book.itemPrefab = itemPrefab;
        book.items = new List<ItemDef> {
            Def( eItem.flour,          "Flour",          eItemKind.ingredient, "Food/flour" ),
            Def( eItem.chocolate,      "Chocolate",      eItemKind.ingredient, "Food/chocolate" ),
            Def( eItem.berries,        "Berries",        eItemKind.ingredient, "Food/berries" ),
            Def( eItem.breadDough,     "Bread Dough",    eItemKind.dough,      "Food/dough_bread",   0, eItem.bread, 7f ),
            Def( eItem.cookieDough,    "Cookie Dough",   eItemKind.dough,      "Food/dough_cookie",  0, eItem.cookies, 4.5f ),
            Def( eItem.cupcakeBatter,  "Cupcake Batter", eItemKind.dough,      "Food/batter_cupcake", 0, eItem.cupcake, 5.5f ),
            Def( eItem.bread,          "Bread",          eItemKind.baked,      "Food/bread", 10 ),
            Def( eItem.cookies,        "Cookies",        eItemKind.baked,      "Food/cookies", 6 ),
            Def( eItem.cupcake,        "Plain Cupcake",  eItemKind.baked,      "Food/cupcake" ),
            Def( eItem.frostedCupcake, "Cupcake",        eItemKind.baked,      "Food/cupcake_frosted", 12 ),
            Def( eItem.burnt,          "Burnt Mess",     eItemKind.burnt,      "Food/bread" ),
        };

        GameManager gm = go.AddComponent<GameManager>();
        gm.days = new[] {
            new DaySettings {
                title = "Day 1", goal = 75, spawnInterval = 12f, grandmaChance = 0.15f,
                menu = new[] { eItem.bread, eItem.cookies },
                intro = "Your very first day running Enzo's Bakery!\n\n" +
                        "Customers will order Bread (just flour) and Cookies (flour + chocolate). " +
                        "Take their order, mix the dough, bake it, and serve it before they lose their patience."
            },
            new DaySettings {
                title = "Day 2", goal = 105, spawnInterval = 11f, doubleOrderChance = 0.1f, hurriedChance = 0.15f, grandmaChance = 0.15f,
                berriesUnlocked = true,
                menu = new[] { eItem.bread, eItem.cookies, eItem.frostedCupcake },
                intro = "New on the menu: Cupcakes! Flour + berries makes batter. Bake it, then frost it at the frosting station (hold E).\n\n" +
                        "Watch for customers in a hurry (marked with a clock): very little patience, but big tips."
            },
            new DaySettings {
                title = "Day 3", goal = 160, spawnInterval = 10f, doubleOrderChance = 0.3f, hurriedChance = 0.2f, grandmaChance = 0.12f,
                berriesUnlocked = true, hasCritic = true,
                menu = new[] { eItem.bread, eItem.cookies, eItem.frostedCupcake },
                intro = "The big day. Lots of customers order two things at once, and a famous food critic (marked with a star) " +
                        "is coming at lunchtime.\n\nServe the critic only PERFECT bakes and they'll pay triple!"
            },
        };
        gm.upgrades = new[] {
            Upgrade( eUpgrade.thirdOven,    "Third Oven",    "Fire up the spare oven.",                70, "Furniture/oven_off" ),
            Upgrade( eUpgrade.turboOven,    "Turbo Ovens",   "Everything bakes 35% faster.",                              60, "Furniture/oven_on_a" ),
            Upgrade( eUpgrade.comfyChairs,  "Comfy Chairs",  "Customers wait 25% longer.",          55, "Decor/chair" ),
            Upgrade( eUpgrade.quickWhisk,   "Quick Whisk",   "Mix dough and frost cupcakes twice as fast.",               40, "Furniture/bowl" ),
            Upgrade( eUpgrade.runningShoes, "Running Shoes", "Enzo walks 20% faster.",                                    45, "UI/icon_shoe" ),
            Upgrade( eUpgrade.tipJar,       "Tip Jar",       "Customers tip 40% more.",                                   50, "Decor/coins" ),
        };
        gm.baker = baker;
        gm.bakerStart = GameObject.Find( "Baker Start" ).transform;
        gm.spawner = spawner;
        gm.cat = cat;
        gm.thirdOven = kitchen.ovens[2];
        gm.berryCrate = kitchen.berryCrate;
        gm.menus = ui.menus;

        ui.tutorial.bowls = kitchen.bowls.ToArray();
        ui.tutorial.ovens = kitchen.ovens.ToArray();

        FX fx = go.AddComponent<FX>();
        fx.floatingTextPrefab = textPrefab;
        fx.puffSprite = Spr( "FX/puff" );
        fx.heartSprite = Spr( "FX/heart" );
        fx.sparkleSprite = Spr( "FX/sparkle" );
        fx.starSprite = Spr( "FX/star" );
        fx.zzzSprite = Spr( "FX/zzz" );

        BuildSound( go );

        GameObject musicGO = new GameObject( "Music" );
        musicGO.transform.SetParent( go.transform, false );
        musicGO.AddComponent<AudioSource>().playOnAwake = false;
        musicGO.AddComponent<MusicDirector>();

        BakeryAmbience amb = go.AddComponent<BakeryAmbience>();
        amb.skies = skies;
        amb.skyByDay = SkyGradient();

        go.AddComponent<AutoPlayer>();
    }

    static ItemDef Def( eItem item, string name, eItemKind kind, string sprite, int price = 0, eItem bakesInto = eItem.none, float bake = 0 ) {
        return new ItemDef { item = item, displayName = name, kind = kind, sprite = Spr( sprite ), price = price, bakesInto = bakesInto, bakeSeconds = bake };
    }

    static UpgradeDef Upgrade( eUpgrade id, string name, string desc, int price, string icon ) {
        return new UpgradeDef { id = id, displayName = name, description = desc, price = price, icon = Spr( icon ) };
    }

    static Gradient SkyGradient() {
        Gradient g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey( new Color( 0.98f, 0.82f, 0.72f ), 0f ),     // 8 AM: soft sunrise
                new GradientColorKey( new Color( 0.62f, 0.82f, 0.98f ), 0.2f ),
                new GradientColorKey( new Color( 0.45f, 0.72f, 1f ), 0.5f ),      // noon
                new GradientColorKey( new Color( 0.6f, 0.75f, 0.95f ), 0.75f ),
                new GradientColorKey( new Color( 1f, 0.7f, 0.45f ), 0.92f ),      // golden hour
                new GradientColorKey( new Color( 0.9f, 0.45f, 0.45f ), 1f ),      // 4 PM sunset
            },
            new[] { new GradientAlphaKey( 1, 0 ), new GradientAlphaKey( 1, 1 ) } );
        return g;
    }

    static void BuildSound( GameObject go ) {
        SFX sfx = go.AddComponent<SFX>();
        sfx.sounds = new List<SfxEntry> {
            Sfx( eSfx.pickUp, 0.6f, "pick_up" ),
            Sfx( eSfx.putDown, 0.6f, "put_down", "plop" ),
            Sfx( eSfx.error, 0.5f, "error" ),
            Sfx( eSfx.sparkle, 0.6f, "sparkle" ),
            Sfx( eSfx.confirm, 0.6f, "confirm" ),
            Sfx( eSfx.voiceBlip, 0.5f, "voice_blip" ),
            Sfx( eSfx.customerArrive, 0.5f, "customer_order" ),
            Sfx( eSfx.takeOrder, 0.8f, "take_order" ),
            Sfx( eSfx.angryLeave, 0.7f, "angry_leave" ),
            Sfx( eSfx.grabFlour, 0.8f, "grab_flour" ),
            Sfx( eSfx.grabSoft, 0.7f, "grab_soft" ),
            Sfx( eSfx.bowlAdd, 0.6f, "bowl_add" ),
            Sfx( eSfx.bowlDone, 0.6f, "bowl_done" ),
            Sfx( eSfx.frosting, 0.5f, "frosting" ),
            Sfx( eSfx.trash, 0.6f, "trash" ),
            Sfx( eSfx.ovenDoor, 0.6f, "oven_door" ),
            Sfx( eSfx.ovenDing, 0.7f, "oven_ding" ),
            Sfx( eSfx.perfectDing, 0.8f, "perfect_ding" ),
            Sfx( eSfx.warningBeep, 0.5f, "warning_beep" ),
            Sfx( eSfx.burnt, 0.7f, "burnt_sizzle" ),
            Sfx( eSfx.cashRegister, 0.7f, "cash_register" ),
            Sfx( eSfx.tipJar, 0.7f, "tip_jar" ),
            Sfx( eSfx.coins, 0.7f, "coins" ),
            Sfx( eSfx.shopBell, 0.6f, "shop_bell" ),
            Sfx( eSfx.doorClose, 0.5f, "door_close" ),
            Sfx( eSfx.footstep, 0.6f, "footstep_a", "footstep_b" ),
            Sfx( eSfx.catMeow, 0.7f, "cat_meow_a", "cat_meow_b", "cat_meow_c" ),
            Sfx( eSfx.catPurr, 0.8f, "cat_purr" ),
            Sfx( eSfx.uiClick, 0.7f, "ui_click" ),
            Sfx( eSfx.uiHover, 0.5f, "ui_hover" ),
            Sfx( eSfx.upgradeBuy, 0.7f, "upgrade_buy" ),
            Sfx( eSfx.jingleDayStart, 0.7f, "jingle_day_start" ),
            Sfx( eSfx.jingleWin, 0.7f, "jingle_win" ),
            Sfx( eSfx.jingleLose, 0.7f, "jingle_lose" ),
            Sfx( eSfx.jingleRush, 0.7f, "jingle_rush" ),
            Sfx( eSfx.jingleVictory, 0.8f, "jingle_victory" ),
        };
        foreach ( SfxEntry e in sfx.sounds ) {
            if ( e.id == eSfx.voiceBlip || e.id.ToString().StartsWith( "jingle" ) ) e.pitchJitter = 0;
        }
        sfx.loops = new List<LoopEntry> {
            new LoopEntry { id = eLoop.whisk, clip = Clip( "whisk_loop" ), maxVolume = 0.55f },
            new LoopEntry { id = eLoop.ovenFire, clip = Clip( "oven_fire_loop" ), maxVolume = 0.3f },
            new LoopEntry { id = eLoop.cafe, clip = Clip( "cafe_ambience_loop" ), maxVolume = 0.22f },
        };
    }

    static SfxEntry Sfx( eSfx id, float volume, params string[] clips ) {
        List<AudioClip> list = new List<AudioClip>();
        foreach ( string c in clips ) list.Add( Clip( c ) );
        return new SfxEntry { id = id, clips = list.ToArray(), volume = volume };
    }

    static AudioClip Clip( string name ) {
        foreach ( string ext in new[] { ".ogg", ".wav" } ) {
            AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>( "Assets/Audio/SFX/" + name + ext );
            if ( c != null ) return c;
        }
        Debug.LogError( "Missing sound: " + name );
        return null;
    }

    // ================================================================== UI

    class UI {
        public MenuScreens      menus;
        public TutorialHints    tutorial;
    }

    static UI BuildUI() {
        UI ui = new UI();

        GameObject es = new GameObject( "EventSystem" );
        es.AddComponent<EventSystem>().sendNavigationEvents = false;   // the keyboard shortcuts are handled by MenuScreens
        es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

        GameObject canvasGO = new GameObject( "Canvas" );
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2( 1920, 1080 );
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        Transform root = canvasGO.transform;

        MenuScreens m = canvasGO.AddComponent<MenuScreens>();
        ui.menus = m;

        m.hud = BuildHUD( root );
        ui.tutorial = BuildTutorial( root );
        m.tutorial = ui.tutorial.gameObject;

        m.dimmer = Img( "Dimmer", root, null, new Color( 0, 0, 0, 0.5f ), Vector2.zero, Vector2.zero );
        Stretch( m.dimmer.rectTransform );

        BuildTitle( root, m );
        BuildHowTo( root, m );
        BuildDayIntro( root, m );
        BuildPause( root, m );
        BuildResults( root, m );
        BuildShop( root, m );
        BuildVictory( root, m );
        return ui;
    }

    static GameObject BuildHUD( Transform root ) {
        RectTransform hudRT = Rect( "HUD", root, Vector2.zero, Vector2.zero );
        Stretch( hudRT );
        HUD hud = hudRT.gameObject.AddComponent<HUD>();

        // Top bar
        Image bar = Img( "Top Bar", hudRT, "UI/panel_wood", Color.white, Vector2.zero, new Vector2( 0, 96 ), true );
        Anchor( bar.rectTransform, new Vector2( 0, 1 ), new Vector2( 1, 1 ), new Vector2( 0.5f, 1 ) );
        bar.rectTransform.sizeDelta = new Vector2( 24, 96 );
        bar.rectTransform.anchoredPosition = new Vector2( 0, 8 );
        Transform t = bar.transform;

        hud.dayText = Text( "Day", t, "Day 1", 44, CREAM, TextAlignmentOptions.Left, new Vector2( 40, -50 ), new Vector2( 220, 60 ), anchor: new Vector2( 0, 1 ) );
        hud.clockText = Text( "Clock", t, "8:00 AM", 38, CREAM, TextAlignmentOptions.Left, new Vector2( 250, -38 ), new Vector2( 260, 50 ), anchor: new Vector2( 0, 1 ) );
        Image clockBg = Img( "Clock Bar", t, "UI/white", new Color( 0.2f, 0.12f, 0.08f ), new Vector2( 250, -72 ), new Vector2( 230, 12 ) );
        Anchor( clockBg.rectTransform, new Vector2( 0, 1 ), new Vector2( 0, 1 ), new Vector2( 0, 0.5f ) );
        hud.clockFill = Img( "Fill", clockBg.transform, "UI/white", new Color( 1f, 0.8f, 0.35f ), Vector2.zero, Vector2.zero );
        Stretch( hud.clockFill.rectTransform );
        Fill( hud.clockFill );

        RectTransform money = Rect( "Money", t, new Vector2( -60, -50 ), new Vector2( 300, 80 ) );
        Anchor( money, new Vector2( 0.5f, 1 ), new Vector2( 0.5f, 1 ), new Vector2( 0.5f, 0.5f ) );
        hud.moneyGroup = money;
        Img( "Coin", money, "UI/coin", Color.white, new Vector2( -110, 0 ), new Vector2( 60, 60 ) );
        hud.moneyText = Text( "Amount", money, "$0", 56, new Color( 1f, 0.88f, 0.4f ), TextAlignmentOptions.Left, new Vector2( 40, 0 ), new Vector2( 220, 70 ) );

        Image goalBg = Img( "Goal Bar", t, "UI/white", new Color( 0.2f, 0.12f, 0.08f ), new Vector2( 250, -50 ), new Vector2( 300, 30 ) );
        Anchor( goalBg.rectTransform, new Vector2( 0.5f, 1 ), new Vector2( 0.5f, 1 ), new Vector2( 0.5f, 0.5f ) );
        hud.goalFill = Img( "Fill", goalBg.transform, "UI/white", new Color( 0.45f, 0.8f, 0.4f ), Vector2.zero, Vector2.zero );
        Stretch( hud.goalFill.rectTransform );
        Fill( hud.goalFill );
        hud.goalText = Text( "Goal", goalBg.transform, "Goal $70", 26, CREAM, TextAlignmentOptions.Center, Vector2.zero, new Vector2( 300, 30 ), outline: true );

        hud.streakText = Text( "Streak", t, "", 34, new Color( 1f, 0.65f, 0.3f ), TextAlignmentOptions.Right, new Vector2( -40, -32 ), new Vector2( 300, 44 ), anchor: new Vector2( 1, 1 ) );
        hud.servedText = Text( "Served", t, "Served 0   Lost 0", 28, CREAM, TextAlignmentOptions.Right, new Vector2( -40, -68 ), new Vector2( 360, 36 ), anchor: new Vector2( 1, 1 ) );

        // Action prompt at the bottom
        Image promptBg = Img( "Prompt", hudRT, "UI/panel_wood", new Color( 1, 1, 1, 0.92f ), new Vector2( 0, 34 ), new Vector2( 980, 62 ), true );
        Anchor( promptBg.rectTransform, new Vector2( 0.5f, 0 ), new Vector2( 0.5f, 0 ), new Vector2( 0.5f, 0.5f ) );
        hud.promptBackground = promptBg;
        hud.promptText = Text( "Text", promptBg.transform, "", 32, CREAM, TextAlignmentOptions.Center, Vector2.zero, new Vector2( 960, 56 ) );

        // Banner for announcements
        RectTransform banner = Rect( "Banner", hudRT, new Vector2( 0, 60 ), new Vector2( 1000, 140 ) );
        hud.banner = banner;
        hud.bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        hud.bannerGroup.blocksRaycasts = false;
        Ribbon( banner, Vector2.zero, new Vector2( 1000, 140 ) );
        hud.bannerText = Text( "Text", banner, "Lunch Rush!", 52, Color.white, TextAlignmentOptions.Center, new Vector2( 0, 6 ), new Vector2( 860, 90 ), outline: true );
        return hudRT.gameObject;
    }

    static TutorialHints BuildTutorial( Transform root ) {
        Image panel = Img( "Tutorial", root, "UI/panel_cream", Color.white, new Vector2( 0, -150 ), new Vector2( 1150, 120 ), true );
        Anchor( panel.rectTransform, new Vector2( 0.5f, 1 ), new Vector2( 0.5f, 1 ), new Vector2( 0.5f, 0.5f ) );
        panel.raycastTarget = false;
        TutorialHints th = panel.gameObject.AddComponent<TutorialHints>();
        th.group = panel.gameObject.AddComponent<CanvasGroup>();
        th.group.alpha = 0;
        th.group.blocksRaycasts = false;
        th.hintText = Text( "Hint", panel.transform, "Walk with WASD", 30, INK, TextAlignmentOptions.Center, Vector2.zero, new Vector2( 1090, 100 ), wrap: true );
        return th;
    }

    static void BuildTitle( Transform root, MenuScreens m ) {
        RectTransform panel = Rect( "Title Screen", root, Vector2.zero, Vector2.zero );
        Stretch( panel );
        m.titlePanel = panel.gameObject;

        RectTransform logo = Rect( "Logo", panel, new Vector2( 0, 250 ), new Vector2( 1200, 260 ) );
        m.logo = logo;
        Ribbon( logo, new Vector2( 0, 10 ), new Vector2( 1250, 220 ) );
        Text( "Name Shadow", logo, "ENZO'S BAKERY", 104, new Color( 0.35f, 0.08f, 0.08f ), TextAlignmentOptions.Center, new Vector2( 7, 13 ), new Vector2( 1100, 140 ), font: fontTitle );
        Text( "Name", logo, "ENZO'S BAKERY", 104, CREAM, TextAlignmentOptions.Center, new Vector2( 0, 20 ), new Vector2( 1100, 140 ), font: fontTitle );
        Text( "Tagline", panel, "Bake it. Time it. Serve it. Before they run out of patience!", 34, CREAM, TextAlignmentOptions.Center, new Vector2( 0, 95 ), new Vector2( 1400, 50 ), outline: true );

        m.playButton = Button( "Play", panel, "[Space] Play", new Vector2( 0, -40 ), new Vector2( 460, 92 ), out _ );
        m.howToButton = Button( "How To Play", panel, "[H] How to Play", new Vector2( 0, -150 ), new Vector2( 460, 92 ), out _ );
        m.quitButton = Button( "Quit", panel, "Quit", new Vector2( 0, -260 ), new Vector2( 460, 92 ), out _ );

        Text( "Credits", panel, "A game by Enzo Weiss.  Art: Kenney, thekingphoenix, bluecarrot16 and others (CC0).  Sounds: Kenney and Freesound (CC0).  Music: generated live by the game.",
              22, new Color( 1f, 0.95f, 0.85f, 0.85f ), TextAlignmentOptions.Center, new Vector2( 0, -500 ), new Vector2( 1800, 40 ), outline: true );
    }

    static void BuildHowTo( Transform root, MenuScreens m ) {
        Image panel = Panel( "How To Play", root, new Vector2( 1560, 900 ) );
        m.howToPanel = panel.gameObject;
        Transform t = panel.transform;
        Text( "Title", t, "How to Play", 64, INK, TextAlignmentOptions.Center, new Vector2( 0, 380 ), new Vector2( 1000, 80 ), font: fontTitle );

        Text( "Controls Title", t, "Controls", 42, new Color( 0.7f, 0.25f, 0.2f ), TextAlignmentOptions.Left, new Vector2( -700 + 300, 280 ), new Vector2( 600, 50 ) );
        Text( "Controls", t,
              "WASD / Arrows   walk\n" +
              "Space   pick up, put down, serve\n" +
              "E   take an order, pet Biscuit\n" +
              "Hold E   mix dough, frost cupcakes\n" +
              "Esc   pause      M   music on/off",
              32, INK, TextAlignmentOptions.TopLeft, new Vector2( -400, 110 ), new Vector2( 640, 280 ) );

        Text( "Recipes Title", t, "Recipes", 42, new Color( 0.7f, 0.25f, 0.2f ), TextAlignmentOptions.Left, new Vector2( 380, 280 ), new Vector2( 600, 50 ) );
        RecipeRow( t, 200, new[] { "Food/flour" }, "Food/bread", "Bread  $10" );
        RecipeRow( t, 100, new[] { "Food/flour", "Food/chocolate" }, "Food/cookies", "Cookies  $6" );
        RecipeRow( t, 0, new[] { "Food/flour", "Food/berries" }, "Food/cupcake_frosted", "Cupcake  $12" );
        Text( "Frost Note", t, "(bake it, then frost it)", 26, INK, TextAlignmentOptions.Left, new Vector2( 600, -45 ), new Vector2( 340, 40 ) );

        Text( "Tips", t,
              "1. Take the order at the counter.   2. Put ingredients in a bowl and hold E to mix.   3. Bake the dough.   4. Serve it!\n\n" +
              "Grab it from the oven right when it dings for a PERFECT bake and a bigger tip. Wait too long and it burns.\n" +
              "The faster you serve, the more they tip. Keep 3+ customers happy in a row for a streak bonus.\n" +
              "Customers getting grumpy? Pet Biscuit the cat. Everyone loves Biscuit.",
              28, INK, TextAlignmentOptions.Top, new Vector2( 0, -230 ), new Vector2( 1440, 260 ), wrap: true );

        m.howToBackButton = Button( "Back", t, "[Space] Back", new Vector2( 0, -390 ), new Vector2( 360, 76 ), out _ );
    }

    // One line of the recipe card: ingredient icons, "=", the result, and its name and price
    static void RecipeRow( Transform parent, float y, string[] inputs, string output, string label ) {
        float x = 70;
        for ( int i = 0; i < inputs.Length; i++ ) {
            if ( i > 0 ) {
                Text( "Plus", parent, "+", 44, INK, TextAlignmentOptions.Center, new Vector2( x - 35, y ), new Vector2( 40, 60 ) );
            }
            Img( "In", parent, inputs[i], Color.white, new Vector2( x, y ), new Vector2( 76, 76 ) );
            x += 110;
        }
        Text( "Equals", parent, "=", 44, INK, TextAlignmentOptions.Center, new Vector2( 300, y ), new Vector2( 40, 60 ) );
        Img( "Out", parent, output, Color.white, new Vector2( 375, y ), new Vector2( 80, 80 ) );
        Text( "Label", parent, label, 30, INK, TextAlignmentOptions.Left, new Vector2( 600, y ), new Vector2( 340, 60 ) );
    }

    static void BuildDayIntro( Transform root, MenuScreens m ) {
        Image panel = Panel( "Day Intro", root, new Vector2( 1150, 660 ) );
        m.dayIntroPanel = panel.gameObject;
        Transform t = panel.transform;
        m.introTitle = Text( "Title", t, "Day 1", 84, INK, TextAlignmentOptions.Center, new Vector2( 0, 230 ), new Vector2( 900, 110 ), font: fontTitle );
        m.introGoal = Text( "Goal", t, "Goal: $70", 42, new Color( 0.7f, 0.25f, 0.2f ), TextAlignmentOptions.Center, new Vector2( 0, 140 ), new Vector2( 900, 60 ) );
        m.introBody = Text( "Body", t, "", 32, INK, TextAlignmentOptions.Top, new Vector2( 0, -30 ), new Vector2( 980, 260 ), wrap: true );
        m.introStartButton = Button( "Start", t, "[Space] Open the doors!", new Vector2( 0, -240 ), new Vector2( 560, 90 ), out _ );
    }

    static void BuildPause( Transform root, MenuScreens m ) {
        Image panel = Panel( "Pause", root, new Vector2( 640, 560 ) );
        m.pausePanel = panel.gameObject;
        Transform t = panel.transform;
        Text( "Title", t, "Paused", 72, INK, TextAlignmentOptions.Center, new Vector2( 0, 190 ), new Vector2( 560, 100 ), font: fontTitle );
        m.resumeButton = Button( "Resume", t, "[Esc] Resume", new Vector2( 0, 60 ), new Vector2( 440, 84 ), out _ );
        m.restartButton = Button( "Restart", t, "[R] Restart day", new Vector2( 0, -50 ), new Vector2( 440, 84 ), out _ );
        m.pauseMenuButton = Button( "Menu", t, "[Q] Main menu", new Vector2( 0, -160 ), new Vector2( 440, 84 ), out _ );
    }

    static void BuildResults( Transform root, MenuScreens m ) {
        Image panel = Panel( "Results", root, new Vector2( 1000, 860 ) );
        m.resultsPanel = panel.gameObject;
        Transform t = panel.transform;
        m.resultsTitle = Text( "Title", t, "Day 1 complete!", 64, INK, TextAlignmentOptions.Center, new Vector2( 0, 345 ), new Vector2( 900, 90 ), font: fontTitle );
        m.resultStars = new Image[3];
        for ( int i = 0; i < 3; i++ ) {
            m.resultStars[i] = Img( "Star", t, "FX/star", Color.white, new Vector2( ( i - 1 ) * 130, 240 ), new Vector2( 110, 110 ) );
        }
        m.resultsBody = Text( "Body", t, "", 34, INK, TextAlignmentOptions.TopLeft, new Vector2( 0, 0 ), new Vector2( 720, 340 ) );
        m.resultsTotal = Text( "Total", t, "", 40, new Color( 0.7f, 0.25f, 0.2f ), TextAlignmentOptions.Center, new Vector2( 0, -230 ), new Vector2( 900, 110 ) );
        m.resultsContinueButton = Button( "Continue", t, "[Space] Visit the shop", new Vector2( -150, -350 ), new Vector2( 520, 84 ), out m.resultsContinueLabel );
        m.resultsMenuButton = Button( "Menu", t, "Main menu", new Vector2( 300, -350 ), new Vector2( 300, 84 ), out _ );
    }

    static void BuildShop( Transform root, MenuScreens m ) {
        Image panel = Panel( "Shop", root, new Vector2( 1720, 940 ) );
        m.shopPanel = panel.gameObject;
        Transform t = panel.transform;
        Text( "Title", t, "Upgrade Shop", 70, INK, TextAlignmentOptions.Center, new Vector2( 0, 400 ), new Vector2( 1000, 90 ), font: fontTitle );
        m.shopSavings = Text( "Savings", t, "Savings: $0", 42, new Color( 0.7f, 0.25f, 0.2f ), TextAlignmentOptions.Center, new Vector2( 0, 320 ), new Vector2( 900, 60 ) );

        eUpgrade[] order = { eUpgrade.thirdOven, eUpgrade.turboOven, eUpgrade.comfyChairs, eUpgrade.quickWhisk, eUpgrade.runningShoes, eUpgrade.tipJar };
        m.upgradeCards = new UpgradeCard[order.Length];
        for ( int i = 0; i < order.Length; i++ ) {
            float x = ( i % 3 - 1 ) * 545;
            float y = ( i < 3 ) ? 130 : -160;
            m.upgradeCards[i] = UpgradeCardUI( t, order[i], new Vector2( x, y ) );
        }
        m.shopContinueButton = Button( "Continue", t, "[Space] Open Day 2", new Vector2( 0, -385 ), new Vector2( 520, 84 ), out m.shopContinueLabel );
    }

    static UpgradeCard UpgradeCardUI( Transform parent, eUpgrade id, Vector2 pos ) {
        Image bg = Img( id.ToString(), parent, "UI/panel_cream_rivets", Color.white, pos, new Vector2( 520, 265 ), true );
        UpgradeCard card = bg.gameObject.AddComponent<UpgradeCard>();
        card.upgrade = id;
        card.background = bg;
        Transform t = bg.transform;
        card.icon = Img( "Icon", t, null, Color.white, new Vector2( -175, 30 ), new Vector2( 112, 112 ) );
        card.icon.preserveAspect = true;
        card.keyText = Text( "Key", t, "1", 32, new Color( 0.7f, 0.25f, 0.2f ), TextAlignmentOptions.Center, new Vector2( -195, 85 ), new Vector2( 40, 40 ) );
        card.nameText = Text( "Name", t, "Upgrade", 36, INK, TextAlignmentOptions.Left, new Vector2( 60, 75 ), new Vector2( 320, 50 ) );
        card.descriptionText = Text( "Description", t, "", 26, INK, TextAlignmentOptions.TopLeft, new Vector2( 60, 0 ), new Vector2( 320, 90 ), wrap: true );
        card.buyButton = Button( "Buy", t, "Buy $50", new Vector2( 60, -85 ), new Vector2( 260, 66 ), out card.buyLabel );
        return card;
    }

    static void BuildVictory( Transform root, MenuScreens m ) {
        Image panel = Panel( "Victory", root, new Vector2( 1150, 760 ) );
        m.victoryPanel = panel.gameObject;
        Transform t = panel.transform;
        Text( "Title", t, "You did it!", 90, INK, TextAlignmentOptions.Center, new Vector2( 0, 270 ), new Vector2( 1000, 120 ), font: fontTitle );
        for ( int i = 0; i < 3; i++ ) Img( "Star", t, "FX/star", new Color( 1f, 0.82f, 0.2f ), new Vector2( ( i - 1 ) * 120, 165 ), new Vector2( 90, 90 ) );
        m.victoryBody = Text( "Body", t, "", 36, INK, TextAlignmentOptions.Top, new Vector2( 0, -20 ), new Vector2( 1000, 300 ), wrap: true );
        m.victoryPlayAgainButton = Button( "Again", t, "[Space] Play again", new Vector2( -170, -290 ), new Vector2( 460, 84 ), out _ );
        m.victoryMenuButton = Button( "Menu", t, "Main menu", new Vector2( 250, -290 ), new Vector2( 300, 84 ), out _ );
    }

    // ================================================================== UI helpers

    // The red ribbon is three square 32px tiles: two folded ends and a middle that stretches.
    // Its ends stay square when the slice border (32px) is drawn exactly as tall as the ribbon.
    static Image Ribbon( Transform parent, Vector2 pos, Vector2 size ) {
        Image img = Img( "Ribbon", parent, "UI/ribbon", Color.white, pos, size, true );
        float borderAtScale1 = 32 * 100f / 16f;     // canvas units per 32px border at a multiplier of 1
        img.pixelsPerUnitMultiplier = borderAtScale1 / size.y;
        return img;
    }

    static Image Panel( string name, Transform parent, Vector2 size ) {
        Image img = Img( name, parent, "UI/panel_cream", Color.white, Vector2.zero, size, true );
        return img;
    }

    static RectTransform Rect( string name, Transform parent, Vector2 pos, Vector2 size ) {
        GameObject go = new GameObject( name, typeof( RectTransform ) );
        go.transform.SetParent( parent, false );
        RectTransform rt = (RectTransform) go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static void Stretch( RectTransform rt ) {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Anchor( RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot ) {
        Vector2 pos = rt.anchoredPosition;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
    }

    static Image Img( string name, Transform parent, string sprite, Color color, Vector2 pos, Vector2 size, bool sliced = false ) {
        RectTransform rt = Rect( name, parent, pos, size );
        Image img = rt.gameObject.AddComponent<Image>();
        if ( sprite != null ) img.sprite = Spr( sprite );
        img.color = color;
        if ( sliced ) {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1.5f;
        }
        return img;
    }

    static void Fill( Image img ) {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        img.fillAmount = 0.5f;
    }

    static TextMeshProUGUI Text( string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align,
                                 Vector2 pos, Vector2 box, Vector2? anchor = null, TMP_FontAsset font = null, bool outline = false, bool wrap = false ) {
        RectTransform rt = Rect( name, parent, pos, box );
        if ( anchor.HasValue ) Anchor( rt, anchor.Value, anchor.Value, new Vector2( anchor.Value.x, 0.5f ) );
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font != null ? font : fontBody;
        if ( outline ) t.fontSharedMaterial = ( t.font == fontTitle ) ? titleOutlineMaterial : outlineMaterial;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Button Button( string name, Transform parent, string label, Vector2 pos, Vector2 size, out TextMeshProUGUI labelText ) {
        Image img = Img( name + " Button", parent, "UI/button_cream", Color.white, pos, size, true );
        img.pixelsPerUnitMultiplier = 2f;
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color( 1f, 0.93f, 0.7f );
        cb.pressedColor = new Color( 0.85f, 0.75f, 0.55f );
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color( 0.6f, 0.58f, 0.55f, 0.8f );
        b.colors = cb;
        img.gameObject.AddComponent<ButtonFeel>();
        labelText = Text( "Label", img.transform, label, Mathf.Min( 38, size.y * 0.45f ), INK, TextAlignmentOptions.Center, Vector2.zero, size );
        return b;
    }

    // ================================================================== world helpers

    static Transform Child( string name, Transform parent ) {
        GameObject go = new GameObject( name );
        go.transform.SetParent( parent, false );
        return go.transform;
    }

    static SpriteRenderer MakeSprite( string name, Transform parent, Vector3 localPos, string sprite, int order,
                                      string layer = "Default", Color? color = null ) {
        GameObject go = new GameObject( name );
        go.transform.SetParent( parent, false );
        go.transform.localPosition = localPos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        if ( sprite != null ) sr.sprite = Spr( sprite );
        // Sort by the pivot (an object's feet), so things lower on screen are drawn in front
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        if ( color.HasValue ) sr.color = color.Value;
        return sr;
    }

    static SpriteRenderer MakeTiled( string name, Transform parent, Vector2 center, Vector2 size, string sprite, int order, string layer = "Background" ) {
        SpriteRenderer sr = MakeSprite( name, parent, center, sprite, order, layer );
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = size;
        return sr;
    }

    static WorldBar MakeBar( string name, Transform parent, Vector3 localPos, float width, float height, int order ) {
        GameObject go = new GameObject( name );
        go.transform.SetParent( parent, false );
        go.transform.localPosition = localPos;
        WorldBar bar = go.AddComponent<WorldBar>();
        bar.width = width;
        bar.height = height;
        // The white sprite is 4px at 16 PPU (0.25 units), so scale it up by 4 to make it 1 unit
        GameObject bg = new GameObject( "Background" );
        bg.transform.SetParent( go.transform, false );
        bar.background = bg.AddComponent<SpriteRenderer>();
        bar.background.sprite = Spr( "UI/white" );
        bar.background.color = new Color( 0.18f, 0.1f, 0.07f, 0.9f );
        bar.background.sortingLayerName = "WorldUI";
        bar.background.sortingOrder = order;
        GameObject fill = new GameObject( "Fill" );
        fill.transform.SetParent( go.transform, false );
        bar.fill = fill.AddComponent<SpriteRenderer>();
        bar.fill.sprite = Spr( "UI/white" );
        bar.fill.color = Color.green;
        bar.fill.sortingLayerName = "WorldUI";
        bar.fill.sortingOrder = order + 1;
        go.SetActive( false );
        return bar;
    }

    static TextMeshPro MakeWorldText( string name, Transform parent, Vector3 localPos, string text, float size, Color color, string layer, int order ) {
        GameObject go = new GameObject( name );
        go.transform.SetParent( parent, false );
        go.transform.localPosition = localPos;
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.font = fontBody;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2( 3, 2 );
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sortingLayerName = layer;
        mr.sortingOrder = order;
        return tmp;
    }

    static Sprite Spr( string path ) {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>( "Assets/Art/" + path + ".png" );
        if ( s == null ) Debug.LogError( "Missing sprite: " + path );
        return s;
    }

    static Sprite[] LoadSprites( string folder ) {
        string[] guids = AssetDatabase.FindAssets( "t:Sprite", new[] { "Assets/Art/" + folder } );
        List<string> paths = new List<string>();
        foreach ( string g in guids ) paths.Add( AssetDatabase.GUIDToAssetPath( g ) );
        paths.Sort();
        List<Sprite> sprites = new List<Sprite>();
        foreach ( string p in paths ) sprites.Add( AssetDatabase.LoadAssetAtPath<Sprite>( p ) );
        return sprites.ToArray();
    }

    static void EnsureFolder( string path ) {
        if ( AssetDatabase.IsValidFolder( path ) ) return;
        string parent = Path.GetDirectoryName( path ).Replace( '\\', '/' );
        AssetDatabase.CreateFolder( parent, Path.GetFileName( path ) );
    }
}
