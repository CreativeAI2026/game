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
    /// documents/ の文字マップ(MapLayout.md ほか、建物ごとに1枚)から床・壁・柵・階段を生成する(1マス = 4u)。
    /// 生成物は Map ルート配下だけで毎回作り直す(手置き小物は Map の外なら残る)。Tools &gt; CreativeAI &gt; Map から実行。
    /// </summary>
    public static class MapLayoutBuilder
    {
        public const char Void = ' '; // 床が無いマス
        public const float Cell = 4f; // 1マスの一辺(u)

        const float FloorSlabThickness = 0.4f;

        const string ScenePath = "Assets/_Project/Scenes/Field/Field_Area01.unity";
        const string EnvDir = "Assets/_Project/Art/Models/Environment";
        const string MaterialDir = "Assets/_Project/Art/Materials";
        const string MapRootName = "Map";

        // 建物ごとに「どの図を・ワールドのどこに・どのパーツで」建てるか。
        // 図そのものは .md 側が正。ここは各階の原点(マス[row 0, col 0] の中心)と床の高さ、見た目の部品だけを持つ。
        static readonly Building[] Buildings =
        {
            new Building(
                "Area01",
                "MapLayout.md",
                new[]
                {
                    new FloorDef("1F", new Vector3(-218f, -44.8f, -30f)),
                    new FloorDef("2F", new Vector3(-218f, -36.8f, 10f)),
                    new FloorDef("3F", new Vector3(-218f, -28.8f, 50f)),
                },
                8f,
                Kit.Standard,
                perimeter: true
            ),
            // 研究棟。3F の入口通路の東端(col 68)が Area01 3F の西端 `X`(col 0 / row 37〜38)に接する。
            // 全階で原点が同じ(上下の階が真上に重なる)。外周は図に壁を描いてあるので外周壁は作らない。
            // 階高は 16u(8u〜12u だと広い部屋に対して壁が低く見える)。3F を Area01 の 3F に揃え、下の階を下げている。
            new Building(
                "Area04",
                "MapLayout_Area04.md",
                new[]
                {
                    new FloorDef("1F", new Vector3(-494f, -60.8f, -114f)),
                    new FloorDef("2F", new Vector3(-494f, -44.8f, -114f)),
                    new FloorDef("3F", new Vector3(-494f, -28.8f, -114f)),
                },
                16f,
                Kit.Lab,
                perimeter: false
            ),
        };

        // 外周壁を最上階の壁の上端からさらに伸ばす高さ。最上階は屋根が無いので、縁が低いと外が見えすぎる。
        // 外周壁のある建物では、最上階の `#` も同じ高さまで伸ばす(内側の壁だけ低いと不揃いに見える)
        const float PerimeterExtraHeight = 16f;

        // 外周に開ける出入口。隣の建物へつながる開口で、外周壁をこの階の床の高さから上だけ抜く。
        const char Exit = 'X';

        // 手すりのモデルの実寸(glb をそのまま Scale 1 で置いたときの大きさ)。スケール計算に使う。
        const string HandrailAsset = "Structure/Handrail.glb";
        const float HandrailLength = 6.09f;
        const float HandrailHeight = 1.25f;

        // 階段の当たりは段形状ではなく斜面にする(段の蹴上げが CharacterController の
        // StepOffset を超えるため。歩ける面は斜面、見た目は段々のまま)。
        const float RampThickness = 1f; // 斜面コライダーの厚み(すり抜け防止)
        const float RampLift = 0.03f; // 段鼻の面取りに引っかからないよう斜面を少し持ち上げる
        const float RampRailThickness = 0.4f;

        // ガラス壁 `$`。モデルは 1マス幅(4.00u)なので横は等倍で1マス1枚置き、縦は壁の高さへ合わせる。
        // 当たりは薄い箱にする(`#` のように4u厚だとガラスの手前で止まって見える)。
        const char Glass = '$';
        const char Fence = '-'; // 柵。ガラスと同じく列にまとめ、縁へ寄せ、角で継ぐ
        const float GlassColliderThickness = 0.4f;
        const float GlassPanelDepth = 0.14f; // モデルの厚み(袖壁を作る幅の計算に使う)

        // 扉に近づいたと見なす球トリガー。壁が4u厚なので、扉の手前で気付ける大きさにする。
        const float DoorInteractRadius = 3.2f;
        const float DoorInteractHeight = 1.2f; // プレイヤー(身長1.8u)の胴の高さ
        const float DoorLeafColliderDepth = 0.4f; // 扉板の当たりの厚み(実寸0.17uだと薄すぎる)

        // 扉の拡大率。glb はプレイヤー(1.8u)基準の実寸だが、階高 8u の建物に対して小さすぎて
        // 「人が通る所」に見えない。2.5 倍で `R` は外形 3.28 × 5.68u(1マス4uに収まる) /
        // 開口 2.55 × 5.40u になる(壁の高さ 7.6u の 3/4 ほど)。
        const float DoorScale = 2.5f;

        // 扉の周りは**壁ではなくガラス**で埋める(袖・垂れ壁)。厚みは `$` の当たりと同じにして
        // 隣のガラス板と面が揃うようにする。見た目は `$` のモデルを開口の外だけ切り出して重ねる。
        const float DoorGlassThickness = GlassColliderThickness;
        const string GlassMaterialPath = "Assets/_Project/Art/Materials/Glass.mat";

        // 扉 `R` `C` `L`。実寸は glb を Scale 1 で置いたときの外形。
        // 原点は開口の中心・床面で、制御パネルのぶん左右非対称。値は **Unity 空間**:
        // glTF -> Unity のインポートで X が反転するので、.glb で見た左右とは逆になる
        // (例: ClassroomDoor-V は glb だと -0.74〜+0.765、Unity では -0.765〜+0.74)。
        static readonly DoorDef[] Doors =
        {
            new DoorDef('R', "Structure/LabDoor-V.glb", -0.685f, 0.620f, 2.270f),
            new DoorDef('C', "Structure/ClassroomDoor-V.glb", -0.765f, 0.740f, 2.320f),
            new DoorDef('L', "Structure/LibraryDoor-V.glb", -0.815f, 0.790f, 2.360f),
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

        sealed class Building
        {
            public readonly string Name;
            public readonly string Doc; // documents/ からのファイル名
            public readonly FloorDef[] Floors;
            public readonly float StoreyHeight; // 階高
            public readonly Kit Kit;
            public readonly bool Perimeter; // 全階を貫く1枚の外周壁で縁を覆うか

            public Building(
                string name,
                string doc,
                FloorDef[] floors,
                float storeyHeight,
                Kit kit,
                bool perimeter
            )
            {
                Name = name;
                Doc = doc;
                Floors = floors;
                StoreyHeight = storeyHeight;
                Kit = kit;
                Perimeter = perimeter;
            }

            // 壁は上の階の床板の下面で止める。階高いっぱいだと壁の上面が上の階の床面と重なってちらつく
            public float WallHeight => StoreyHeight - FloorSlabThickness;

            /// <summary>全階の原点が同じ(上下の階が真上に重なる)か。</summary>
            public bool Stacked =>
                Floors.All(f =>
                    Mathf.Approximately(f.Origin.x, Floors[0].Origin.x)
                    && Mathf.Approximately(f.Origin.z, Floors[0].Origin.z)
                );
        }

        /// <summary>建物の見た目の部品一式。形(図)とは独立に差し替えられる。</summary>
        sealed class Kit
        {
            public string WallMaterial;
            public Color WallColor;
            public string FloorMaterial;
            public Color FloorColor;

            // 床の色分け(```floor の図の記号 → マテリアル)。既にあるマテリアルは色を上書きしない
            public (char Symbol, string Material, Color Color)[] FloorColors = { };

            // `$` のモデル。複数あればマスごとに決まった順で使い分ける(同じ柄が並ぶのを避ける)
            public string[] GlassAssets;
            public float GlassModelHeight; // モデルの実寸の高さ(壁の高さへ伸縮する)

            public float HandrailScale = 1f; // 手すりの高さ・太さの倍率(長さはマスに合わせる)
            public StairsModel Stairs;

            public static readonly Kit Standard = new Kit
            {
                WallMaterial = "Map_Wall",
                WallColor = new Color(0.72f, 0.72f, 0.70f),
                FloorMaterial = "Map_Floor",
                FloorColor = new Color(0.42f, 0.44f, 0.47f),
                GlassAssets = new[] { "Structure/GlassWall-V.glb" },
                GlassModelHeight = 9.6f, // 旧階高ぴったりで作ってある
                // 階段は「歩ける面」の寸法を使う。バウンディングボックス(高さ5.045 / 奥行6.736)は
                // 手すりの上端と踏面のはみ出しを含むので、それで割ると踏面が上階の床に届かない。
                Stairs = new StairsModel(
                    "Structure/Stairs.glb",
                    width: 2.12f, // X幅(手すり込み。吹き抜け2マス=8u に合わせる)
                    rise: 4.08f, // 最上段の踏面まで(0.17 × 24段)。上階の床面に一致させる
                    run: 6.72f, // 踏面の総奥行(0.28 × 24段)
                    steps: 24,
                    railTop: 0.95f
                ),
            };

            // 研究棟(Field_Area04 のモデル)から切り出した部品。Tools/Blender/build_lab_kit.py で作る。
            // 色は元のモデルが Unity で付いていた色に合わせる(赤だけは元のシーンが使っている LabRed 1)。
            public static readonly Kit Lab = new Kit
            {
                WallMaterial = "Lab_Wall",
                WallColor = new Color(0.906f, 0.906f, 0.906f),
                FloorMaterial = "Lab_Floor",
                FloorColor = new Color(0.24f, 0.24f, 0.24f),
                FloorColors = new[]
                {
                    ('B', "Lab_FloorBlue", new Color(0.256f, 0.297f, 0.405f)),
                    ('R', "LabRed 1", new Color(0.547f, 0.194f, 0.246f)),
                    ('Y', "Lab_FloorYellow", new Color(0.62f, 0.612f, 0.243f)),
                },
                GlassAssets = new[]
                {
                    "Structure/Lab/LabLatticeWall_A.glb",
                    "Structure/Lab/LabLatticeWall_B.glb",
                    "Structure/Lab/LabLatticeWall_C.glb",
                },
                GlassModelHeight = 12f, // 1u タイル 12段(壁の高さへ縦に伸ばす)
                HandrailScale = 2.26f, // 元のシーンが Handrail.glb を 2.26 倍で置いていた
                Stairs = new StairsModel(
                    "Structure/Lab/LabStairs.glb",
                    width: 5.876f,
                    rise: 8f,
                    run: 8.211f,
                    steps: 19,
                    railTop: 0.5f // 段板の脇の帯の高さ
                ),
            };
        }

        /// <summary>階段モデルの「歩ける面」の寸法。原点は登り始めの端・下階の床、モデルは -Z へ登る。</summary>
        readonly struct StairsModel
        {
            public readonly string Asset;
            public readonly float Width,
                Rise,
                Run,
                RailTop;
            public readonly int Steps; // 斜面コライダーを段鼻に通すのに使う

            public StairsModel(
                string asset,
                float width,
                float rise,
                float run,
                int steps,
                float railTop
            )
            {
                Asset = asset;
                Width = width;
                Rise = rise;
                Run = run;
                Steps = steps;
                RailTop = railTop;
            }
        }

        /// <summary>`$` のモデル一式(柄違いの複数枚と、その実寸の高さ)。</summary>
        sealed class GlassParts
        {
            public GameObject[] Prefabs;
            public float ModelHeight;

            public GameObject Pick(int row, int col) =>
                Prefabs.Length == 0 ? null : Prefabs[Mathf.Abs(row * 7 + col * 3) % Prefabs.Length];
        }

        /// <summary>1つの建物について .md から読んだ図。</summary>
        sealed class BuildingMap
        {
            public Building Building;
            public List<char[,]> Grids;
            public List<char[,]> FloorColors; // 階ごと。```floor が無い階は null
        }

        [MenuItem("Tools/CreativeAI/Map/Build Field_Area01 From MapLayout")]
        public static void BuildArea01()
        {
            var maps = LoadAll();
            if (maps == null)
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateSun();
            BuildInto(maps, scene);

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

            var maps = LoadAll();
            if (maps == null)
                return;

            // 既に開いていればその場で作り直す(Single で開き直すと、小物用シーンを
            // 開いて作業している最中に実行したときにそれを閉じてしまう)。
            var scene = FindOpenScene(ScenePath);
            if (!scene.IsValid())
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BuildInto(maps, scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.Refresh();
            Debug.Log($"[MapLayoutBuilder] {ScenePath} の Map ルートを作り直しました。");
        }

        /// <summary>いま開いているシーンの Map ルートだけを作り直す(Map の外は触らない)。</summary>
        [MenuItem("Tools/CreativeAI/Map/Rebuild Map In Current Scene")]
        public static void RebuildCurrentScene()
        {
            var maps = LoadAll();
            if (maps == null)
                return;
            // マップシーンが開いていればそちらへ。無ければアクティブなシーンに作る。
            var target = FindOpenScene(ScenePath);
            if (!target.IsValid())
                target = SceneManager.GetActiveScene();
            BuildInto(maps, target);
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
            // 屋根の無い建物に太陽の影を落とすと、下の階の床にカメラ追従の円弧状の光漏れが出る
            light.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // ---------------------------------------------------------------- 読み込み

        static string DocPath(Building b) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "../../documents", b.Doc));

        /// <summary>全建物の図を読む。どれか1つでも読めなければ null(半端な状態で作り直さない)。</summary>
        static List<BuildingMap> LoadAll()
        {
            var maps = new List<BuildingMap>();
            foreach (var b in Buildings)
            {
                var map = LoadMap(b);
                if (map == null)
                    return null;
                maps.Add(map);
            }
            return maps;
        }

        /// <summary>
        /// 1つの建物の .md を読む。```text のブロックが階の図(下の階から順)、```floor のブロックが
        /// 床の色分け(同じく下の階から順、省略可)。どちらも「行番号|」で始まる行だけを拾う。
        /// </summary>
        static BuildingMap LoadMap(Building b)
        {
            var path = DocPath(b);
            if (!File.Exists(path))
            {
                Debug.LogError($"[MapLayoutBuilder] {b.Name}: マップ定義が見つかりません: {path}");
                return null;
            }

            var maps = new List<Dictionary<int, string>>();
            var colors = new List<Dictionary<int, string>>();
            List<Dictionary<int, string>> target = null;
            Dictionary<int, string> current = null;
            var rowPattern = new Regex(@"^\s*(\d+)\|(.*)$");

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.TrimEnd();
                var fence = line.Trim();
                if (current == null && (fence == "```text" || fence == "```floor"))
                {
                    target = fence == "```text" ? maps : colors;
                    current = new Dictionary<int, string>();
                    continue;
                }
                if (current == null)
                    continue;
                if (fence == "```")
                {
                    if (current.Count > 0)
                        target.Add(current);
                    current = null;
                    continue;
                }

                var m = rowPattern.Match(line);
                if (m.Success)
                    current[int.Parse(m.Groups[1].Value)] = m.Groups[2].Value.TrimEnd();
            }

            if (maps.Count < b.Floors.Length)
            {
                Debug.LogError(
                    $"[MapLayoutBuilder] {b.Name}: マップが {maps.Count} 個しか読めませんでした(必要: {b.Floors.Length})。"
                );
                return null;
            }

            var grids = new List<char[,]>();
            var floorColors = new List<char[,]>();
            for (var f = 0; f < b.Floors.Length; f++)
            {
                var grid = ToGrid(maps[f], $"{b.Name} {b.Floors[f].Name}");
                grids.Add(grid);
                floorColors.Add(f < colors.Count ? ToGrid(colors[f], null) : null);
                Debug.Log(
                    $"[MapLayoutBuilder] {b.Name} {b.Floors[f].Name}: "
                        + $"{grid.GetLength(1)} × {grid.GetLength(0)} マス を読みました。"
                );
            }

            return new BuildingMap
            {
                Building = b,
                Grids = grids,
                FloorColors = floorColors,
            };
        }

        /// <summary>行番号 → 文字列 の表を、足りない所を「床なし」で埋めた矩形にする。</summary>
        static char[,] ToGrid(Dictionary<int, string> block, string logName)
        {
            var rows = block.Keys.Max() + 1;
            var cols = block.Values.Max(s => s.Length);
            // 上階を後退させる(1F の上を開ける)ときは行を空にするので、短い行は異常ではない。
            // 足りないぶんは「床なし」で埋まる ─ 意図せず短い行があると床が欠けるので Log で残す。
            var shortRows = Enumerable
                .Range(0, rows)
                .Where(r => (block.TryGetValue(r, out var s) ? s.Length : 0) != cols)
                .ToArray();
            if (logName != null && shortRows.Length > 0)
                Debug.Log(
                    $"[MapLayoutBuilder] {logName}: row [{string.Join(", ", shortRows)}] は "
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
            return grid;
        }

        // ---------------------------------------------------------------- 生成

        /// <summary>
        /// マップ一式を <paramref name="target"/> シーンの中に作り直す。
        /// <b>生成先を明示するのが要点</b>: 小物用シーンをアクティブにして作業している最中に
        /// 実行すると、アクティブなシーン(= 小物側)にマップが生成されてしまうため。
        /// </summary>
        static void BuildInto(List<BuildingMap> maps, Scene target)
        {
            var old = FindMapRoot(target);
            if (old != null)
                Object.DestroyImmediate(old);

            // 扉の周りを埋めるガラス。枠(方立・無目)の無い1枚板にしたいので glb ではなく
            // 透過マテリアルを貼った箱で作る。見つからなければ壁で埋めて続行する。
            var doorGlassMat = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
            if (doorGlassMat == null)
                Debug.LogWarning(
                    $"[MapLayoutBuilder] {GlassMaterialPath} が見つかりません。扉の周りは壁で埋めます。"
                );
            var handrail = LoadModel(HandrailAsset, "柵は生成しません。");

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

            foreach (var map in maps)
                BuildBuilding(root.transform, map, handrail, doorPrefabs, doorGlassMat);

            ApplyStaticFlags(root);
            Selection.activeGameObject = root;
        }

        static GameObject LoadModel(string asset, string fallback)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvDir}/{asset}");
            if (prefab == null)
                Debug.LogWarning($"[MapLayoutBuilder] {asset} が見つかりません。{fallback}");
            return prefab;
        }

        /// <summary>1つの建物を Map/建物名 の下に作る。</summary>
        static void BuildBuilding(
            Transform mapRoot,
            BuildingMap map,
            GameObject handrail,
            Dictionary<char, GameObject> doorPrefabs,
            Material doorGlassMat
        )
        {
            var b = map.Building;
            var kit = b.Kit;
            var floors = b.Floors;
            var grids = map.Grids;

            var wallMat = GetOrCreateMaterial(kit.WallMaterial, kit.WallColor);
            var floorMat = GetOrCreateMaterial(kit.FloorMaterial, kit.FloorColor);
            var floorColorMats = kit.FloorColors.ToDictionary(
                c => c.Symbol,
                c => GetOrCreateMaterial(c.Material, c.Color)
            );
            if (doorGlassMat == null)
                doorGlassMat = wallMat;

            var stairs = LoadModel(kit.Stairs.Asset, "階段は生成しません。");
            var glass = new GlassParts
            {
                Prefabs = kit
                    .GlassAssets.Select(a => LoadModel(a, "`$` のこの柄は使いません。"))
                    .Where(p => p != null)
                    .ToArray(),
                ModelHeight = kit.GlassModelHeight,
            };
            if (glass.Prefabs.Length == 0)
                Debug.LogWarning($"[MapLayoutBuilder] {b.Name}: `$` は当たりだけになります。");

            var buildingRoot = new GameObject(b.Name);
            buildingRoot.transform.SetParent(mapRoot, false);
            var floorRoots = new Transform[floors.Length];
            for (var f = 0; f < floors.Length; f++)
            {
                var go = new GameObject(floors[f].Name);
                go.transform.SetParent(buildingRoot.transform, false);
                go.transform.localPosition = floors[f].Origin;
                floorRoots[f] = go.transform;
            }

            var stairCells = CollectStairs(grids, floors, b.Name);

            var rowOffsets = WorldRowOffsets(floors);
            var worldRows = Enumerable
                .Range(0, floors.Length)
                .Max(f => rowOffsets[f] + grids[f].GetLength(0));
            var worldCols = grids.Max(g => g.GetLength(1));
            var perimeter = b.Perimeter
                ? BuildPerimeterMask(grids, rowOffsets, worldRows, worldCols)
                : new bool[worldRows, worldCols];
            if (b.Perimeter)
                CreatePerimeterWall(
                    buildingRoot.transform,
                    b,
                    grids,
                    rowOffsets,
                    perimeter,
                    worldRows,
                    worldCols,
                    wallMat
                );

            // 床のマスクと、ガラス・柵の列は**先に全階ぶん**作る。寄せ量(EdgeOffset)を
            // 階をまたいで揃えるため ── 同じ列のガラスが階によって 2u ずれると、
            // 建物としては1枚の面のはずのものが食い違って見える。
            var floorMasks = new bool[floors.Length][,];
            var fenceLayouts = new LineLayout[floors.Length];
            var glassLayouts = new LineLayout[floors.Length];
            for (var f = 0; f < floors.Length; f++)
            {
                var g = grids[f];
                var nr = g.GetLength(0);
                var nc = g.GetLength(1);

                // 空白は床なし。上の階に着く階段のマスも床を抜く(階段の吹き抜け)。
                // 抜くのは「上の階の図での」マスなので Upper* を使う(下の階の row とずれることがある)
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
            // 上下の階が真上に重なる建物は横の列も同じ世界行なので揃える
            AlignAcrossFloors(glassLayouts, "ガラス", b.Stacked);
            AlignAcrossFloors(fenceLayouts, "柵", b.Stacked);
            for (var f = 0; f < floors.Length; f++)
                FlushToCorridor(glassLayouts[f], grids[f], floorMasks[f]);

            for (var f = 0; f < floors.Length; f++)
            {
                var grid = grids[f];
                var rows = grid.GetLength(0);
                var cols = grid.GetLength(1);
                var parent = floorRoots[f];
                var floorMask = floorMasks[f];
                var where = $"{b.Name} {floors[f].Name}";

                var floorGroup = NewGroup("Floor", parent);
                foreach (
                    var (mask, mat) in FloorMasksByColor(
                        floorMask,
                        map.FloorColors[f],
                        rows,
                        cols,
                        floorMat,
                        floorColorMats,
                        where
                    )
                )
                foreach (var rect in MergeRects(mask, rows, cols))
                    CreateBox(
                        floorGroup,
                        "Floor",
                        rect,
                        FloorSlabThickness,
                        -FloorSlabThickness * 0.5f,
                        mat
                    );

                // 建物の外周にあたるマスは階ごとに作らない(全階を貫く1枚の外壁で置き換える)
                var wallMask = new bool[rows, cols];
                for (var r = 0; r < rows; r++)
                for (var c = 0; c < cols; c++)
                    wallMask[r, c] = grid[r, c] == '#' && !perimeter[r + rowOffsets[f], c];

                // 外周壁のある建物の最上階は、`#` とガラス・扉の周りを外周壁の上端まで伸ばす(上に床が無いので床板ぶんも縮めない)
                var isTop = Mathf.Approximately(floors[f].Origin.y, floors.Max(x => x.Origin.y));
                var wallHeight =
                    isTop && b.Perimeter ? b.StoreyHeight + PerimeterExtraHeight : b.WallHeight;
                var wallGroup = NewGroup("Walls", parent);
                foreach (var rect in MergeRects(wallMask, rows, cols))
                    CreateBox(wallGroup, "Wall", rect, wallHeight, wallHeight * 0.5f, wallMat);

                // 外周壁のある建物では、上のどの階にも床が無いマス(吹き抜けに面した所)のガラス・扉も
                // 外周壁の上端まで伸ばす。階高で止めると、上の空間に対して囲いが低く見える
                var openHeight =
                    floors.Max(x => x.Origin.y)
                    + b.StoreyHeight
                    + PerimeterExtraHeight
                    - floors[f].Origin.y;
                bool OpenAbove(int r, int c) =>
                    b.Perimeter
                    && Enumerable
                        .Range(0, floors.Length)
                        .Where(g => floors[g].Origin.y > floors[f].Origin.y + 0.01f)
                        .All(g =>
                        {
                            var ug = grids[g];
                            var ur = r + rowOffsets[f] - rowOffsets[g];
                            return ur < 0
                                || ur >= ug.GetLength(0)
                                || c >= ug.GetLength(1)
                                || ug[ur, c] == Void;
                        });

                if (handrail != null)
                {
                    var fenceLayout = fenceLayouts[f];
                    var fenceGroup = NewGroup("Fences", parent);
                    for (var i = 0; i < fenceLayout.Runs.Count; i++)
                        CreateFence(
                            fenceGroup,
                            handrail,
                            kit.HandrailScale,
                            grid,
                            rows,
                            cols,
                            fenceLayout,
                            i
                        );
                }

                // ガラス壁: 見た目は1マス1枚の等倍、当たりは run ごとの薄い箱
                for (var r = 0; r < rows; r++)
                for (var c = 0; c < cols; c++)
                    if (grid[r, c] == Glass && perimeter[r + rowOffsets[f], c])
                        Debug.LogWarning(
                            $"[MapLayoutBuilder] {where} row {r} col {c}: "
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
                            wallMat,
                            RunCells(glassLayout, i).All(rc => OpenAbove(rc.Row, rc.Col))
                                ? openHeight
                                : wallHeight
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
                            $"[MapLayoutBuilder] {where} row {r} col {c}: "
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
                        glass,
                        WallOffsetAt(glassLayout, rows, cols, r, c, eastWest),
                        OpenAbove(r, c) ? openHeight : wallHeight
                    );
                }
            }

            if (stairs != null)
                foreach (var s in stairCells)
                    CreateStairs(
                        NewGroup("Stairs", floorRoots[s.LowerFloor]),
                        stairs,
                        kit.Stairs,
                        s,
                        floors[s.UpperFloor].Origin.y - floors[s.LowerFloor].Origin.y
                    );
        }

        /// <summary>
        /// 床を色ごとのマスクに分ける。```floor の図で記号が書かれたマスはその色、それ以外は既定の床。
        /// 知らない記号は既定の床にして警告する。
        /// </summary>
        static List<(bool[,] Mask, Material Mat)> FloorMasksByColor(
            bool[,] floorMask,
            char[,] colors,
            int rows,
            int cols,
            Material defaultMat,
            Dictionary<char, Material> colorMats,
            string where
        )
        {
            var masks = new Dictionary<Material, bool[,]>();
            var unknown = new HashSet<char>();
            for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                if (!floorMask[r, c])
                    continue;
                var mat = defaultMat;
                if (
                    colors != null
                    && r < colors.GetLength(0)
                    && c < colors.GetLength(1)
                    && colors[r, c] != Void
                    && colors[r, c] != '.'
                )
                {
                    if (!colorMats.TryGetValue(colors[r, c], out mat))
                    {
                        unknown.Add(colors[r, c]);
                        mat = defaultMat;
                    }
                }
                if (!masks.TryGetValue(mat, out var m))
                    masks[mat] = m = new bool[rows, cols];
                m[r, c] = true;
            }
            if (unknown.Count > 0)
                Debug.LogWarning(
                    $"[MapLayoutBuilder] {where}: 床の色の記号 [{string.Join(", ", unknown)}] は"
                        + "定義に無いので既定の床にします。"
                );
            return masks.Select(kv => (kv.Value, kv.Key)).ToList();
        }

        // ---------------------------------------------------------------- Static

        // 固定物はライトマップに焼き、バッチングとオクルージョンカリングの対象にする
        const StaticEditorFlags SolidFlags =
            StaticEditorFlags.ContributeGI
            | StaticEditorFlags.BatchingStatic
            | StaticEditorFlags.OccluderStatic
            | StaticEditorFlags.OccludeeStatic
            | StaticEditorFlags.ReflectionProbeStatic;

        // 透ける物は奥を隠さず、ベイクで影も落とさない(ガラスの真っ黒な影になる)
        const StaticEditorFlags SeeThroughFlags =
            StaticEditorFlags.BatchingStatic
            | StaticEditorFlags.OccludeeStatic
            | StaticEditorFlags.ReflectionProbeStatic;

        /// <summary>Field_Area01 を作り直さずに、今の Map へ Static 設定だけ当てて保存する。</summary>
        [MenuItem("Tools/CreativeAI/Map/Apply Static Flags To Field_Area01")]
        public static void ApplyStaticFlagsToArea01()
        {
            var scene = FindOpenScene(ScenePath);
            if (!scene.IsValid())
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = FindMapRoot(scene);
            if (root == null)
            {
                Debug.LogError($"[MapLayoutBuilder] {ScenePath} に {MapRootName} がありません。");
                return;
            }
            ApplyStaticFlags(root);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Map 配下の見た目に Static を付ける。動く扉板は外す(Static だと開閉しても描画が動かない)。
        /// ライトマップ用 UV(UV2)の無いメッシュはライトマップに焼けないので、光はライトプローブから受ける。
        /// </summary>
        static void ApplyStaticFlags(GameObject root)
        {
            var moving = new HashSet<Transform>();
            foreach (var door in root.GetComponentsInChildren<SlidingDoor>(true))
            {
                var leaf = door.Leaf != null ? door.Leaf : SlidingDoor.FindLeaf(door.transform);
                if (leaf != null)
                    moving.Add(leaf);
            }

            int solid = 0,
                seeThrough = 0,
                probeLit = 0,
                skipped = 0;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var go = renderer.gameObject;
                if (moving.Any(leaf => go.transform.IsChildOf(leaf)))
                {
                    GameObjectUtility.SetStaticEditorFlags(go, 0);
                    skipped++;
                    continue;
                }

                if (IsSeeThrough(renderer))
                {
                    GameObjectUtility.SetStaticEditorFlags(go, SeeThroughFlags);
                    seeThrough++;
                    continue;
                }

                GameObjectUtility.SetStaticEditorFlags(go, SolidFlags);
                solid++;
                var hasLightmapUV =
                    go.TryGetComponent<MeshFilter>(out var filter)
                    && filter.sharedMesh != null
                    && filter.sharedMesh.HasVertexAttribute(
                        UnityEngine.Rendering.VertexAttribute.TexCoord1
                    );
                renderer.receiveGI = hasLightmapUV ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
                // Prefab インスタンスは記録しないとシーン保存時に上書きが残らない
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                if (!hasLightmapUV)
                    probeLit++;
            }

            Debug.Log(
                $"[MapLayoutBuilder] Static を付けました: 固定物 {solid}(うちUV2なしでプローブ受光 {probeLit})"
                    + $" / 透ける物 {seeThrough} / 動く扉板 {skipped}"
            );
        }

        // 1マス(4u)の壁片が遮蔽物として残るように、既定の 5 から下げる
        const float OcclusionSmallestOccluder = 4f;
        const float OcclusionSmallestHole = 0.25f;

        /// <summary>
        /// Field_Area01 だけを開いてオクルージョンカリングを焼く。小物は Static でないので焼く対象に要らず、
        /// 小物シーンを一緒に開くとそちらにも参照が書き込まれて担当者と競合する。Rebuild したら焼き直す。
        /// </summary>
        [MenuItem("Tools/CreativeAI/Map/Bake Occlusion Field_Area01")]
        public static void BakeOcclusionArea01()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            StaticOcclusionCulling.smallestOccluder = OcclusionSmallestOccluder;
            StaticOcclusionCulling.smallestHole = OcclusionSmallestHole;
            StaticOcclusionCulling.backfaceThreshold = 100f;
            if (!StaticOcclusionCulling.Compute())
            {
                Debug.LogError("[MapLayoutBuilder] オクルージョンカリングのベイクに失敗しました。");
                return;
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MapLayoutBuilder] オクルージョンカリングを焼きました。");
        }

        static bool IsSeeThrough(Renderer renderer) =>
            renderer.sharedMaterials.Any(m =>
                m != null && m.renderQueue > (int)UnityEngine.Rendering.RenderQueue.GeometryLast
            );

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
        static int[] WorldRowOffsets(FloorDef[] floors)
        {
            var baseZ = floors.Min(f => f.Origin.z);
            var baseX = floors[0].Origin.x;
            if (floors.Any(f => !Mathf.Approximately(f.Origin.x, baseX)))
                Debug.LogWarning(
                    "[MapLayoutBuilder] 階ごとに原点Xが違います。外周壁の列がずれます。"
                );

            var offsets = new int[floors.Length];
            for (var f = 0; f < floors.Length; f++)
                offsets[f] = Mathf.RoundToInt((floors[f].Origin.z - baseZ) / Cell);
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

        /// <summary>
        /// 外周は最下階の床から最上階の壁の上端までを1枚で作る(上端・下端がフラットになる)。
        /// 出入口 `X` のマスだけは、その階の床から壁の高さ(1階ぶん)までを開ける(隣の建物へ抜ける開口)。
        /// </summary>
        static void CreatePerimeterWall(
            Transform root,
            Building b,
            List<char[,]> grids,
            int[] rowOffsets,
            bool[,] perimeter,
            int worldRows,
            int worldCols,
            Material mat
        )
        {
            var floors = b.Floors;
            var bottomY = floors.Min(f => f.Origin.y);
            var topY = floors.Max(f => f.Origin.y) + b.StoreyHeight + PerimeterExtraHeight;
            var height = topY - bottomY;

            var go = new GameObject("Perimeter");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(
                floors[0].Origin.x,
                bottomY,
                floors.Min(f => f.Origin.z)
            );

            // 出入口のマス → その階の床の高さ
            var exits = new Dictionary<Vector2Int, float>();
            for (var f = 0; f < grids.Count; f++)
            for (var r = 0; r < grids[f].GetLength(0); r++)
            for (var c = 0; c < grids[f].GetLength(1); c++)
            {
                if (grids[f][r, c] != Exit)
                    continue;
                var cell = new Vector2Int(c, r + rowOffsets[f]);
                if (!perimeter[cell.y, cell.x])
                {
                    Debug.LogWarning(
                        $"[MapLayoutBuilder] {floors[f].Name} row {r} col {c}: "
                            + "`X` が外周にありません(外周以外では普通の床として扱います)。"
                    );
                    continue;
                }
                exits[cell] = floors[f].Origin.y;
            }

            var full = (bool[,])perimeter.Clone();
            foreach (var cell in exits.Keys)
                full[cell.y, cell.x] = false;

            var rects = MergeRects(full, worldRows, worldCols);
            foreach (var rect in rects)
                CreateBox(go.transform, "Perimeter", rect, height, height * 0.5f, mat);

            foreach (var (cell, floorY) in exits)
            {
                var rect = new RectInt(cell.x, cell.y, 1, 1);
                var below = floorY - FloorSlabThickness - bottomY;
                if (below > 0.01f)
                    CreateBox(go.transform, "PerimeterBelowExit", rect, below, below * 0.5f, mat);

                // 開口は1階ぶんの壁の高さまで。その上(外周壁を伸ばしたぶん)は塞ぐ
                var openTop = floorY + b.WallHeight - bottomY;
                var above = height - openTop;
                if (above > 0.01f)
                    CreateBox(
                        go.transform,
                        "PerimeterAboveExit",
                        rect,
                        above,
                        openTop + above * 0.5f,
                        mat
                    );
            }

            Debug.Log(
                $"[MapLayoutBuilder] 外周壁: {worldCols} × {worldRows} マスの縁を高さ {height} "
                    + $"(Y {bottomY} 〜 {topY}) で {rects.Count} 個にまとめました"
                    + (exits.Count > 0 ? $"(出入口 {exits.Count} マス)。" : "。")
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
            float scale,
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
                    scale,
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
                        scale,
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
            float scale,
            string name,
            Vector3 center,
            bool horizontal,
            float length
        )
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            PlaceModel(go, prefab, center, Quaternion.Euler(0f, horizontal ? 0f : 90f, 0f));
            // 長さはマスに合わせて伸ばし、高さ・太さは建物ごとの倍率。PlaceModel が入れたプレハブのスケールを上書きする。
            go.transform.localScale = new Vector3(length / HandrailLength, scale, scale);

            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, HandrailHeight * 0.5f, 0f);
            box.size = new Vector3(HandrailLength, HandrailHeight, 0.6f);
        }

        /// <summary>
        /// glb のプレハブを置く。glTF のノード変換に配置オフセットが入っているので、Transform を潰さず
        /// 目標の位置・向きにオフセットを足して置く(0 にすると床に沈む・横にずれる)。
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

        /// <summary>ガラス壁のモデルを、原点の床面を保ったまま壁の高さへ伸縮する。</summary>
        static void FitToWallHeight(GameObject go, float modelHeight, float wallHeight)
        {
            var s = go.transform.localScale;
            go.transform.localScale = new Vector3(s.x, s.y * (wallHeight / modelHeight), s.z);
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
        /// ガラス・柵の寄せ量を、列番号をキーに階をまたいで揃える(真上から見て同じ線に乗せる)。
        /// 横の列は <paramref name="alignRows"/> のときだけ揃える(Area01 は階ごとに Z 原点が 10マスずれ、
        /// 同じ世界行でも別の壁のことが多い。上下の階が真上に重なる建物なら行番号がそのまま世界の行)。
        /// 扉は <see cref="WallOffsetAt"/> で壁の面に追従する。同じ列で逆向きの寄せが要求されたら揃えずに警告する。
        /// </summary>
        static void AlignAcrossFloors(LineLayout[] layouts, string what, bool alignRows)
        {
            var want = new Dictionary<(bool, int), float>();
            var conflict = new HashSet<(bool, int)>();
            (bool, int) Key(LineLayout l, int i) =>
                l.Horizontals[i] ? (true, l.Runs[i].Row) : (false, l.Runs[i].Col);

            for (var f = 0; f < layouts.Length; f++)
            for (var i = 0; i < layouts[f].Runs.Count; i++)
            {
                if (layouts[f].Horizontals[i] && !alignRows)
                    continue;
                var off = layouts[f].Offsets[i];
                if (Mathf.Approximately(off, 0f))
                    continue;
                var key = Key(layouts[f], i);
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
                    $"[MapLayoutBuilder] {what}: {(k.Item1 ? "行" : "列")} {k.Item2} で階ごとに逆向きの寄せが要求されました。"
                        + "この列は階ごとの判定に任せます。"
                );
            }

            var changed = 0;
            for (var f = 0; f < layouts.Length; f++)
            for (var i = 0; i < layouts[f].Runs.Count; i++)
            {
                if (layouts[f].Horizontals[i] && !alignRows)
                    continue;
                if (!want.TryGetValue(Key(layouts[f], i), out var off))
                    continue;
                if (Mathf.Approximately(layouts[f].Offsets[i], off))
                    continue;
                layouts[f].Offsets[i] = off;
                changed++;
            }
            if (changed > 0)
                Debug.Log($"[MapLayoutBuilder] {what}: {changed} 本の列を他の階の面に揃えました。");
        }

        /// <summary>
        /// 板・柵を床の縁へ寄せる量。直交方向の隣に床が無いマスがあれば、その側のマス境界(±2u)まで列単位で寄せる
        /// (中心のままだと柵の外に足場ができる)。判定は床生成のマスクを使うので、階段の吹き抜けに面した柵も寄る。
        /// </summary>
        static float EdgeOffset(bool[,] hasFloor, int rows, int cols, FenceRun run, bool horizontal)
        {
            var (voidMinus, voidPlus) = VoidSides(hasFloor, rows, cols, run, horizontal);
            if (voidMinus == voidPlus) // 両側とも床がある / 両側とも無い → 中心のまま
                return 0f;
            return voidMinus ? -Cell * 0.5f : Cell * 0.5f;
        }

        /// <summary>列の直交方向の両隣に、床が無いマスがあるか(-側, +側)。</summary>
        static (bool Minus, bool Plus) VoidSides(
            bool[,] hasFloor,
            int rows,
            int cols,
            FenceRun run,
            bool horizontal
        )
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
            return (voidMinus, voidPlus);
        }

        static IEnumerable<(int Row, int Col)> RunCells(LineLayout layout, int runIndex)
        {
            var run = layout.Runs[runIndex];
            var horizontal = layout.Horizontals[runIndex];
            for (var k = 0; k < run.Length; k++)
                yield return (run.Row + (horizontal ? 0 : k), run.Col + (horizontal ? k : 0));
        }

        /// <summary>
        /// 両側に床があって中心に立つガラスを、廊下側(歩ける床が広い側)の `#` の面へ寄せる。
        /// 中心のままだと 4u 厚の `#` に対して薄い板が 2u 引っ込み、壁の面がそろわない。
        /// 階をまたいだ揃えの後に行う(揃えは縁への寄せだけを対象にしたいので)。扉は <see cref="WallOffsetAt"/> で追従する。
        /// </summary>
        static void FlushToCorridor(LineLayout layout, char[,] grid, bool[,] hasFloor)
        {
            var rows = grid.GetLength(0);
            var cols = grid.GetLength(1);
            for (var i = 0; i < layout.Runs.Count; i++)
            {
                if (!Mathf.Approximately(layout.Offsets[i], 0f))
                    continue;
                var run = layout.Runs[i];
                var horizontal = layout.Horizontals[i];
                var (voidMinus, voidPlus) = VoidSides(hasFloor, rows, cols, run, horizontal);
                if (voidMinus || voidPlus)
                    continue;

                int plus = 0,
                    minus = 0;
                for (var k = 0; k < run.Length; k++)
                {
                    var row = run.Row + (horizontal ? 0 : k);
                    var col = run.Col + (horizontal ? k : 0);
                    var dr = horizontal ? 1 : 0;
                    var dc = horizontal ? 0 : 1;
                    plus += ReachableFloor(grid, rows, cols, row + dr, col + dc);
                    minus += ReachableFloor(grid, rows, cols, row - dr, col - dc);
                }
                if (plus == minus) // どちらが廊下か決められない → 中心のまま
                    continue;
                var face = Cell * 0.5f - GlassPanelDepth * 0.5f;
                layout.Offsets[i] = plus > minus ? face : -face;
            }
        }

        /// <summary>
        /// ガラス壁。1マス × 階高のモデルを等倍で1枚ずつ置き(引き伸ばすと歪む)、床の縁に接していれば縁へ寄せる。
        /// 当たりは列全体で1つの薄い箱(4u厚だとガラスの2u手前で止まる)。
        /// </summary>
        static void CreateGlassWall(
            Transform parent,
            GlassParts glass,
            char[,] grid,
            int rows,
            int cols,
            LineLayout layout,
            int runIndex,
            Material wallMat,
            float wallHeight
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

            for (var i = 0; i < run.Length; i++)
            {
                var row = run.Row + (horizontal ? 0 : i);
                var col = run.Col + (horizontal ? i : 0);
                var prefab = glass.Pick(row, col);
                if (prefab == null)
                    continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = $"GlassWall_{row}_{col}";
                PlaceModel(go, prefab, Place(row, col, 0f), Quaternion.Euler(0f, yaw, 0f));
                FitToWallHeight(go, glass.ModelHeight, wallHeight);
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
                                glass,
                                row,
                                col,
                                horizontal,
                                mid,
                                span,
                                layout.Offsets[other],
                                wallHeight
                            );
                        continue;
                    }
                    if (IsDoor(neighbour))
                    {
                        // 直角に並ぶ扉の脇。扉の周りはガラスで埋めるので、袖壁で塞がず扉の面までガラスで継ぐ
                        var doorHorizontal = IsEastWest(grid, rows, cols, nr, nc);
                        if (doorHorizontal != horizontal)
                        {
                            CreateGlassCorner(
                                parent,
                                glass,
                                row,
                                col,
                                horizontal,
                                mid,
                                span,
                                WallOffsetAt(layout, rows, cols, nr, nc, doorHorizontal),
                                wallHeight
                            );
                            continue;
                        }
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
                            wallHeight * 0.5f,
                            horizontal ? gapMid : Cell * row
                        ),
                        horizontal
                            ? new Vector3(Cell, wallHeight, gap)
                            : new Vector3(gap, wallHeight, Cell),
                        wallMat
                    );
                }
            }

            var length = Cell * run.Length;
            var colliderHeight = wallHeight + FloorSlabThickness;
            var collider = new GameObject($"GlassWallCollider_{run.Row}_{run.Col}");
            collider.transform.SetParent(parent, false);
            collider.transform.localPosition =
                Place(run.Row, run.Col, Cell * (run.Length - 1) * 0.5f)
                + new Vector3(0f, colliderHeight * 0.5f, 0f);
            var box = collider.AddComponent<BoxCollider>();
            box.size = horizontal
                ? new Vector3(length, colliderHeight, GlassColliderThickness)
                : new Vector3(GlassColliderThickness, colliderHeight, length);
        }

        /// <summary>
        /// 扉のマス。扉モデルは等倍で置き、左右の袖壁と上の垂れ壁で開口を埋める。当たりは埋めた壁だけで扉は通れる。
        /// 扉の表(サイン面)は廊下側(<see cref="FacesPositive"/>)を向く。
        /// </summary>
        /// <summary>
        /// 扉のマスが乗る壁(ガラス/柵の列)の寄せ量。扉も壁と同じ面へずらさないと 2u 食い違って隙間が空く。
        /// 壁の向きに沿った隣のマスから列の寄せ量をもらう。
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
            GlassParts glass,
            float wallOffset,
            float wallHeight
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

            // 見た目は隣の `$` と同じモデルを開口の外だけ残して置く(方立・無目の線が隣と揃う)。
            // モデルが無ければ下の袖・垂れ壁の箱をガラスの板として見せる
            var glassPrefab = glass.Pick(row, col);
            if (glassPrefab != null)
            {
                void Clip(string name, Vector2 min, Vector2 max) =>
                    CreateClippedGlass(
                        root.transform,
                        name,
                        glassPrefab,
                        glass.ModelHeight,
                        wallHeight,
                        min,
                        max
                    );
                Clip("GlassL", new Vector2(-half, 0f), new Vector2(minX, height));
                Clip("GlassR", new Vector2(maxX, 0f), new Vector2(half, height));
                Clip("GlassTop", new Vector2(-half, height), new Vector2(half, wallHeight));
            }

            void Fill(string name, Vector3 center, Vector3 size)
            {
                var box = CreateFiller(root.transform, name, center, size, glassMat);
                if (box == null || glassPrefab == null)
                    return;
                // 当たりだけ残す
                Object.DestroyImmediate(box.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(box.GetComponent<MeshFilter>());
            }

            // 袖は左右で幅が違う(扉の原点は開口の中心で、外形の中心ではない)
            Fill(
                "SideL",
                new Vector3((-half + minX) * 0.5f, wallHeight * 0.5f, 0f),
                new Vector3(minX + half, wallHeight, DoorGlassThickness)
            );
            Fill(
                "SideR",
                new Vector3((maxX + half) * 0.5f, wallHeight * 0.5f, 0f),
                new Vector3(half - maxX, wallHeight, DoorGlassThickness)
            );
            Fill(
                "Lintel",
                new Vector3((minX + maxX) * 0.5f, (height + wallHeight) * 0.5f, 0f),
                new Vector3(maxX - minX, wallHeight - height, DoorGlassThickness)
            );
        }

        // 切り出したメッシュはシーンに埋め込まれるので、同じ形(扉の種類 × 壁の高さ)は使い回す
        static readonly Dictionary<(Mesh, Vector2, Vector2, float), Mesh> ClippedMeshes = new();

        /// <summary>
        /// `$` のモデルを隣と同じ伸縮で置き、親のローカルで X・Y が min〜max の範囲だけ残す。
        /// 枠もガラスも箱なので、頂点を範囲へ寄せれば範囲との交わりになる(外の箱は潰れて見えなくなる)。
        /// </summary>
        static void CreateClippedGlass(
            Transform parent,
            string name,
            GameObject prefab,
            float modelHeight,
            float wallHeight,
            Vector2 min,
            Vector2 max
        )
        {
            if (max.x - min.x < 0.01f || max.y - min.y < 0.01f)
                return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            // 差し替えたメッシュをシーンに持たせるため、プレハブとのつながりを切る
            PrefabUtility.UnpackPrefabInstance(
                go,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction
            );
            go.name = name;
            PlaceModel(go, prefab, Vector3.zero, Quaternion.identity);
            FitToWallHeight(go, modelHeight, wallHeight);

            foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var key = (filter.sharedMesh, min, max, wallHeight);
                if (ClippedMeshes.TryGetValue(key, out var cached) && cached != null)
                {
                    filter.sharedMesh = cached;
                    continue;
                }

                // 扉ルートに対するモデルの置き方はどの扉でも同じなので、範囲と壁の高さで形が決まる
                var toParent = parent.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var fromParent = toParent.inverse;
                var mesh = Object.Instantiate(filter.sharedMesh);
                mesh.name = $"{filter.sharedMesh.name}_{name}";
                var vertices = mesh.vertices;
                for (var i = 0; i < vertices.Length; i++)
                {
                    var p = toParent.MultiplyPoint3x4(vertices[i]);
                    p.x = Mathf.Clamp(p.x, min.x, max.x);
                    p.y = Mathf.Clamp(p.y, min.y, max.y);
                    vertices[i] = fromParent.MultiplyPoint3x4(p);
                }
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
                ClippedMeshes[key] = mesh;
                filter.sharedMesh = mesh;
            }
        }

        /// <summary>
        /// 扉の表(サイン面)を廊下側に向ける。奥行きでは部屋の方が深いことがあるので、両側の歩ける床の面積で判定する。
        /// true なら +Z(図の行番号が大きい側)を向ける。
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
        /// ガラスが直角に折れる角を、幅 0.5 倍のガラスの半コマで隣の列の面まで継ぐ(板はマス中心に立つので角で 2u 足りない)。
        /// 当たりも同じ長さで足す(run のコライダーは半コマを覆わないため)。
        /// </summary>
        static void CreateGlassCorner(
            Transform parent,
            GlassParts glass,
            int row,
            int col,
            bool horizontal,
            float mid,
            float span,
            float otherOffset,
            float wallHeight
        )
        {
            // 継ぐのは「隣の列の面」なので、向きはこの列と直角になり、
            // 面の位置(直交方向)は隣の列のずらし量に合わせる。
            float yaw = horizontal ? 90f : 0f;
            var center = horizontal
                ? new Vector3(Cell * col + otherOffset, 0f, mid)
                : new Vector3(mid, 0f, Cell * row + otherOffset);

            var prefab = glass.Pick(row, col);
            if (prefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = $"GlassCorner_{row}_{col}";
                PlaceModel(go, prefab, center, Quaternion.Euler(0f, yaw, 0f));
                FitToWallHeight(go, glass.ModelHeight, wallHeight);
                var scale = go.transform.localScale;
                scale.x *= span / Cell; // 板の幅方向(モデルのローカル X)を継ぐ長さに合わせる
                go.transform.localScale = scale;
            }

            var collider = new GameObject($"GlassCornerCollider_{row}_{col}");
            collider.transform.SetParent(parent, false);
            var colliderHeight = wallHeight + FloorSlabThickness;
            collider.transform.localPosition = center + new Vector3(0f, colliderHeight * 0.5f, 0f);
            var box = collider.AddComponent<BoxCollider>();
            box.size = horizontal
                ? new Vector3(GlassColliderThickness, colliderHeight, span)
                : new Vector3(span, colliderHeight, GlassColliderThickness);
        }

        /// <summary>
        /// 扉ルートに <see cref="DoorInteractor"/> と <see cref="SlidingDoor"/> を付け、glb 内の扉板("Leaf")を割り当てる。
        /// 扉板が見つからない(結合された古い glb)場合は警告だけ出してそのままにする。
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
        static GameObject CreateFiller(
            Transform parent,
            string name,
            Vector3 center,
            Vector3 size,
            Material mat
        )
        {
            if (size.x <= 0f || size.y <= 0f)
                return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static void CreateStairs(
            Transform parent,
            GameObject prefab,
            StairsModel model,
            StairCells s,
            float rise
        )
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
            var yScale = rise / model.Rise;
            go.transform.localPosition = basePos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = new Vector3(width / model.Width, yScale, run / model.Run);

            // モデルは見た目だけ。当たりは斜面の箱で別に作る
            foreach (var col in go.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(col);
            CreateStairsRamp(parent, model, s, basePos, width, run, rise, yScale);
        }

        /// <summary>
        /// 階段の当たり。段形状だと蹴上げ(階高/段数)が CharacterController の StepOffset を超えて登れないので、
        /// 段鼻を通る斜面の箱にする。斜面は1踏面ぶん手前から始めて下階の床面と、上端で上階の床面と面一になる。
        /// 左右には手すりぶんの壁を立てて、モデルの手すりを擦り抜けないようにする。
        /// </summary>
        static void CreateStairsRamp(
            Transform parent,
            StairsModel model,
            StairCells s,
            Vector3 basePos,
            float width,
            float run,
            float rise,
            float yScale
        )
        {
            var root = new GameObject($"StairsRamp_{s.Letter}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = basePos;

            var tread = run / model.Steps;

            // 段鼻は「1踏面ぶん手前で床面(Y=0)に届く直線」の上に並ぶ。その直線をそのまま斜面にする。
            // 上端は最上段の1つ手前の段鼻(= 上階の床面)で終わるので、最上段の踏面は下の Top で埋める
            var slope = new GameObject("Slope");
            slope.transform.SetParent(root.transform, false);
            slope.transform.localPosition = new Vector3(
                0f,
                rise * 0.5f + RampLift,
                run * 0.5f - tread
            );
            slope.transform.localRotation = Quaternion.Euler(
                -Mathf.Atan2(rise, run) * Mathf.Rad2Deg,
                0f,
                0f
            );

            var length = Mathf.Sqrt(run * run + rise * rise);
            var ramp = slope.AddComponent<BoxCollider>();
            ramp.center = new Vector3(0f, -RampThickness * 0.5f, 0f);
            ramp.size = new Vector3(width, RampThickness, length);

            // モデルの手すりを擦り抜けないように左右に壁を立てる
            var railHeight = model.RailTop * yScale;
            foreach (var side in new[] { -1f, 1f })
            {
                var rail = slope.AddComponent<BoxCollider>();
                rail.center = new Vector3(side * width * 0.5f, railHeight * 0.5f, 0f);
                rail.size = new Vector3(RampRailThickness, railHeight, length);
            }

            // 最上段の踏面。上階の床は吹き抜けで抜いてあるので、ここが無いと登りきった所で落ちる
            var top = new GameObject("Top");
            top.transform.SetParent(root.transform, false);
            top.transform.localPosition = new Vector3(0f, rise, run - tread * 0.5f);
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
        static List<StairCells> CollectStairs(List<char[,]> grids, FloorDef[] floors, string where)
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
                        $"[MapLayoutBuilder] {where}: 階段 '{letter}' が {found.Count} 階に出ています(2階ぶん必要)。とばします。"
                    );
                    continue;
                }

                var lower = found[0];
                var upper = found[1];
                var rowOffset = Mathf.RoundToInt(
                    (floors[upper.Floor].Origin.z - floors[lower.Floor].Origin.z) / Cell
                );
                if (lower.R0 != upper.R0 + rowOffset || lower.C0 != upper.C0)
                    Debug.LogWarning(
                        $"[MapLayoutBuilder] {where}: 階段 '{letter}': 上下の階でワールド座標が合っていません"
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
