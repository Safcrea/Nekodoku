using UnityEngine;

namespace Meowdoku
{
    /// <summary>
    /// Loads the ordered level list from a <see cref="LevelDatabase"/>. Not a
    /// MonoBehaviour - it has no scene presence of its own, just wraps the one
    /// call NekoGameManager needs at startup.
    /// </summary>
    public sealed class LevelLoader
    {
        private readonly LevelDatabase database;

        public LevelLoader(LevelDatabase database)
        {
            this.database = database;
        }

        public Level[] LoadLevels()
        {
            Level[] loaded = database.LoadLevels();
            if (loaded.Length == 0)
            {
                Debug.LogError("Level database contained no valid levels.");
            }

            return loaded;
        }
    }
}
