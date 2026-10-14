using CreativeAI.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 投げた鎌(見た目は傘)の飛行中の軌跡パーティクルを SytheThrow.prefab の子に生成し、
    /// ScytheBoomerang._trail に割り当てる。
    ///
    /// 層の構成(水っぽさは控えめ。主役はあくまで帯):
    /// - 帯(ルート): 飛んだ経路に残る薄い水色の帯。Trails モジュールの Ribbon で粒同士をつなぐ。粒そのものは描かない
    /// - 水滴      : 経路からまばらに散って落ちる細い水滴(CFXR stretch rain)
    ///
    /// 投擲体のルートは見た目の回転(ScytheBoomerang._spinRoot)ごと回るが、軌跡は回転の中心から出して
    /// World 空間でシミュレーションするので、回転の影響を受けずに経路に沿って残る。
    /// 購入アセット(Assets/PurchasedAssets)は git 管理外なので、各自インポートしてから実行する。
    /// 再実行すると既存の軌跡オブジェクトを作り直すので、値を調整したら再実行すればよい。
    /// </summary>
    public static class ScytheThrowTrailBuilder
    {
        private const string LogPrefix = "[ScytheThrowTrailBuilder]";

        private const string BoomerangPrefabPath =
            "Assets/_Project/Features/Player/Prefabs/SytheThrow.prefab";
        private const string TrailObjectName = "ThrowTrail";

        private const string RibbonMaterialPath =
            "Assets/PurchasedAssets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics/cfxr sword trail plain.mat";
        private const string DropletMaterialPath =
            "Assets/PurchasedAssets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics/cfxr stretch rain blur ab.mat";

        // ScytheBoomerang の軌跡欄(SerializeField 名)
        private const string TrailFieldName = "_trail";

        // 帯と水滴の色。白寄りの水色にして、水であることが分かる程度に留める
        private static readonly Color RibbonColor = new(0.78f, 0.92f, 1f, 0.45f);
        private static readonly Color DropletColor = new(0.7f, 0.88f, 1f, 0.8f);

        [MenuItem("Tools/CreativeAI/VFX/鎌の投擲軌跡を生成")]
        private static void Build()
        {
            var ribbonMaterial = LoadAsset<Material>(RibbonMaterialPath);
            var dropletMaterial = LoadAsset<Material>(DropletMaterialPath);
            if (ribbonMaterial == null || dropletMaterial == null)
                return;

            GameObject contents = PrefabUtility.LoadPrefabContents(BoomerangPrefabPath);
            try
            {
                var boomerang = contents.GetComponent<ScytheBoomerang>();
                if (boomerang == null)
                {
                    Debug.LogError(
                        $"{LogPrefix} {BoomerangPrefabPath} のルートに ScytheBoomerang が見つかりません"
                    );
                    return;
                }

                Transform old = contents.transform.Find(TrailObjectName);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);

                ParticleSystem ribbon = CreateRibbon(contents.transform, ribbonMaterial);
                CreateDroplets(ribbon.transform, dropletMaterial);

                var so = new SerializedObject(boomerang);
                so.FindProperty(TrailFieldName).objectReferenceValue = ribbon;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, BoomerangPrefabPath);
                Debug.Log($"{LogPrefix} {BoomerangPrefabPath} に軌跡を生成して割り当てました");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

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

        /// <summary>経路に残る帯。粒は描かず、Ribbon でつないだ帯だけを描く。</summary>
        private static ParticleSystem CreateRibbon(Transform parent, Material material)
        {
            ParticleSystem ps = CreateSystem(parent, TrailObjectName);

            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = 0.25f;
            main.startSize = 1f;
            main.startColor = RibbonColor;

            ParticleSystem.EmissionModule emission = ps.emission;
            // 1m あたりの粒の数。少ないと高速時に帯がカクつく
            emission.rateOverDistance = 12f;

            ParticleSystem.TrailModule trails = ps.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.Ribbon;
            trails.ribbonCount = 1;
            trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
            trails.sizeAffectsWidth = false;
            trails.inheritParticleColor = true;
            trails.dieWithParticles = true;
            // 帯の太さ(m)。傘の回転半径(約0.7m)より細くして、回転の中心を通る1本の筋に見せる
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(
                0.35f,
                AnimationCurve.Linear(0f, 1f, 1f, 0.2f)
            );

            SetAlphaOverLifetime(ps, (0f, 1f), (1f, 0f));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.None;
            renderer.trailMaterial = material;
            return ps;
        }

        /// <summary>経路からまばらに散って落ちる細い水滴。多すぎると派手になるので数は控えめ。</summary>
        private static void CreateDroplets(Transform parent, Material material)
        {
            ParticleSystem ps = CreateSystem(parent, "Droplets");

            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.startColor = DropletColor;
            main.gravityModifier = 1.2f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverDistance = 3f;

            // 傘の縁から飛び散るように、回転半径あたりの球面から出す
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            shape.radiusThickness = 0f;

            SetAlphaOverLifetime(ps, (0f, 1f), (0.6f, 0.8f), (1f, 0f));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.08f;
            renderer.lengthScale = 1.5f;
            renderer.sharedMaterial = material;
        }

        /// <summary>
        /// 投擲中ずっと流し続ける既定の設定にする(再生・停止は ScytheBoomerang が行う)。
        /// 粒は World 空間に残すので、投擲体の移動・回転には引きずられない。
        /// </summary>
        private static ParticleSystem CreateSystem(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            // AddComponent 直後は再生中扱いなので、設定を変える前に止める(duration 等は再生中に変更できない)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;
            return ps;
        }

        /// <summary>寿命に沿った透明度の変化。色は startColor で決めるので、ここではアルファだけを掛ける。</summary>
        private static void SetAlphaOverLifetime(
            ParticleSystem ps,
            params (float time, float alpha)[] alphaKeys
        )
        {
            var alphas = new GradientAlphaKey[alphaKeys.Length];
            for (int i = 0; i < alphaKeys.Length; i++)
                alphas[i] = new GradientAlphaKey(alphaKeys[i].alpha, alphaKeys[i].time);

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphas
            );

            ParticleSystem.ColorOverLifetimeModule colorModule = ps.colorOverLifetime;
            colorModule.enabled = true;
            colorModule.color = gradient;
        }
    }
}
