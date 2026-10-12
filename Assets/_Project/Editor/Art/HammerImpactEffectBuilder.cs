using System.Collections.Generic;
using System.IO;
using CreativeAI.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// ハンマーの叩きつけ(SlamImpact)で地面に出す衝撃エフェクトの Prefab を生成し、
    /// PlayerArmature の HammerController._impactEffectPrefab に割り当てる。
    ///
    /// 層の構成(時間順):
    /// - 閃光          : 叩いた瞬間の短い発光(Hovl「Ground AOE explosion」の Flash)
    /// - ギザギザの衝撃: カメラ向きのトゲ状バースト・地面に寝かせたトゲ状バースト・放射状のライン(CFXR「Explosion 1」)
    /// - 衝撃波        : 外周が濃く内側へフェードする円盤を時間差で2枚 + 地面を走る空間の歪み(Mirza)
    /// - 塵            : 上へ噴き上がる塵の柱 + 地面を這って外へ広がる塵の輪(Hovl Smoke26。Guard/Parry と同じ素材)
    /// - 岩            : ライティングされる岩片(大)と小石(CFXR debris lit)。地面で跳ねる
    /// - 凹み跡(ルート): すり鉢状の凹み・盛り上がった縁・ひび割れを法線マップで表現し、数秒残ってから消える。
    ///                   最も長く残るので、ルートの StopAction = Destroy でエフェクト全体を片付ける
    ///
    /// 衝撃波と凹み跡のテクスチャは Assets/_Project/Art/VFX/Textures に置いた専用のもの(このエフェクト用に生成)。
    /// 購入アセット(Assets/PurchasedAssets)は git 管理外なので、各自インポートしてから実行する。
    /// 再実行すると同じパスに上書き(GUID は維持)するので、値を調整したら再実行すればよい。
    /// </summary>
    public static class HammerImpactEffectBuilder
    {
        private const string LogPrefix = "[HammerImpactEffectBuilder]";

        private const string OutputPath = "Assets/_Project/Art/VFX/Hit/HammerImpact.prefab";
        private const string PlayerPrefabPath = "Assets/_Project/Features/Player/Prefabs/PlayerArmature.prefab";

        private const string ShockwaveTexturePath = "Assets/_Project/Art/VFX/Textures/HammerShockwave.png";
        private const string CraterAlbedoPath = "Assets/_Project/Art/VFX/Textures/HammerCrater_Albedo.png";
        private const string CraterNormalPath = "Assets/_Project/Art/VFX/Textures/HammerCrater_Normal.png";
        private const string ShockwaveMaterialPath = "Assets/_Project/Art/Materials/Particles/HammerShockwave.mat";
        private const string CraterMaterialPath = "Assets/_Project/Art/Materials/Particles/HammerCrater.mat";

        // 新規マテリアルの元(シェーダー設定済みのものを複製してテクスチャだけ差し替える)
        private const string FadeParticleTemplatePath =
            "Assets/PurchasedAssets/Hovl Studio/Magic effects pack/Materials/Smoke26.mat";
        private const string LitParticleTemplatePath = "Assets/_Project/Art/Materials/Particles/BloodDrop.mat";

        private const string DustMaterialPath = FadeParticleTemplatePath;
        private const string DebrisMaterialPath =
            "Assets/PurchasedAssets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics/cfxr debris 3x3 ab lit.mat";

        private const string HovlGroundAoePath =
            "Assets/PurchasedAssets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Ground AOE explosion.prefab";
        private const string CfxrExplosionPath =
            "Assets/PurchasedAssets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR Explosion 1.prefab";
        private const string MirzaShockwavePath =
            "Assets/PurchasedAssets/Mirza/Distortion Shockwaves VFX/Prefabs/Particle Shockwave 1.prefab";

        // HammerController の衝撃エフェクト欄(SerializeField 名)
        private const string ImpactEffectFieldName = "_impactEffectPrefab";

        // 範囲攻撃判定(PlayerArmature の SlamHitbox)の半径。判定の半径を変えたらここも合わせて再生成する
        private const float SlamRadius = 3f;

        // 地面と同じ高さに描くとちらつく(Zファイティング)ため、地面に寝かせる板はわずかに浮かせる
        private const float GroundOffset = 0.03f;

        // エフェクト全体の色味。土埃と、叩いた瞬間の白っぽい閃き
        private static readonly Color DustColor = new(0.66f, 0.58f, 0.47f);
        private static readonly Color ImpactLightColor = new(1f, 0.92f, 0.78f);
        private static readonly Color RockColorDark = new(0.36f, 0.31f, 0.26f);
        private static readonly Color RockColorLight = new(0.55f, 0.49f, 0.42f);

        [MenuItem("Tools/CreativeAI/VFX/ハンマーの衝撃エフェクトを生成")]
        private static void Build()
        {
            GameObject hovl = LoadAsset<GameObject>(HovlGroundAoePath);
            GameObject cfxr = LoadAsset<GameObject>(CfxrExplosionPath);
            GameObject mirza = LoadAsset<GameObject>(MirzaShockwavePath);
            Material dust = LoadAsset<Material>(DustMaterialPath);
            Material debris = LoadAsset<Material>(DebrisMaterialPath);
            Material shockwave = CreateShockwaveMaterial();
            Material crater = CreateCraterMaterial();
            if (hovl == null || cfxr == null || mirza == null || dust == null || debris == null || shockwave == null || crater == null)
                return;

            var root = new GameObject("HammerImpact");
            try
            {
                BuildCrater(root, crater);

                if (!AddFlash(root, hovl) || !AddImpactSpikes(root, cfxr) || !AddDistortion(root, mirza))
                    return;

                AddShockwaveDisc(root, shockwave, "Shockwave", delay: 0f, lifetime: 0.4f, diameter: SlamRadius * 2.1f, alpha: 0.95f);
                AddShockwaveDisc(root, shockwave, "ShockwaveTrail", delay: 0.07f, lifetime: 0.6f, diameter: SlamRadius * 2.6f, alpha: 0.45f);
                AddDustPlume(root, dust);
                AddDustRing(root, dust);
                AddDebris(root, debris, "Rocks", count: 16, size: (0.22f, 0.5f), speed: (6f, 11f), coneAngle: 30f, lifetime: (2f, 3f));
                AddDebris(root, debris, "Pebbles", count: 40, size: (0.05f, 0.14f), speed: (8f, 15f), coneAngle: 50f, lifetime: (1f, 1.8f));

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, OutputPath);
                Debug.Log($"{LogPrefix} {OutputPath} を生成しました", prefab);
                AssignToHammer(prefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ------------------------------------------------------------------
        // 素材
        // ------------------------------------------------------------------

        private static T LoadAsset<T>(string path)
            where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                Debug.LogError(
                    $"{LogPrefix} {path} が見つかりません。"
                        + "Assets/PurchasedAssets は git 管理外のため、購入アセットをインポートしてから実行してください"
                );
            return asset;
        }

        private static Texture2D ImportTexture(string path, bool isNormalMap)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogError($"{LogPrefix} テクスチャ {path} が見つかりません");
                return null;
            }

            importer.textureType = isNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.alphaIsTransparency = !isNormalMap;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>template を dstPath に複製する(既にあればそれを使い、GUID と参照を保つ)。</summary>
        private static Material CopyMaterial(string templatePath, string dstPath)
        {
            if (!File.Exists(dstPath) && !AssetDatabase.CopyAsset(templatePath, dstPath))
            {
                Debug.LogError($"{LogPrefix} {templatePath} を {dstPath} へ複製できませんでした");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<Material>(dstPath);
        }

        /// <summary>衝撃波の円盤用。フェード(アルファ)合成のパーティクルマテリアルにテクスチャを差し替える。</summary>
        private static Material CreateShockwaveMaterial()
        {
            Texture2D texture = ImportTexture(ShockwaveTexturePath, isNormalMap: false);
            Material material = CopyMaterial(FadeParticleTemplatePath, ShockwaveMaterialPath);
            if (texture == null || material == null)
                return null;

            material.mainTexture = texture;
            // 元マテリアル(煙)は色を2倍に明るくしているが、円盤は白飛びしないよう等倍にし、色はパーティクル側で決める
            material.color = Color.white;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>凹み跡用。ライティングされる透明パーティクル(URP Particles/Lit)に色・法線マップを差し替える。</summary>
        private static Material CreateCraterMaterial()
        {
            Texture2D albedo = ImportTexture(CraterAlbedoPath, isNormalMap: false);
            Texture2D normal = ImportTexture(CraterNormalPath, isNormalMap: true);
            Material material = CopyMaterial(LitParticleTemplatePath, CraterMaterialPath);
            if (albedo == null || normal == null || material == null)
                return null;

            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_BumpMap", normal);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_BumpScale", 1f);
            // 土なので艶を消す(元の血のしずくは艶あり)
            material.SetFloat("_Smoothness", 0.05f);
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------
        // 凹み跡(ルート)
        // ------------------------------------------------------------------

        private const float CraterSize = 4f;
        private const float CraterLifetime = 6f;

        // 寿命のこの割合までは濃いまま残し、そこから消える
        private const float CraterFadeStartRatio = 0.75f;

        private static void BuildCrater(GameObject root, Material material)
        {
            var ps = root.AddComponent<ParticleSystem>();
            ConfigureOneShot(ps, material);
            ConfigureFlatOnGround(ps, GroundOffset);

            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = CraterLifetime;
            main.startSize = CraterSize;
            // 毎回同じ模様に見えないよう、向き(Y軸回り)をばらつかせる
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.maxParticles = 1;
            main.stopAction = ParticleSystemStopAction.Destroy;
            SetBurst(ps, 1);

            // 叩いた瞬間に少し広がって見せる
            SetSizeOverLifetime(ps, (0f, 0.55f), (0.035f, 1f), (1f, 1f));
            SetColorOverLifetime(ps, Color.white, (0f, 1f), (CraterFadeStartRatio, 1f), (1f, 0f));

            // 法線マップでライティングするため、法線・接線を頂点に流す
            ps.GetComponent<ParticleSystemRenderer>().SetActiveVertexStreams(
                new List<ParticleSystemVertexStream>
                {
                    ParticleSystemVertexStream.Position,
                    ParticleSystemVertexStream.Normal,
                    ParticleSystemVertexStream.Tangent,
                    ParticleSystemVertexStream.Color,
                    ParticleSystemVertexStream.UV,
                }
            );
        }

        // ------------------------------------------------------------------
        // 閃光・ギザギザの衝撃・歪み(購入アセットの調整済みパーティクルを複製して色・大きさを合わせる)
        // ------------------------------------------------------------------

        private static bool AddFlash(GameObject root, GameObject hovl)
        {
            ParticleSystem flash = CopySystem(root, hovl, "Flash", "Flash");
            if (flash == null)
                return false;

            ParticleSystem.MainModule main = flash.main;
            main.startColor = ImpactLightColor;
            return true;
        }

        private static bool AddImpactSpikes(GameObject root, GameObject cfxr)
        {
            // カメラを向いたトゲ状の閃き(元素材の本体)
            ParticleSystem spikes = CopySystem(root, cfxr, "CFXR Explosion 1", "ImpactSpikes");
            // 地面に寝かせて範囲いっぱいに走るトゲ状の衝撃
            ParticleSystem groundSpikes = CopySystem(root, cfxr, "Impact small", "GroundSpikes");
            // 放射状に飛ぶ線(勢いの演出)
            ParticleSystem lines = CopySystem(root, cfxr, "Lines", "ImpactLines");
            if (spikes == null || groundSpikes == null || lines == null)
                return false;

            ParticleSystem.MainModule spikesMain = spikes.main;
            spikesMain.startColor = ImpactLightColor;
            spikesMain.startSize = new ParticleSystem.MinMaxCurve(3f, 3.8f);

            ConfigureFlatOnGround(groundSpikes, GroundOffset * 2f);
            ParticleSystem.MainModule groundMain = groundSpikes.main;
            groundMain.startColor = ImpactLightColor;
            groundMain.startSize = new ParticleSystem.MinMaxCurve(SlamRadius * 1.5f, SlamRadius * 1.9f);
            groundMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            ParticleSystem.MainModule linesMain = lines.main;
            linesMain.startColor = ImpactLightColor;
            return true;
        }

        private static bool AddDistortion(GameObject root, GameObject mirza)
        {
            ParticleSystem distortion = CopySystem(root, mirza, "Particle Shockwave 1", "Distortion");
            if (distortion == null)
                return false;

            // 空間の歪みを地面に沿って範囲の外まで走らせる
            ConfigureFlatOnGround(distortion, GroundOffset * 3f);
            ParticleSystem.MainModule main = distortion.main;
            main.startLifetime = 0.5f;
            main.startSize = SlamRadius * 2.6f;
            return true;
        }

        // ------------------------------------------------------------------
        // 衝撃波の円盤(外周が濃く、内側へフェード)
        // ------------------------------------------------------------------

        private static void AddShockwaveDisc(
            GameObject root,
            Material material,
            string name,
            float delay,
            float lifetime,
            float diameter,
            float alpha
        )
        {
            ParticleSystem ps = CreateChildSystem(root, name, material);
            ConfigureFlatOnGround(ps, GroundOffset * 2f);

            ParticleSystem.MainModule main = ps.main;
            main.startDelay = delay;
            main.startLifetime = lifetime;
            main.startSize = diameter;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            SetBurst(ps, 1);

            // 一気に広がって、外へ行くほど減速する
            SetSizeOverLifetime(ps, (0f, 0.12f), (0.3f, 0.78f), (1f, 1f));
            SetColorOverLifetime(ps, ImpactLightColor, (0f, alpha), (0.45f, alpha * 0.65f), (1f, 0f));
        }

        // ------------------------------------------------------------------
        // 塵
        // ------------------------------------------------------------------

        /// <summary>叩いた点から上へ噴き上がり、減速しながら膨らんで消える塵の柱。</summary>
        private static void AddDustPlume(GameObject root, Material material)
        {
            ParticleSystem ps = CreateChildSystem(root, "DustPlume", material);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = DustColor;
            // 舞い上がった塵がゆっくり漂うよう、重力をわずかに逆向きにする
            main.gravityModifier = -0.03f;
            SetBurst(ps, 16);

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.7f;
            // Cone は +Z 方向へ出るので、上(+Y)へ向ける
            shape.rotation = new Vector3(-90f, 0f, 0f);

            SetDrag(ps, 1.5f);
            SetSpin(ps, 0.6f);
            SetSizeOverLifetime(ps, (0f, 0.5f), (0.3f, 1.2f), (1f, 2f));
            SetColorOverLifetime(ps, Color.white, (0f, 0f), (0.08f, 0.75f), (0.5f, 0.45f), (1f, 0f));
        }

        /// <summary>地面を這って外へ広がる塵の輪。</summary>
        private static void AddDustRing(GameObject root, Material material)
        {
            ParticleSystem ps = CreateChildSystem(root, "DustRing", material);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 12f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = DustColor;
            main.gravityModifier = -0.02f;
            SetBurst(ps, 26);

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.9f;
            // 円周上から外向きに出す(内側から出すと中心に塵が溜まる)
            shape.radiusThickness = 0f;
            // Circle は XY 平面なので、地面(XZ 平面)に寝かせる
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.position = new Vector3(0f, 0.25f, 0f);

            // 勢いよく飛び出して、範囲の端あたりで止まる
            SetDrag(ps, 3.5f);
            SetSpin(ps, 0.8f);
            SetSizeOverLifetime(ps, (0f, 0.5f), (0.25f, 1.3f), (1f, 2.6f));
            SetColorOverLifetime(ps, Color.white, (0f, 0f), (0.05f, 0.8f), (1f, 0f));
        }

        // ------------------------------------------------------------------
        // 岩・小石
        // ------------------------------------------------------------------

        private static void AddDebris(
            GameObject root,
            Material material,
            string name,
            int count,
            (float min, float max) size,
            (float min, float max) speed,
            float coneAngle,
            (float min, float max) lifetime
        )
        {
            ParticleSystem ps = CreateChildSystem(root, name, material);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.min, lifetime.max);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.min, speed.max);
            main.startSize = new ParticleSystem.MinMaxCurve(size.min, size.max);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(RockColorDark, RockColorLight);
            main.gravityModifier = 2.2f;
            SetBurst(ps, count);

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = 0.5f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            SetSpin(ps, 8f);
            SetColorOverLifetime(ps, Color.white, (0f, 1f), (0.85f, 1f), (1f, 0f));

            // 3x3 の岩片シートから、1粒ごとにランダムな1コマを選んで固定する
            ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 3;
            sheet.numTilesY = 3;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.cycleCount = 1;

            // 地面に当たって跳ね、転がって止まる
            ParticleSystem.CollisionModule collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.bounce = 0.3f;
            collision.dampen = 0.35f;
            collision.lifetimeLoss = 0f;
            collision.radiusScale = 0.5f;

            // 法線マップ付きの岩片をライティングするため、法線・接線を頂点に流す
            ps.GetComponent<ParticleSystemRenderer>().SetActiveVertexStreams(
                new List<ParticleSystemVertexStream>
                {
                    ParticleSystemVertexStream.Position,
                    ParticleSystemVertexStream.Normal,
                    ParticleSystemVertexStream.Tangent,
                    ParticleSystemVertexStream.Color,
                    ParticleSystemVertexStream.UV,
                }
            );
        }

        // ------------------------------------------------------------------
        // 共通
        // ------------------------------------------------------------------

        private static ParticleSystem CreateChildSystem(GameObject root, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ConfigureOneShot(ps, material);
            return ps;
        }

        /// <summary>1回だけバーストで出す既定の設定にする(新規 ParticleSystem は 5 秒ループ・Cone 放出が既定)。</summary>
        private static void ConfigureOneShot(ParticleSystem ps, Material material)
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.duration = 1f;
            main.startDelay = 0f;
            main.startSpeed = 0f;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        }

        /// <summary>地面に寝かせた板として描く(プレイヤーの向きで生成されても寝たまま)。高さだけ少し浮かせる。</summary>
        private static void ConfigureFlatOnGround(ParticleSystem ps, float height)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startSpeed = 0f;
            main.startRotation3D = false;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.0001f;
            shape.position = new Vector3(0f, height, 0f);

            ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        }

        /// <summary>
        /// 購入アセットの Prefab 内のパーティクル(childName)の設定を、root の子の新しいオブジェクトへ複製する。
        /// 位置・回転は引き継がない(使う側で必要な向きに設定する)。
        /// </summary>
        private static ParticleSystem CopySystem(GameObject root, GameObject sourcePrefab, string childName, string newName)
        {
            Transform source = sourcePrefab.name == childName ? sourcePrefab.transform : FindDeep(sourcePrefab.transform, childName);
            if (source == null || !source.TryGetComponent(out ParticleSystem sourceSystem))
            {
                Debug.LogError($"{LogPrefix} {AssetDatabase.GetAssetPath(sourcePrefab)} にパーティクル {childName} が見つかりません");
                return null;
            }

            var go = new GameObject(newName);
            go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            EditorUtility.CopySerialized(sourceSystem, ps);
            EditorUtility.CopySerialized(source.GetComponent<ParticleSystemRenderer>(), go.GetComponent<ParticleSystemRenderer>());

            // 元素材の他のパーティクルを参照するサブエミッターや、元素材のスクリプト向けの停止通知は持ち込まない
            ParticleSystem.SubEmittersModule subEmitters = ps.subEmitters;
            subEmitters.enabled = false;
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.None;
            return ps;
        }

        private static void SetBurst(ParticleSystem ps, int count)
        {
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        /// <summary>空気抵抗。勢いよく出て、だんだん止まる動きにする。</summary>
        private static void SetDrag(ParticleSystem ps, float drag)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = float.MaxValue;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }

        /// <summary>1粒ごとにランダムな向き・速さで回す(ラジアン/秒)。</summary>
        private static void SetSpin(ParticleSystem ps, float maxAngularSpeed)
        {
            ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-maxAngularSpeed, maxAngularSpeed);
        }

        private static void SetSizeOverLifetime(ParticleSystem ps, params (float time, float value)[] keys)
        {
            var curve = new AnimationCurve();
            foreach (var (time, value) in keys)
                curve.AddKey(time, value);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void SetColorOverLifetime(ParticleSystem ps, Color color, params (float time, float alpha)[] alphaKeys)
        {
            var alphas = new GradientAlphaKey[alphaKeys.Length];
            for (int i = 0; i < alphaKeys.Length; i++)
                alphas[i] = new GradientAlphaKey(alphaKeys[i].alpha, alphaKeys[i].time);

            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) }, alphas);

            ParticleSystem.ColorOverLifetimeModule colorModule = ps.colorOverLifetime;
            colorModule.enabled = true;
            colorModule.color = gradient;
        }

        /// <summary>PlayerArmature の HammerController に生成した Prefab を割り当てる。</summary>
        private static void AssignToHammer(GameObject effectPrefab)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var hammer = contents.GetComponentInChildren<HammerController>(true);
                if (hammer == null)
                {
                    Debug.LogError($"{LogPrefix} {PlayerPrefabPath} に HammerController が見つかりません");
                    return;
                }

                var so = new SerializedObject(hammer);
                SerializedProperty prop = so.FindProperty(ImpactEffectFieldName);
                if (prop.objectReferenceValue == effectPrefab)
                    return;

                prop.objectReferenceValue = effectPrefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                Debug.Log($"{LogPrefix} {PlayerPrefabPath} の HammerController に割り当てました");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static Transform FindDeep(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;
                Transform found = FindDeep(child, childName);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
