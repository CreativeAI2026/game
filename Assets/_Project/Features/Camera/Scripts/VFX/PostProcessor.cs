using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CreativeAI.VisualTeam
{
    public class PostProcessor : MonoBehaviour
    {
        [SerializeField]
        private Emotion source;

        [SerializeField]
        private Volume target;

        private Vignette vignette;
        private ColorAdjustments colorAdjustments;
        private DepthOfField depth;

        private Pulse pulse;

        void Awake()
        {
            this.target.profile.TryGet(out this.vignette);
            this.target.profile.TryGet(out this.colorAdjustments);
            this.target.profile.TryGet(out this.depth);
        }

        void Start()
        {
            // arousal値の二乗(符号を考慮)に比例して早くなる仮想心拍: float [0f, 1f]
            this.pulse = new(factor: () => 15f * Mathf.Min(this.source.Arousal, 0.75f));
        }

        void Update()
        {
            // emotions
            (var arousal, var valence) = (this.source.Arousal, this.source.Valence);

            var anxiety = (arousal + -valence) / 2f;
            var depressed = (-arousal + -valence) / 2f;
            var enthusiastic = (arousal + valence) / 2f;
            var serene = (-arousal + valence) / 2f;

            // paramaters
            ref var vignette = ref this.vignette.intensity;
            ref var saturation = ref this.colorAdjustments.saturation;
            ref var contrast = ref this.colorAdjustments.contrast;
            ref var depth = ref this.depth.focalLength;

            // exaggerations
            anxiety.Affect(
                parameter: ref vignette,
                curve: (arg) => 0.4f * (arg + 0.1f) * this.pulse.Value
            );

            anxiety.Affect(
                parameter: ref depth,
                curve: (arg) => 7f * Mathf.Max(arg, -0.4f) * this.pulse.Value
            );

            valence.Affect(
                parameter: ref saturation,
                curve: (arg) => 60f * Mathf.Clamp(arg, -0.6f, 0f)
            );
            valence.Affect(
                parameter: ref contrast,
                curve: (arg) => 45f * Mathf.Clamp(arg, -0.6f, 0f)
            );

            Debug.Log(this.pulse.Value);
        }
    }

    public class Pulse
    {
        private float progress = 0f;

        private readonly Func<float, float> curve;
        private readonly Func<float> factor;

        private readonly CancellationTokenSource tokenSource;

        public float Value => this.curve(this.progress);

        public Pulse(Func<float, float> curve = null, Func<float> factor = null)
        {
            this.curve = curve ?? ((arg) => Mathf.Clamp(Mathf.Sin(arg), 0f, 1f));
            this.factor = factor ?? (() => 1f);

            this.tokenSource = new();

            Update(this.tokenSource.Token);
        }

        private async void Update(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    progress = unchecked(progress + Time.deltaTime * factor());

                    await UniTask.Yield();
                }
            }
            catch { }
        }

        public void Cancel()
        {
            this.tokenSource.Cancel();
        }

        public void Restart()
        {
            Update(this.tokenSource.Token);
        }
    }

    public static class PostProcessorExtension
    {
        public static void Affect(
            this float factor,
            ref ClampedFloatParameter parameter,
            float? max = null,
            float? min = null,
            Func<float, float> curve = null
        )
        {
            min ??= parameter.min;
            max ??= parameter.max;

            curve ??= (
                (arg) => (AnimationCurve.Linear(-1f, min.Value, 1f, max.Value).Evaluate(arg))
            );

            parameter.value = curve(factor);
        }

        public static void Affect(
            this float factor,
            ref MinFloatParameter parameter,
            float max,
            float? min = null,
            Func<float, float> curve = null
        )
        {
            min ??= parameter.min;

            curve ??= ((arg) => (AnimationCurve.Linear(-1f, min.Value, 1f, max).Evaluate(arg)));

            parameter.value = curve(factor);
        }
    }
}
