using Unity.InferenceEngine;
using UnityEngine;

namespace CreativeAI.VisualTeam
{
    public class Inferer : MonoBehaviour
    {
        [SerializeField]
        private ModelAsset modelAsset;

        [SerializeField]
        private BackendType backendType = BackendType.GPUCompute;

        private Worker worker;

        void Start()
        {
            var model = ModelLoader.Load(this.modelAsset);
            this.worker = new(model, this.backendType);
        }

        void OnDestroy()
        {
            this.worker?.Dispose();
        }

        public void Infer(Sight sight, ref Emotion emotion)
        {
            this.worker.Schedule(sight.Tensorized);

            using var prediction = (
                this.worker.PeekOutput("prediction") as Tensor<float>
            ).ReadbackAndClone();

            emotion.Assign(valence: prediction[1], arousal: prediction[0]);
        }
    }
}
