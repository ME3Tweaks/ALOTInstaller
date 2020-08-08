namespace ALOTInstallerCore.Objects
{
    public class Enums
    {
        /// <summary>
        /// Reference to a game. Defaults to unknown.
        /// </summary>
        public enum MEGame
        {
            Unknown,
            ME1,
            ME2,
            ME3
        }

        public static readonly MEGame[] AllGames = new[] { MEGame.ME1, MEGame.ME2, MEGame.ME3 };
    }
}
