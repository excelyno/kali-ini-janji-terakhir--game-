namespace kali_ini_janji_terakhir.Core.Quests
{
    public enum GameEventType
    {
        Tilled, Planted, Watered, Harvested, Earned, Chopped, Mined,
        Talked, DebtPaid, EggCollected, Slept,
    }

    /// <summary>Kabar dari dunia untuk sistem misi, misalnya "panen 2 jagung" atau "bicara dengan Wira".</summary>
    public readonly record struct GameEvent(GameEventType Type, int Amount = 1, string? Subject = null);
}
