using TMPro;
using UnityEngine;

namespace CreativeAI.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TextWaveAnimation : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        private float _waveHeight = 8f;

        [SerializeField, Min(0f)]
        private float _characterDelay = 0.08f;

        [SerializeField, Min(0.01f)]
        private float _cycleDuration = 1f;

        [SerializeField]
        private bool _useUnscaledTime = true;

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void LateUpdate()
        {
            if (_text == null || _waveHeight <= 0f)
                return;

            // Reset to TMP's original vertices before applying this frame's offsets.
            _text.ForceMeshUpdate();

            TMP_TextInfo textInfo = _text.textInfo;
            float time = _useUnscaledTime ? Time.unscaledTime : Time.time;
            float angularSpeed = Mathf.PI * 2f / Mathf.Max(0.01f, _cycleDuration);

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[i];
                if (!character.isVisible)
                    continue;

                // Only the positive half of the sine wave lifts a character upward.
                float phase = (time - i * _characterDelay) * angularSpeed;
                float offsetY = Mathf.Max(0f, Mathf.Sin(phase)) * _waveHeight;
                int vertexIndex = character.vertexIndex;
                Vector3[] vertices = textInfo.meshInfo[character.materialReferenceIndex].vertices;
                Vector3 offset = Vector3.up * offsetY;

                vertices[vertexIndex] += offset;
                vertices[vertexIndex + 1] += offset;
                vertices[vertexIndex + 2] += offset;
                vertices[vertexIndex + 3] += offset;
            }

            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                TMP_MeshInfo meshInfo = textInfo.meshInfo[i];
                meshInfo.mesh.vertices = meshInfo.vertices;
                _text.UpdateGeometry(meshInfo.mesh, i);
            }
        }

        private void OnDisable()
        {
            if (_text == null)
                return;

            _text.ForceMeshUpdate();
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        private void OnValidate()
        {
            _waveHeight = Mathf.Max(0f, _waveHeight);
            _characterDelay = Mathf.Max(0f, _characterDelay);
            _cycleDuration = Mathf.Max(0.01f, _cycleDuration);
        }
    }
}
