using System;
using System.Collections.Generic;

namespace CreativeAI.Gameplay
{
    /// <summary>プレイヤーの行動制限の種類。組み合わせて指定する。</summary>
    [Flags]
    public enum PlayerLock
    {
        None = 0,
        Move = 1 << 0,
        Jump = 1 << 1,
        ChangeWeapon = 1 << 2,
        All = Move | Jump | ChangeWeapon,
    }

    /// <summary>
    /// プレイヤーの行動制限を「誰が(owner)・何を」単位で管理する。
    ///
    /// 以前は CanMove などの bool を各所が直接 true/false で書き換えていたため、
    /// 「怯みが明けた瞬間にボスの拘束まで解除する」といった上書き合いが起きていた。
    /// ここでは要求を owner ごとに保持し、1つでも残っていれば制限中とみなす。
    /// 解除は自分が掛けた分(<see cref="Release"/>)だけなので、他の制限を消すことはない。
    /// </summary>
    public sealed class PlayerActionLocks
    {
        private readonly Dictionary<object, PlayerLock> _requests = new();

        /// <summary>owner の制限を locks に置き換える(None なら解除)。</summary>
        public void Set(object owner, PlayerLock locks)
        {
            if (locks == PlayerLock.None)
                _requests.Remove(owner);
            else
                _requests[owner] = locks;
        }

        /// <summary>owner が掛けた制限をすべて解除する。掛けていなければ何もしない。</summary>
        public void Release(object owner) => _requests.Remove(owner);

        /// <summary>指定した制限のいずれかを、誰かが掛けているか。</summary>
        public bool IsLocked(PlayerLock locks)
        {
            foreach (PlayerLock requested in _requests.Values)
            {
                if ((requested & locks) != 0)
                    return true;
            }
            return false;
        }
    }
}
