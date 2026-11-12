using System;
using System.Collections.Generic;
using FairyGUI;
using UI;
using Tools;
using UnityEngine;
using CesiumLoader.SDK.Logging;

namespace HandViewerMod
{
    internal sealed class HandViewerUi : IDisposable
    {
        sealed class StripComponent : GComponent
        {
            public StripComponent() { SetupOverflow(OverflowType.Hidden); }
        }
        sealed class Binding
        {
            public UIBattleInfo_Button_PlayerInfo Owner;
            public HandStripModel Model = new HandStripModel();
            public GComponent Button, Strip, Content;
            public GTextField Arrow;
            public Color ButtonColor;
            public string Signature;
            public float Height, ContentWidth, Offset;
            public bool Anchored;
            public bool Shown;
            public Vector2 HeadTop, HeadBottom, StarRight;
            public string LastRenderError;
            public float PanelWidth, PanelHeight;
        }
        readonly List<Binding> bindings = new List<Binding>();
        readonly HandQueryController query;
        string lastDiagnostic;
        public string UnavailableReason { get; set; }
        public string RoomId { get; set; }
        readonly int handRows, autoCollapseSeconds;
        readonly bool autoExpandOnEnter;
        string autoExpandedRoom;
        public HandViewerUi(HandQueryController query, bool doubleRow, int autoCollapseSeconds, bool autoExpandOnEnter = false)
        { this.query = query; this.handRows = doubleRow ? 2 : 1; this.autoCollapseSeconds = autoCollapseSeconds;
          this.autoExpandOnEnter = autoExpandOnEnter; }
        public void Tick(long now)
        {
            foreach (var binding in bindings)
            {
                binding.Model.Tick(now);
                if (!binding.Model.Open) binding.Strip.visible = false;
            }
            UpdateViewing(now);
        }
        void UpdateViewing(long now)
        {
            bool visible = false;
            foreach (var binding in bindings)
            {
                // The expanded strip is what the player sees: follow it instead of the
                // triangle handle so a visible strip can never report "collapsed".
                if (!binding.Model.Open || binding.Strip == null || binding.Strip.isDisposed || !binding.Strip.onStage) continue;
                bool shown = true;
                for (GObject node = binding.Strip; node != null; node = node.parent)
                    if (!node.visible || node.scaleX == 0 || node.scaleY == 0) { shown = false; break; }
                if (shown) { visible = true; break; }
            }
            query.SetViewing(visible, now);
        }
        void Diagnose(string message)
        {
            if (lastDiagnostic == message) return;
            lastDiagnostic = message; SdkLog.Info("HandViewer", "[hud] " + message);
        }
        static GTextField Text(string text, float width, float height, int size)
        {
            var label = new GTextField { text = text, touchable = false };
            label.SetSize(width, height);
            label.textFormat.size = size; label.textFormat.color = Color.white;
            label.textFormat = label.textFormat;
            return label;
        }
        /// <summary>
        /// 框内文字: 固定尺寸 + 自动折行 + 自动缩字号, 保证文字不会溢出边框。
        /// (FairyGUI 的 GTextField 默认 autoSize = Both 且不换行, 直接放文字会撑出边框。)
        /// </summary>
        static GTextField TextInBox(string text, float width, float height, int size, int minSize = 10,
            AlignType align = AlignType.Left)
        {
            var layout = StatusTextLayout.Fit(text, width, height, size, minSize);
            var label = new GTextField { touchable = false };
            label.autoSize = AutoSizeType.None; // 固定尺寸: 由 StatusTextLayout 负责折行与字号
            label.SetSize(width, height);
            label.text = layout.Text;
            label.align = align;
            label.verticalAlign = VertAlignType.Middle;
            label.textFormat.size = layout.FontSize;
            label.textFormat.color = Color.white;
            label.textFormat = label.textFormat;
            return label;
        }
        public void Poll(long now)
        {
            var root = GRoot.inst;
            var panel = Find(root, 0);
            var container = panel?.com_BattlePlayer?.com_Container;
            if (container == null)
            {
                foreach (var binding in bindings) { binding.Button.visible = false; binding.Strip.visible = false; }
                Diagnose("等待主 HUD 玩家容器"); return;
            }
            var owners = new[] { container.com_Player_1, container.com_Player_2, container.com_Player_3, container.com_Player_4 };
            bool rebuild = bindings.Count != owners.Length;
            if (!rebuild)
                for (int i = 0; i < owners.Length; i++)
                    if (!ReferenceEquals(bindings[i].Owner, owners[i]) || bindings[i].Button.isDisposed
                        || bindings[i].Button.parent != panel) rebuild = true;
            if (rebuild)
            {
                var opened = new Dictionary<long, HandStripModel>();
                foreach (var binding in bindings) if (binding.Model.Open) opened[binding.Model.PlayerId] = binding.Model;
                ClearBindings();
                var colors = new[] { new Color(0.9f, 0.18f, 0.2f), new Color(0.18f, 0.75f, 0.3f),
                    new Color(0.15f, 0.4f, 0.95f), new Color(1f, 0.82f, 0.12f) };
                for (int slot = 0; slot < owners.Length; slot++)
                {
                    var owner = owners[slot];
                    if (owner == null) continue;
                    var binding = new Binding { Owner = owner, ButtonColor = colors[slot] };
                    binding.Model.Bind(owner.PlayerData?.player.Id ?? 0);
                    if (opened.TryGetValue(binding.Model.PlayerId, out var previous)) binding.Model = previous;
                    binding.Button = new GComponent { opaque = true }; binding.Button.SetSize(28, 32);
                    binding.Arrow = Text(">", 28, 32, 26);
                    binding.Arrow.stroke = 2;
                    binding.Arrow.strokeColor = Color.black;
                    binding.Arrow.textFormat.bold = true;
                    binding.Arrow.autoSize = AutoSizeType.None;
                    binding.Arrow.align = AlignType.Center; binding.Arrow.verticalAlign = VertAlignType.Middle;
                    binding.Button.AddChild(binding.Arrow);
                    binding.Strip = new StripComponent { opaque = true };
                    binding.Content = new GComponent(); binding.Strip.AddChild(binding.Content);
                    binding.Button.onClick.Add(context =>
                    {
                        context.StopPropagation();
                        long now = DateTime.UtcNow.Ticks / 10000;
                        bool opened = binding.Model.Toggle(now, autoCollapseSeconds);
                        SdkLog.Info("HandViewer", "[hud] 三角点击: slot=" + bindings.IndexOf(binding) + " open=" + opened);
                        binding.Strip.visible = binding.Model.Open;
                        UpdateViewing(now);
                        if (opened && string.IsNullOrEmpty(UnavailableReason)) query.Refresh(now);
                    });
                    binding.Strip.onClick.Add(context => context.StopPropagation());
                    binding.Strip.AddEventListener("onMouseWheel", context =>
                    {
                        context.StopPropagation();
                        binding.Offset = Math.Max(0, Math.Min(Math.Max(0, binding.ContentWidth - binding.Strip.width),
                            binding.Offset + context.inputEvent.mouseWheelDelta * -48));
                        binding.Content.x = -binding.Offset;
                    });
                    panel.AddChild(binding.Button); panel.AddChild(binding.Strip); bindings.Add(binding);
                }
            }
            bool hudVisible = panel.visible && panel.scaleX != 0 && panel.scaleY != 0
                && panel.com_BattlePlayer.visible && panel.com_BattlePlayer.scaleX != 0
                && panel.com_BattlePlayer.scaleY != 0 && panel.com_BattlePlayer.touchableAll;
            // 进入对局自动展开他人手牌（不含自己），每局只展开一次：
            // 配置了自动收回时展示 30 秒后自动收回，未配置时不自动收回。
            if (autoExpandOnEnter && hudVisible && !string.IsNullOrEmpty(RoomId) && RoomId != autoExpandedRoom)
            {
                long selfId = SelfId();
                if (selfId > 0)
                {
                    int seconds = HandStripModel.EntryCollapseSeconds(autoCollapseSeconds);
                    int opened = 0;
                    foreach (var binding in bindings)
                    {
                        long id = binding.Owner.PlayerData?.player.Id ?? 0;
                        if (binding.Model.PlayerId != id) { binding.Model.Bind(id); binding.Signature = null; }
                        if (id == 0 || binding.Model.IsOwn(selfId) || IsSelfPlayer(id)) continue;
                        binding.Model.OpenForEntry(now, seconds);
                        opened++;
                    }
                    autoExpandedRoom = RoomId;
                    SdkLog.Info("HandViewer", "[hud] 进入对局自动展开他人手牌: room=" + RoomId + " opened=" + opened + " collapse=" + seconds + "秒");
                    if (opened > 0 && string.IsNullOrEmpty(UnavailableReason)) query.Refresh(now);
                }
            }
            bool expanded = false;
            foreach (var binding in bindings)
            {
                long id = binding.Owner.PlayerData?.player.Id ?? 0;
                if (binding.Model.PlayerId != id) { binding.Model.Bind(id); binding.Signature = null; }
                var info = binding.Owner.com_player as UICom_PlayerInfo;
                bool visible = hudVisible && id != 0 && binding.Owner.visible && binding.Owner.scaleX != 0
                    && binding.Owner.scaleY != 0 && info?.com_Head != null && info.list_Level != null;
                binding.Shown = visible;
                binding.Button.visible = visible;
                binding.Strip.visible = visible && binding.Model.Open;
                if (visible && binding.Model.Open) expanded = true;
            }
            // Publish the whole-frame state before rendering: the placeholder text below reads
            // query.Status, so an expanded strip must never paint the status of a paused query.
            query.SetViewing(expanded, now);
            foreach (var binding in bindings)
            {
                if (!binding.Shown) continue;
                long id = binding.Model.PlayerId;
                var info = binding.Owner.com_player as UICom_PlayerInfo;
                var head = info.com_Head;
                // Capture child layout once: hover/selection animations must not reposition our controls.
                if (!binding.Anchored || binding.PanelWidth != panel.width || binding.PanelHeight != panel.height)
                {
                    binding.PanelWidth = panel.width; binding.PanelHeight = panel.height;
                    binding.HeadTop = panel.GlobalToLocal(head.LocalToGlobal(Vector2.zero));
                    binding.HeadBottom = panel.GlobalToLocal(head.LocalToGlobal(new Vector2(head.width, head.height)));
                    var stars = info.list_Level;
                    binding.StarRight = panel.GlobalToLocal(stars.LocalToGlobal(new Vector2(stars.width, 0)));
                    binding.Anchored = true;
                }
                var top = binding.HeadTop;
                var bottom = binding.HeadBottom;
                var starRight = binding.StarRight;
                float height = Math.Max(1, Math.Abs(bottom.y - top.y));
                float x = Math.Max(bottom.x, starRight.x) + 4;
                float buttonSize = height / 2;
                binding.Button.SetSize(buttonSize, buttonSize);
                binding.Arrow.SetSize(buttonSize, buttonSize);
                binding.Arrow.textFormat.size = Math.Max(12, (int)(buttonSize * 0.7f));
                binding.Button.SetXY(x, top.y + (height - buttonSize) / 2);
                binding.Strip.SetXY(x + buttonSize + 4, top.y);
                var screenRight = panel.GlobalToLocal(root.LocalToGlobal(new Vector2(root.width - 8, 0)));
                float available = Math.Max(1, screenRight.x - binding.Strip.x);
                PlayerHand hand = null;
                if (query.Snapshot != null)
                    foreach (var player in query.Snapshot.players) if (player.playerId == id.ToString()) { hand = player; break; }
                binding.Arrow.textFormat.color = binding.ButtonColor;
                binding.Arrow.textFormat = binding.Arrow.textFormat;
                binding.Button.tooltips = (binding.Model.Open ? "收起手牌；再次展开会刷新" : "展开并刷新手牌") + "\n" + query.Status;
                binding.Strip.tooltips = query.Status + (query.IsStale ? " · 旧快照，等待更新" : "")
                    + "\n采样：" + (query.Snapshot?.sampledUtc ?? "—") + "\n手牌过多时滚轮横向查看";
                if (binding.Model.Open)
                {
                    string signature = HandStripModel.Signature(hand) + "/" + UnavailableReason + (hand == null ? "/" + query.Status : "");
                    if (signature != binding.Signature || binding.Height != height)
                    { Render(binding, hand, height, available); binding.Signature = signature; }
                    binding.Strip.SetSize(Math.Min(available, binding.ContentWidth), height);
                    binding.Offset = Math.Min(binding.Offset, Math.Max(0, binding.ContentWidth - binding.Strip.width));
                    binding.Content.x = -binding.Offset;
                }
            }
            Diagnose("已挂载升星右侧三角入口与四人独立手牌条");
        }
        void Render(Binding binding, PlayerHand hand, float height, float available)
        {
            binding.Content.RemoveChildren(0, -1, true);
            binding.Height = height; binding.Offset = 0;
            var groups = HandStripModel.Group(hand);
            if (groups.Count == 0)
            {
                string text = hand == null ? (UnavailableReason ?? query.Status) : !hand.handKnown ? "手牌未知" : "空手牌（0）";
                bool wide = hand == null || !string.IsNullOrEmpty(UnavailableReason);
                // 状态文案可能很长: 盒子最多铺到可用宽度, 再由 TextInBox 折行缩字号, 不让文字撑出边框。
                float width = wide ? Math.Min(520f, Math.Max(160f, available)) : 150f;
                width = Math.Max(60f, Math.Min(width, available));
                var bg = new GGraph(); bg.SetSize(width, height);
                bg.DrawRect(width, height, 1, Color.white, new Color(0.04f, 0.06f, 0.1f, 0.92f));
                bg.touchable = false; binding.Content.AddChild(bg);
                var placeholder = TextInBox(text, width - 10, height - 4, 18, 10);
                placeholder.x = 5; placeholder.y = 2;
                binding.Content.AddChild(placeholder); binding.ContentWidth = width;
                binding.Content.SetSize(width, height); return;
            }
            const float rowGap = 2;
            const float columnGap = 4;
            float cardHeight = Math.Max(1, (height - rowGap * (handRows - 1)) / handRows);
            int cardsPerRow = HandStripModel.CardsPerRow(groups.Count, handRows == 2);
            float x = 0, maxWidth = 0;
            for (int index = 0; index < groups.Count; index++)
            {
                if (index % cardsPerRow == 0) x = 0;
                float y = (index / cardsPerRow) * (cardHeight + rowGap);
                var group = groups[index];
                var data = group.Card;
                var cell = new GComponent();
                float width = cardHeight * 0.62f;
                try
                {
                    var config = StaticConfigure.Card.InfoDict[data.cardId];
                    var view = config.GetBattlePlayerCardView(binding.Model.PlayerId);
                    var card = UICom_Card.CreateInstance(); cell.AddChild(card);
                    var name = config.NameID.GetLocal(UIStringType.Card);
                    var description = config.DescId.GetLocal(UIStringType.Card);
                    var handData = new GameLogic.HandCardData(data.cardId, data.cardUid, data.purifyNum, data.isTemp, data.battleCost);
                    if (GameLogic.GameLogicManager.inst.card.cardActions.TryGetValue(data.cardId, out var action))
                        description = action.CardDescription(binding.Model.PlayerId, handData);
                    CommonUIManager.RendererCard(card, view, name, description, "", "", handData.BattleCost, config.CardType, config.CardTargetType);
                    if (card.height <= 0 || card.width <= 0) throw new InvalidOperationException("卡面尺寸为空");
                    float scale = cardHeight / card.height; width = card.width * scale;
                    card.SetScale(scale, scale); card.touchable = false;
                    cell.tooltips = name + " ×" + group.Count + "\n费用 " + handData.BattleCost
                        + " · 净化 " + data.purifyNum + (data.isTemp ? " · 临时牌" : "") + "\n" + description;
                }
                catch (Exception ex)
                {
                    string error = ex.GetType().Name + ": " + ex.Message;
                    if (binding.LastRenderError != error)
                    {
                        binding.LastRenderError = error;
                        SdkLog.Warn("HandViewer", "[card] 原生卡片失败 id=" + data.cardId + " " + error);
                    }
                    cell.RemoveChildren(0, -1, true);
                    try
                    {
                        // The small strip only needs artwork. Use the game's Addressables loader,
                        // without depending on full-card controllers or contextual skill descriptions.
                        var config = StaticConfigure.Card.InfoDict[data.cardId];
                        var image = new TextureLoader { touchable = false, fill = FillType.Scale, url = config.GetImage() };
                        image.SetSize(width, cardHeight); cell.AddChild(image);
                        cell.tooltips = config.NameID.GetLocal(UIStringType.Card) + " ×" + group.Count;
                    }
                    catch (Exception imageError)
                    {
                        SdkLog.Warn("HandViewer", "[card] 卡图失败 id=" + data.cardId + " " + imageError.GetType().Name + ": " + imageError.Message);
                        // 卡位很窄, 这里只放一个占位符, 完整原因留给 tooltip。
                        cell.tooltips = "卡图不可用 id=" + data.cardId + "\n" + imageError.GetType().Name + ": " + imageError.Message;
                        cell.AddChild(TextInBox("?", width, cardHeight, 10, 8, AlignType.Center));
                    }
                }
                cell.SetSize(width, cardHeight); cell.SetXY(x, y);
                if (group.Count > 1)
                {
                    float badgeHeight = Math.Min(cardHeight, 14);
                    float badgeWidth = Math.Min(width, group.Count >= 10 ? 20 : 15);
                    var bg = new GGraph(); bg.SetSize(badgeWidth, badgeHeight); bg.SetXY(Math.Max(0, width - badgeWidth), 0);
                    bg.DrawRect(badgeWidth, badgeHeight, 1, Color.white, new Color(0.05f, 0.07f, 0.12f, 0.95f)); bg.touchable = false;
                    cell.AddChild(bg);
                    var count = TextInBox(group.Count.ToString(), badgeWidth, badgeHeight, 11, 8, AlignType.Center);
                    count.SetXY(bg.x, 0); cell.AddChild(count);
                }
                binding.Content.AddChild(cell);
                maxWidth = Math.Max(maxWidth, x + width);
                x += width + columnGap;
            }
            binding.ContentWidth = Math.Max(1, maxWidth); binding.Content.SetSize(binding.ContentWidth, height);
        }
        static UIBattleInfoPanel Find(GObject node, int depth)
        {
            if (node == null || depth > 12) return null;
            if (node is UIBattleInfoPanel panel) return panel;
            if (node is GComponent component)
                for (int i = 0; i < component.numChildren; i++) { var found = Find(component.GetChildAt(i), depth + 1); if (found != null) return found; }
            return null;
        }
        void ClearBindings()
        {
            foreach (var binding in bindings)
            {
                try { binding.Button.RemoveFromParent(); binding.Button.Dispose(); } catch { }
                try { binding.Strip.RemoveFromParent(); binding.Strip.Dispose(); } catch { }
            }
            bindings.Clear();
        }
        static long SelfId()
        {
            try
            {
                var gm = GameLogic.GameLogicManager.inst;
                if (gm == null) return 0;
                try
                {
                    if (gm.account != null)
                    {
                        long id = gm.account.GetPlayerID();
                        if (id > 0) return id;
                    }
                }
                catch { }
                try
                {
                    var self = gm.battle != null ? gm.battle.GetSelfPlayerData() : null;
                    if (self != null && self.player != null) return self.player.Id;
                }
                catch { }
            }
            catch { }
            return 0;
        }
        static bool IsSelfPlayer(long id)
        {
            if (id == 0) return false;
            try
            {
                var gm = GameLogic.GameLogicManager.inst;
                if (gm != null && gm.account != null && gm.account.IsSelf(id)) return true;
            }
            catch { }
            return false;
        }
        public void Dispose() { ClearBindings(); query.SetViewing(false, DateTime.UtcNow.Ticks / 10000); }
    }
}
