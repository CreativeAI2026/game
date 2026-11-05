using UnityEngine;
using Vector = System.Numerics.Vector2;

namespace CreativeAI.VisualTeam
{
    [CreateAssetMenu(fileName = "Emotion", menuName = "Scriptable Objects/Emotion")]
    public class Emotion : ScriptableObject
    {
        [SerializeField, Range(-1f, 1f)]
        private float valence,
            arousal;

        public float Valence
        {
            get => this.valence;
        }
        public float Arousal
        {
            get => this.arousal;
        }

        // assign with normalize
        public void Assign(float valence, float arousal)
        {
            Vector vector = new(valence, arousal);

            vector = Vector.Normalize(vector);

            (this.valence, this.arousal) = (vector.X, vector.Y);
        }
    }
}
