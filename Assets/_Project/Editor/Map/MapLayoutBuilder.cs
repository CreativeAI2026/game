#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CreativeAI.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// documents/MapLayout.md の文字マップを読んで、フィールドの床・壁・柵・階段をシーンに生成する。
    /// 1マス = 4u。生成物は Map ルート配下だけに作り、再生成のたびに丸ごと作り直す
    /// (手置きの小物は Map の外に置けば消えない)。
    /// Tools &gt; CreativeAI &gt; Map から実行(バッチモード -executeMethod も可)。
    /// </summary>
    public static class MapLayoutBuilder
    {
        public const char Void = ' '; // 床が無いマス
        public const float Cell = 4f; // 1マスの一辺(u)
        public const float FloorHeight = 9.6f; // 階高 = 壁の高さ
        const float FloorSlabThickness = 0.4f;

        const string ScenePath = "Assets/_Project/Scenes/Field/Field_Area01.unity";
        const string EnvDir = "Assets/_Project/Art/Models/Environment";
        const string MaterialDir = "Assets/_Project/Art/Materials";
        const string MapRootName = "Map";

        // MapLayout.md の各階の原点(マス[row 0, col 0] の中心)と床の高さ。
        // 図そのものは .md 側が正。ここは「図をワールドのどこに置くか」だけを持つ。
        static readonly FloorDef[] Floors =
        {
            new FloorDef("1F", new Vector3(-218f, -48.0f, -30f)),
            new FloorDef("2F", new Vector3(-218f, -38.4f, 10f)),
            new FloorDef("3F", new Vector3(-218f, -28.8f, 50f)),
        };

        // モデルの実寸(glb をそのまま Scale 1 で置いたときの大きさ)。スケール計算に使う。
        const float HandrailLength = 6.09f;
        const float HandrailHeight = 1.25f;

        // 階段は「歩ける面」の寸法を使う。バウンディングボックス(高さ5.045 / 奥行6.736)は
        // 手すりの上端と踏面のはみ出しを含むので、それで割ると踏面が上階の床に届かない。
        const float StairsWidth = 2.12f; // X幅(手すり込み。吹き抜け2マス=8u に合わせる)
        const float StairsRise = 4.08f; // 最上段の踏面まで(0.17 × 24段)。上階の床面に一致させる
        const float StairsRun = 6.72f; // 踏面の総奥行(0.28 × 24段)
        const int StairsSteps = 24; // 段数。斜面コライダーを段鼻に通すのに使う
        const float StairsRailTop = 0.95f; // 手すり上端の、踏面からの高さ

        // 階段の当たりは段形状ではなく斜面にする(段の蹴上げが CharacterController の
        // StepOffset を超えるため。歩ける面は斜面、見た目は段々のまま)。
        const float RampThickness = 1f; // 斜面コライダーの厚み(すり抜け防止)
        const float RampLift = 0.03f; // 段鼻の面取りに引っかからないよう斜面を少し持ち上げる
        const float RampRailThickness = 0.4f;

        // ガラス壁 `$`。モデルは 1マス×階高(4.00 × 9.60u)ちょうどなので等倍で1マス1枚置く。
        // 当たりは薄い箱にする(`#` のように4u厚だとガラスの手前で止まって見える)。
        const char Glass = '$';
        const char Fence = '-'; // 柵。ガラスと同じく列にまとめ、縁へ寄せ、角で継ぐ
        const string GlassAsset = "Structure/GlassWall-V.glb";
        const float GlassColliderThickness = 0.4f;
        const float GlassPanelDepth = 0.14f; // モデルの厚み(袖壁を作る幅の計算に使う)

        // 扉に近づいたと見なす球トリガー。壁が4u厚なので、扉の手前で気付ける大きさにする。
        const float DoorInteractRadius = 3.2f;
        const float DoorInteractHeight = 1.2f; // プレイヤー(身長1.8u)の胴の高さ
        const float DoorLeafColliderDepth = 0.4f; // 扉板の当たりの厚み(実寸0.17uだと薄すぎる)

        // 扉の拡大率。glb はプレイヤー(1.8u)基準の実寸だが、階高 9.6u の建物に対して小さすぎて
        // 「人が通る所」に見えない。2.5 倍で `R` は外形 3.28 × 5.68u(1マス4uに収まる) /
        // 開口 2.55 × 5.40u になり、Field_Area03 の手置き扉(高さ 5.34u ≒ 階高の56%)とほぼ同じ見え方。
        const float DoorScale = 2.5f;

        // 扉の周りは**壁ではなくガラス**で埋める(袖・垂れ壁)。厚みは `$` の当たりと同じにして
        // 隣のガラス板と面が揃うようにする。枠の無い1枚板なので方立・無目の線が出ない。
        const float DoorGlassThickness = GlassColliderThickness;
        const string GlassMaterialPath = "Assets/_Project/Art/Materials/Glass.mat";

        // 扉 `R` `C` `L`。実寸(glb を Scale 1 で置いたときの外形)は documents/MapLayout.md の表と同じ。
        // 原点は開口の中心・床面で、制御パネルのぶん左右非対称。値は **Unity 空間**:
        // glTF -> Unity のインポートで X が反転するので、.glb で見た左右とは逆になる
        // (例: ClassroomDoor-V は glb だと -0.74〜+0.765、Unity では -0.765〜+0.74)。
        static readonly DoorDef[] Doors =
        {
            new DoorDef('R', "Door/LabDoor-V.glb", -0.685f, 0.620f, 2.270f),
            new DoorDef('C', "Door/ClassroomDoor-V.glb", -0.765f, 0.740f, 2.320f),
            new DoorDef('L', "Door/LibraryDoor-V.glb", -0.815f, 0.790f, 2.360f),
        };

        readonly struct DoorDef
        {
            public readonly char Symbol;
            public readonly string Asset;
            public readonly float MinX,
                MaxX,
                Height;

            public DoorDef(char symbol, string asset, float minX, float maxX, float height)
            {
                Symbol = symbol;
                Asset = asset;
                MinX = minX;
                MaxX = maxX;
                Height = height;
            }
        }

        static bool IsDoor(char c) => Doors.Any(d => d.Symbol == c);

        readonly struct FloorDef
        {
            public readonly string Name;
            public readonly Vector3 Origin;

            public FloorDef(string name, Vector3 origin)
            {
                Name = name;
                Origin = origin;
            }
        }

        [MenuItem("Tools/CreativeAI/Map/Build Field_Area01 From MapLayout")]
        public static void BuildArea01()
        {
            var grids = LoadGrids();
            if (grids == null)
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateSun();
            BuildInto(grids, scene);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log(
                $"[MapLayoutBuilder] {ScenePath} を作成しました。Build Settings への登録は手動で行ってください。"
            );
        }

        /// <summary>Field_Area01 を開いて Map ルートだけ作り直し、保存する(Map の外は触らない)。</summary>
        [MenuItem("Tools/CreativeAI/Map/Rebuild Field_Area01")]
        public static void RebuildArea01()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError(
                    $"[MapLayoutBuilder] {ScenePath} がありません。先に Build Field_Area01 を実行してください。"
                );
                return;
            }

            var grids = LoadGrids();
            if (grids == null)
                return;

            // 既に開いていればその場で作り直す(Single で開き直すと、小物用シーンを
            // 開いて作業している最中に実行したときにそれを閉じてしまう)。
            var scene = FindOpenScene(ScenePath);
            if (!scene.IsValid())
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BuildInto(grids, scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.Refresh();
            Debug.Log($"[MapLayoutBuilder] {ScenePath} の Map ルートを作り直しました。");
        }

        /// <summary>いま開いているシーンの Map ルートだけを作り直す(Map の外は触らない)。</summary>
        [MenuItem("Tools/CreativeAI/Map/Rebuild Map In Current Scene")]
        public static void RebuildCurrentScene()
        {
            var grids = LoadGrids();
            if (grids == null)
                return;
            // マップシーンが開いていればそちらへ。無ければアクティブなシーンに作る。
            var target = FindOpenScene(ScenePath);
            if (!target.IsValid())
                target = SceneManager.GetActiveScene();
            BuildInto(grids, target);
            EditorSceneManager.MarkSceneDirty(target);
            Debug.Log($"[MapLayoutBuilder] {target.name} の Map ルートを作り直しました。");
        }

        /// <summary>開いているシーンからパスで探す(見つからなければ IsValid() == false)。</summary>
        public static Scene FindOpenScene(string path)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == path)
                    return s;
            }
            return default;
        }

        /// <summary>そのシーンの中の `Map` ルート(他のシーンの同名オブジェクトは触らない)。</summary>
        static GameObject FindMapRoot(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return GameObject.Find("/" + MapRootName);
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == MapRootName)
                    return go;
            return null;
        }

        /// <summary>マップシーンのパス(小物シーンのツールから参照する)。</summary>
        public static string MapScenePath => ScenePath;

        /// <summary>マップのルート名(ピッキング無効化などに使う)。</summary>
        public static string MapRoot => MapRootName;

        static void CreateSun()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // ---------------------------------------------------------------- 読み込み

        static string DocPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "../../documents/MapLayout.md"));

        static List<char[,]> LoadGrids()
        {
            if (!File.Exists(DocPath))
            {
                Debug.LogError($"[MapLayoutBuilder] マップ定義が見つかりません: {DocPath}");
                return null;
            }

            var blocks = new List<Dictionary<int, string>>();
            Dictionary<int, string> current = null;
            var rowPattern = new Regex(@"^\s*(\d+)\|(.*)$");

            foreach (var raw in File.ReadAllLines(DocPath))
            {
                var line = raw.TrimEnd();
                if (line.Trim() == "```text")
                {
                    current = new Dictionary<int, string>();
                    continue;
                }
                if (current == null)
                    continue;
                if (line.Trim() == "```")
                {
                    if (current.Count > 0)
                        blocks.Add(current);
                    current = null;
                    continue;
                }

                var m = rowPattern.Match(line);
                if (m.Success)
                    current[int.Parse(m.Groups[1].Value)] = m.Groups[2].Value.TrimEnd();
            }

            if (blocks.Count < Floors.Length)
            {
                Debug.LogError(
                    $"[MapLayoutBuilder] マップが {blocks.Count} 個しか読めませんでした(必要: {Floors.Length})。"
                );
                return null;
            }

            var grids = new List<char[,]>();
            for (var f = 0; f < Floors.Length; f++)
            {
                var block = blocks[f];
                var rows = block.Keys.Max() + 1;
                var cols = block.Values.Max(s => s.Length);
                // 上階を後退させる(1F の上を開ける)ときは行を空にするので、短い行は異常ではない。
                // 足りないぶんは「床なし」で埋まる ─ 意図せず短い行があると床が欠けるので Log で残す。
                var shortRows = Enumerable
                    .Range(0, rows)
                    .Where(r => (block.TryGetValue(r, out var s) ? s.Length : 0) != cols)
                    .ToArray();
                if (shortRows.Length > 0)
                    Debug.Log(
                        $"[MapLayoutBuilder] {Floors[f].Name}: row [{string.Join(", ", shortRows)}] は "
                            + $"{cols} 字に足りないので、足りないぶんを「床なし」で埋めます。"
                    );

                var grid = new char[rows, cols];
                for (var r = 0; r < rows; r++)
                {
                    block.TryGetValue(r, out var s);
                    s ??= string.Empty;
                    // 短い行の余りは「床なし」で埋める(末尾の空白がエディタに落とされた場合の保険)
                    for (var c = 0; c < cols; c++)
                        grid[r, c] = c < s.Length ? s[c] : Void;
                }

                grids.Add(grid);
                Debug.Log(
                    $"[MapLayoutBuilder] {Floors[f].Name}: {cols} × {rows} マス を読みました。"
                );
            }

            return grids;
        }

        // ---------------------------------------------------------------- 生成

        /// <summary>
        /// マップ一式を <paramref name="target"/> シーンの中に作り直す。
        /// <b>生成先を明示するのが要点</b>: 小物用シーンをアクティブにして作業している最中に
        /// 実行すると、アクティブなシーン(= 小物側)にマップが生成されてしまうため。
        /// </summary>
        static void BuildInto(List<char[,]> grids, Scene target)
        {
            var old = FindMapRoot(target);
            if (old != null)
                Object.DestroyImmediate(old);

            var wallMat = GetOrCreateMaterial("Map_Wall", new Color(0.72f, 0.72f, 0.70f));
            var floorMat = GetOrCreateMaterial("Map_Floor", new Color(0.42f, 0.44f, 0.47f));

            // 扉の周りを埋めるガラス。枠(方立・無目)の無い1枚板にしたいので glb ではなく
            // 透過マテリアルを貼った箱で作る。見つからなければ壁で埋めて続行する。
            var doorGlassMat = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
            if (doorGlassMat == null)
            {
                Debug.LogWarning(
                    $"[MapLayoutBuilder] {GlassMaterialPath} が見つかりません。扉の周りは壁で埋めます。"
                );
                doorGlassMat = wallMat;
            }
            var handrail = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{EnvDir}/Structure/Handrail.glb"
            );
            var stairs = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{EnvDir}/Structure/Stairs.glb"
            );
            if (handrail == null)
                Debug.LogWarning(
                    "[MapLayoutBuilder] Handrail.glb が見つかりません。柵は生成しません。"
                );
            if (stairs == null)
                Debug.LogWarning(
                    "[MapLayoutBuilder] Stairs.glb が見つかりません。階段は生成しません。"
                );

            var glass = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvDir}/{GlassAsset}");
            if (glass == null)
                Debug.LogWarning(
                    $"[MapLayoutBuilder] {GlassAsset} が見つかりません。`$` は当たりだけになります。"
                );

            var doorPrefabs = new Dictionary<char, GameObject>();
            foreach (var d in Doors)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvDir}/{d.Asset}");
                if (prefab == null)
                    Debug.LogWarning(
                        $"[MapLayoutBuilder] {d.Asset} が見つかりません。`{d.Symbol}` は扉なしの開口にします。"
                    );
                else
                    doorPrefabs[d.Symbol] = prefab;
            }

            var root = new GameObject(MapRootName);
            if (target.IsValid())
                SceneManager.MoveGameObjectToScene(root, target);
            var floorRoots = new Transform[Floors.Length];
            for (var f = 0; f < Floors.Length; f++)
            {
                var go = new GameObject(Floors[f].Name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Floors[f].Origin;
                floorRoots[f] = go.transform;
            }

            var stairCells = CollectStairs(grids);

            var rowOffsets = WorldRowOffsets();
            var worldRows = Enumerable
                .Range(0, Floors.Length)
                .Max(f => rowOffsets[f] + grids[f].GetLength(0));
            var worldCols = grids.Max(g => g.GetLength(1));
            var perimeter = BuildPerimeterMask(grids, rowOffsets, worldRows, worldCols);
            CreatePerimeterWall(root.transform, perimeter, worldRows, worldCols, wallMat);

            // 床のマスクと、ガラス・柵の列は**先に全階ぶん**作る。寄せ量(EdgeOffset)を
            // 階をまたいで揃えるため ── 同じ列のガラスが階によって 2u ずれると、
            // 建物としては1枚の面のはずのものが食い違って見える。
            var floorMasks = new bool[Floors.Length][,];
            var fenceLayouts = new LineLayout[Floors.Length];
            var glassLayouts = new LineLayout[Floors.Length];
            for (var f = 0; f < Floors.Length; f++)
            {
                var g = grids[f];
                var nr = g.GetLength(0);
                var nc = g.GetLength(1);

                // 空白は床なし。上の階に着く階段のマスも床を抜く(階段の吹き抜け)。
                // 抜くのは「上の階の図での」マスなので Upper* を使う(下の階の row とは10マスずれる)
                var mask = new bool[nr, nc];
                for (var r = 0; r < nr; r++)
                for (var c = 0; c < nc; c++)
                    mask[r, c] = g[r, c] != Void;
                foreach (var s in stairCells.Where(s => s.UpperFloor == f))
                    for (var r = s.UpperRow0; r <= s.UpperRow1; r++)
                    for (var c = s.UpperCol0; c <= s.UpperCol1; c++)
                        mask[r, c] = false;

                floorMasks[f] = mask;
                fenceLayouts[f] = LineLayout.Build(g, mask, nr, nc, Fence);
                glassLayouts[f] = LineLayout.Build(g, mask, nr, nc, Glass);
            }
            AlignAcrossFloors(glassLayouts, grids, "ガラス");
            AlignAcrossFloors(fenceLayouts, grids, "柵");

            for (var f = 0; f < Floors.Length; f++)
            {
                var grid = grids[f];
                var rows = grid.GetLength(0);
                var cols = grid.GetLength(1);
                var parent = floorRoots[f];
                var floorMask = floorMasks[f];

                var floorGroup = NewGroup("Floor", parent);
                foreach (var rect in MergeRects(floorMask, rows, cols))
                    CreateBox(
                        floorGroup,
                        "Floor",
                        rect,
                        FloorSlabThickness,
                        -FloorSlabThickness * 0.5f,
                        floorMat
                    );

                // 建物の外周にあたるマスは階ごとに作らない(全階を貫く1枚の外壁で置き換える)
                var wallMask = new bool[rows, cols];
                for (var r = 0; r < rows; r++)
                for (var c = 0; c < cols; c++)
                    wallMask[r, c] = grid[r, c] == '#' && !perimeter[r + rowOffsets[f], c];

                var wallGroup = NewGroup("Walls", parent);
                foreach (var rect in MergeRects(wallMask, rows, cols))
                    CreateBox(wallGroup, "Wall", rect, FloorHeight, FloorHeight * 0.5f, wallMat);

                if (handrail != null)
                {
                    var fenceLayout = fenceLayouts[f];
                    var fenceGroup = NewGroup("Fences", parent);
                    for (var i = 0; i < fenceLayout.Runs.Count; i++)
                        CreateFence(fenceGroup, handrail, grid, rows, cols, fenceLayout, i);
                }

                // ガラス壁: 見た目は1マス1枚の等倍、当たりは run ごとの薄い箱
                for (var r = 0; r < rows; r++)
                for (var c = 0; c < cols; c++)
                    if (grid[r, c] == Glass && perimeter[r + rowOffsets[f], c])
                        Debug.LogWarning(
                            $"[MapLayoutBuilder] {Floors[f].Name} row {r} col {c}: "
                                + "`$` が建物の外周にあります。外周は1枚の壁で覆われるのでガラスは埋まります。"
                        );

                var glassLayout = glassLayouts[f];
                if (glassLayout.Runs.Count > 0)
                {
                    var glassGroup = NewGroup("GlassWalls", parent);
                    for (var i = 0; i < glassLayout.Runs.Count; i++)
                        CreateGlassWall(
                            glassGroup,
                            glass,
                            grid,
                            rows,
                            cols,
                            glassLayout,
                            i,
                            wallMat
                        );
                }

                // 扉: 扉モデル + 開口からはみ出したぶんの袖壁・垂れ壁
                var doorGroup = (Transform)null;
                for (var r = 0; r < rows; r++)
                for (var c = 0; c < cols; c++)
                {
                    if (!IsDoor(grid[r, c]))
                        continue;
                    if (perimeter[r + rowOffsets[f], c])
                    {
                        Debug.LogWarning(
                            $"[MapLayoutBuilder] {Floors[f].Name} row {r} col {c}: "
                                + $"`{grid[r, c]}` が建物の外周にあります。外周は1枚の壁で覆われるので扉は埋まります。"
                        );
                        continue;
                    }
                    doorGroup ??= NewGroup("Doors", parent);
                    var def = Doors.First(d => d.Symbol == grid[r, c]);
                    doorPrefabs.TryGetValue(def.Symbol, out var prefab);
                    var eastWest = IsEastWest(grid, rows, cols, r, c);
                    CreateDoor(
                        doorGroup,
                        prefab,
                        def,
                        r,
                        c,
                        eastWest,
                        FacesPositive(grid, rows, cols, r, c, eastWest),
                        doorGlassMat,
                        WallOffsetAt(glassLayout, rows, cols, r, c, eastWest)
                    );
                }
            }

            if (stairs != null)
                foreach (var s in stairCells)
                    CreateStairs(NewGroup("Stairs", floorRoots[s.LowerFloor]), stairs, s);

            Selection.activeGameObject = root;
        }

        static Transform NewGroup(string name, Transform parent)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // ---------------------------------------------------------------- 外周壁

        /// <summary>各階の図の row 0 が、全階を重ねた共通マス目の何行目に来るか。</summary>
        static int[] WorldRowOffsets()
        {
            var baseZ = Floors.Min(f => f.Origin.z);
            var baseX = Floors[0].Origin.x;
            if (Floors.Any(f => !Mathf.Approximately(f.Origin.x, baseX)))
                Debug.LogWarning(
                    "[MapLayoutBuilder] 階ごとに原点Xが違います。外周壁の列がずれます。"
                );

            var offsets = new int[Floors.Length];
            for (var f = 0; f < Floors.Length; f++)
                offsets[f] = Mathf.RoundToInt((Floors[f].Origin.z - baseZ) / Cell);
            return offsets;
        }

        /// <summary>
        /// 全階の図をワールドの共通マス目に重ね、建物の外気に触れる縁のマスを求める。
        /// 階ごとにZ原点がずれているので、ある階の図の縁が別の階の室内に来ることがある。
        /// そこは外周ではなく普通の壁(階高ぶん)のままにしないと、上階の床を貫いてしまう。
        /// </summary>
        static bool[,] BuildPerimeterMask(
            List<char[,]> grids,
            int[] rowOffsets,
            int worldRows,
            int worldCols
        )
        {
            var covered = new bool[worldRows, worldCols];
            for (var f = 0; f < grids.Count; f++)
            {
                var grid = grids[f];
                for (var r = 0; r < grid.GetLength(0); r++)
                for (var c = 0; c < grid.GetLength(1); c++)
                    covered[r + rowOffsets[f], c] = true;
            }

            var ring = new bool[worldRows, worldCols];
            for (var r = 0; r < worldRows; r++)
            for (var c = 0; c < worldCols; c++)
            {
                if (!covered[r, c])
                    continue;
                ring[r, c] =
                    r == 0
                    || r == worldRows - 1
                    || c == 0
                    || c == worldCols - 1
                    || !covered[r - 1, c]
                    || !covered[r + 1, c]
                    || !covered[r, c - 1]
                    || !covered[r, c + 1];
            }

            return ring;
        }

        /// <summary>外周は最下階の床から最上階の壁の上端までを1枚で作る(上端・下端がフラットになる)。</summary>
        static void CreatePerimeterWall(
            Transform root,
            bool[,] perimeter,
            int worldRows,
            int worldCols,
            Material mat
        )
        {
            var bottomY = Floors.Min(f => f.Origin.y);
            var topY = Floors.Max(f => f.Origin.y) + FloorHeight;
            var height = topY - bottomY;

            var go = new GameObject("Perimeter");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(
                Floors[0].Origin.x,
                bottomY,
                Floors.Min(f => f.Origin.z)
            );

            var rects = MergeRects(perimeter, worldRows, worldCols);
            foreach (var rect in rects)
                CreateBox(go.transform, "Perimeter", rect, height, height * 0.5f, mat);

            Debug.Log(
                $"[MapLayoutBuilder] 外周壁: {worldCols} × {worldRows} マスの縁を高さ {height} "
                    + $"(Y {bottomY} 〜 {topY}) で {rects.Count} 個にまとめました。"
            );
        }

        static void CreateBox(
            Transform parent,
            string name,
            RectInt rect,
            float height,
            float centerY,
            Material mat
        )
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"{name}_{rect.x}_{rect.y}";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(
                Cell * (rect.x + (rect.width - 1) * 0.5f),
                centerY,
                Cell * (rect.y + (rect.height - 1) * 0.5f)
            );
            go.transform.localScale = new Vector3(Cell * rect.width, height, Cell * rect.height);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>
        /// 柵。ガラス壁と同じ扱いで、床の縁に接している列は<b>縁へ寄せ</b>、
        /// 直角に折れる角は<b>相手の列の面まで届く柵</b>で継ぐ(継がないと角が 2〜4u 空く)。
        /// 長さはマス数ぶんに引き伸ばす(手すりは繰り返しの造形なので伸ばしても破綻しない)。
        /// </summary>
        static void CreateFence(
            Transform parent,
            GameObject prefab,
            char[,] grid,
            int rows,
            int cols,
            LineLayout layout,
            int runIndex
        )
        {
            var run = layout.Runs[runIndex];
            var horizontal = layout.Horizontals[runIndex];
            var offset = layout.Offsets[runIndex];

            for (var i = 0; i < run.Length; i++)
            {
                var row = run.Row + (horizontal ? 0 : i);
                var col = run.Col + (horizontal ? i : 0);
                var axis = horizontal ? Cell * row : Cell * col;

                // 1マス = 1本。列の長さいっぱいに1本を引き伸ばすと、区間ごとに桟の間隔が
                // まるで変わってしまう(南の縁 256u = 42倍 / 吹き抜け 52u = 8.5倍 …)。
                // マス単位に切って全部同じ縮尺(4 / 6.09)で置けば、どこも同じ見た目になる。
                CreateHandrail(
                    parent,
                    prefab,
                    $"Handrail_{row}_{col}",
                    new Vector3(
                        Cell * col + (horizontal ? 0f : offset),
                        0f,
                        Cell * row + (horizontal ? offset : 0f)
                    ),
                    horizontal,
                    Cell
                );

                foreach (var side in new[] { -1, 1 })
                {
                    var nr = row + (horizontal ? side : 0);
                    var nc = col + (horizontal ? 0 : side);
                    if (nr < 0 || nc < 0 || nr >= rows || nc >= cols || grid[nr, nc] != Fence)
                        continue;
                    var other = layout.RunOfCell[nr, nc];
                    if (other < 0 || layout.Horizontals[other] == horizontal)
                        continue; // 同じ向きの列は端で既に繋がっている

                    var start = axis + offset;
                    var end = axis + side * Cell * 0.5f;
                    var span = Mathf.Abs(end - start);
                    if (span < 0.01f)
                        continue;
                    var mid = (start + end) * 0.5f;
                    var otherOffset = layout.Offsets[other];
                    CreateHandrail(
                        parent,
                        prefab,
                        $"HandrailCorner_{row}_{col}",
                        horizontal
                            ? new Vector3(Cell * col + otherOffset, 0f, mid)
                            : new Vector3(mid, 0f, Cell * row + otherOffset),
                        !horizontal,
                        span
                    );
                }
            }
        }

        static void CreateHandrail(
            Transform parent,
            GameObject prefab,
            string name,
            Vector3 center,
            bool horizontal,
            float length
        )
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            PlaceModel(go, prefab, center, Quaternion.Euler(0f, horizontal ? 0f : 90f, 0f));
            // 伸ばすのは長さ方向だけ。PlaceModel が入れたプレハブのスケールを上書きする。
            go.transform.localScale = new Vector3(length / HandrailLength, 1f, 1f);

            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, HandrailHeight * 0.5f, 0f);
            box.size = new Vector3(HandrailLength, HandrailHeight, 0.6f);
        }

        /// <summary>
        /// glb のプレハブを置く。**プレハブ自身の Transform を潰さない**のが要点:
        /// glTF のノード変換にモデルの配置オフセット(原点からのずれ)が入っているので、
        /// localPosition を 0 にすると床に沈んだり横にずれたりする
        /// (例: ClassroomDoor-V は +0.68 / +1.16 を持っている)。目標の位置・向きに
        /// プレハブのオフセットを「足して」置く。
        /// </summary>
        static void PlaceModel(
            GameObject go,
            GameObject prefab,
            Vector3 pos,
            Quaternion rot,
            float scale = 1f
        )
        {
            var offset = prefab.transform.localPosition;
            go.transform.localPosition = pos + rot * (offset * scale);
            go.transform.localRotation = rot * prefab.transform.localRotation;
            go.transform.localScale = prefab.transform.localScale * scale;
        }

        /// <summary>
        /// 図の上でそのマスを通る壁の線が東西向きか。左右が壁なら東西、そうでなく上下が壁なら南北。
        /// 図の外は壁扱い(外周に接する扉・ガラスも向きが決まる)。
        /// </summary>
        static bool IsEastWest(char[,] grid, int rows, int cols, int r, int c)
        {
            bool WallLike(int y, int x)
            {
                if (y < 0 || x < 0 || y >= rows || x >= cols)
                    return true;
                var ch = grid[y, x];
                return ch == '#' || ch == Glass || IsDoor(ch);
            }

            if (WallLike(r, c - 1) && WallLike(r, c + 1))
                return true;
            return !(WallLike(r - 1, c) && WallLike(r + 1, c));
        }

        /// <summary>
        /// 線状に並ぶもの(ガラス壁 `$` / 柵 `-`)の列(run)の並びと、列ごとの
        /// 「マスの中心からのずらし量」。角で先端同士を突き合わせるには、隣の列がどの面に
        /// 立っているかを知る必要があるので、先に全部の列を求めてから置く。
        /// </summary>
        readonly struct LineLayout
        {
            public readonly List<FenceRun> Runs;
            public readonly float[] Offsets; // 列に直交する向きのずらし量(±Cell/2 か 0)
            public readonly bool[] Horizontals;
            public readonly int[,] RunOfCell; // マス -> 列の番号(無ければ -1)

            LineLayout(List<FenceRun> runs, float[] offsets, bool[] horizontals, int[,] runOfCell)
            {
                Runs = runs;
                Offsets = offsets;
                Horizontals = horizontals;
                RunOfCell = runOfCell;
            }

            public static LineLayout Build(
                char[,] grid,
                bool[,] hasFloor,
                int rows,
                int cols,
                char symbol
            )
            {
                var runs = SymbolRuns(grid, rows, cols, symbol);
                var offsets = new float[runs.Count];
                var horizontals = new bool[runs.Count];
                var runOfCell = new int[rows, cols];
                for (var r = 0; r < rows; r++)
                for (var c = 0; c < cols; c++)
                    runOfCell[r, c] = -1;

                for (var i = 0; i < runs.Count; i++)
                {
                    var run = runs[i];
                    // 1マスだけの列は線の向きが決まらないので、周りの壁から向きを取る
                    horizontals[i] =
                        run.Length > 1
                            ? run.Horizontal
                            : IsEastWest(grid, rows, cols, run.Row, run.Col);
                    offsets[i] = MapLayoutBuilder.EdgeOffset(
                        hasFloor,
                        rows,
                        cols,
                        run,
                        horizontals[i]
                    );
                    for (var k = 0; k < run.Length; k++)
                        runOfCell[
                            run.Row + (horizontals[i] ? 0 : k),
                            run.Col + (horizontals[i] ? k : 0)
                        ] = i;
                }
                return new LineLayout(runs, offsets, horizontals, runOfCell);
            }
        }

        /// <summary>
        /// ガラス・柵の寄せ量を<b>階をまたいで揃える</b>。対象は<b>縦(南北)の列だけ</b>で、
        /// キーは<b>列番号</b>。真上から見て同じ線に乗るべき面を1つに揃えるのが目的。
        /// (例: 3F の col 89 は `dd` 階段の吹き抜けに面して西端へ寄るので、1F の col 89 も西端へ揃える)
        ///
        /// <b>横(東西)の列は揃えない。</b> 階ごとに Z 原点が 10マスずれているので「同じ世界行」に
        /// 来る横の列は、列の範囲が重ならない<b>別の壁</b>であることが多い
        /// (2F row 20 の研究室正面 cols 5-83 と 3F row 10 の南の縁 cols 89-98 が同じ世界行30)。
        /// 揃えると無関係な壁を動かしてしまう。
        ///
        /// 扉は<b>自分が乗る壁の面に追従する</b>(<see cref="WallOffsetAt"/>)。追従しないと、寄せた
        /// ガラスと扉の袖・垂れが 2u 食い違って壁に隙間が空く。
        ///
        /// 同じ列で逆向きの寄せが要求された場合は揃えずに警告する(どちらかが必ず不正になるため)。
        /// </summary>
        static void AlignAcrossFloors(LineLayout[] layouts, List<char[,]> grids, string what)
        {
            var want = new Dictionary<int, float>();
            var conflict = new HashSet<int>();

            for (var f = 0; f < layouts.Length; f++)
            for (var i = 0; i < layouts[f].Runs.Count; i++)
            {
                if (layouts[f].Horizontals[i])
                    continue;
                var off = layouts[f].Offsets[i];
                if (Mathf.Approximately(off, 0f))
                    continue;
                var key = layouts[f].Runs[i].Col;
                if (want.TryGetValue(key, out var prev) && !Mathf.Approximately(prev, off))
                {
                    conflict.Add(key);
                    continue;
                }
                want[key] = off;
            }

            foreach (var k in conflict)
            {
                want.Remove(k);
                Debug.LogWarning(
                    $"[MapLayoutBuilder] {what}: 列 {k} で階ごとに逆向きの寄せが要求されました。"
                        + "この列は階ごとの判定に任せます。"
                );
            }

            var changed = 0;
            for (var f = 0; f < layouts.Length; f++)
            for (var i = 0; i < layouts[f].Runs.Count; i++)
            {
                if (layouts[f].Horizontals[i])
                    continue;
                if (!want.TryGetValue(layouts[f].Runs[i].Col, out var off))
                    continue;
                if (Mathf.Approximately(layouts[f].Offsets[i], off))
                    continue;
                layouts[f].Offsets[i] = off;
                changed++;
            }
            if (changed > 0)
                Debug.Log(
                    $"[MapLayoutBuilder] {what}: {changed} 本の縦の列を他の階の面に揃えました。"
                );
        }

        /// <summary>
        /// 板・柵を「マスの中心」ではなく「床の縁」へ寄せる量。
        /// 直交方向の隣に<b>床が無い</b>マスが1つでもあれば、その側の
        /// マス境界(±2u)まで列ごとまとめて寄せる。中心に立てたままだと、そのマスの床が
        /// ガラス・柵の外へ 2u はみ出し、<b>柵の外側に立てる足場</b>ができてしまうため
        /// (落ちられる / 本来行けない所へ行ける)。列の途中で寄せ方を変えると
        /// 線が食い違うので、判断は<b>列単位</b>で行う。
        ///
        /// 「床が無い」の判定には<b>床の生成に使ったマスク</b>を使う。図の空白だけでなく
        /// <b>階段の吹き抜け</b>(上階では階段マスが穴になる)も床が無いので、そこに面した
        /// 柵・ガラスも縁へ寄せないと吹き抜けの縁に足場が残る。
        /// </summary>
        static float EdgeOffset(bool[,] hasFloor, int rows, int cols, FenceRun run, bool horizontal)
        {
            bool voidMinus = false;
            bool voidPlus = false;
            for (var k = 0; k < run.Length; k++)
            {
                var row = run.Row + (horizontal ? 0 : k);
                var col = run.Col + (horizontal ? k : 0);
                foreach (var side in new[] { -1, 1 })
                {
                    var nr = row + (horizontal ? side : 0);
                    var nc = col + (horizontal ? 0 : side);
                    bool empty = nr < 0 || nc < 0 || nr >= rows || nc >= cols || !hasFloor[nr, nc];
                    if (!empty)
                        continue;
                    if (side < 0)
                        voidMinus = true;
                    else
                        voidPlus = true;
                }
            }
            if (voidMinus == voidPlus) // 両側とも床がある / 両側とも無い → 中心のまま
                return 0f;
            return voidMinus ? -Cell * 0.5f : Cell * 0.5f;
        }

        /// <summary>
        /// ガラス壁。モデルは 1マス × 階高ちょうどなので、列の各マスに等倍で1枚ずつ置く
        /// (引き伸ばすと方立と無目が歪む)。列は床の縁に接していれば縁へ寄せる
        /// (<see cref="LineLayout"/>)。当たりは列全体を1つの薄い箱で作る:
        /// `#` と同じ4u厚の箱にすると、透けて見えているガラスの2u手前で止まってしまう。
        /// </summary>
        static void CreateGlassWall(
            Transform parent,
            GameObject prefab,
            char[,] grid,
            int rows,
            int cols,
            LineLayout layout,
            int runIndex,
            Material wallMat
        )
        {
            var run = layout.Runs[runIndex];
            var horizontal = layout.Horizontals[runIndex];
            var offset = layout.Offsets[runIndex];
            var yaw = horizontal ? 0f : 90f;

            Vector3 Place(int row, int col, float along) =>
                new Vector3(
                    Cell * col + (horizontal ? 0f : offset),
                    0f,
                    Cell * row + (horizontal ? offset : 0f)
                ) + (horizontal ? new Vector3(along, 0f, 0f) : new Vector3(0f, 0f, along));

            if (prefab != null)
                for (var i = 0; i < run.Length; i++)
                {
                    var row = run.Row + (horizontal ? 0 : i);
                    var col = run.Col + (horizontal ? i : 0);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    go.name = $"GlassWall_{row}_{col}";
                    PlaceModel(go, prefab, Place(row, col, 0f), Quaternion.Euler(0f, yaw, 0f));
                }

            for (var i = 0; i < run.Length; i++)
            {
                var row = run.Row + (horizontal ? 0 : i);
                var col = run.Col + (horizontal ? i : 0);
                var axis = horizontal ? Cell * row : Cell * col; // 直交方向のマス中心
                foreach (var side in new[] { -1, 1 })
                {
                    var nr = row + (horizontal ? side : 0);
                    var nc = col + (horizontal ? 0 : side);
                    if (nr < 0 || nc < 0 || nr >= rows || nc >= cols)
                        continue;

                    // この板の面から、隣のマスとの境界まで
                    var start = axis + offset;
                    var end = axis + side * Cell * 0.5f;
                    var span = Mathf.Abs(end - start);
                    if (span < 0.01f)
                        continue;
                    var mid = (start + end) * 0.5f;

                    var neighbour = grid[nr, nc];
                    if (neighbour == Glass)
                    {
                        // ガラスが直角に折れる角。相手の列の面まで届くガラスを1枚足して、
                        // 縦の面と横の面の先端を突き合わせる(塞ぐと角だけ不透明になる)。
                        var other = layout.RunOfCell[nr, nc];
                        if (other >= 0 && layout.Horizontals[other] != horizontal)
                            CreateGlassCorner(
                                parent,
                                prefab,
                                row,
                                col,
                                horizontal,
                                mid,
                                span,
                                layout.Offsets[other]
                            );
                        continue;
                    }
                    if (neighbour != '#' && !IsDoor(neighbour))
                        continue;

                    // 厚い壁・扉に突き当たる側は見通す必要がないので袖壁で塞ぐ。
                    var gapStart = start + side * GlassPanelDepth * 0.5f;
                    var gap = Mathf.Abs(end - gapStart);
                    if (gap < 0.01f)
                        continue;
                    var gapMid = (gapStart + end) * 0.5f;
                    CreateFiller(
                        parent,
                        $"GlassJamb_{row}_{col}",
                        new Vector3(
                            horizontal ? Cell * col : gapMid,
                            FloorHeight * 0.5f,
                            horizontal ? gapMid : Cell * row
                        ),
                        horizontal
                            ? new Vector3(Cell, FloorHeight, gap)
                            : new Vector3(gap, FloorHeight, Cell),
                        wallMat
                    );
                }
            }

            var length = Cell * run.Length;
            var collider = new GameObject($"GlassWallCollider_{run.Row}_{run.Col}");
            collider.transform.SetParent(parent, false);
            collider.transform.localPosition =
                Place(run.Row, run.Col, Cell * (run.Length - 1) * 0.5f)
                + new Vector3(0f, FloorHeight * 0.5f, 0f);
            var box = collider.AddComponent<BoxCollider>();
            box.size = horizontal
                ? new Vector3(length, FloorHeight, GlassColliderThickness)
                : new Vector3(GlassColliderThickness, FloorHeight, length);
        }

        /// <summary>
        /// 扉のマス。扉モデルは等倍で置き(プレイヤー基準の実寸なのでマスより小さい)、
        /// 残りの開口 — 左右の袖壁と扉の上の垂れ壁 — を壁で埋める。当たりは埋めた壁だけが持ち、
        /// 扉自体はコライダーを持たないので通れる(開閉は扱わない)。
        /// 扉の表(サイン面)は廊下側(<see cref="FacesPositive"/>で判定)を向く。
        /// </summary>
        /// <summary>
        /// 扉のマスが乗る壁(ガラス/柵の列)の寄せ量。壁が縁へ寄っているのに扉だけマス中心に置くと、
        /// 壁と扉の袖・垂れが 2u 食い違って隙間が空くので、扉も同じ面へずらす。
        /// 壁の向きに沿った隣のマスから、その列の寄せ量をもらう。
        /// </summary>
        static float WallOffsetAt(
            LineLayout layout,
            int rows,
            int cols,
            int row,
            int col,
            bool horizontal
        )
        {
            foreach (var side in new[] { -1, 1 })
            {
                var r = row + (horizontal ? 0 : side);
                var c = col + (horizontal ? side : 0);
                if (r < 0 || c < 0 || r >= rows || c >= cols)
                    continue;
                var idx = layout.RunOfCell[r, c];
                if (idx < 0 || layout.Horizontals[idx] != horizontal)
                    continue;
                return layout.Offsets[idx];
            }
            return 0f;
        }

        static void CreateDoor(
            Transform parent,
            GameObject prefab,
            DoorDef def,
            int row,
            int col,
            bool horizontal,
            bool facesPositive,
            Material glassMat,
            float wallOffset
        )
        {
            var root = new GameObject($"Door_{def.Symbol}_{row}_{col}");
            root.transform.SetParent(parent, false);
            // 壁の面に合わせて、壁と直交する向きへずらす
            root.transform.localPosition = new Vector3(
                Cell * col + (horizontal ? 0f : wallOffset),
                0f,
                Cell * row + (horizontal ? wallOffset : 0f)
            );
            // 表(サイン面)は広い方=廊下側へ。裏返すときは 180° 回す(袖壁も一緒に回る)。
            var yaw = (horizontal ? 0f : 90f) + (facesPositive ? 0f : 180f);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            if (prefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                go.name = $"{def.Symbol}_Door";
                PlaceModel(go, prefab, Vector3.zero, Quaternion.identity, DoorScale);
                AddDoorInteraction(root, go, DoorScale);
            }

            // 外形は拡大後の寸法で埋める(埋め残し / はみ出しを防ぐ)
            var minX = def.MinX * DoorScale;
            var maxX = def.MaxX * DoorScale;
            var height = def.Height * DoorScale;

            var half = Cell * 0.5f;
            // 袖は左右で幅が違う(扉の原点は開口の中心で、外形の中心ではない)
            CreateFiller(
                root.transform,
                "SideL",
                new Vector3((-half + minX) * 0.5f, FloorHeight * 0.5f, 0f),
                new Vector3(minX + half, FloorHeight, DoorGlassThickness),
                glassMat
            );
            CreateFiller(
                root.transform,
                "SideR",
                new Vector3((maxX + half) * 0.5f, FloorHeight * 0.5f, 0f),
                new Vector3(half - maxX, FloorHeight, DoorGlassThickness),
                glassMat
            );
            CreateFiller(
                root.transform,
                "Lintel",
                new Vector3((minX + maxX) * 0.5f, (height + FloorHeight) * 0.5f, 0f),
                new Vector3(maxX - minX, FloorHeight - height, DoorGlassThickness),
                glassMat
            );
        }

        /// <summary>
        /// 扉の表(サイン面)を廊下側に向ける。廊下は階全体に繋がっていて広く、部屋は壁で
        /// 囲まれていて狭いので、<b>両側の歩ける床の広さ</b>で判定する。
        /// 「奥行き」で測ると部屋の方が深いことがある(3F の教室は5マス、廊下は4マス)ため、
        /// 面積で見るのが確実。true なら +Z(図の行番号が大きい側)を向ける。
        /// </summary>
        static bool FacesPositive(
            char[,] grid,
            int rows,
            int cols,
            int row,
            int col,
            bool horizontal
        )
        {
            var plus = ReachableFloor(
                grid,
                rows,
                cols,
                row + (horizontal ? 1 : 0),
                col + (horizontal ? 0 : 1)
            );
            var minus = ReachableFloor(
                grid,
                rows,
                cols,
                row - (horizontal ? 1 : 0),
                col - (horizontal ? 0 : 1)
            );
            return plus >= minus; // 引き分け(両側とも塞がり/同じ広さ)は既定の +Z
        }

        /// <summary>
        /// そのマスから歩いて行ける床の数。広さの比較にしか使わないので上限で打ち切る。
        /// <b>出入口(`+` と扉)は通らない</b>: 通してしまうと部屋と廊下が繋がって同じ広さになり、
        /// どちらが廊下か判定できなくなる(部屋は「出入口でしか出られない狭い所」で見分ける)。
        /// </summary>
        static int ReachableFloor(char[,] grid, int rows, int cols, int startRow, int startCol)
        {
            const int Cap = 600;
            bool Walkable(int r, int c) =>
                r >= 0
                && c >= 0
                && r < rows
                && c < cols
                && (grid[r, c] == '.' || IsStair(grid[r, c]));

            if (!Walkable(startRow, startCol))
                return 0;

            var seen = new HashSet<(int, int)> { (startRow, startCol) };
            var queue = new Queue<(int, int)>();
            queue.Enqueue((startRow, startCol));
            var count = 0;
            while (queue.Count > 0 && count < Cap)
            {
                var (r, c) = queue.Dequeue();
                count++;
                foreach (var (dr, dc) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var nr = r + dr;
                    var nc = c + dc;
                    if (!Walkable(nr, nc) || !seen.Add((nr, nc)))
                        continue;
                    queue.Enqueue((nr, nc));
                }
            }
            return count;
        }

        static bool IsStair(char c) => c >= 'a' && c <= 'd';

        /// <summary>
        /// ガラスが直角に折れる角を、隣の列の面まで届く**ガラスの半コマ**で継ぐ。
        /// 板は1マス1枚・マスの中心に立つので、角では片方の面が半コマ(2u)手前で終わる。
        /// 半コマは幅を 0.5 倍にしたガラスで、マスの中心から隣との境界までを埋める
        /// (方立の間隔だけが角で半分になるが、面同士はぴったり突き合う)。
        /// 当たりも同じ長さで足す(run のコライダーはこの半コマを覆っていないため)。
        /// </summary>
        static void CreateGlassCorner(
            Transform parent,
            GameObject prefab,
            int row,
            int col,
            bool horizontal,
            float mid,
            float span,
            float otherOffset
        )
        {
            // 継ぐのは「隣の列の面」なので、向きはこの列と直角になり、
            // 面の位置(直交方向)は隣の列のずらし量に合わせる。
            float yaw = horizontal ? 90f : 0f;
            var center = horizontal
                ? new Vector3(Cell * col + otherOffset, 0f, mid)
                : new Vector3(mid, 0f, Cell * row + otherOffset);

            if (prefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = $"GlassCorner_{row}_{col}";
                PlaceModel(go, prefab, center, Quaternion.Euler(0f, yaw, 0f));
                var scale = go.transform.localScale;
                scale.x *= span / Cell; // 板の幅方向(モデルのローカル X)を継ぐ長さに合わせる
                go.transform.localScale = scale;
            }

            var collider = new GameObject($"GlassCornerCollider_{row}_{col}");
            collider.transform.SetParent(parent, false);
            collider.transform.localPosition = center + new Vector3(0f, FloorHeight * 0.5f, 0f);
            var box = collider.AddComponent<BoxCollider>();
            box.size = horizontal
                ? new Vector3(GlassColliderThickness, FloorHeight, span)
                : new Vector3(span, FloorHeight, GlassColliderThickness);
        }

        /// <summary>
        /// 扉を「近づいて開けられる」状態にする。近接判定の球トリガー(<see cref="DoorInteractor"/>)と
        /// 引き戸の開閉(<see cref="SlidingDoor"/>)を扉ルートに付け、モデル側の扉板を割り当てる。
        /// 扉板は glb で別オブジェクトとして書き出してある("Leaf")。結合された古い glb では
        /// 見つからないので、その場合は警告だけ出して当たり・見た目はそのままにする。
        /// </summary>
        static void AddDoorInteraction(GameObject root, GameObject model, float modelScale = 1f)
        {
            var leaf = SlidingDoor.FindLeaf(model.transform);
            if (leaf == null)
            {
                Debug.LogWarning(
                    $"[MapLayoutBuilder] {root.name}: 扉板 \"{SlidingDoor.DefaultLeafName}\" が "
                        + "モデルにありません(結合された古い glb?)。開閉は付けません。"
                );
                return;
            }

            var door = root.AddComponent<SlidingDoor>();
            var so = new SerializedObject(door);
            so.FindProperty("_leaf").objectReferenceValue = leaf;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 扉板そのものに当たりを付ける。開くと扉板ごと袖壁の内側へ逃げるので、
            // 「閉まっていれば通れない / 開ければ通れる」が当たりの付け外し無しで成立する。
            // 厚みは板の実寸ではなく 0.4u にする(薄すぎるとすり抜ける)。
            // size はモデルのローカル空間なので、拡大して置いた扉では拡大率で割って
            // ワールドでの厚みを 0.4u に保つ(割らないと 2.5 倍で 1.0u になりガラス面から飛び出す)。
            var mesh = leaf.GetComponentInChildren<Renderer>();
            if (mesh != null)
            {
                var local = mesh.localBounds;
                var block = mesh.gameObject.AddComponent<BoxCollider>();
                block.center = local.center;
                block.size = new Vector3(
                    local.size.x,
                    local.size.y,
                    DoorLeafColliderDepth / Mathf.Max(modelScale, 0.0001f)
                );
            }

            // 近づいたことの判定。壁の厚み(4u)を通り抜ける前に出したいので、扉の前後に届く半径にする。
            var zone = root.AddComponent<SphereCollider>();
            zone.isTrigger = true;
            zone.radius = DoorInteractRadius;
            zone.center = new Vector3(0f, DoorInteractHeight, 0f);

            var interactor = root.AddComponent<DoorInteractor>();
            var iso = new SerializedObject(interactor);
            iso.FindProperty("_door").objectReferenceValue = door;
            iso.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>扉の周りを埋める壁片。`#` の壁と同じ4u厚なので隣の壁と面が揃う。</summary>
        static void CreateFiller(
            Transform parent,
            string name,
            Vector3 center,
            Vector3 size,
            Material mat
        )
        {
            if (size.x <= 0f || size.y <= 0f)
                return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void CreateStairs(Transform parent, GameObject prefab, StairCells s)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = $"Stairs_{s.Letter}";
            var width = Cell * (s.LowerCol1 - s.LowerCol0 + 1);
            var run = Cell * (s.LowerRow1 - s.LowerRow0 + 1);

            // 南端(下側)の床にピボットを置き、Y180 で +Z へ登らせる(モデルは -Z へ登る)
            var basePos = new Vector3(
                Cell * (s.LowerCol0 + s.LowerCol1) * 0.5f,
                0f,
                Cell * s.LowerRow0 - Cell * 0.5f
            );
            var yScale = FloorHeight / StairsRise;
            go.transform.localPosition = basePos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = new Vector3(width / StairsWidth, yScale, run / StairsRun);

            // モデルは見た目だけ。当たりは斜面の箱で別に作る
            foreach (var col in go.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(col);
            CreateStairsRamp(parent, s, basePos, width, run, yScale);
        }

        /// <summary>
        /// 階段の当たり。段形状だと蹴上げ(階高/段数)が CharacterController の StepOffset を超えて登れないので、
        /// 段鼻を通る斜面の箱にする。斜面は1踏面ぶん手前から始めて下階の床面と、上端で上階の床面と面一になる。
        /// 左右には手すりぶんの壁を立てて、モデルの手すりを擦り抜けないようにする。
        /// </summary>
        static void CreateStairsRamp(
            Transform parent,
            StairCells s,
            Vector3 basePos,
            float width,
            float run,
            float yScale
        )
        {
            var root = new GameObject($"StairsRamp_{s.Letter}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = basePos;

            var tread = run / StairsSteps;

            // 段鼻は「1踏面ぶん手前で床面(Y=0)に届く直線」の上に並ぶ。その直線をそのまま斜面にする。
            // 上端は最上段の1つ手前の段鼻(= 上階の床面)で終わるので、最上段の踏面は下の Top で埋める
            var slope = new GameObject("Slope");
            slope.transform.SetParent(root.transform, false);
            slope.transform.localPosition = new Vector3(
                0f,
                FloorHeight * 0.5f + RampLift,
                run * 0.5f - tread
            );
            slope.transform.localRotation = Quaternion.Euler(
                -Mathf.Atan2(FloorHeight, run) * Mathf.Rad2Deg,
                0f,
                0f
            );

            var length = Mathf.Sqrt(run * run + FloorHeight * FloorHeight);
            var ramp = slope.AddComponent<BoxCollider>();
            ramp.center = new Vector3(0f, -RampThickness * 0.5f, 0f);
            ramp.size = new Vector3(width, RampThickness, length);

            // モデルの手すりを擦り抜けないように左右に壁を立てる
            var railHeight = StairsRailTop * yScale;
            foreach (var side in new[] { -1f, 1f })
            {
                var rail = slope.AddComponent<BoxCollider>();
                rail.center = new Vector3(side * width * 0.5f, railHeight * 0.5f, 0f);
                rail.size = new Vector3(RampRailThickness, railHeight, length);
            }

            // 最上段の踏面。上階の床は吹き抜けで抜いてあるので、ここが無いと登りきった所で落ちる
            var top = new GameObject("Top");
            top.transform.SetParent(root.transform, false);
            top.transform.localPosition = new Vector3(0f, FloorHeight, run - tread * 0.5f);
            var landing = top.AddComponent<BoxCollider>();
            landing.center = new Vector3(0f, -RampThickness * 0.5f, 0f);
            landing.size = new Vector3(width, RampThickness, tread);
        }

        static Material GetOrCreateMaterial(string name, Color color)
        {
            var path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            Directory.CreateDirectory(MaterialDir);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------------------------------------------------------------- マス→矩形

        /// <summary>true のマスを、なるべく大きい矩形にまとめる(オブジェクト数を減らすため)。</summary>
        static List<RectInt> MergeRects(bool[,] mask, int rows, int cols)
        {
            var used = new bool[rows, cols];
            var result = new List<RectInt>();

            for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                if (!mask[r, c] || used[r, c])
                    continue;

                var c1 = c;
                while (c1 + 1 < cols && mask[r, c1 + 1] && !used[r, c1 + 1])
                    c1++;

                var r1 = r;
                while (
                    r1 + 1 < rows
                    && Enumerable.Range(c, c1 - c + 1).All(x => mask[r1 + 1, x] && !used[r1 + 1, x])
                )
                    r1++;

                for (var y = r; y <= r1; y++)
                for (var x = c; x <= c1; x++)
                    used[y, x] = true;

                result.Add(new RectInt(c, r, c1 - c + 1, r1 - r + 1));
            }

            return result;
        }

        readonly struct FenceRun
        {
            public readonly int Row,
                Col,
                Length;
            public readonly bool Horizontal;

            public FenceRun(int row, int col, int length, bool horizontal)
            {
                Row = row;
                Col = col;
                Length = length;
                Horizontal = horizontal;
            }
        }

        /// <summary>同じ記号のマスを矩形ではなく線分にまとめる。まず東西、残りを南北、最後に1マスずつ。</summary>
        static List<FenceRun> SymbolRuns(char[,] grid, int rows, int cols, char symbol)
        {
            var used = new bool[rows, cols];
            var runs = new List<FenceRun>();
            bool IsFence(int r, int c) => grid[r, c] == symbol && !used[r, c];

            for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                if (!IsFence(r, c))
                    continue;
                var len = 0;
                while (c + len < cols && IsFence(r, c + len))
                    len++;
                if (len < 2)
                    continue;
                for (var i = 0; i < len; i++)
                    used[r, c + i] = true;
                runs.Add(new FenceRun(r, c, len, true));
            }

            for (var c = 0; c < cols; c++)
            for (var r = 0; r < rows; r++)
            {
                if (!IsFence(r, c))
                    continue;
                var len = 0;
                while (r + len < rows && IsFence(r + len, c))
                    len++;
                for (var i = 0; i < len; i++)
                    used[r + i, c] = true;
                runs.Add(new FenceRun(r, c, len, false));
            }

            return runs;
        }

        /// <summary>
        /// 1本の階段。同じワールド位置でも階ごとに図の row が違う(階のZ原点がずれているため)ので、
        /// 下の階の座標(階段本体を置く)と上の階の座標(床を抜く)を別々に持つ。
        /// </summary>
        readonly struct StairCells
        {
            public readonly char Letter;
            public readonly int LowerFloor,
                UpperFloor;
            public readonly int LowerRow0,
                LowerRow1,
                LowerCol0,
                LowerCol1;
            public readonly int UpperRow0,
                UpperRow1,
                UpperCol0,
                UpperCol1;

            public StairCells(
                char letter,
                int lower,
                int upper,
                int lowerRow0,
                int lowerRow1,
                int lowerCol0,
                int lowerCol1,
                int upperRow0,
                int upperRow1,
                int upperCol0,
                int upperCol1
            )
            {
                Letter = letter;
                LowerFloor = lower;
                UpperFloor = upper;
                LowerRow0 = lowerRow0;
                LowerRow1 = lowerRow1;
                LowerCol0 = lowerCol0;
                LowerCol1 = lowerCol1;
                UpperRow0 = upperRow0;
                UpperRow1 = upperRow1;
                UpperCol0 = upperCol0;
                UpperCol1 = upperCol1;
            }
        }

        /// <summary>同じ文字が2つの階に出てくるのが1本の階段。若い階が下、もう一方が上。</summary>
        static List<StairCells> CollectStairs(List<char[,]> grids)
        {
            var result = new List<StairCells>();

            for (var letter = 'a'; letter <= 'z'; letter++)
            {
                var found = new List<(int Floor, int R0, int R1, int C0, int C1)>();
                for (var f = 0; f < grids.Count; f++)
                {
                    var grid = grids[f];
                    int r0 = int.MaxValue,
                        r1 = -1,
                        c0 = int.MaxValue,
                        c1 = -1;
                    for (var r = 0; r < grid.GetLength(0); r++)
                    for (var c = 0; c < grid.GetLength(1); c++)
                    {
                        if (grid[r, c] != letter)
                            continue;
                        r0 = Mathf.Min(r0, r);
                        r1 = Mathf.Max(r1, r);
                        c0 = Mathf.Min(c0, c);
                        c1 = Mathf.Max(c1, c);
                    }
                    if (r1 >= 0)
                        found.Add((f, r0, r1, c0, c1));
                }

                if (found.Count == 0)
                    continue;
                if (found.Count != 2)
                {
                    Debug.LogWarning(
                        $"[MapLayoutBuilder] 階段 '{letter}' が {found.Count} 階に出ています(2階ぶん必要)。とばします。"
                    );
                    continue;
                }

                var lower = found[0];
                var upper = found[1];
                var rowOffset = Mathf.RoundToInt(
                    (Floors[upper.Floor].Origin.z - Floors[lower.Floor].Origin.z) / Cell
                );
                if (lower.R0 != upper.R0 + rowOffset || lower.C0 != upper.C0)
                    Debug.LogWarning(
                        $"[MapLayoutBuilder] 階段 '{letter}': 上下の階でワールド座標が合っていません"
                            + $"(下 row {lower.R0}〜{lower.R1} / 上 row {upper.R0}〜{upper.R1})。"
                    );

                result.Add(
                    new StairCells(
                        letter,
                        lower.Floor,
                        upper.Floor,
                        lower.R0,
                        lower.R1,
                        lower.C0,
                        lower.C1,
                        upper.R0,
                        upper.R1,
                        upper.C0,
                        upper.C1
                    )
                );
            }

            return result;
        }
    }
}
#endif
