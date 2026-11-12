using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// ミニマップに描く図形を、建物の階ごとにまとめたデータ。
    /// マップの文字図からエディタの Bake Minimap で作る(手では編集しない)。座標はワールドの XZ(x = X, y = Z)。
    /// </summary>
    [CreateAssetMenu(menuName = "CreativeAI/UI/Minimap Layout", fileName = "MinimapLayout")]
    public sealed class MinimapLayout : ScriptableObject
    {
        /// <summary>図形の種類。描く順(下から)に並べている。</summary>
        public enum ShapeKind
        {
            Floor,
            Stairs,
            Wall,
            WallEdge,
            Fence,
            Glass,
            Door,
        }

        /// <summary>1つの図形。<see cref="Up"/> は階段の上る向き(それ以外は zero)。</summary>
        [Serializable]
        public struct Shape
        {
            /// <summary>種類。</summary>
            public ShapeKind Kind;

            /// <summary>ワールドの XZ での範囲。</summary>
            public Rect Rect;

            /// <summary>階段の上る向き(XZ)。</summary>
            public Vector2 Up;
        }

        /// <summary>1つの建物の1つの階の図形一式。</summary>
        [Serializable]
        public sealed class Level
        {
            /// <summary>建物の識別名(マップの図の建物名)。同じ建物の階を見分けるのに使う。</summary>
            public string BuildingId;

            /// <summary>建物の表示名(本棟は空)。</summary>
            public string Building;

            /// <summary>階の名前(2F など)。</summary>
            public string Label;

            /// <summary>床面の高さ(ワールド Y)。</summary>
            public float Height;

            /// <summary>図形のある XZ の範囲。</summary>
            public Rect Bounds;

            /// <summary>描く図形(<see cref="ShapeKind"/> の順に並んでいる)。</summary>
            public List<Shape> Shapes = new();

            /// <summary>建物の表示名を添えた階の名前(例: 研究棟 2F)。</summary>
            public string Title => string.IsNullOrEmpty(Building) ? Label : $"{Building} {Label}";
        }

        // 床面より少し下にいても(段差・沈み込み)その階と見なす幅
        private const float FloorTolerance = 0.5f;

        [SerializeField]
        private List<Level> _levels = new();

        /// <summary>階の一覧(建物ごとに、低い順)。</summary>
        public IReadOnlyList<Level> Levels => _levels;

        /// <summary>
        /// <paramref name="position"/> にいるときの階の番号。まず XZ がどの建物の範囲にあるか(無ければ一番近い建物)を決め、
        /// その建物の中で床面が y 以下の一番高い階を選ぶ(どの階よりも低ければ一番下の階)。階が無ければ -1。
        /// </summary>
        public int FindLevel(Vector3 position)
        {
            if (_levels.Count == 0)
                return -1;

            var point = new Vector2(position.x, position.z);
            string building = null;
            var nearest = float.MaxValue;
            foreach (var level in _levels)
            {
                var distance = DistanceTo(level.Bounds, point);
                if (distance < nearest)
                {
                    nearest = distance;
                    building = level.BuildingId;
                }
            }

            int below = -1,
                lowest = -1;
            for (var i = 0; i < _levels.Count; i++)
            {
                var level = _levels[i];
                if (level.BuildingId != building)
                    continue;
                if (lowest < 0 || level.Height < _levels[lowest].Height)
                    lowest = i;
                if (
                    level.Height <= position.y + FloorTolerance
                    && (below < 0 || level.Height > _levels[below].Height)
                )
                    below = i;
            }
            return below >= 0 ? below : lowest;
        }

        /// <summary>床面が <paramref name="height"/> の階を(建物をまたいで)全部 <paramref name="result"/> に入れる。</summary>
        public void LevelsAtHeight(float height, List<Level> result)
        {
            result.Clear();
            foreach (var level in _levels)
                if (Mathf.Approximately(level.Height, height))
                    result.Add(level);
        }

        private static float DistanceTo(Rect rect, Vector2 point)
        {
            var dx = Mathf.Max(rect.xMin - point.x, 0f, point.x - rect.xMax);
            var dy = Mathf.Max(rect.yMin - point.y, 0f, point.y - rect.yMax);
            return dx * dx + dy * dy;
        }

        /// <summary>中身を差し替える(エディタの Bake から使う)。</summary>
        public void SetLevels(List<Level> levels) => _levels = levels;
    }
}
