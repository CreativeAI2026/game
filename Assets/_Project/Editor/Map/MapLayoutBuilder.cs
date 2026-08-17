#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
            BuildInto(grids);

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

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BuildInto(grids);
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
            BuildInto(grids);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[MapLayoutBuilder] Map ルートを作り直しました。");
        }

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
                if (block.Values.Any(s => s.Length != cols))
                    Debug.LogWarning(
                        $"[MapLayoutBuilder] {Floors[f].Name}: 行ごとに文字数が違います。短い行は壁で埋めます。"
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

        static void BuildInto(List<char[,]> grids)
        {
            var old = GameObject.Find("/" + MapRootName);
            if (old != null)
                Object.DestroyImmediate(old);

            var wallMat = GetOrCreateMaterial("Map_Wall", new Color(0.72f, 0.72f, 0.70f));
            var floorMat = GetOrCreateMaterial("Map_Floor", new Color(0.42f, 0.44f, 0.47f));
            var handrail = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvDir}/Handrail.glb");
            var stairs = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvDir}/Stairs.glb");
            if (handrail == null)
                Debug.LogWarning(
                    "[MapLayoutBuilder] Handrail.glb が見つかりません。柵は生成しません。"
                );
            if (stairs == null)
                Debug.LogWarning(
                    "[MapLayoutBuilder] Stairs.glb が見つかりません。階段は生成しません。"
                );

            var root = new GameObject(MapRootName);
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

            for (var f = 0; f < Floors.Length; f++)
            {
                var grid = grids[f];
                var rows = grid.GetLength(0);
                var cols = grid.GetLength(1);
                var parent = floorRoots[f];

                // 空白は床なし。上の階に着く階段のマスも床を抜く(階段の吹き抜け)。
                // 抜くのは「上の階の図での」マスなので Upper* を使う(下の階の row とは10マスずれる)
                var floorMask = new bool[rows, cols];
                for (var r = 0; r < rows; r++)
                for (var c = 0; c < cols; c++)
                    floorMask[r, c] = grid[r, c] != Void;
                foreach (var s in stairCells.Where(s => s.UpperFloor == f))
                    for (var r = s.UpperRow0; r <= s.UpperRow1; r++)
                    for (var c = s.UpperCol0; c <= s.UpperCol1; c++)
                        floorMask[r, c] = false;

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
                    var fenceGroup = NewGroup("Fences", parent);
                    foreach (var run in FenceRuns(grid, rows, cols))
                        CreateHandrail(fenceGroup, handrail, run);
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

        static void CreateHandrail(Transform parent, GameObject prefab, FenceRun run)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = $"Handrail_{run.Row}_{run.Col}";
            var length = Cell * run.Length;
            go.transform.localPosition = new Vector3(
                Cell * (run.Col + (run.Horizontal ? (run.Length - 1) * 0.5f : 0f)),
                0f,
                Cell * (run.Row + (run.Horizontal ? 0f : (run.Length - 1) * 0.5f))
            );
            go.transform.localRotation = Quaternion.Euler(0f, run.Horizontal ? 0f : 90f, 0f);
            go.transform.localScale = new Vector3(length / HandrailLength, 1f, 1f);

            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, HandrailHeight * 0.5f, 0f);
            box.size = new Vector3(HandrailLength, HandrailHeight, 0.6f);
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

        /// <summary>柵は矩形ではなく線分にまとめる。まず東西、残りを南北、最後に1マスずつ。</summary>
        static List<FenceRun> FenceRuns(char[,] grid, int rows, int cols)
        {
            var used = new bool[rows, cols];
            var runs = new List<FenceRun>();
            bool IsFence(int r, int c) => grid[r, c] == '-' && !used[r, c];

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
