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
        ChangeWeapon = 1 << 1,

        /// <summary>武器の行動(攻撃・ガード・構えなど)。掛かると装備中の武器は中立状態へ戻り、入力を受け付けない。</summary>
        Attack = 1 << 2,
        All = Move | ChangeWeapon | Attack,
    }

    /// <summary>
    /// プレイヤーがいま何ができるか・どういう状態か(行動の制限・怯み中・掴まれ中)。
    /// どれも「誰が(owner)」単位で持ち、1つでも残っていればその状態とみなす。
    /// 解除は自分が掛けた分だけなので、ほかの owner の分を消すことはない。
    /// 持ち主は <see cref="PlayerController.Conditions"/>。
    /// </summary>
    public sealed class PlayerConditions
    {
        private readonly Dictionary<object, PlayerLock> _requests = new();
        private readonly HashSet<object> _flinchOwners = new();
        private readonly HashSet<object> _grabOwners = new();

        /// <summary>
        /// 誰も掛けていなかった制限が新たに掛かった時に通知する(引数は新たに掛かった制限)。
        /// 既に他の owner が掛けている制限を重ねても通知しない。
        /// </summary>
        public event Action<PlayerLock> Locked;

        /// <summary>owner の制限を locks に置き換える(None なら解除)。</summary>
        public void Set(object owner, PlayerLock locks)
        {
            PlayerLock before = Combined();
            if (locks == PlayerLock.None)
                _requests.Remove(owner);
            else
                _requests[owner] = locks;

            PlayerLock added = Combined() & ~before;
            if (added != PlayerLock.None)
                Locked?.Invoke(added);
        }

        /// <summary>owner が掛けた制限をすべて解除する。掛けていなければ何もしない。</summary>
        public void Release(object owner) => _requests.Remove(owner);

        /// <summary>指定した制限のいずれかを、誰かが掛けているか。</summary>
        public bool IsLocked(PlayerLock locks) => (Combined() & locks) != 0;

        public bool CanMove => !IsLocked(PlayerLock.Move);
        public bool CanChangeWeapon => !IsLocked(PlayerLock.ChangeWeapon);

        /// <summary>武器の行動(攻撃・ガード・構えなど)ができるか。武器はこれが false の間、入力を受け付けない。</summary>
        public bool CanAttack => !IsLocked(PlayerLock.Attack);

        /// <summary>怯み中か(PlayerFlinchHandler の怯みと、PlayerEscapeHandler のはじき返され)。この間は被弾しない。</summary>
        public bool IsFlinching => _flinchOwners.Count > 0;

        /// <summary>敵に掴まれているか(掴み・拘束を行う敵)。</summary>
        public bool IsGrabbed => _grabOwners.Count > 0;

        /// <summary>owner による怯み中の扱いを始める・終える。他の owner の分は消さない。</summary>
        public void SetFlinching(object owner, bool flinching) =>
            SetOwned(_flinchOwners, owner, flinching);

        /// <summary>owner による掴まれ中の扱いを始める・終える。他の owner の分は消さない。</summary>
        public void SetGrabbed(object owner, bool grabbed) => SetOwned(_grabOwners, owner, grabbed);

        private static void SetOwned(HashSet<object> owners, object owner, bool on)
        {
            if (on)
                owners.Add(owner);
            else
                owners.Remove(owner);
        }

        private PlayerLock Combined()
        {
            PlayerLock combined = PlayerLock.None;
            foreach (PlayerLock requested in _requests.Values)
                combined |= requested;
            return combined;
        }
    }
}
