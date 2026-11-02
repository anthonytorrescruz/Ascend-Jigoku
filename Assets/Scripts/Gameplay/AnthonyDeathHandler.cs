namespace AscendJigoku.Gameplay
{
    public sealed class AnthonyDeathHandler : PlayerDeathHandler
    {
        public AnthonySession session;
        public override void OnPlayerDied() => session.ShowDeath();
    }
}
