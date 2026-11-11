using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// クラゲボスの突きに出す光のエフェクトの Prefab とマテリアルを生成し、
    /// KurageBoss の <see cref="KurageStabEffects"/> に割り当てる。
    ///
    /// 天使のわっかに合わせた「神々しい光」。色はわっかの淡いローズと、白に近い金。
    /// 既定の Volume はブルーム無しなので、淡い加算の光を重ねて作る(強く光らせて主張させない)。
    ///
    /// 生成する Prefab(時間順):
    /// - KurageStabGather : 溜めの間、触手の先端へ光の粒と細い筋が吸い込まれ続ける(ループ)
    /// - KurageStabGlint  : 突き出す直前の閃き。輪が一点へ絞られ → 針のような光が狙いへ走り、
    ///                      長さの違う細い光が斜めに交差し、小さな輪が弾ける
    /// - KurageStabThrust : 軌道に垂直な輪の連なり・軌道を走る筋・芯の線・軌道に残る粒、
    ///                      触手の中ほどから先端を包む槍の穂先(細い円錐。外側の殻と、細く明るい芯の2重)、
    ///                      届いた先で広がる輪と散るオーブ
    ///
    /// 形は CreativeAI/KurageHolyGlow(粒・輪・筋)と CreativeAI/KurageHolyLance(槍の穂先)のシェーダーが計算する
    /// (画像・購入アセットは使わない)。
    /// 再実行すると同じパスに上書き(GUID は維持)するので、値を調整したら再実行すればよい。
    /// Prefab を直接編集した調整は再実行で消えるため、残したい調整はここの値へ反映する。
    /// </summary>
    public static class KurageStabEffectBuilder
    {
        private const string LogPrefix = "[KurageStabEffectBuilder]";

        private const string ShaderName = "CreativeAI/KurageHolyGlow";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Particles";
        private const string PrefabParentFolder = "Assets/_Project/Art/VFX";
        private const string PrefabFolderName = "KurageBoss";
        private const string PrefabFolder = PrefabParentFolder + "/" + PrefabFolderName;
        private const string GatherPath = PrefabFolder + "/KurageStabGather.prefab";
        private const string GlintPath = PrefabFolder + "/KurageStabGlint.prefab";
        private const string ThrustPath = PrefabFolder + "/KurageStabThrust.prefab";
        private const string LanceShaderName = "CreativeAI/KurageHolyLance";
        private const string LanceMeshPath = PrefabFolder + "/KurageStabLanceCone.asset";
        private const string BossPrefabPath =
            "Assets/_Project/Features/Enemy/Prefabs/KurageBoss.prefab";

        // 色。アルファは光の濃さ(シェーダーが明るさに掛ける)
        private static readonly Color Rose = new(1f, 0.78f, 0.82f);
        private static readonly Color Gold = new(1f, 0.92f, 0.74f);
        private static readonly Color White = new(1f, 0.97f, 0.94f);

        // 閃きの中で「輪が絞られ切って光が走る」瞬間(秒)。絞られる輪はここまでに消え、他はここから出る
        private const float GlintFlashTime = 0.14f;

        private sealed class Materials
        {
            public Material Orb;
            public Material Ring;
            public Material Streak;
            public Material Beam;
            public Material LanceOuter;
            public Material LanceInner;
        }

        [MenuItem("Tools/CreativeAI/VFX/クラゲの突きエフェクトを生成")]
        private static void Build()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"{LogPrefix} シェーダー {ShaderName} が見つかりません");
                return;
            }
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder(PrefabParentFolder, PrefabFolderName);

            var materials = new Materials
            {
                Orb = CreateMaterial(shader, "KurageHoly_Orb", "_SHAPE_ORB", 0f),
                Ring = CreateMaterial(shader, "KurageHoly_Ring", "_SHAPE_RING", 1f),
                Streak = CreateMaterial(shader, "KurageHoly_Streak", "_SHAPE_STREAK", 2f),
                Beam = CreateMaterial(shader, "KurageHoly_Beam", "_SHAPE_STREAK", 2f),
            };
            // 粒は板の大きさいっぱいに近い柔らかい光にする(芯を絞りすぎると遠くのボスの手元で点にしか見えない)
            materials.Orb.SetFloat("_OrbCoreSharpness", 9f);
            materials.Orb.SetFloat("_OrbHaloAmount", 0.35f);
            SaveMaterial(materials.Orb);
            // 輪は広がるほど細く・絞られるほど太く見せたいので、太さをパーティクル側の曲線で動かす
            materials.Ring.SetFloat("_RingThicknessFromParticle", 1f);
            // 芯の線は根元から先端までほぼ同じ濃さで通す(筋は両端へ向けて細く消える)
            materials.Beam.SetFloat("_StreakTaper", 0.35f);
            materials.Beam.SetFloat("_StreakSharpness", 120f);
            SaveMaterial(materials.Ring);
            SaveMaterial(materials.Beam);
            if (!CreateLanceMaterials(materials))
                return;

            GameObject gather = SavePrefab(BuildGather(materials), GatherPath);
            GameObject glint = SavePrefab(BuildGlint(materials), GlintPath);
            GameObject thrust = SavePrefab(BuildThrust(materials), ThrustPath);
            AssignToBoss(gather, glint, thrust);
        }

        // ------------------------------------------------------------------
        // 溜め: 触手の先端へ光が集まる
        // ------------------------------------------------------------------

        // 光の粒が吸い込まれ始める距離（m）
        private const float GatherRadius = 6f;

        private static GameObject BuildGather(Materials materials)
        {
            // ルート: 先端にぼんやり灯る光。重なって少し揺らぐ
            ParticleSystem core = CreateSystem(null, "KurageStabGather", materials.Orb);
            SetLooping(core);
            ParticleSystem.MainModule coreMain = core.main;
            coreMain.startLifetime = 0.5f;
            coreMain.startSize = new ParticleSystem.MinMaxCurve(3f, 4.5f);
            coreMain.startColor = WithAlpha(Gold, 0.45f);
            SetRate(core, 6f);
            SetSizeOverLifetime(core, (0f, 0.4f), (1f, 1f));
            SetAlphaOverLifetime(core, (0f, 0f), (0.4f, 1f), (1f, 0f));

            // 周りから先端へ吸い込まれる光の粒。速さを負にすると、球の表面から中心へ向かう
            ParticleSystem orbs = CreateSystem(core.gameObject, "GatherOrbs", materials.Orb);
            SetLooping(orbs);
            ParticleSystem.MainModule orbsMain = orbs.main;
            const float orbLifetime = 0.6f;
            orbsMain.startLifetime = orbLifetime;
            orbsMain.startSpeed = -GatherRadius / orbLifetime;
            orbsMain.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            orbsMain.startColor = new ParticleSystem.MinMaxGradient(
                WithAlpha(Rose, 0.8f),
                WithAlpha(Gold, 0.8f)
            );
            SetRate(orbs, 36f);
            SetSphereShell(orbs, GatherRadius, thickness: 0.3f);
            SetSizeOverLifetime(orbs, (0f, 1f), (1f, 0.3f));
            SetAlphaOverLifetime(orbs, (0f, 0f), (0.25f, 1f), (0.85f, 1f), (1f, 0f));

            // 粒より速く先端へ走る細い筋。集まる勢いを足す
            ParticleSystem streaks = CreateSystem(
                core.gameObject,
                "GatherStreaks",
                materials.Streak
            );
            SetLooping(streaks);
            ParticleSystem.MainModule streaksMain = streaks.main;
            const float streakLifetime = 0.3f;
            streaksMain.startLifetime = streakLifetime;
            streaksMain.startSpeed = -GatherRadius * 1.1f / streakLifetime;
            streaksMain.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            streaksMain.startColor = WithAlpha(White, 0.6f);
            SetRate(streaks, 20f);
            SetSphereShell(streaks, GatherRadius * 1.1f, thickness: 0.2f);
            SetStretched(streaks, lengthScale: 14f);
            SetAlphaOverLifetime(streaks, (0f, 0f), (0.3f, 1f), (1f, 0f));

            return core.gameObject;
        }

        // ------------------------------------------------------------------
        // 閃き: 突き出す直前
        // ------------------------------------------------------------------

        private static GameObject BuildGlint(Materials materials)
        {
            // ルート: 光の芯。一瞬膨らんでから小さく残る
            ParticleSystem core = CreateSystem(null, "KurageStabGlint", materials.Orb);
            ParticleSystem.MainModule coreMain = core.main;
            // KurageStabEffects がこの長さで片付けるので、一番長く残る光(弾ける輪・こぼれる粒)より長くする
            coreMain.duration = 1.2f;
            coreMain.startLifetime = 0.28f;
            coreMain.startSize = 3.5f;
            coreMain.startColor = WithAlpha(White, 0.9f);
            SetBurst(core, 1, GlintFlashTime);
            SetSizeOverLifetime(core, (0f, 0.2f), (0.15f, 1f), (1f, 0.25f));
            SetAlphaOverLifetime(core, (0f, 1f), (0.3f, 0.8f), (1f, 0f));

            // 狙いへ向いた輪が、先端の一点へ絞られる(光が走る前の「溜め」)
            ParticleSystem converge = CreateSystem(core.gameObject, "ConvergeRing", materials.Ring);
            ParticleSystem.MainModule convergeMain = converge.main;
            convergeMain.startLifetime = GlintFlashTime + 0.02f;
            convergeMain.startSize = 10f;
            convergeMain.startColor = WithAlpha(Rose, 0.9f);
            SetBurst(converge, 1);
            SetFacingSystemForward(converge);
            // 終わり際ほど速く絞る
            SetSizeOverLifetime(converge, (0f, 1f), (0.6f, 0.7f), (1f, 0.04f));
            SetAlphaOverLifetime(converge, (0f, 0f), (0.5f, 0.7f), (1f, 1f));
            // 太さは板に対する割合なので、絞られて板が小さくなるぶん割合を上げて線が消えないようにする
            SetRingThickness(converge, (0f, 0.6f), (1f, 3f));

            // 狙いの方向へ走る針のような光。この後に触手が通る線を一瞬だけ示す
            ParticleSystem needle = CreateSystem(core.gameObject, "NeedleStreak", materials.Streak);
            ParticleSystem.MainModule needleMain = needle.main;
            needleMain.startLifetime = 0.3f;
            // 向きを決めるためだけの速さ(伸びる向き=速度の向き)。見た目には動かない
            needleMain.startSpeed = 0.01f;
            needleMain.startSize = 0.5f;
            needleMain.startColor = WithAlpha(White, 1f);
            SetBurst(needle, 1, GlintFlashTime);
            SetForwardPoint(needle);
            SetStretched(needle, lengthScale: 40f);
            SetSizeOverLifetime(needle, (0f, 0.15f), (0.2f, 1f), (1f, 0f));
            SetAlphaOverLifetime(needle, (0f, 1f), (0.5f, 0.8f), (1f, 0f));

            // 画面に対して斜めに交差する、長さの違う2本の細い光。短い方を少し遅らせて「キラン」の余韻にする
            AddCrossStreak(
                core.gameObject,
                materials,
                "CrossStreakLong",
                GlintFlashTime,
                12f,
                -18f,
                0.26f,
                1f
            );
            AddCrossStreak(
                core.gameObject,
                materials,
                "CrossStreakShort",
                GlintFlashTime + 0.03f,
                6f,
                72f,
                0.2f,
                0.7f
            );

            // 閃いた瞬間に弾ける小さな輪(カメラ向き)
            ParticleSystem ping = CreateSystem(core.gameObject, "PingRing", materials.Ring);
            ParticleSystem.MainModule pingMain = ping.main;
            pingMain.startLifetime = 0.4f;
            pingMain.startSize = 9f;
            pingMain.startColor = WithAlpha(Rose, 0.8f);
            SetBurst(ping, 1, GlintFlashTime);
            SetSizeOverLifetime(ping, (0f, 0.1f), (0.35f, 0.75f), (1f, 1f));
            SetAlphaOverLifetime(ping, (0f, 1f), (1f, 0f));
            SetRingThickness(ping, (0f, 1.4f), (1f, 0.2f));

            // 閃きからこぼれる光の粒
            ParticleSystem orbs = CreateSystem(core.gameObject, "GlintOrbs", materials.Orb);
            ParticleSystem.MainModule orbsMain = orbs.main;
            orbsMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            orbsMain.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            orbsMain.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            orbsMain.startColor = new ParticleSystem.MinMaxGradient(
                WithAlpha(Rose, 0.8f),
                WithAlpha(Gold, 0.8f)
            );
            SetBurst(orbs, 8, GlintFlashTime);
            SetSphereShell(orbs, 0.4f, thickness: 1f);
            SetDrag(orbs, 2.5f);
            SetAlphaOverLifetime(orbs, (0f, 1f), (0.6f, 0.7f), (1f, 0f));

            return core.gameObject;
        }

        /// <summary>カメラ向きの細い光を1本足す。伸びながら細くなって消える。</summary>
        private static void AddCrossStreak(
            GameObject root,
            Materials materials,
            string name,
            float delay,
            float length,
            float angleDegrees,
            float lifetime,
            float alpha
        )
        {
            // 板の高さ（m）。光の線の太さはこの一部(シェーダーの絞り)で、残りはにじみの余白
            const float quadHeight = 1.2f;

            ParticleSystem streak = CreateSystem(root, name, materials.Streak);
            ParticleSystem.MainModule main = streak.main;
            main.startLifetime = lifetime;
            main.startSize3D = true;
            main.startSizeX = length;
            main.startSizeY = quadHeight;
            main.startSizeZ = 1f;
            main.startRotation = angleDegrees * Mathf.Deg2Rad;
            main.startColor = WithAlpha(Gold, alpha);
            SetBurst(streak, 1, delay);

            ParticleSystem.SizeOverLifetimeModule size = streak.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            size.x = new ParticleSystem.MinMaxCurve(
                1f,
                Curve((0f, 0.1f), (0.25f, 1f), (1f, 1.15f))
            );
            size.y = new ParticleSystem.MinMaxCurve(1f, Curve((0f, 1f), (1f, 0.1f)));
            size.z = 1f;
            SetAlphaOverLifetime(streak, (0f, 1f), (0.4f, 0.9f), (1f, 0f));
        }

        // ------------------------------------------------------------------
        // 突き: 軌道と届いた先
        // ------------------------------------------------------------------

        private static GameObject BuildThrust(Materials materials)
        {
            var root = new GameObject("KurageStabThrust");

            // 軌道に並べる3種は KurageStabThrustEffect が1つずつ置く。自分では放出しないが、
            // 置かれた粒を動かし続けるためループ再生にしておく
            ParticleSystem rings = CreateSystem(root, "PathRings", materials.Ring);
            SetLooping(rings, ParticleSystemSimulationSpace.World);
            ParticleSystem.MainModule ringsMain = rings.main;
            ringsMain.startLifetime = 0.45f;
            ringsMain.startColor = WithAlpha(Rose, 0.75f);
            SetFacingSystemForward(rings);
            // 一気に広がって、外へ行くほど減速する
            SetSizeOverLifetime(rings, (0f, 0.35f), (0.3f, 0.8f), (1f, 1f));
            SetAlphaOverLifetime(rings, (0f, 1f), (0.4f, 0.6f), (1f, 0f));
            SetRingThickness(rings, (0f, 1.3f), (1f, 0.25f));

            ParticleSystem streaks = CreateSystem(root, "PathStreaks", materials.Streak);
            SetLooping(streaks, ParticleSystemSimulationSpace.World);
            ParticleSystem.MainModule streaksMain = streaks.main;
            streaksMain.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            streaksMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
            // 突きはプレイヤー(カメラ)へ向かってくるので、濃いと手前で画面を覆ってしまう
            streaksMain.startColor = new ParticleSystem.MinMaxGradient(
                WithAlpha(White, 0.35f),
                WithAlpha(Gold, 0.35f)
            );
            SetStretched(streaks, lengthScale: 20f);
            SetAlphaOverLifetime(streaks, (0f, 1f), (1f, 0f));

            ParticleSystem orbs = CreateSystem(root, "PathOrbs", materials.Orb);
            SetLooping(orbs, ParticleSystemSimulationSpace.World);
            ParticleSystem.MainModule orbsMain = orbs.main;
            orbsMain.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            orbsMain.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            orbsMain.startColor = new ParticleSystem.MinMaxGradient(
                WithAlpha(Rose, 0.8f),
                WithAlpha(Gold, 0.8f)
            );
            // ゆっくり浮かび上がらせる
            orbsMain.gravityModifier = -0.03f;
            SetDrag(orbs, 2f);
            SetSizeOverLifetime(orbs, (0f, 1f), (1f, 0.4f));
            SetAlphaOverLifetime(orbs, (0f, 0f), (0.1f, 1f), (0.6f, 0.7f), (1f, 0f));

            LineRenderer coreLine = AddCoreLine(root, materials.Beam);
            KurageStabLance lance = AddLance(root, materials);
            ParticleSystem impact = BuildImpact(root, materials);

            var effect = root.AddComponent<KurageStabThrustEffect>();
            var so = new SerializedObject(effect);
            so.FindProperty("_pathRings").objectReferenceValue = rings;
            so.FindProperty("_pathStreaks").objectReferenceValue = streaks;
            so.FindProperty("_pathOrbs").objectReferenceValue = orbs;
            so.FindProperty("_coreLine").objectReferenceValue = coreLine;
            so.FindProperty("_lance").objectReferenceValue = lance;
            so.FindProperty("_impact").objectReferenceValue = impact;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static LineRenderer AddCoreLine(GameObject root, Material material)
        {
            var go = new GameObject("CoreLine");
            go.transform.SetParent(root.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            // 線の全長にシェーダーの筋を1本だけ引き伸ばして描く
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = 2;
            Color color = WithAlpha(White, 0.9f);
            line.startColor = color;
            line.endColor = color;
            return line;
        }

        // ------------------------------------------------------------------
        // 槍の穂先
        // ------------------------------------------------------------------

        // 芯の円錐の太さ（外側の殻に対する倍率）
        private const float LanceInnerRadiusScale = 0.4f;

        /// <summary>外側の殻(淡いローズ・筋が多い)と、細く明るい芯(白・筋が少ない)のマテリアルを作る。</summary>
        private static bool CreateLanceMaterials(Materials materials)
        {
            Shader shader = Shader.Find(LanceShaderName);
            if (shader == null)
            {
                Debug.LogError($"{LogPrefix} シェーダー {LanceShaderName} が見つかりません");
                return false;
            }

            materials.LanceOuter = LoadOrCreateMaterial(shader, "KurageHoly_LanceOuter");
            materials.LanceOuter.SetColor("_Color", Rose);
            materials.LanceOuter.SetFloat("_Intensity", 0.45f);
            materials.LanceOuter.SetFloat("_LineCount", 14f);
            // 殻は輪郭を立てて、筋の束ではなく円錐の形として読めるようにする
            materials.LanceOuter.SetFloat("_RimAmount", 1f);
            SaveMaterial(materials.LanceOuter);

            materials.LanceInner = LoadOrCreateMaterial(shader, "KurageHoly_LanceInner");
            materials.LanceInner.SetColor("_Color", White);
            materials.LanceInner.SetFloat("_Intensity", 0.5f);
            materials.LanceInner.SetFloat("_LineCount", 6f);
            // 芯は根元側まで見せて、槍の柄が続いているように見せる
            materials.LanceInner.SetFloat("_BaseFade", 0.25f);
            SaveMaterial(materials.LanceInner);
            return true;
        }

        private static KurageStabLance AddLance(GameObject root, Materials materials)
        {
            Mesh cone = CreateConeMesh();
            var go = new GameObject("Lance");
            go.transform.SetParent(root.transform, false);
            AddLanceLayer(go, "LanceOuter", cone, materials.LanceOuter, 1f);
            AddLanceLayer(go, "LanceInner", cone, materials.LanceInner, LanceInnerRadiusScale);
            return go.AddComponent<KurageStabLance>();
        }

        private static void AddLanceLayer(
            GameObject parent,
            string name,
            Mesh cone,
            Material material,
            float radiusScale
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localScale = new Vector3(radiusScale, radiusScale, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = cone;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// 槍の穂先の円錐(底なし)。+Z が先端で、根元(z=0)の半径1・長さ1。大きさは KurageStabLance が拡大率で決める。
        /// UV は u=周方向、v=軸方向(0=根元、1=先端)。既にあれば中身だけ作り直し、GUID と参照を保つ。
        /// </summary>
        private static Mesh CreateConeMesh()
        {
            // 周方向の分割数。輪郭が角ばって見えない程度
            const int segments = 32;
            // 軸方向の分割数。先端へ収束する筋(UV の補間)がゆがまない程度
            const int rows = 6;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int row = 0; row <= rows; row++)
            {
                float v = (float)row / rows;
                for (int segment = 0; segment <= segments; segment++)
                {
                    float u = (float)segment / segments;
                    float angle = u * Mathf.PI * 2f;
                    var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    vertices.Add(radial * (1f - v) + Vector3.forward * v);
                    // 半径1・長さ1 の円錐の面は軸に対して45°傾くので、法線は外向きと先端向きを半分ずつ
                    normals.Add((radial + Vector3.forward).normalized);
                    uvs.Add(new Vector2(u, v));
                }
            }
            int columns = segments + 1;
            for (int row = 0; row < rows; row++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int a = row * columns + segment;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(LanceMeshPath);
            bool isNew = mesh == null;
            if (isNew)
                mesh = new Mesh { name = "KurageStabLanceCone" };
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            if (isNew)
                AssetDatabase.CreateAsset(mesh, LanceMeshPath);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
            return mesh;
        }

        /// <summary>届いた先。KurageStabThrustEffect が位置を決めて再生するので、自動では再生しない。</summary>
        private static ParticleSystem BuildImpact(GameObject root, Materials materials)
        {
            // ルート: 届いた瞬間の淡い閃光
            ParticleSystem flash = CreateSystem(root, "Impact", materials.Orb);
            ParticleSystem.MainModule flashMain = flash.main;
            flashMain.duration = 2f;
            flashMain.playOnAwake = false;
            flashMain.startLifetime = 0.16f;
            flashMain.startSize = 4.5f;
            flashMain.startColor = WithAlpha(White, 0.18f);
            SetBurst(flash, 1);
            SetSizeOverLifetime(flash, (0f, 0.4f), (1f, 1f));
            SetAlphaOverLifetime(flash, (0f, 1f), (1f, 0f));

            // 軌道に垂直な輪が衝撃波のように広がる。少し遅れてもう1枚、大きく淡く追う
            AddImpactRing(flash.gameObject, materials, "ImpactRing", 0f, 0.5f, 7f, 0.85f);
            AddImpactRing(flash.gameObject, materials, "ImpactRingTrail", 0.06f, 0.75f, 10f, 0.4f);

            // 散っていくオーブ。勢いよく散ってから減速し、ゆっくり浮かんで消える
            ParticleSystem orbs = CreateSystem(flash.gameObject, "ImpactOrbs", materials.Orb);
            ParticleSystem.MainModule orbsMain = orbs.main;
            orbsMain.playOnAwake = false;
            orbsMain.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.7f);
            orbsMain.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
            orbsMain.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.55f);
            orbsMain.startColor = new ParticleSystem.MinMaxGradient(
                WithAlpha(Rose, 0.9f),
                WithAlpha(Gold, 0.9f)
            );
            orbsMain.gravityModifier = -0.04f;
            SetBurst(orbs, 30);
            SetSphereShell(orbs, 0.4f, thickness: 1f);
            SetDrag(orbs, 3f);
            SetSizeOverLifetime(orbs, (0f, 1f), (1f, 0.4f));
            SetAlphaOverLifetime(orbs, (0f, 0f), (0.05f, 1f), (0.5f, 0.8f), (1f, 0f));
            // まっすぐ飛ぶだけにせず、漂うように揺らす
            ParticleSystem.NoiseModule noise = orbs.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.5f;

            // 一点から放射状に走る細い筋
            ParticleSystem sparks = CreateSystem(
                flash.gameObject,
                "ImpactSparks",
                materials.Streak
            );
            ParticleSystem.MainModule sparksMain = sparks.main;
            sparksMain.playOnAwake = false;
            sparksMain.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            // 速すぎるとカメラの手前まで飛んできて、太い帯になって画面を覆う
            sparksMain.startSpeed = new ParticleSystem.MinMaxCurve(10f, 18f);
            sparksMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.12f);
            sparksMain.startColor = WithAlpha(White, 0.35f);
            SetBurst(sparks, 10);
            SetSphereShell(sparks, 0.3f, thickness: 1f);
            SetDrag(sparks, 4f);
            SetStretched(sparks, lengthScale: 8f);
            SetAlphaOverLifetime(sparks, (0f, 1f), (1f, 0f));

            return flash;
        }

        private static void AddImpactRing(
            GameObject root,
            Materials materials,
            string name,
            float delay,
            float lifetime,
            float diameter,
            float alpha
        )
        {
            ParticleSystem ring = CreateSystem(root, name, materials.Ring);
            ParticleSystem.MainModule main = ring.main;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSize = diameter;
            main.startColor = WithAlpha(Rose, alpha);
            SetBurst(ring, 1, delay);
            SetFacingSystemForward(ring);
            SetSizeOverLifetime(ring, (0f, 0.12f), (0.3f, 0.75f), (1f, 1f));
            SetAlphaOverLifetime(ring, (0f, 1f), (0.4f, 0.6f), (1f, 0f));
            SetRingThickness(ring, (0f, 1.5f), (1f, 0.15f));
        }

        // ------------------------------------------------------------------
        // 素材・保存
        // ------------------------------------------------------------------

        /// <summary>形ごとのマテリアルを作る(既にあればそれを使い、GUID と参照を保つ)。</summary>
        private static Material CreateMaterial(
            Shader shader,
            string name,
            string shapeKeyword,
            float shapeValue
        )
        {
            Material material = LoadOrCreateMaterial(shader, name);
            foreach (string keyword in new[] { "_SHAPE_ORB", "_SHAPE_RING", "_SHAPE_STREAK" })
                material.DisableKeyword(keyword);
            material.EnableKeyword(shapeKeyword);
            material.SetFloat("_Shape", shapeValue);
            SaveMaterial(material);
            return material;
        }

        /// <summary>マテリアルを読み込む。無ければ作る(既にあればそれを使い、GUID と参照を保つ)。</summary>
        private static Material LoadOrCreateMaterial(Shader shader, string name)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            return material;
        }

        private static void SaveMaterial(Material material)
        {
            EditorUtility.SetDirty(material);
            // SaveAssets は編集中の他のアセットまで書き出すので、このマテリアルだけを保存する
            AssetDatabase.SaveAssetIfDirty(material);
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            try
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"{LogPrefix} {path} を生成しました", prefab);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>KurageBoss の KurageStabEffects(無ければ追加)に、生成した Prefab を割り当てる。</summary>
        private static void AssignToBoss(GameObject gather, GameObject glint, GameObject thrust)
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == BossPrefabPath)
            {
                Debug.LogError(
                    $"{LogPrefix} {BossPrefabPath} を Prefab モードで開いているため割り当てられません。"
                        + "Prefab モードを閉じてから再実行してください"
                );
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            try
            {
                var controller = contents.GetComponentInChildren<KurageBossController>(true);
                if (controller == null)
                {
                    Debug.LogError(
                        $"{LogPrefix} {BossPrefabPath} に KurageBossController が見つかりません"
                    );
                    return;
                }

                if (!controller.TryGetComponent(out KurageStabEffects effects))
                    effects = controller.gameObject.AddComponent<KurageStabEffects>();

                var so = new SerializedObject(effects);
                so.FindProperty("_gatherPrefab").objectReferenceValue =
                    gather.GetComponent<ParticleSystem>();
                so.FindProperty("_glintPrefab").objectReferenceValue =
                    glint.GetComponent<ParticleSystem>();
                so.FindProperty("_thrustPrefab").objectReferenceValue =
                    thrust.GetComponent<KurageStabThrustEffect>();
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, BossPrefabPath);
                Debug.Log($"{LogPrefix} {BossPrefabPath} の KurageStabEffects に割り当てました");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ------------------------------------------------------------------
        // パーティクルの共通設定
        // ------------------------------------------------------------------

        /// <summary>
        /// 1回だけ再生する・自分では何も放出しない設定のパーティクルを作る
        /// (新規 ParticleSystem は 5 秒ループ・Cone 放出・毎秒10個が既定)。
        /// 触手の先端に付いて動かすため、粒はエフェクトと一緒に動く(Local)。
        /// </summary>
        private static ParticleSystem CreateSystem(
            GameObject parent,
            string name,
            Material material
        )
        {
            var go = new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.duration = 1f;
            main.startDelay = 0f;
            main.startSpeed = 0f;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static void SetLooping(
            ParticleSystem ps,
            ParticleSystemSimulationSpace space = ParticleSystemSimulationSpace.Local
        )
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.simulationSpace = space;
        }

        private static void SetRate(ParticleSystem ps, float perSecond)
        {
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = perSecond;
        }

        private static void SetBurst(ParticleSystem ps, int count, float time = 0f)
        {
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(time, (short)count) });
        }

        /// <summary>球の表面(thickness=0)〜球の中全体(thickness=1)から、外向きに放出する。速さが負なら中心へ向かう。</summary>
        private static void SetSphereShell(ParticleSystem ps, float radius, float thickness)
        {
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = thickness;
        }

        /// <summary>エフェクトの +Z(狙いの向き)へ、一点から放出する。</summary>
        private static void SetForwardPoint(ParticleSystem ps)
        {
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 0f;
            shape.radius = 0.0001f;
        }

        /// <summary>板をカメラへ向けず、エフェクトの +Z(狙いの向き)へ向ける。輪を軌道に垂直に立てるのに使う。</summary>
        private static void SetFacingSystemForward(ParticleSystem ps)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.Local;
        }

        /// <summary>進む向きへ細長く伸ばして描く。長さは「大きさ × lengthScale」で、速さでは変えない。</summary>
        private static void SetStretched(ParticleSystem ps, float lengthScale)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = lengthScale;
            renderer.velocityScale = 0f;
            renderer.cameraVelocityScale = 0f;
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

        private static void SetSizeOverLifetime(
            ParticleSystem ps,
            params (float time, float value)[] keys
        )
        {
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, Curve(keys));
        }

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
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                alphas
            );

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = gradient;
        }

        /// <summary>
        /// 輪の太さの倍率を寿命に沿って動かす。パーティクルの Custom1.x に曲線を入れ、
        /// 頂点ストリームでシェーダー(KurageHolyGlow の Ring)へ渡す。
        /// </summary>
        private static void SetRingThickness(
            ParticleSystem ps,
            params (float time, float value)[] keys
        )
        {
            ParticleSystem.CustomDataModule custom = ps.customData;
            custom.enabled = true;
            custom.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
            custom.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 1);
            custom.SetVector(
                ParticleSystemCustomData.Custom1,
                0,
                new ParticleSystem.MinMaxCurve(1f, Curve(keys))
            );

            // シェーダーは TEXCOORD0 の xy を UV、z を太さの倍率として読む
            ps.GetComponent<ParticleSystemRenderer>()
                .SetActiveVertexStreams(
                    new List<ParticleSystemVertexStream>
                    {
                        ParticleSystemVertexStream.Position,
                        ParticleSystemVertexStream.Color,
                        ParticleSystemVertexStream.UV,
                        ParticleSystemVertexStream.Custom1X,
                    }
                );
        }

        private static AnimationCurve Curve(params (float time, float value)[] keys)
        {
            var curve = new AnimationCurve();
            foreach (var (time, value) in keys)
                curve.AddKey(time, value);
            return curve;
        }

        private static Color WithAlpha(Color color, float alpha) =>
            new(color.r, color.g, color.b, alpha);
    }
}
