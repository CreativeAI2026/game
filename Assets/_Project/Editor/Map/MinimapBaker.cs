using System.Collections.Generic;
using System.IO;
using System.Linq;
using CreativeAI.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// マップの文字図(<see cref="MapLayoutBuilder"/> と同じもの)から、ミニマップの距離フィールドのテクスチャを焼く。
    /// 建物の階ごとに、距離フィールドと線の種類の ID 画像を 1 枚ずつ PNG で書き出し、<see cref="MinimapLayout"/> にまとめる。
    /// Tools &gt; CreativeAI &gt; Map から実行。
    /// </summary>
    public static class MinimapBaker
    {
        /// <summary>書き出し先。</summary>
        public const string AssetPath = "Assets/_Project/Features/UI/Map/Data/MinimapLayout.asset";

        /// <summary>テクスチャ(PNG)の書き出し先のフォルダ。</summary>
        public const string TextureFolder = "Assets/_Project/Features/UI/Map/Data/Fields";

        /// <summary>テクスチャの合計の大きさの目安(バイト)。超えたら警告する。</summary>
        private const long TextureBudgetBytes = 15L * 1024 * 1024;

        private const float Cell = MapLayoutBuilder.Cell;

        // 建物の表示名(全画面マップの階の名前に添える)。載っていない建物(本棟)は名前を添えない
        private static readonly Dictionary<string, string> BuildingNames = new()
        {
            { "Lab", "研究棟" },
        };
        private static readonly float WallEdgeWidth = MinimapField.LineWidth(
            MinimapField.LineKind.WallEdge
        ); // 壁が床に面する縁の線の太さ(u)
        private static readonly float DoorWidth = MinimapField.LineWidth(
            MinimapField.LineKind.Door
        ); // 扉の線の太さ(u)
        private const float DoorInset = 0.4f; // 扉の線を両隣の壁から離す量(u)。壁と見分けるため

        [MenuItem("Tools/CreativeAI/Map/Bake Minimap")]
        public static void Bake()
        {
            var plans = MapLayoutBuilder.LoadFloorPlans();
            if (plans == null)
                return;

            Directory.CreateDirectory(TextureFolder);
            // 図の読み込み順(建物ごとに低い階から)のまま並べる
            var levels = plans.Select(BuildLevel).ToList();
            AssetDatabase.Refresh();

            long bytes = 0;
            foreach (var level in levels)
            {
                level.Field = ImportTexture(FieldPath(level, "field"), false);
                level.Ids = ImportTexture(FieldPath(level, "ids"), true);
                var size =
                    Profiler.GetRuntimeMemorySizeLong(level.Field)
                    + Profiler.GetRuntimeMemorySizeLong(level.Ids);
                bytes += size;
                Debug.Log(
                    $"[MinimapBaker] {level.Title}(高さ {level.Height:F1}): "
                        + $"{level.Field.width}x{level.Field.height} テクセル、階段 {level.Stairs.Count} 個、{size / 1024f / 1024f:F2}MB"
                );
            }

            var layout = AssetDatabase.LoadAssetAtPath<MinimapLayout>(AssetPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<MinimapLayout>();
                AssetDatabase.CreateAsset(layout, AssetPath);
            }
            layout.SetLevels(levels);
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();

            var total = $"テクスチャは合計 {bytes / 1024f / 1024f:F1}MB";
            if (bytes > TextureBudgetBytes)
                Debug.LogWarning($"[MinimapBaker] {total}で、目安の 15MB を超えています。");
            Debug.Log(
                $"[MinimapBaker] {AssetPath} に {levels.Count} 階ぶん書き出しました。{total}。"
            );
        }

        private static string FieldPath(MinimapLayout.Level level, string kind)
        {
            var building = string.IsNullOrEmpty(level.BuildingId) ? "Main" : level.BuildingId;
            return $"{TextureFolder}/{building}_{level.Label}_{kind}.png";
        }

        private static MinimapLayout.Level BuildLevel(MapLayoutBuilder.FloorPlan plan)
        {
            var level = new MinimapLayout.Level
            {
                BuildingId = plan.Building,
                Building = BuildingNames.TryGetValue(plan.Building, out var name)
                    ? name
                    : string.Empty,
                Label = plan.Floor,
                Height = plan.Origin.y,
            };

            var grid = plan.Grid;
            var rows = grid.GetLength(0);
            var cols = grid.GetLength(1);

            // 階段の本体は床とは別に持つ(下の階の図では床なしのマスにかかることがある)
            var stairMask = new bool[rows, cols];
            foreach (var (cells, up) in plan.Stairs)
            {
                for (var r = cells.yMin; r < cells.yMax; r++)
                for (var c = cells.xMin; c < cells.xMax; c++)
                    if (r >= 0 && c >= 0 && r < rows && c < cols)
                        stairMask[r, c] = true;
                level.Stairs.Add(
                    new MinimapLayout.Stair { Rect = CellsRect(plan, cells), Up = up }
                );
            }

            var groundMask = new bool[rows, cols];
            var wallMask = new bool[rows, cols];
            int minRow = rows,
                maxRow = -1,
                minCol = cols,
                maxCol = -1;
            for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                groundMask[r, c] = plan.HasFloor[r, c] || stairMask[r, c];
                wallMask[r, c] = plan.HasFloor[r, c] && grid[r, c] == '#';
                if (!groundMask[r, c])
                    continue;
                minRow = Mathf.Min(minRow, r);
                maxRow = Mathf.Max(maxRow, r);
                minCol = Mathf.Min(minCol, c);
                maxCol = Mathf.Max(maxCol, c);
            }

            // 床と階段のある範囲(建物の図の余白は含めない)。全画面マップはこれが収まるように描く
            level.Bounds = CellsRect(
                plan,
                new RectInt(minCol, minRow, maxCol - minCol + 1, maxRow - minRow + 1)
            );

            var lines = new List<MinimapDistanceField.Line>();
            AddWallEdges(lines, plan, wallMask);
            for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                if (!plan.HasFloor[r, c])
                    continue;
                var ch = grid[r, c];
                if (MapLayoutBuilder.IsPanel(ch))
                    AddLine(lines, plan, r, c, MinimapField.LineKind.Glass);
                else if (ch == MapLayoutBuilder.Fence)
                    AddLine(lines, plan, r, c, MinimapField.LineKind.Fence);
                else if (MapLayoutBuilder.IsDoor(ch))
                    AddDoor(lines, plan, r, c);
            }

            var image = MinimapDistanceField.Build(
                level.FieldRect,
                new Vector2(plan.Origin.x, plan.Origin.z),
                Cell,
                groundMask,
                stairMask,
                wallMask,
                lines
            );
            WritePng(FieldPath(level, "field"), image.Width, image.Height, image.Field);
            WritePng(
                FieldPath(level, "ids"),
                image.Width,
                image.Height,
                image.Ids.Select(id => new Color32(id, id, id, 255)).ToArray()
            );
            return level;
        }

        private static void WritePng(string assetPath, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        /// <summary>
        /// 書き出した PNG を、データとして正しく読めるように取り込み直す
        /// (色を補正しない・縮小しない・圧縮しない・端を繰り返さない)。ID 画像は 1 チャンネルにして補間しない。
        /// </summary>
        private static Texture2D ImportTexture(string assetPath, bool ids)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = ids
                ? TextureImporterType.SingleChannel
                : TextureImporterType.Default;
            if (ids)
            {
                // 1 チャンネルにするときの読み元(R)は、TextureImporterSettings 経由でしか決められない
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
                importer.SetTextureSettings(settings);
            }
            importer.sRGBTexture = false;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = ids ? FilterMode.Point : FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 8192;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        /// <summary>
        /// 壁のマスのうち、歩ける床(壁以外の床・階段)に面した辺に、壁の内側へ細い線を引く。
        /// 壁のかたまりは暗く塗り、部屋の形はこの線で見せる。
        /// </summary>
        private static void AddWallEdges(
            List<MinimapDistanceField.Line> lines,
            MapLayoutBuilder.FloorPlan plan,
            bool[,] wallMask
        )
        {
            var rows = wallMask.GetLength(0);
            var cols = wallMask.GetLength(1);
            var walkable = new bool[rows, cols];
            for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
                walkable[r, c] = plan.HasFloor[r, c] && !wallMask[r, c];
            foreach (var (cells, _) in plan.Stairs)
                for (var r = Mathf.Max(0, cells.yMin); r < Mathf.Min(rows, cells.yMax); r++)
                for (var c = Mathf.Max(0, cells.xMin); c < Mathf.Min(cols, cells.xMax); c++)
                    walkable[r, c] = true;

            bool Open(int y, int x) => y >= 0 && x >= 0 && y < rows && x < cols && walkable[y, x];

            for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                if (!wallMask[r, c])
                    continue;
                var cell = CellsRect(plan, new RectInt(c, r, 1, 1));
                if (Open(r, c - 1))
                    Add(
                        lines,
                        MinimapField.LineKind.WallEdge,
                        new Rect(cell.xMin, cell.yMin, WallEdgeWidth, cell.height)
                    );
                if (Open(r, c + 1))
                    Add(
                        lines,
                        MinimapField.LineKind.WallEdge,
                        new Rect(cell.xMax - WallEdgeWidth, cell.yMin, WallEdgeWidth, cell.height)
                    );
                if (Open(r - 1, c))
                    Add(
                        lines,
                        MinimapField.LineKind.WallEdge,
                        new Rect(cell.xMin, cell.yMin, cell.width, WallEdgeWidth)
                    );
                if (Open(r + 1, c))
                    Add(
                        lines,
                        MinimapField.LineKind.WallEdge,
                        new Rect(cell.xMin, cell.yMax - WallEdgeWidth, cell.width, WallEdgeWidth)
                    );
            }
        }

        /// <summary>
        /// ガラス・柵の1マス。マスの中心から、同じ種類の隣(ガラス・格子どうし / 柵どうし)へ腕を伸ばし、
        /// 線の端だけは向きの先にある壁(ガラス・格子は扉・壁、柵は `#` の壁)まで伸ばして隙間を埋める。
        /// 線の横にある壁にはつながない(壁沿いの柵が、くし歯のように壁へつながって見えるため)。
        /// 同じ種類の隣が無ければ壁の向きに1本引く。
        /// </summary>
        private static void AddLine(
            List<MinimapDistanceField.Line> lines,
            MapLayoutBuilder.FloorPlan plan,
            int r,
            int c,
            MinimapField.LineKind kind
        )
        {
            var grid = plan.Grid;
            var rows = grid.GetLength(0);
            var cols = grid.GetLength(1);
            var isFence = kind == MinimapField.LineKind.Fence;
            char? At(int y, int x) =>
                y < 0 || x < 0 || y >= rows || x >= cols || !plan.HasFloor[y, x]
                    ? null
                    : grid[y, x];
            bool Same(int y, int x) =>
                At(y, x) is char n
                && (isFence ? n == MapLayoutBuilder.Fence : MapLayoutBuilder.IsPanel(n));
            bool EndsAt(int y, int x) =>
                At(y, x) is char n
                && (
                    n == '#'
                    || (!isFence && (MapLayoutBuilder.IsPanel(n) || MapLayoutBuilder.IsDoor(n)))
                );

            var west = Same(r, c - 1);
            var east = Same(r, c + 1);
            var south = Same(r - 1, c);
            var north = Same(r + 1, c);
            var eastWest = west || east;
            var northSouth = south || north;
            if (!eastWest && !northSouth)
            {
                eastWest = MapLayoutBuilder.IsEastWest(grid, rows, cols, r, c);
                northSouth = !eastWest;
            }
            if (eastWest)
            {
                west |= EndsAt(r, c - 1);
                east |= EndsAt(r, c + 1);
            }
            if (northSouth)
            {
                south |= EndsAt(r - 1, c);
                north |= EndsAt(r + 1, c);
            }

            var center = CellCenter(plan, r, c);
            var width = MinimapField.LineWidth(kind);
            var half = width * 0.5f;
            var reach = Cell * 0.5f;
            Add(lines, kind, new Rect(center.x - half, center.y - half, width, width));
            if (west)
                Add(lines, kind, new Rect(center.x - reach, center.y - half, reach, width));
            if (east)
                Add(lines, kind, new Rect(center.x, center.y - half, reach, width));
            if (south)
                Add(lines, kind, new Rect(center.x - half, center.y - reach, width, reach));
            if (north)
                Add(lines, kind, new Rect(center.x - half, center.y, width, reach));
        }

        /// <summary>扉。壁の向きに沿った太い線を、両隣の壁から少し離して引く。</summary>
        private static void AddDoor(
            List<MinimapDistanceField.Line> lines,
            MapLayoutBuilder.FloorPlan plan,
            int r,
            int c
        )
        {
            var grid = plan.Grid;
            var eastWest = MapLayoutBuilder.IsEastWest(
                grid,
                grid.GetLength(0),
                grid.GetLength(1),
                r,
                c
            );
            var center = CellCenter(plan, r, c);
            var length = Cell - DoorInset * 2f;
            var rect = eastWest
                ? new Rect(center.x - length * 0.5f, center.y - DoorWidth * 0.5f, length, DoorWidth)
                : new Rect(
                    center.x - DoorWidth * 0.5f,
                    center.y - length * 0.5f,
                    DoorWidth,
                    length
                );
            Add(lines, MinimapField.LineKind.Door, rect);
        }

        private static Vector2 CellCenter(MapLayoutBuilder.FloorPlan plan, int r, int c) =>
            new(plan.Origin.x + c * Cell, plan.Origin.z + r * Cell);

        /// <summary>図のマス範囲(x = col, y = row)をワールドの XZ の範囲にする。</summary>
        private static Rect CellsRect(MapLayoutBuilder.FloorPlan plan, RectInt cells) =>
            new(
                plan.Origin.x + (cells.x - 0.5f) * Cell,
                plan.Origin.z + (cells.y - 0.5f) * Cell,
                cells.width * Cell,
                cells.height * Cell
            );

        private static void Add(
            List<MinimapDistanceField.Line> lines,
            MinimapField.LineKind kind,
            Rect rect
        ) => lines.Add(new MinimapDistanceField.Line(kind, rect));
    }
}
