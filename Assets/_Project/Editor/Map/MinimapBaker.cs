using System.Collections.Generic;
using System.Linq;
using CreativeAI.UI;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// マップの文字図(<see cref="MapLayoutBuilder"/> と同じもの)から、ミニマップの図形データを作る。
    /// 建物の階ごとに1つずつ作る。Tools &gt; CreativeAI &gt; Map から実行。
    /// </summary>
    public static class MinimapBaker
    {
        /// <summary>書き出し先。</summary>
        public const string AssetPath =
            "Assets/_Project/Features/UI/MapUI/Data/MinimapLayout.asset";

        private const float Cell = MapLayoutBuilder.Cell;

        // 建物の表示名(全画面マップの階の名前に添える)。載っていない建物(本棟)は名前を添えない
        private static readonly Dictionary<string, string> BuildingNames = new()
        {
            { "Lab", "研究棟" },
        };
        private const float WallEdgeWidth = 0.6f; // 壁が床に面する縁の線の太さ(u)
        private const float GlassWidth = 0.7f; // ガラス・格子の線の太さ(u)
        private const float FenceWidth = 0.5f; // 柵の線の太さ(u)
        private const float DoorWidth = 1.4f; // 扉の線の太さ(u)
        private const float DoorInset = 0.4f; // 扉の線を両隣の壁から離す量(u)。壁と見分けるため

        [MenuItem("Tools/CreativeAI/Map/Bake Minimap")]
        public static void Bake()
        {
            var plans = MapLayoutBuilder.LoadFloorPlans();
            if (plans == null)
                return;

            // 図の読み込み順(建物ごとに低い階から)のまま並べる
            var levels = plans.Select(BuildLevel).ToList();

            var layout = AssetDatabase.LoadAssetAtPath<MinimapLayout>(AssetPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<MinimapLayout>();
                AssetDatabase.CreateAsset(layout, AssetPath);
            }
            layout.SetLevels(levels);
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();

            foreach (var level in levels)
                Debug.Log(
                    $"[MinimapBaker] {level.Title}(高さ {level.Height:F1}): 図形 {level.Shapes.Count} 個"
                );
            Debug.Log($"[MinimapBaker] {AssetPath} に {levels.Count} 階ぶん書き出しました。");
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
            AddPlan(level.Shapes, plan);

            // 種類の順に描く(床 → 階段 → 壁 → 線)
            level.Shapes = level.Shapes.OrderBy(s => s.Kind).ToList();
            // 図形のある範囲(建物の図の余白は含めない)。全画面マップはこれが収まるように描く
            level.Bounds = level
                .Shapes.Select(s => s.Rect)
                .Aggregate(
                    (a, b) =>
                        Rect.MinMaxRect(
                            Mathf.Min(a.xMin, b.xMin),
                            Mathf.Min(a.yMin, b.yMin),
                            Mathf.Max(a.xMax, b.xMax),
                            Mathf.Max(a.yMax, b.yMax)
                        )
                );
            return level;
        }

        private static void AddPlan(
            List<MinimapLayout.Shape> shapes,
            MapLayoutBuilder.FloorPlan plan
        )
        {
            var grid = plan.Grid;
            var rows = grid.GetLength(0);
            var cols = grid.GetLength(1);

            // 階段の本体は床とは別に塗る(下の階の図では床なしのマスにかかることがある)
            var stairMask = new bool[rows, cols];
            foreach (var (cells, _) in plan.Stairs)
                for (var r = cells.yMin; r < cells.yMax; r++)
                for (var c = cells.xMin; c < cells.xMax; c++)
                    if (r >= 0 && c >= 0 && r < rows && c < cols)
                        stairMask[r, c] = true;

            var floorMask = new bool[rows, cols];
            var wallMask = new bool[rows, cols];
            for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                floorMask[r, c] = plan.HasFloor[r, c] && !stairMask[r, c];
                wallMask[r, c] = plan.HasFloor[r, c] && grid[r, c] == '#';
            }

            foreach (var rect in MapLayoutBuilder.MergeRects(floorMask, rows, cols))
                Add(shapes, MinimapLayout.ShapeKind.Floor, CellsRect(plan, rect));
            foreach (var (cells, up) in plan.Stairs)
                Add(shapes, MinimapLayout.ShapeKind.Stairs, CellsRect(plan, cells), up);
            foreach (var rect in MapLayoutBuilder.MergeRects(wallMask, rows, cols))
                Add(shapes, MinimapLayout.ShapeKind.Wall, CellsRect(plan, rect));
            AddWallEdges(shapes, plan, wallMask);

            for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                if (!plan.HasFloor[r, c])
                    continue;
                var ch = grid[r, c];
                if (MapLayoutBuilder.IsPanel(ch))
                    AddLine(shapes, plan, r, c, MinimapLayout.ShapeKind.Glass, GlassWidth);
                else if (ch == MapLayoutBuilder.Fence)
                    AddLine(shapes, plan, r, c, MinimapLayout.ShapeKind.Fence, FenceWidth);
                else if (MapLayoutBuilder.IsDoor(ch))
                    AddDoor(shapes, plan, r, c);
            }
        }

        /// <summary>
        /// 壁のマスのうち、歩ける床(壁以外の床・階段)に面した辺に、壁の内側へ細い線を引く。
        /// 壁のかたまりは暗く塗り、部屋の形はこの線で見せる。
        /// </summary>
        private static void AddWallEdges(
            List<MinimapLayout.Shape> shapes,
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
                        shapes,
                        MinimapLayout.ShapeKind.WallEdge,
                        new Rect(cell.xMin, cell.yMin, WallEdgeWidth, cell.height)
                    );
                if (Open(r, c + 1))
                    Add(
                        shapes,
                        MinimapLayout.ShapeKind.WallEdge,
                        new Rect(cell.xMax - WallEdgeWidth, cell.yMin, WallEdgeWidth, cell.height)
                    );
                if (Open(r - 1, c))
                    Add(
                        shapes,
                        MinimapLayout.ShapeKind.WallEdge,
                        new Rect(cell.xMin, cell.yMin, cell.width, WallEdgeWidth)
                    );
                if (Open(r + 1, c))
                    Add(
                        shapes,
                        MinimapLayout.ShapeKind.WallEdge,
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
            List<MinimapLayout.Shape> shapes,
            MapLayoutBuilder.FloorPlan plan,
            int r,
            int c,
            MinimapLayout.ShapeKind kind,
            float width
        )
        {
            var grid = plan.Grid;
            var rows = grid.GetLength(0);
            var cols = grid.GetLength(1);
            var isFence = kind == MinimapLayout.ShapeKind.Fence;
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
            var half = width * 0.5f;
            var reach = Cell * 0.5f;
            Add(shapes, kind, new Rect(center.x - half, center.y - half, width, width));
            if (west)
                Add(shapes, kind, new Rect(center.x - reach, center.y - half, reach, width));
            if (east)
                Add(shapes, kind, new Rect(center.x, center.y - half, reach, width));
            if (south)
                Add(shapes, kind, new Rect(center.x - half, center.y - reach, width, reach));
            if (north)
                Add(shapes, kind, new Rect(center.x - half, center.y, width, reach));
        }

        /// <summary>扉。壁の向きに沿った太い線を、両隣の壁から少し離して引く。</summary>
        private static void AddDoor(
            List<MinimapLayout.Shape> shapes,
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
            Add(shapes, MinimapLayout.ShapeKind.Door, rect);
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
            List<MinimapLayout.Shape> shapes,
            MinimapLayout.ShapeKind kind,
            Rect rect,
            Vector2Int up = default
        ) =>
            shapes.Add(
                new MinimapLayout.Shape
                {
                    Kind = kind,
                    Rect = rect,
                    Up = up,
                }
            );
    }
}
