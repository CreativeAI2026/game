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
    /// - 帯(ThrowTrail): 刃先から出る水の帯。Trails モジュールの Ribbon で粒同士をつなぐ。粒そのものは描かない。
    ///                    素材は NamuFX の水トレイル(M_WaterTrail。流れるノイズで水の筋に見える)
    /// - 水滴(Droplets): 刃先からまばらに振り飛ばされる水滴(NamuFX Water_Impact の splash_long を複製)
    ///
    /// 帯の発生源は投擲体のルート(ScytheBoomerang._spinRoot。回転する)の子として刃先に置くので、
    /// World 空間に残る帯が回転の渦を描きながら経路に沿って伸びる。
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
            "Assets/PurchasedAssets/NamuFX/StylizedWaterEffects/Materials/M_WaterTrail.mat";
        private const string WaterImpactPath =
            "Assets/PurchasedAssets/NamuFX/StylizedWaterEffects/Prefabs/Water_Impact.prefab";
        private const string DropletSourceName = "splash_long";

        // ScytheBoomerang の軌跡欄(SerializeField 名)
        private const string TrailFieldName = "_trail";

        // 刃先の位置(投擲体ルートのローカル)。モデルの刃(NURBSパス)の外端付近。モデルを差し替えたら合わせる
        private static readonly Vector3 BladeTipLocalPosition = new(0.7f, 0f, -0.3f);

        // 帯と水滴の色。白寄りの水色にして、水であることが分かる程度に留める。
        // 帯の素材(M_WaterTrail)はアルファが下がるほどノイズで削れて消える作りなので、帯は不透明から始める
        private static readonly Color RibbonColor = new(0.7f, 0.9f, 1f, 1f);
        private static readonly Color DropletColor = new(0.8f, 0.93f, 1f, 0.9f);

        [MenuItem("Tools/CreativeAI/VFX/鎌の投擲軌跡を生成")]
        private static void Build()
        {
            var ribbonMaterial = LoadAsset<Material>(RibbonMaterialPath);
            var waterImpact = LoadAsset<GameObject>(WaterImpactPath);
            if (ribbonMaterial == null || waterImpact == null)
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
                if (!CreateDroplets(ribbon.transform, waterImpact))
                    return;

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

        /// <summary>刃先から出る水の帯。粒は描かず、Ribbon でつないだ帯だけを描く。</summary>
        private static ParticleSystem CreateRibbon(Transform parent, Material material)
        {
            ParticleSystem ps = CreateSystem(parent, TrailObjectName);
            ps.transform.localPosition = BladeTipLocalPosition;

            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = 0.3f;
            main.startSize = 1f;
            main.startColor = RibbonColor;

            ParticleSystem.EmissionModule emission = ps.emission;
            // 刃先は回転で高速に動くため、移動距離ではなく時間で細かく出す(距離基準だと渦の曲線がカクつく)
            emission.rateOverTime = 120f;

            ParticleSystem.TrailModule trails = ps.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.Ribbon;
            trails.ribbonCount = 1;
            trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
            trails.sizeAffectsWidth = false;
            trails.inheritParticleColor = true;
            trails.dieWithParticles = true;
            // 帯の太さ(m)。先端(刃先側)が太く、尾に向かって細くなる
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(
                0.4f,
                AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f)
            );

            // 前半は形を保ち、後半で水の筋が削れながら消える
            SetAlphaOverLifetime(ps, (0f, 1f), (0.4f, 0.9f), (1f, 0f));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.None;
            renderer.trailMaterial = material;
            return ps;
        }

        /// <summary>
        /// 刃先からまばらに振り飛ばされる水滴。素材側のカスタムデータ設定ごと NamuFX の splash_long を複製し、
        /// 一度に噴き出す設定から「飛行中に少しずつ出す」設定に変える。多すぎると派手になるので数は控えめ。
        /// </summary>
        private static bool CreateDroplets(Transform parent, GameObject waterImpact)
        {
            Transform source = FindDeep(waterImpact.transform, DropletSourceName);
            if (source == null || !source.TryGetComponent(out ParticleSystem sourceSystem))
            {
                Debug.LogError(
                    $"{LogPrefix} {WaterImpactPath} にパーティクル {DropletSourceName} が見つかりません"
                );
                return false;
            }

            ParticleSystem ps = CreateSystem(parent, "Droplets");
            EditorUtility.CopySerialized(sourceSystem, ps);
            EditorUtility.CopySerialized(
                source.GetComponent<ParticleSystemRenderer>(),
                ps.GetComponent<ParticleSystemRenderer>()
            );

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.playOnAwake = false;
            main.startDelay = 0f;
            main.stopAction = ParticleSystemStopAction.None;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            // 元素材は XYZ 別指定(縦長)なので、一様な大きさに戻してから指定する
            main.startSize3D = false;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.startColor = DropletColor;
            main.gravityModifier = 1.5f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            emission.rateOverTime = 14f;
            emission.rateOverDistance = 0f;

            // 刃先から全方向へ少しだけ散らす(元素材は上向きのコーン)
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;

            // 元素材の噴き上げる動き(速度・力の時間変化)は持ち込まず、刃先から散って重力で落ちるだけにする
            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = false;
            ParticleSystem.ForceOverLifetimeModule force = ps.forceOverLifetime;
            force.enabled = false;

            // 元素材は勢いよく噴き上がる前提で速度による伸びが大きく、ここでは長い棒に見えるので短い粒に抑える
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.velocityScale = 0.03f;
            renderer.lengthScale = 1.2f;

            // 元素材の他のパーティクルを参照するサブエミッターは持ち込まない
            ParticleSystem.SubEmittersModule subEmitters = ps.subEmitters;
            subEmitters.enabled = false;
            return true;
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
