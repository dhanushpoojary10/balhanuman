using System;
using UnityEngine;

namespace BalHanuman
{
    public enum QuestState { NotStarted, FindBasket, BasketCollected, ReturnToSage, Completed }

    /// <summary>Lightweight quest state machine. Everything else reacts to StateChanged instead of polling.</summary>
    public class QuestManager : MonoBehaviour
    {
        public QuestState State { get; private set; } = QuestState.NotStarted;
        public event Action<QuestState> StateChanged;

        public string Objective
        {
            get
            {
                switch (State)
                {
                    case QuestState.NotStarted: return "Talk to the sage.";
                    case QuestState.FindBasket: return "Find the missing mango basket.";
                    case QuestState.BasketCollected: return "Return the basket to the sage.";
                    case QuestState.ReturnToSage: return "Give the basket to the sage.";
                    default: return "Level complete!";
                }
            }
        }

        void Set(QuestState s) { State = s; StateChanged?.Invoke(s); }

        public bool StartQuest() { if (State != QuestState.NotStarted) return false; Set(QuestState.FindBasket); return true; }
        public bool CollectBasket() { if (State != QuestState.FindBasket) return false; Set(QuestState.BasketCollected); return true; }
        public bool ReachSage() { if (State != QuestState.BasketCollected) return false; Set(QuestState.ReturnToSage); return true; }

        /// <summary>The quest cannot be completed before the basket has been collected.</summary>
        public bool Complete()
        {
            if (State != QuestState.BasketCollected && State != QuestState.ReturnToSage) return false;
            Set(QuestState.Completed);
            return true;
        }
    }
}
