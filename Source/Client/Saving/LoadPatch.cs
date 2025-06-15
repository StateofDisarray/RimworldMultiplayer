using HarmonyLib;
using Multiplayer.Client.Saving;
using Verse;
using Verse.Profile;
using Multiplayer.Client.AsyncTime;
using System.Linq;

namespace Multiplayer.Client
{
    [HarmonyPatch(typeof(SavedGameLoaderNow))]
    [HarmonyPatch(nameof(SavedGameLoaderNow.LoadGameFromSaveFileNow))]
    [HarmonyPatch(new[] { typeof(string) })]
    public static class LoadPatch
    {
        public static TempGameData gameToLoad;

        static bool Prefix()
        {
            if (gameToLoad == null) return true;

            SaveCompression.doSaveCompression = true;

            try
            {
                ScribeUtil.InitFromXmlDoc(gameToLoad.SaveData);
                ScribeMetaHeaderUtility.LoadGameDataHeader(ScribeMetaHeaderUtility.ScribeHeaderMode.Map, false);
                Scribe.EnterNode("game");
                Current.Game = new Game();
                Current.Game.LoadGame(); // calls Scribe.loader.FinalizeLoading()

                // Prevent errors when the client is disconnected during loading
                // todo revisit disconnection during loading
                // todo loading can be async, concurrency issues
                if (Multiplayer.Client != null)
                    SessionData.ReadSessionData(gameToLoad.SessionData);
            }
            finally
            {
                SaveCompression.doSaveCompression = false;
                gameToLoad = null;
            }

            Log.Message("Game loaded");

            if (Multiplayer.Client != null)
            {
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    Log.Message("Multiplayer: Executing post-load TickPatch.Reset()");
                    TickPatch.Reset();

                    // world graphics if a mapless scenario is loaded.
                    if (!Current.Game.Maps.Any())
                    {
                        MemoryUtility.UnloadUnusedUnityAssets();
                        Find.World.renderer.RegenerateAllLayersNow();
                    }
                });
            }

            return false;
        }
    }
}
