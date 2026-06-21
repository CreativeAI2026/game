using UnityEngine;

namespace CreativeAI.Gameplay
{
    [CreateAssetMenu(fileName = "ImportantData", menuName = "Scriptable Objects/ImportantData")]
    public class ImportantData : ItemData
    {
        private void OnEnable() => category = ItemCategory.Important;
    }
}
