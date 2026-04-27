using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class RetroCursedParkBuilderWindow : EditorWindow
{
    private const string RootPath = "Assets/_Project/Content/World/Attractions/CursedDesert";
    private const string SpriteRootPath = RootPath + "/Sprites";
    private const string MaterialFolderPath = RootPath + "/Materials";
    private const string PrefabFolderPath = RootPath + "/Prefabs";
    private const string GeneratedFolderPath = RootPath + "/Generated";
    private const string LibraryPath = GeneratedFolderPath + "/CursedParkAssetLibrary.asset";
    private const string SpriteMaterialPath = MaterialFolderPath + "/CursedParkSprite.mat";
    private const string GroundMaterialPath = MaterialFolderPath + "/CursedParkDesertGround.mat";
    private const string PathMaterialPath = MaterialFolderPath + "/CursedParkMidwayPath.mat";
    private const string GeneratorPrefabPath = PrefabFolderPath + "/CursedParkGenerator.prefab";
    private const string ExamplePrefabPath = PrefabFolderPath + "/CursedParkExample.prefab";

    private static readonly AssetSpec[] AssetSpecs =
    {
        new("MajorAttractions", "RustFerrisWheel", "rust ferris wheel", RetroCursedParkAssetCategory.MajorAttraction, 12.5f, 0.92f, 1.28f, true, true, false, 1.15f, 0.34f, 0.08f, 1f),
        new("MajorAttractions", "SkullCoasterGate", "skull coaster gate", RetroCursedParkAssetCategory.MajorAttraction, 9.4f, 0.88f, 1.24f, true, true, false, 0.92f, 0.25f, 0.08f, 1f),
        new("MajorAttractions", "CrookedFunhouse", "crooked funhouse", RetroCursedParkAssetCategory.MajorAttraction, 10.5f, 0.9f, 1.3f, true, true, false, 1.2f, 0.42f, 0.14f, 1f),
        new("MajorAttractions", "ClownMouthTunnel", "mouth tunnel", RetroCursedParkAssetCategory.MajorAttraction, 9.7f, 0.9f, 1.28f, true, true, false, 0.82f, 0.32f, 0.08f, 1f),
        new("MajorAttractions", "BrokenCarousel", "broken carousel", RetroCursedParkAssetCategory.MajorAttraction, 9.1f, 0.9f, 1.2f, true, true, false, 0.88f, 0.28f, 0.08f, 1f),
        new("MajorAttractions", "HauntedBumperCars", "haunted bumper cars", RetroCursedParkAssetCategory.MajorAttraction, 7.6f, 0.88f, 1.22f, true, true, false, 1.05f, 0.4f, 0.08f, 1f),
        new("MajorAttractions", "CursedShootingGallery", "cursed shooting gallery", RetroCursedParkAssetCategory.MajorAttraction, 7.9f, 0.92f, 1.24f, true, true, false, 0.95f, 0.32f, 0.07f, 1f),
        new("MajorAttractions", "FortuneTent", "fortune tent", RetroCursedParkAssetCategory.MajorAttraction, 8.5f, 0.88f, 1.25f, true, true, false, 1.55f, 0.5f, 0.15f, 1f),
        new("MajorAttractions", "DropTower", "drop tower", RetroCursedParkAssetCategory.MajorAttraction, 12.2f, 0.9f, 1.25f, true, true, false, 0.75f, 0.22f, 0.12f, 0.8f),
        new("MajorAttractions", "MirrorMaze", "mirror maze", RetroCursedParkAssetCategory.MajorAttraction, 8.4f, 0.88f, 1.18f, true, true, false, 1.05f, 0.28f, 0.08f, 1f),
        new("MajorAttractions", "CoffinRideEntrance", "coffin ride entrance", RetroCursedParkAssetCategory.MajorAttraction, 9.1f, 0.92f, 1.22f, true, true, false, 0.78f, 0.22f, 0.07f, 1f),
        new("MajorAttractions", "RottenTeacupRide", "rotten teacup ride", RetroCursedParkAssetCategory.MajorAttraction, 6.5f, 0.9f, 1.18f, true, true, false, 0.95f, 0.28f, 0.06f, 0.9f),
        new("MajorAttractions", "GiantTicketArch", "giant ticket arch", RetroCursedParkAssetCategory.MajorAttraction, 10.6f, 0.9f, 1.22f, true, true, false, 0.9f, 0.32f, 0.08f, 0.9f),
        new("MajorAttractions", "FreakshowWagon", "freak show wagon", RetroCursedParkAssetCategory.MajorAttraction, 8.0f, 0.9f, 1.2f, true, true, false, 0.8f, 0.25f, 0.06f, 0.9f),
        new("MajorAttractions", "BoardedHauntedArcade", "boarded haunted arcade", RetroCursedParkAssetCategory.MajorAttraction, 8.7f, 0.9f, 1.24f, true, true, false, 1.1f, 0.42f, 0.12f, 1f),
        new("MajorAttractions", "CollapsedCircusTent", "collapsed circus tent", RetroCursedParkAssetCategory.MajorAttraction, 7.8f, 0.9f, 1.3f, true, true, false, 0.65f, 0.18f, 0.05f, 0.8f),

        new("Machines", "RustSlotMachine", "rust slot machine", RetroCursedParkAssetCategory.Machine, 3.3f, 0.9f, 1.25f, true, true, false, 1.25f, 0.48f, 0.08f, 1.2f),
        new("Machines", "FortuneTellerCabinet", "fortune teller cabinet", RetroCursedParkAssetCategory.Machine, 3.9f, 0.9f, 1.18f, true, true, false, 1.55f, 0.55f, 0.06f, 1.1f),
        new("Machines", "CursedClawMachine", "cursed claw machine", RetroCursedParkAssetCategory.Machine, 3.8f, 0.9f, 1.2f, true, true, false, 1.05f, 0.38f, 0.04f, 1f),
        new("Machines", "RottenTicketBooth", "rotten ticket booth", RetroCursedParkAssetCategory.Machine, 3.5f, 0.88f, 1.18f, true, true, false, 0.85f, 0.26f, 0.04f, 1f),
        new("Machines", "CrookedPrizeWheel", "crooked prize wheel", RetroCursedParkAssetCategory.Machine, 3.4f, 0.9f, 1.22f, true, true, false, 1.1f, 0.42f, 0.08f, 1f),
        new("Machines", "StrengthTester", "strength tester", RetroCursedParkAssetCategory.Machine, 3.6f, 0.9f, 1.18f, true, true, false, 0.95f, 0.3f, 0.05f, 1f),
        new("Machines", "SkullRouletteTable", "skull roulette table", RetroCursedParkAssetCategory.Machine, 3.1f, 0.95f, 1.25f, true, true, false, 1.15f, 0.38f, 0.03f, 1f),
        new("Machines", "HauntedVendingMachine", "haunted vending machine", RetroCursedParkAssetCategory.Machine, 3.6f, 0.9f, 1.18f, true, true, false, 1.2f, 0.46f, 0.04f, 1f),
        new("Machines", "CoinPusherCabinet", "coin pusher cabinet", RetroCursedParkAssetCategory.Machine, 3.2f, 0.9f, 1.2f, true, true, false, 1.35f, 0.5f, 0.04f, 1f),
        new("Machines", "ShootingGalleryCounter", "shooting gallery counter", RetroCursedParkAssetCategory.Machine, 3.0f, 0.9f, 1.2f, true, true, false, 0.85f, 0.26f, 0.03f, 1f),
        new("Machines", "BrokenPhotobooth", "broken photobooth", RetroCursedParkAssetCategory.Machine, 3.4f, 0.9f, 1.18f, true, true, false, 0.9f, 0.3f, 0.03f, 1f),
        new("Machines", "TeethArcadeCabinet", "teeth arcade cabinet", RetroCursedParkAssetCategory.Machine, 3.6f, 0.9f, 1.18f, true, true, false, 1.25f, 0.5f, 0.05f, 1f),
        new("Machines", "PopcornCoffinCart", "popcorn coffin cart", RetroCursedParkAssetCategory.Machine, 3.0f, 0.9f, 1.18f, true, true, false, 1.05f, 0.34f, 0.04f, 0.9f),
        new("Machines", "UglyPrizeShelf", "ugly prize shelf", RetroCursedParkAssetCategory.Machine, 3.1f, 0.9f, 1.22f, true, true, false, 0.9f, 0.25f, 0.03f, 0.95f),
        new("Machines", "CashierKiosk", "cashier kiosk", RetroCursedParkAssetCategory.Machine, 3.25f, 0.9f, 1.2f, true, true, false, 0.9f, 0.28f, 0.03f, 0.9f),
        new("Machines", "CarnivalGeneratorBox", "carnival generator box", RetroCursedParkAssetCategory.Machine, 2.8f, 0.9f, 1.22f, true, true, false, 0.85f, 0.25f, 0.05f, 0.8f),

        new("Automatons", "SkeletalTicketTaker", "skeletal ticket taker", RetroCursedParkAssetCategory.Automaton, 3.8f, 0.9f, 1.18f, true, true, false, 1f, 0.3f, 0.22f, 1.1f),
        new("Automatons", "ClownConductor", "clown conductor", RetroCursedParkAssetCategory.Automaton, 3.5f, 0.9f, 1.2f, true, true, false, 1.15f, 0.4f, 0.32f, 1.1f),
        new("Automatons", "CymbalPuppetMachine", "cymbal puppet machine", RetroCursedParkAssetCategory.Automaton, 3.2f, 0.9f, 1.18f, true, true, false, 1.0f, 0.36f, 0.25f, 1f),
        new("Automatons", "CrackedBallerina", "cracked ballerina", RetroCursedParkAssetCategory.Automaton, 3.4f, 0.9f, 1.2f, true, true, false, 0.82f, 0.2f, 0.38f, 0.9f),
        new("Automatons", "OrganGrinderMachine", "organ grinder machine", RetroCursedParkAssetCategory.Automaton, 3.2f, 0.9f, 1.2f, true, true, false, 0.95f, 0.28f, 0.18f, 0.9f),
        new("Automatons", "AnimatronicStrongman", "animatronic strongman", RetroCursedParkAssetCategory.Automaton, 3.25f, 0.9f, 1.2f, true, true, false, 0.88f, 0.22f, 0.22f, 0.95f),
        new("Automatons", "ScarecrowRingmaster", "ringmaster statue", RetroCursedParkAssetCategory.Automaton, 3.8f, 0.9f, 1.18f, true, true, false, 0.85f, 0.25f, 0.28f, 1f),
        new("Automatons", "CursedPortraitStand", "cursed portrait stand", RetroCursedParkAssetCategory.Automaton, 3.1f, 0.9f, 1.18f, true, true, false, 1.0f, 0.3f, 0.05f, 0.85f),
        new("Automatons", "MannequinChoir", "mannequin choir", RetroCursedParkAssetCategory.Automaton, 2.8f, 0.92f, 1.22f, true, true, false, 0.72f, 0.18f, 0.1f, 0.85f),
        new("Automatons", "JackInBoxHead", "jack box head", RetroCursedParkAssetCategory.Automaton, 3.0f, 0.9f, 1.18f, true, true, false, 1.05f, 0.38f, 0.3f, 1f),
        new("Automatons", "PalmReaderHands", "palm reader hands", RetroCursedParkAssetCategory.Automaton, 2.9f, 0.9f, 1.2f, true, true, false, 1.65f, 0.55f, 0.1f, 1f),
        new("Automatons", "BentBrassRobot", "bent brass robot", RetroCursedParkAssetCategory.Automaton, 3.3f, 0.9f, 1.22f, true, true, false, 1f, 0.32f, 0.28f, 1f),
        new("Automatons", "DollPileShrine", "doll pile shrine", RetroCursedParkAssetCategory.Automaton, 3.1f, 0.9f, 1.2f, true, true, false, 1.1f, 0.36f, 0.08f, 0.9f),
        new("Automatons", "CryingMaskStatue", "crying mask statue", RetroCursedParkAssetCategory.Automaton, 3.5f, 0.92f, 1.18f, true, true, false, 0.78f, 0.18f, 0.05f, 0.85f),
        new("Automatons", "PuppetTheater", "puppet theater", RetroCursedParkAssetCategory.Automaton, 3.35f, 0.9f, 1.2f, true, true, false, 1.05f, 0.32f, 0.08f, 1f),
        new("Automatons", "CrackedAngelBulb", "cracked angel bulb", RetroCursedParkAssetCategory.Automaton, 3.25f, 0.9f, 1.18f, true, true, false, 1.45f, 0.5f, 0.12f, 1f),

        new("SignageClutter", "EntranceSignFrame", "entrance sign frame", RetroCursedParkAssetCategory.SignageClutter, 4.9f, 0.92f, 1.18f, true, true, false, 1.15f, 0.42f, 0.08f, 0.45f),
        new("SignageClutter", "CrookedArrowSign", "crooked arrow sign", RetroCursedParkAssetCategory.SignageClutter, 2.8f, 0.82f, 1.28f, false, true, false, 0.55f, 0.18f, 0.12f, 1f),
        new("SignageClutter", "DeadLampPost", "dead lamp post", RetroCursedParkAssetCategory.SignageClutter, 3.6f, 0.85f, 1.25f, false, true, false, 1.2f, 0.52f, 0.14f, 1f),
        new("SignageClutter", "BrokenStringLightPole", "broken string light pole", RetroCursedParkAssetCategory.SignageClutter, 3.8f, 0.85f, 1.25f, false, true, false, 1.35f, 0.56f, 0.2f, 0.85f),
        new("SignageClutter", "BarbedFenceSegment", "barbed fence", RetroCursedParkAssetCategory.SignageClutter, 2.1f, 0.9f, 1.12f, false, false, false, 0.25f, 0.05f, 0.05f, 0.35f),
        new("SignageClutter", "TornBannerPole", "torn banner pole", RetroCursedParkAssetCategory.SignageClutter, 3.2f, 0.82f, 1.2f, false, true, false, 0.35f, 0.08f, 0.26f, 0.9f),
        new("SignageClutter", "TicketTokensPile", "ticket tokens pile", RetroCursedParkAssetCategory.SignageClutter, 1.0f, 0.8f, 1.5f, false, true, false, 0.45f, 0.1f, 0.02f, 1f),
        new("SignageClutter", "LoudspeakerTower", "loudspeaker tower", RetroCursedParkAssetCategory.SignageClutter, 3.3f, 0.85f, 1.25f, false, true, false, 0.65f, 0.18f, 0.16f, 0.75f),
        new("SignageClutter", "DeadShrubStreamers", "dead shrub streamers", RetroCursedParkAssetCategory.SignageClutter, 2.6f, 0.78f, 1.3f, false, true, false, 0.25f, 0.06f, 0.18f, 0.9f),
        new("SignageClutter", "WindBentUmbrella", "wind bent umbrella", RetroCursedParkAssetCategory.SignageClutter, 2.4f, 0.8f, 1.25f, false, true, false, 0.3f, 0.08f, 0.22f, 0.85f),
        new("SignageClutter", "TrashBarrel", "trash barrel", RetroCursedParkAssetCategory.SignageClutter, 1.5f, 0.82f, 1.25f, false, true, false, 0.45f, 0.12f, 0.05f, 1.2f),
        new("SignageClutter", "BrokenBench", "broken bench", RetroCursedParkAssetCategory.SignageClutter, 1.45f, 0.85f, 1.25f, false, true, false, 0.2f, 0.05f, 0.02f, 1.05f),
        new("SignageClutter", "WarningPlacard", "warning placard", RetroCursedParkAssetCategory.SignageClutter, 2.0f, 0.8f, 1.18f, false, true, false, 0.38f, 0.08f, 0.08f, 0.85f),
        new("SignageClutter", "SkullWayfindingPost", "wayfinding post", RetroCursedParkAssetCategory.SignageClutter, 2.7f, 0.82f, 1.2f, false, true, false, 0.45f, 0.1f, 0.1f, 0.85f),
        new("SignageClutter", "CableCoil", "buried cable coil", RetroCursedParkAssetCategory.SignageClutter, 1.45f, 0.85f, 1.25f, false, true, false, 0.25f, 0.05f, 0.02f, 1f),
        new("SignageClutter", "GeneratorSmokeStack", "generator smoke stack", RetroCursedParkAssetCategory.SignageClutter, 2.3f, 0.82f, 1.22f, false, true, false, 0.65f, 0.18f, 0.08f, 0.75f),

        new("GroundDecals", "CrackedDesertSand", "cracked desert sand", RetroCursedParkAssetCategory.GroundDecal, 5.2f, 0.85f, 1.65f, false, false, true, 0.05f, 0f, 0f, 1.25f),
        new("GroundDecals", "DustyMidwayPath", "dusty midway path", RetroCursedParkAssetCategory.GroundDecal, 5.8f, 0.85f, 1.75f, false, false, true, 0.05f, 0f, 0f, 1.25f),
        new("GroundDecals", "OilStainedAsphalt", "oil stained asphalt", RetroCursedParkAssetCategory.GroundDecal, 5.1f, 0.75f, 1.45f, false, false, true, 0.18f, 0.04f, 0f, 0.9f),
        new("GroundDecals", "RottenPlankPatch", "rotten plank patch", RetroCursedParkAssetCategory.GroundDecal, 5.0f, 0.8f, 1.35f, false, false, true, 0.05f, 0f, 0f, 0.9f),
        new("GroundDecals", "BoneConfetti", "bone confetti", RetroCursedParkAssetCategory.GroundDecal, 4.6f, 0.75f, 1.25f, false, false, true, 0.35f, 0.06f, 0f, 0.7f),
        new("GroundDecals", "TireTracks", "tire tracks", RetroCursedParkAssetCategory.GroundDecal, 5.4f, 0.8f, 1.55f, false, false, true, 0.04f, 0f, 0f, 0.9f),
        new("GroundDecals", "CableTrench", "cable trench", RetroCursedParkAssetCategory.GroundDecal, 5.2f, 0.75f, 1.45f, false, false, true, 0.08f, 0f, 0f, 0.8f),
        new("GroundDecals", "ChalkCircle", "chalk circle", RetroCursedParkAssetCategory.GroundDecal, 5.2f, 0.8f, 1.35f, false, false, true, 0.42f, 0.12f, 0f, 0.65f),
        new("GroundDecals", "BlackPuddle", "black puddle", RetroCursedParkAssetCategory.GroundDecal, 4.5f, 0.7f, 1.25f, false, false, true, 0.18f, 0.04f, 0f, 0.75f),
        new("GroundDecals", "RustScrapPile", "rust scrap pile", RetroCursedParkAssetCategory.GroundDecal, 4.4f, 0.7f, 1.2f, false, false, true, 0.12f, 0.03f, 0f, 0.55f),
        new("GroundDecals", "AshScorch", "ash scorch", RetroCursedParkAssetCategory.GroundDecal, 4.8f, 0.75f, 1.35f, false, false, true, 0.22f, 0.06f, 0f, 0.7f),
        new("GroundDecals", "BrokenTilePatch", "broken tile patch", RetroCursedParkAssetCategory.GroundDecal, 4.7f, 0.75f, 1.28f, false, false, true, 0.08f, 0f, 0f, 0.65f),
        new("GroundDecals", "CarnivalPaintStripe", "carnival paint stripe", RetroCursedParkAssetCategory.GroundDecal, 5.0f, 0.8f, 1.5f, false, false, true, 0.15f, 0.02f, 0f, 0.75f),
        new("GroundDecals", "CollapsedBanner", "collapsed banner", RetroCursedParkAssetCategory.GroundDecal, 4.7f, 0.75f, 1.25f, false, false, true, 0.14f, 0.02f, 0f, 0.7f),
        new("GroundDecals", "CoinScatter", "coin scatter", RetroCursedParkAssetCategory.GroundDecal, 4.0f, 0.7f, 1.25f, false, false, true, 0.42f, 0.08f, 0f, 0.55f),
        new("GroundDecals", "DeadGrassPapers", "dead grass papers", RetroCursedParkAssetCategory.GroundDecal, 4.6f, 0.75f, 1.35f, false, false, true, 0.08f, 0f, 0f, 0.85f),
    };

    private Vector2 scroll;
    private bool selectCreatedAsset = true;
    private int sceneSeed = 666013;

    [MenuItem("Tools/Ultraloud/Attraction Parks/Cursed Desert Park Builder")]
    public static void Open()
    {
        RetroCursedParkBuilderWindow window = GetWindow<RetroCursedParkBuilderWindow>("Cursed Park");
        window.minSize = new Vector2(560f, 420f);
    }

    [MenuItem("GameObject/Ultraloud/Attraction Parks/Cursed Desert Park", false, 31)]
    public static void CreateScenePark(MenuCommand command)
    {
        GameObject park = CreateConfiguredParkObject("CursedDesertAttractionPark", Random.Range(1000, 999999));
        RetroCursedParkGenerator generator = park.GetComponent<RetroCursedParkGenerator>();
        generator.RebuildParkNow();
        PlaceCreatedObject(command, park, "Create Cursed Desert Park");
    }

    [MenuItem("Assets/Create/Ultraloud/Attraction Parks/Cursed Desert Park Assets")]
    public static void CreateAssetsFromMenu()
    {
        CreateOrUpdateAssets(true);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        using (EditorGUILayout.ScrollViewScope scope = new(scroll))
        {
            scroll = scope.scrollPosition;
            EditorGUILayout.LabelField("Cursed Desert Attraction Park", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Builds a procedural desert attraction park from ImageGen sprite sheets. The generated library includes major attractions, machines, automatons, signage/clutter, and ground decals with emission maps for flickering cursed glow.", MessageType.Info);

            EditorGUILayout.Space(8f);
            sceneSeed = EditorGUILayout.IntField("Scene Seed", sceneSeed);
            selectCreatedAsset = EditorGUILayout.Toggle("Select Created Asset", selectCreatedAsset);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create / Update Assets", GUILayout.Height(30f)))
                {
                    CreateOrUpdateAssets(selectCreatedAsset);
                }

                if (GUILayout.Button("Create Park In Scene", GUILayout.Height(30f)))
                {
                    GameObject park = CreateConfiguredParkObject("CursedDesertAttractionPark", sceneSeed);
                    park.GetComponent<RetroCursedParkGenerator>().RebuildParkNow();
                    Undo.RegisterCreatedObjectUndo(park, "Create Cursed Desert Park");
                    Selection.activeGameObject = park;
                    EditorSceneManager.MarkSceneDirty(park.scene);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generator Prefab", GUILayout.Height(30f)))
                {
                    CreateOrReplaceGeneratorPrefab(selectCreatedAsset);
                }

                if (GUILayout.Button("Generated Example Prefab", GUILayout.Height(30f)))
                {
                    CreateOrReplaceExamplePrefab(selectCreatedAsset);
                }
            }

            if (GUILayout.Button("Rebuild Selected Park Generators", GUILayout.Height(30f)))
            {
                RebuildSelectedGenerators();
            }

            EditorGUILayout.Space(10f);
            DrawAssetStatus("Core Assets", new[] { LibraryPath, SpriteMaterialPath, GroundMaterialPath, PathMaterialPath, GeneratorPrefabPath, ExamplePrefabPath });
            DrawSpriteStatus();
        }
    }

    public static void CreateOrUpdateAssets(bool selectAsset)
    {
        EnsureAssetFolder(GeneratedFolderPath);
        EnsureAssetFolder(MaterialFolderPath);
        EnsureAssetFolder(PrefabFolderPath);
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        ConfigureTextureImporters();
        Material spriteMaterial = CreateOrUpdateSpriteMaterial();
        CreateOrUpdateFlatMaterial(GroundMaterialPath, "CursedParkDesertGround", new Color(0.58f, 0.47f, 0.31f, 1f), 0.86f);
        CreateOrUpdateFlatMaterial(PathMaterialPath, "CursedParkMidwayPath", new Color(0.24f, 0.19f, 0.15f, 1f), 0.92f);
        RetroCursedParkAssetLibrary library = CreateOrUpdateLibrary();

        EditorUtility.SetDirty(spriteMaterial);
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (selectAsset)
        {
            Selection.activeObject = library;
            EditorGUIUtility.PingObject(library);
        }
    }

    public static GameObject CreateConfiguredParkObject(string objectName, int seed)
    {
        CreateOrUpdateAssets(false);
        GameObject park = new(objectName);
        RetroCursedParkGenerator generator = park.AddComponent<RetroCursedParkGenerator>();
        SerializedObject serialized = new(generator);
        serialized.Update();
        SetObject(serialized, "assetLibrary", AssetDatabase.LoadAssetAtPath<RetroCursedParkAssetLibrary>(LibraryPath));
        SetObject(serialized, "spriteMaterial", AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath));
        SetObject(serialized, "groundMaterial", AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath));
        SetObject(serialized, "pathMaterial", AssetDatabase.LoadAssetAtPath<Material>(PathMaterialPath));
        SetInt(serialized, "seed", seed);
        SetFloat(serialized, "parkRadius", 52f);
        SetFloat(serialized, "innerDeadZoneRadius", 8f);
        SetFloat(serialized, "midwayWidth", 7.5f);
        SetInt(serialized, "majorAttractionCount", 15);
        SetInt(serialized, "machineCount", 34);
        SetInt(serialized, "automatonCount", 28);
        SetInt(serialized, "signageClutterCount", 92);
        SetInt(serialized, "groundDecalCount", 150);
        SetInt(serialized, "fenceSegmentCount", 64);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(generator);
        return park;
    }

    public static void CreateOrReplaceGeneratorPrefab(bool selectAsset)
    {
        EnsureAssetFolder(PrefabFolderPath);
        GameObject park = CreateConfiguredParkObject("CursedParkGenerator", 666013);
        StripGeneratedChildren(park.transform);
        SavePrefab(park, GeneratorPrefabPath, selectAsset);
    }

    public static void CreateOrReplaceExamplePrefab(bool selectAsset)
    {
        EnsureAssetFolder(PrefabFolderPath);
        GameObject park = CreateConfiguredParkObject("CursedParkExample", 666013);
        park.GetComponent<RetroCursedParkGenerator>().RebuildParkNow();
        SavePrefab(park, ExamplePrefabPath, selectAsset);
    }

    public static void ConfigureTextureImporters()
    {
        foreach (AssetSpec spec in AssetSpecs)
        {
            ConfigureTextureImporter(BuildSpritePath(spec.Folder, spec.Id, "Base"), false, true);
            ConfigureTextureImporter(BuildSpritePath(spec.Folder, spec.Id, "Normal"), true, true);
            ConfigureTextureImporter(BuildSpritePath(spec.Folder, spec.Id, "Emission"), false, true);
        }
    }

    private static RetroCursedParkAssetLibrary CreateOrUpdateLibrary()
    {
        RetroCursedParkAssetLibrary library = AssetDatabase.LoadAssetAtPath<RetroCursedParkAssetLibrary>(LibraryPath);
        if (library == null)
        {
            library = CreateInstance<RetroCursedParkAssetLibrary>();
            library.name = "CursedParkAssetLibrary";
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        List<RetroCursedParkSpriteAsset> entries = new(AssetSpecs.Length);
        foreach (AssetSpec spec in AssetSpecs)
        {
            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(BuildSpritePath(spec.Folder, spec.Id, "Base"));
            Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(BuildSpritePath(spec.Folder, spec.Id, "Normal"));
            Texture2D emissionMap = AssetDatabase.LoadAssetAtPath<Texture2D>(BuildSpritePath(spec.Folder, spec.Id, "Emission"));
            if (baseMap == null)
            {
                Debug.LogWarning($"Missing cursed park sprite base map for {spec.Id}.");
                continue;
            }

            RetroCursedParkSpriteAsset entry = new()
            {
                Id = spec.Id,
                DisplayName = spec.DisplayName,
                Category = spec.Category,
                BaseMap = baseMap,
                NormalMap = normalMap,
                EmissionMap = emissionMap,
                BaseSize = ResolveSize(baseMap, spec.TargetHeight, spec.GroundDecal),
                ScaleRange = new Vector2(spec.ScaleMin, spec.ScaleMax),
                Weight = spec.Weight,
                Billboard = spec.Billboard,
                CastShadow = !spec.GroundDecal,
                ReceiveShadow = true,
                InteractableCandidate = spec.Interactable,
                GroundDecal = spec.GroundDecal,
                GlowStrength = spec.GlowStrength,
                FlickerStrength = spec.FlickerStrength,
                SwayStrength = spec.SwayStrength,
                Tint = Color.white,
                EmissionColor = Color.white,
                RimColor = new Color(1f, 0.34f, 0.08f, 1f)
            };
            entries.Add(entry);
        }

        library.EditorReplaceAssets(entries);
        EditorUtility.SetDirty(library);
        return library;
    }

    private static Vector2 ResolveSize(Texture2D texture, float targetHeight, bool groundDecal)
    {
        if (texture == null)
        {
            return Vector2.one;
        }

        float aspect = texture.width / Mathf.Max(1f, texture.height);
        if (groundDecal)
        {
            return new Vector2(Mathf.Max(0.2f, targetHeight * aspect), Mathf.Max(0.2f, targetHeight));
        }

        return new Vector2(Mathf.Max(0.2f, targetHeight * aspect), Mathf.Max(0.2f, targetHeight));
    }

    private static Material CreateOrUpdateSpriteMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
        Shader shader = Shader.Find("Ultraloud/Resources/Sprite Glow HDRP");
        shader ??= Shader.Find("HDRP/Unlit");
        shader ??= Shader.Find("Unlit/Transparent");
        if (material == null)
        {
            material = new Material(shader)
            {
                name = "CursedParkSprite"
            };
            AssetDatabase.CreateAsset(material, SpriteMaterialPath);
        }
        else if (shader != null)
        {
            material.shader = shader;
        }

        SetMaterialFloat(material, "_AlphaCutoff", 0.045f);
        SetMaterialFloat(material, "_CoverageSoftness", 0.03f);
        SetMaterialFloat(material, "_EmissionStrength", 1.0f);
        SetMaterialFloat(material, "_RimStrength", 0.22f);
        SetMaterialFloat(material, "_RimPower", 2.7f);
        SetMaterialFloat(material, "_WrapDiffuse", 0.46f);
        SetMaterialFloat(material, "_SpecularStrength", 0.24f);
        SetMaterialColor(material, "_BaseColor", Color.white);
        SetMaterialColor(material, "_EmissionColor", Color.white);
        SetMaterialColor(material, "_RimColor", new Color(1f, 0.34f, 0.08f, 1f));
        SetMaterialColor(material, "_AmbientColor", new Color(0.44f, 0.38f, 0.32f, 1f));
        SetMaterialColor(material, "_LightColor", new Color(1f, 0.78f, 0.48f, 1f));
        return material;
    }

    private static void CreateOrUpdateFlatMaterial(string path, string materialName, Color color, float roughness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("HDRP/Lit");
        shader ??= Shader.Find("Standard");
        if (material == null)
        {
            material = new Material(shader)
            {
                name = materialName
            };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (shader != null)
        {
            material.shader = shader;
        }

        SetMaterialColor(material, "_BaseColor", color);
        SetMaterialColor(material, "_Color", color);
        SetMaterialFloat(material, "_Smoothness", Mathf.Clamp01(1f - roughness));
        EditorUtility.SetDirty(material);
    }

    private static void ConfigureTextureImporter(string assetPath, bool normalMap, bool alpha)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = alpha && !normalMap;
        importer.mipMapsPreserveCoverage = alpha && !normalMap;
        importer.sRGBTexture = !normalMap;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.anisoLevel = 4;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static void RebuildSelectedGenerators()
    {
        bool rebuilt = false;
        foreach (GameObject selected in Selection.gameObjects)
        {
            if (selected == null)
            {
                continue;
            }

            RetroCursedParkGenerator[] generators = selected.GetComponentsInChildren<RetroCursedParkGenerator>(true);
            foreach (RetroCursedParkGenerator generator in generators)
            {
                generator.RebuildParkNow();
                EditorUtility.SetDirty(generator);
                if (!Application.isPlaying && generator.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
                }

                rebuilt = true;
            }
        }

        if (!rebuilt)
        {
            Debug.LogWarning("Select a RetroCursedParkGenerator first.");
        }
    }

    private static void PlaceCreatedObject(MenuCommand command, GameObject createdObject, string undoName)
    {
        GameObject parent = command.context as GameObject;
        if (parent != null)
        {
            Undo.SetTransformParent(createdObject.transform, parent.transform, undoName);
            createdObject.transform.localPosition = Vector3.zero;
            createdObject.transform.localRotation = Quaternion.identity;
        }
        else if (SceneView.lastActiveSceneView != null)
        {
            createdObject.transform.position = SceneView.lastActiveSceneView.pivot;
        }

        Undo.RegisterCreatedObjectUndo(createdObject, undoName);
        Selection.activeGameObject = createdObject;
        EditorSceneManager.MarkSceneDirty(createdObject.scene);
    }

    private static void SavePrefab(GameObject root, string path, bool selectAsset)
    {
        EnsureAssetFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
        bool success;
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, path, out success);
        DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!success || savedPrefab == null)
        {
            Debug.LogError($"Failed to save cursed park prefab at {path}.");
            return;
        }

        if (selectAsset)
        {
            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);
        }
    }

    private static void StripGeneratedChildren(Transform root)
    {
        Transform generated = root.Find("__CursedParkGenerated");
        if (generated != null)
        {
            DestroyImmediate(generated.gameObject);
        }
    }

    private static void DrawAssetStatus(string title, string[] paths)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        foreach (string path in paths)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(path, GUILayout.Width(390f));
                EditorGUILayout.ObjectField(asset, typeof(Object), false);
            }
        }
    }

    private static void DrawSpriteStatus()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Generated Sprite Maps", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"{AssetSpecs.Length} base sprites, plus matching normal and emission maps under {SpriteRootPath}.");
    }

    private static void EnsureAssetFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        EnsureAssetFolder(parent);
        string leaf = Path.GetFileName(folder);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf) && !AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }

    private static string BuildSpritePath(string folder, string id, string suffix)
    {
        return $"{SpriteRootPath}/{folder}/{id}_{suffix}.png";
    }

    private static void SetObject(SerializedObject target, string propertyName, Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetInt(SerializedObject target, string propertyName, int value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetFloat(SerializedObject target, string propertyName, float value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetMaterialFloat(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetMaterialColor(Material material, string propertyName, Color value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private readonly struct AssetSpec
    {
        public readonly string Folder;
        public readonly string Id;
        public readonly string DisplayName;
        public readonly RetroCursedParkAssetCategory Category;
        public readonly float TargetHeight;
        public readonly float ScaleMin;
        public readonly float ScaleMax;
        public readonly bool Interactable;
        public readonly bool Billboard;
        public readonly bool GroundDecal;
        public readonly float GlowStrength;
        public readonly float FlickerStrength;
        public readonly float SwayStrength;
        public readonly float Weight;

        public AssetSpec(
            string folder,
            string id,
            string displayName,
            RetroCursedParkAssetCategory category,
            float targetHeight,
            float scaleMin,
            float scaleMax,
            bool interactable,
            bool billboard,
            bool groundDecal,
            float glowStrength,
            float flickerStrength,
            float swayStrength,
            float weight)
        {
            Folder = folder;
            Id = id;
            DisplayName = displayName;
            Category = category;
            TargetHeight = targetHeight;
            ScaleMin = scaleMin;
            ScaleMax = scaleMax;
            Interactable = interactable;
            Billboard = billboard;
            GroundDecal = groundDecal;
            GlowStrength = glowStrength;
            FlickerStrength = flickerStrength;
            SwayStrength = swayStrength;
            Weight = weight;
        }
    }
}

[CustomEditor(typeof(RetroCursedParkGenerator))]
public sealed class RetroCursedParkGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Builder", EditorStyles.boldLabel);
        if (GUILayout.Button("Create / Update Cursed Park Assets"))
        {
            RetroCursedParkBuilderWindow.CreateOrUpdateAssets(false);
        }

        if (GUILayout.Button("Rebuild Park Now"))
        {
            foreach (Object targetObject in targets)
            {
                RetroCursedParkGenerator generator = (RetroCursedParkGenerator)targetObject;
                generator.RebuildParkNow();
                EditorUtility.SetDirty(generator);
                if (!Application.isPlaying && generator.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
                }
            }
        }

        if (GUILayout.Button("Clear Generated Children"))
        {
            foreach (Object targetObject in targets)
            {
                RetroCursedParkGenerator generator = (RetroCursedParkGenerator)targetObject;
                generator.ClearGenerated();
                EditorUtility.SetDirty(generator);
            }
        }
    }
}
