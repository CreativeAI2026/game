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

        // 揺らす前の頂点。文字が変わったときだけ取り直し、毎フレームのメッシュ作り直しを避ける
        private TMP_MeshInfo[] _baseMeshInfo;
        private bool _baseDirty = true;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            _baseDirty = true;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        }

        private void OnTextChanged(Object changed)
        {
            if (changed == _text)
                _baseDirty = true;
        }

        private void LateUpdate()
        {
            if (_text == null || _waveHeight <= 0f)
                return;

            TMP_TextInfo textInfo = _text.textInfo;
            if (_baseDirty || _baseMeshInfo == null)
            {
                _text.ForceMeshUpdate();
                _baseMeshInfo = textInfo.CopyMeshInfoVertexData();
                // ForceMeshUpdate 自身の変更通知で立った印も下ろす
                _baseDirty = false;
            }

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
                int materialIndex = character.materialReferenceIndex;
                Vector3[] source = _baseMeshInfo[materialIndex].vertices;
                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
                Vector3 offset = Vector3.up * offsetY;

                vertices[vertexIndex] = source[vertexIndex] + offset;
                vertices[vertexIndex + 1] = source[vertexIndex + 1] + offset;
                vertices[vertexIndex + 2] = source[vertexIndex + 2] + offset;
                vertices[vertexIndex + 3] = source[vertexIndex + 3] + offset;
            }

            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        private void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
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
