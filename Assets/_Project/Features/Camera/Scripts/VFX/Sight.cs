using Unity.InferenceEngine;
using UnityEngine;

namespace CreativeAI.VisualTeam
{
    [CreateAssetMenu(fileName = "Sight", menuName = "Scriptable Objects/Sight")]
    public class Sight : ScriptableObject
    {
        public float enemy_in_view,
            enemy_visible,
            enemy_count,
            enemy_distance,
            enemy_angle,
            enemy_screen_x,
            enemy_screen_y,
            enemy_area;

        public Tensor<float> Tensorized
        {
            get
            {
                var features = typeof(Sight).GetFields();
                TensorShape shape = new(1, 1, features.Length);

                var values = new float[features.Length];
                for (int index = 0; index < features.Length; ++index)
                {
                    values[index] = (float)features[index].GetValue(this);
                }

                return new(shape, values);
            }
        }
    }
}
