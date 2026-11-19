using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// HP を「通常の HP バー」と「バフで増えた分のゲージ」に分けた表示用の値。
    /// バフ分は通常の HP の上に積まれる扱いで、ダメージはバフ分から先に減り、回復は通常分から先に戻る。
    /// </summary>
    public readonly struct HpGaugeSplit
    {
        /// <summary>通常の HP バーに出す現在値。</summary>
        public readonly float MainHp;

        /// <summary>通常の HP バーの最大値(最大HPからバフ分を除いたもの)。</summary>
        public readonly float MainMaxHp;

        /// <summary>バフ分のゲージの残量(0〜1)。バフが無いときは 0。</summary>
        public readonly float BonusRatio;

        /// <summary>バフで最大HPが増えているか(バフ分のゲージを出すか)。</summary>
        public readonly bool HasBonus;

        private HpGaugeSplit(float mainHp, float mainMaxHp, float bonusRatio, bool hasBonus)
        {
            MainHp = mainHp;
            MainMaxHp = mainMaxHp;
            BonusRatio = bonusRatio;
            HasBonus = hasBonus;
        }

        /// <summary>
        /// 現在HP・最大HP(バフ込み)・バフで増えた最大HPから、表示用に分ける。
        /// バフで増えた分が 0 以下(バフ無し・最大HPが下がる装備)のときは、全部を通常の HP バーに出す。
        /// </summary>
        public static HpGaugeSplit Calculate(float currentHp, float maxHp, float bonusMaxHp)
        {
            float bonus = Mathf.Clamp(bonusMaxHp, 0f, maxHp);
            float mainMax = maxHp - bonus;
            float mainHp = Mathf.Clamp(currentHp, 0f, mainMax);
            bool hasBonus = bonus > 0f;
            float bonusRatio = hasBonus ? Mathf.Clamp01((currentHp - mainMax) / bonus) : 0f;
            return new HpGaugeSplit(mainHp, mainMax, bonusRatio, hasBonus);
        }
    }
}
