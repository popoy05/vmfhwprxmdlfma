using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.MathMode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Tools > Math Mode > 씬에 수학 모드 설치
// 한글 폰트 에셋, 머티리얼, 테마별 하늘, 수학 무기를 만들고 현재 씬에 MathModeManager를 추가한다.
public static class MathModeSetup
{
    const string k_FontPath = "Assets/Fonts/NanumGothic/NanumGothic-Bold.ttf";
    const string k_OldFontAssetPath = "Assets/Fonts/NanumGothic/NanumGothic-Bold SDF.asset";
    const string k_FontAssetPath = "Assets/Fonts/NanumGothic/NanumGothic-Bold SDF Multi.asset";
    const string k_MaterialFolder = "Assets/FPS/Art/Materials/MathMode";
    const string k_RewardPath = "Assets/FPS/Prefabs/Pickups/Loot_Health.prefab";
    const string k_EnemyPath = "Assets/FPS/Prefabs/Enemies/Enemy_HoverBot.prefab";
    const string k_ExplosionPath = "Assets/FPS/Prefabs/VFX/VFX_Explosion.prefab";
    const string k_RootName = "===== MATH MODE =====";

    [MenuItem("Tools/Math Mode/씬에 수학 모드 설치")]
    static void Install()
    {
        TMP_FontAsset font = EnsureFontAsset();
        if (!font) return;

        Material unlit = EnsureMaterial("Math_Unlit.mat", "Universal Render Pipeline/Unlit");
        Material line = EnsureMaterial("Math_Line.mat", "Universal Render Pipeline/Particles/Unlit");
        Material lit = EnsureMaterial("Math_Lit.mat", "Universal Render Pipeline/Lit");

        var root = GameObject.Find(k_RootName);
        if (!root)
        {
            root = new GameObject(k_RootName);
            Undo.RegisterCreatedObjectUndo(root, "Install Math Mode");
        }

        var manager = root.GetComponent<MathModeManager>();
        if (!manager)
            manager = Undo.AddComponent<MathModeManager>(root);
        Undo.RecordObject(manager, "Configure Math Mode");
        manager.Font = font;
        manager.UnlitMaterial = unlit;
        manager.LineMaterial = line;
        manager.LitMaterial = lit;
        manager.RewardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_RewardPath);
        manager.PenaltyEnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_EnemyPath);
        manager.ExplosionVfx = AssetDatabase.LoadAssetAtPath<GameObject>(k_ExplosionPath);
        manager.ThemeSkyboxes = CreateSkyboxes();
        manager.EnemyColor = new Color(1f, 0.32f, 0.38f);

        List<WeaponController> weapons = CreateWeaponsAndGiveToPlayer(line, lit, unlit);
        SaveAssetsResource(manager, weapons);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeObject = root;
        Debug.Log("[MathMode] 설치 완료. 씬을 저장(Ctrl+S)한 뒤 플레이하세요. 설정은 '===== MATH MODE =====' 오브젝트에서 바꿀 수 있습니다.");
    }

    // ------------------------------------------------------------------ 테마 하늘

    static Material[] CreateSkyboxes()
    {
        // 순서: 좌표 도시, 기하 사막, 설원 요새, 네온 우주 (MapTheme 순서와 같음)
        var settings = new[]
        {
            ("Sky_GridCity", new Color(0.5f, 0.6f, 0.8f), new Color(0.37f, 0.35f, 0.34f), 1.0f, 1.3f, 0.04f),
            ("Sky_Desert", new Color(0.95f, 0.6f, 0.4f), new Color(0.6f, 0.45f, 0.3f), 1.7f, 1.2f, 0.07f),
            ("Sky_Snow", new Color(0.75f, 0.8f, 0.9f), new Color(0.8f, 0.82f, 0.85f), 0.7f, 1.1f, 0.03f),
            ("Sky_NeonSpace", new Color(0.4f, 0.2f, 0.65f), new Color(0.08f, 0.06f, 0.14f), 0.4f, 0.75f, 0.02f),
        };

        Shader shader = Shader.Find("Skybox/Procedural");
        var result = new Material[settings.Length];
        for (int i = 0; i < settings.Length; i++)
        {
            var (name, tint, ground, atmosphere, exposure, sunSize) = settings[i];
            string path = $"{k_MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                Directory.CreateDirectory(k_MaterialFolder);
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_SkyTint", tint);
            mat.SetColor("_GroundColor", ground);
            mat.SetFloat("_AtmosphereThickness", atmosphere);
            mat.SetFloat("_Exposure", exposure);
            mat.SetFloat("_SunSize", sunSize);
            EditorUtility.SetDirty(mat);
            result[i] = mat;
        }

        return result;
    }

    // ------------------------------------------------------------------ 수학 무기

    const string k_WeaponFolder = "Assets/FPS/Prefabs/MathWeapons";
    const string k_BlasterPath = "Assets/FPS/Prefabs/Weapons/Weapon_Blaster.prefab";
    const string k_ShotgunPath = "Assets/FPS/Prefabs/Weapons/Weapon_Shotgun.prefab";
    const string k_LauncherPath = "Assets/FPS/Prefabs/Weapons/Weapon_Launcher.prefab";

    struct WeaponDef
    {
        public string File, Name, BaseWeapon;
        public WeaponShootType? ShootType;
        public float Delay;
        public int Pellets;
        public int MaxAmmo;
        public float ReloadRate;
        public float Speed, Damage, Gravity;
        public MathTrajectory.Function Function;
        public float A, K, Extra;
        public Color Trail;
        public float TrailTime;
    }

    static readonly WeaponDef[] k_Weapons =
    {
        new WeaponDef { File = "Sine", Name = "사인 블래스터", BaseWeapon = k_BlasterPath, ShootType = WeaponShootType.Automatic,
            Delay = 0.12f, Pellets = 1, MaxAmmo = 16, ReloadRate = 12f, Speed = 70f, Damage = 16f,
            Function = MathTrajectory.Function.Sine, A = 0.3f, K = 0.7f, Trail = new Color(1f, 0.9f, 0.3f), TrailTime = 0.35f },
        new WeaponDef { File = "Zigzag", Name = "지그재그 SMG", BaseWeapon = k_BlasterPath, ShootType = WeaponShootType.Automatic,
            Delay = 0.06f, Pellets = 1, MaxAmmo = 30, ReloadRate = 20f, Speed = 90f, Damage = 7f,
            Function = MathTrajectory.Function.Zigzag, A = 0.25f, K = 1.2f, Trail = new Color(0.4f, 1f, 0.4f), TrailTime = 0.25f },
        new WeaponDef { File = "Helix", Name = "삼중 나선총", BaseWeapon = k_BlasterPath, ShootType = WeaponShootType.Automatic,
            Delay = 0.22f, Pellets = 3, MaxAmmo = 12, ReloadRate = 8f, Speed = 60f, Damage = 11f,
            Function = MathTrajectory.Function.Helix, A = 0.25f, K = 1.0f, Trail = new Color(0.3f, 0.95f, 1f), TrailTime = 0.4f },
        new WeaponDef { File = "Cone", Name = "원뿔 샷건", BaseWeapon = k_ShotgunPath, ShootType = WeaponShootType.Manual,
            Delay = 0.6f, Pellets = 7, MaxAmmo = 4, ReloadRate = 1.5f, Speed = 90f, Damage = 14f,
            Function = MathTrajectory.Function.Cone, A = 6f, Trail = new Color(1f, 0.6f, 0.2f), TrailTime = 0.2f },
        new WeaponDef { File = "Damped", Name = "감쇠 진동 저격총", BaseWeapon = k_BlasterPath, ShootType = WeaponShootType.Manual,
            Delay = 0.7f, Pellets = 1, MaxAmmo = 5, ReloadRate = 1.2f, Speed = 120f, Damage = 70f,
            Function = MathTrajectory.Function.Damped, A = 0.5f, K = 0.8f, Extra = 0.2f, Trail = new Color(0.75f, 0.45f, 1f), TrailTime = 0.4f },
        new WeaponDef { File = "Rose", Name = "장미 곡선 캐논", BaseWeapon = k_BlasterPath, ShootType = WeaponShootType.Manual,
            Delay = 0.45f, Pellets = 1, MaxAmmo = 8, ReloadRate = 2f, Speed = 45f, Damage = 45f,
            Function = MathTrajectory.Function.Rose, A = 0.45f, K = 1.0f, Extra = 3f, Trail = new Color(1f, 0.45f, 0.75f), TrailTime = 0.6f },
        new WeaponDef { File = "Exponential", Name = "지수 레일건", BaseWeapon = k_BlasterPath, ShootType = WeaponShootType.Manual,
            Delay = 0.9f, Pellets = 1, MaxAmmo = 3, ReloadRate = 0.8f, Speed = 25f, Damage = 90f,
            Function = MathTrajectory.Function.Exponential, K = 1.5f, Trail = new Color(0.7f, 0.85f, 1f), TrailTime = 0.3f },
        new WeaponDef { File = "Parabola", Name = "포물선 런처", BaseWeapon = k_LauncherPath, ShootType = null,
            Pellets = 1, Function = MathTrajectory.Function.Straight, Trail = new Color(1f, 0.3f, 0.25f), TrailTime = 0.8f },
    };

    [MenuItem("Tools/Math Mode/수학 무기만 다시 만들기")]
    static void RecreateWeapons()
    {
        List<WeaponController> weapons = CreateWeaponsAndGiveToPlayer(
            EnsureMaterial("Math_Line.mat", "Universal Render Pipeline/Particles/Unlit"),
            EnsureMaterial("Math_Lit.mat", "Universal Render Pipeline/Lit"),
            EnsureMaterial("Math_Unlit.mat", "Universal Render Pipeline/Unlit"));
        var assets = AssetDatabase.LoadAssetAtPath<MathModeAssets>(k_AssetsResourcePath);
        if (assets)
        {
            assets.Weapons = weapons;
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    // 실행 파일에서도 메뉴·결과 화면·게임이 에셋을 찾을 수 있도록 Resources 에 저장
    const string k_AssetsResourcePath = "Assets/FPS/Resources/MathModeAssets.asset";

    static void SaveAssetsResource(MathModeManager manager, List<WeaponController> weapons)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(k_AssetsResourcePath));
        var assets = AssetDatabase.LoadAssetAtPath<MathModeAssets>(k_AssetsResourcePath);
        if (!assets)
        {
            assets = ScriptableObject.CreateInstance<MathModeAssets>();
            AssetDatabase.CreateAsset(assets, k_AssetsResourcePath);
        }

        assets.Font = manager.Font;
        assets.UnlitMaterial = manager.UnlitMaterial;
        assets.LineMaterial = manager.LineMaterial;
        assets.LitMaterial = manager.LitMaterial;
        assets.ThemeSkyboxes = manager.ThemeSkyboxes;
        assets.RewardPrefab = manager.RewardPrefab;
        assets.PenaltyEnemyPrefab = manager.PenaltyEnemyPrefab;
        assets.ExplosionVfx = manager.ExplosionVfx;
        assets.Weapons = weapons;
        EditorUtility.SetDirty(assets);
        AssetDatabase.SaveAssets();
    }

    static List<WeaponController> CreateWeaponsAndGiveToPlayer(Material trailMaterial, Material lit, Material unlit)
    {
        Directory.CreateDirectory(k_WeaponFolder);
        Directory.CreateDirectory($"{k_WeaponFolder}/Materials");

        // 예전 버전의 부채꼴 샷건은 원뿔 샷건으로 교체됨
        AssetDatabase.DeleteAsset($"{k_WeaponFolder}/Weapon_Math_Fan.prefab");
        AssetDatabase.DeleteAsset($"{k_WeaponFolder}/Projectile_Math_Fan.prefab");

        var created = new List<WeaponController>();
        foreach (WeaponDef def in k_Weapons)
        {
            var baseWeapon = AssetDatabase.LoadAssetAtPath<GameObject>(def.BaseWeapon);
            var baseController = baseWeapon ? baseWeapon.GetComponent<WeaponController>() : null;
            if (!baseController || !baseController.ProjectilePrefab)
            {
                Debug.LogError($"[MathMode] 베이스 무기를 찾을 수 없습니다: {def.BaseWeapon}");
                continue;
            }

            // 1) 투사체 Variant: 수식 궤적 + 궤적이 보이는 트레일
            var projInstance = (GameObject)PrefabUtility.InstantiatePrefab(baseController.ProjectilePrefab.gameObject);
            var standard = projInstance.GetComponent<Unity.FPS.Gameplay.ProjectileStandard>();
            if (standard && def.Speed > 0f)
            {
                standard.Speed = def.Speed;
                standard.Damage = def.Damage;
                standard.GravityDownAcceleration = def.Gravity;
            }

            var trajectory = projInstance.GetComponent<MathTrajectory>();
            if (!trajectory) trajectory = projInstance.AddComponent<MathTrajectory>();
            trajectory.Type = def.Function;
            trajectory.Amplitude = def.A;
            trajectory.Frequency = def.K;
            trajectory.Extra = def.Extra;
            trajectory.PelletsPerShot = def.Pellets;

            var trail = projInstance.GetComponent<TrailRenderer>();
            if (!trail) trail = projInstance.AddComponent<TrailRenderer>();
            trail.time = def.TrailTime;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.08f, 1f, 0f);
            trail.sharedMaterial = trailMaterial;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(def.Trail, 0f), new GradientColorKey(def.Trail, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;

            GameObject projAsset = PrefabUtility.SaveAsPrefabAsset(projInstance, $"{k_WeaponFolder}/Projectile_Math_{def.File}.prefab");
            Object.DestroyImmediate(projInstance);

            // 2) 무기 Variant (+ 무기마다 다른 색과 부착물)
            var weaponInstance = (GameObject)PrefabUtility.InstantiatePrefab(baseWeapon);
            var controller = weaponInstance.GetComponent<WeaponController>();
            controller.WeaponName = def.Name;
            controller.ProjectilePrefab = projAsset.GetComponent<ProjectileBase>();
            if (def.ShootType.HasValue)
            {
                controller.ShootType = def.ShootType.Value;
                controller.DelayBetweenShots = def.Delay;
                controller.BulletsPerShot = def.Pellets;
                controller.BulletSpreadAngle = 0f; // 퍼짐은 수식이 담당
                controller.MaxAmmo = def.MaxAmmo;
                controller.AmmoReloadRate = def.ReloadRate;
            }

            WeaponLooks.Decorate(controller, def.File, def.Trail, lit, unlit, $"{k_WeaponFolder}/Materials");

            GameObject weaponAsset = PrefabUtility.SaveAsPrefabAsset(weaponInstance, $"{k_WeaponFolder}/Weapon_Math_{def.File}.prefab");
            Object.DestroyImmediate(weaponInstance);
            created.Add(weaponAsset.GetComponent<WeaponController>());
        }

        // 3) 전체 무기 목록: 기본 Blaster + 수학 무기 (최대 9칸)
        var weapons = new List<WeaponController>();
        var blaster = AssetDatabase.LoadAssetAtPath<GameObject>(k_BlasterPath);
        if (blaster) weapons.Add(blaster.GetComponent<WeaponController>());
        foreach (WeaponController w in created)
            if (weapons.Count < 9) weapons.Add(w);

        // 씬의 플레이어 시작 무기에도 넣는다 (게임 시작 시 MathModeManager가 한 번 더 확인해 빠진 무기를 채운다)
        var player = Object.FindAnyObjectByType<Unity.FPS.Gameplay.PlayerWeaponsManager>();
        if (player)
        {
            Undo.RecordObject(player, "Give Math Weapons");
            player.StartingWeapons = new List<WeaponController>(weapons);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player);
        }

        Debug.Log($"[MathMode] 수학 무기 {created.Count}종 생성, 무기 {weapons.Count}칸 (숫자키 1~{weapons.Count}, Q/E).");
        return weapons;
    }

    // ------------------------------------------------------------------ 한글 폰트

    static TMP_FontAsset EnsureFontAsset()
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_FontAssetPath);
        if (!fontAsset)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(k_FontPath);
            if (!font)
            {
                Debug.LogError($"[MathMode] 한글 폰트가 없습니다: {k_FontPath}");
                return null;
            }

            // 한글은 글자 종류가 많다: 샘플 크기를 줄이고(64pt) 아틀라스를 여러 장 쓰도록(multi-atlas) 만들어
            // 아틀라스가 가득 차서 글자가 네모(□)로 나오는 문제를 막는다
            fontAsset = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            fontAsset.name = Path.GetFileNameWithoutExtension(k_FontAssetPath);
            AssetDatabase.CreateAsset(fontAsset, k_FontAssetPath);
            fontAsset.atlasTextures[0].name = fontAsset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            AssetDatabase.SaveAssets();
        }

        fontAsset.isMultiAtlasTexturesEnabled = true;

        // 기존 HUD(Roboto 폰트)에서도 한글이 보이도록 전역 대체 폰트로 등록 (예전 단일 아틀라스 폰트는 제거)
        TMP_Settings settings = TMP_Settings.instance;
        if (settings)
        {
            var oldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_OldFontAssetPath);
            TMP_Settings.fallbackFontAssets.RemoveAll(f => !f || f == oldFont);
            if (!TMP_Settings.fallbackFontAssets.Contains(fontAsset))
                TMP_Settings.fallbackFontAssets.Add(fontAsset);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        return fontAsset;
    }

    static Material EnsureMaterial(string fileName, string shaderName)
    {
        string path = $"{k_MaterialFolder}/{fileName}";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material) return material;

        Directory.CreateDirectory(k_MaterialFolder);
        Shader shader = Shader.Find(shaderName) ?? Shader.Find("Universal Render Pipeline/Unlit");
        material = new Material(shader);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
