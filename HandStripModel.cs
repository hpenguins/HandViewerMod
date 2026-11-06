using System.Collections.Generic;

namespace HandViewerMod
{
    public sealed class HandCardGroup
    {
        public HandCard Card;
        public int Count;
    }

    public sealed class HandStripModel
    {
        /// <summary>进入对局自动展开后展示秒数：仅在 <c>AutoCollapseSeconds &gt; 0</c>
        /// （已配置自动收回）时使用；为 0（不自动收回）时自动展开后也不收回。</summary>
        public const int AutoExpandDisplaySeconds = 30;
        public static int CardsPerRow(int count, bool doubleRow)
            => count <= 0 ? 1 : (count - 1) / (doubleRow ? 2 : 1) + 1;
        public long PlayerId { get; private set; }
        public bool Open { get; private set; }
        long closeAt;
        public void Bind(long playerId)
        {
            if (PlayerId == playerId) return;
            PlayerId = playerId; Open = false; closeAt = 0;
        }
        public bool Toggle(long now = 0, int autoCollapseSeconds = 0)
        {
            if (PlayerId == 0) return false;
            Open = !Open;
            closeAt = Open && autoCollapseSeconds > 0 ? now + autoCollapseSeconds * 1000L : 0;
            return Open;
        }
        /// <summary>进入对局自动展开：幂等地打开（已打开不翻转），
        /// 按传入的收回秒数定时；0 表示不自动收回。</summary>
        public void OpenForEntry(long now, int collapseSeconds)
        {
            if (PlayerId == 0) return;
            Open = true;
            closeAt = collapseSeconds > 0 ? now + collapseSeconds * 1000L : 0;
        }
        /// <summary>是否是不自动展开的自己：仅跳过自身手牌，其余照常展开。</summary>
        public bool IsOwn(long selfId) => selfId > 0 && PlayerId == selfId;
        /// <summary>自动展开的展示时长：配置了自动收回时展示 30 秒后自动收回，
        /// 未配置（0）时不自动收回。</summary>
        public static int EntryCollapseSeconds(int autoCollapseSeconds)
            => autoCollapseSeconds > 0 ? AutoExpandDisplaySeconds : 0;
        public void Tick(long now)
        {
            if (Open && closeAt > 0 && now >= closeAt) { Open = false; closeAt = 0; }
        }
        public static List<HandCardGroup> Group(PlayerHand hand)
        {
            var result = new List<HandCardGroup>();
            if (hand == null || !hand.handKnown || hand.cards == null) return result;
            foreach (var card in hand.cards)
            {
                HandCardGroup found = null;
                foreach (var group in result)
                    if (group.Card.cardId == card.cardId && group.Card.purifyNum == card.purifyNum
                        && group.Card.isTemp == card.isTemp && group.Card.battleCost == card.battleCost)
                    { found = group; break; }
                if (found == null) result.Add(new HandCardGroup { Card = card, Count = 1 });
                else found.Count++;
            }
            return result;
        }
        public static string Signature(PlayerHand hand)
        {
            if (hand == null) return "waiting";
            if (!hand.handKnown) return "unknown:" + hand.handReason;
            string value = "known";
            foreach (var group in Group(hand))
                value += "/" + group.Card.cardId + ":" + group.Card.purifyNum + ":" + group.Card.isTemp
                    + ":" + group.Card.battleCost + ":" + group.Count;
            return value;
        }
    }
}
