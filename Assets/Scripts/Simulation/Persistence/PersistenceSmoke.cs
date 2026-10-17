#if SIMPIENS_PERSISTENCE_SMOKE
using System;
using Cysharp.Threading.Tasks;
using Simpiens.Diagnostics;
using Simpiens.Simulation.Economy;
using UnityEngine;
using ZLogger;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// Player-build smoke test for the save toolchain. It is compiled only into the build made by
    /// <c>scripts/run-persistence-smoke.sh</c> (define <c>SIMPIENS_PERSISTENCE_SMOKE</c>), never into a normal build.
    /// A few seconds after boot it round-trips a save header and an array of unmanaged records through
    /// <see cref="SaveSerializer"/>, then saves the running outpost to a file and loads it back (Milestone 1 save/load):
    /// the reloaded session must save to the same bytes. It logs the result and quits: exit code 0 on a pass, 3 on a
    /// failure.
    /// </summary>
    internal static class PersistenceSmoke
    {
        private const int FailExitCode = 3;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Schedule() => RunAsync().Forget();

        private static async UniTaskVoid RunAsync()
        {
            // Let the session boot first, so the run also shows the game starts with MemoryPack present
            await UniTask.Delay(TimeSpan.FromSeconds(3), DelayType.Realtime);

            string failure;
            int bytes = 0;
            try
            {
                failure = RoundTrip(ref bytes);
            }
            catch (Exception e)
            {
                failure = e.ToString();
            }

            if (failure == null)
            {
                SimLog.Bootstrap.ZLogInformation($"PERSISTENCE SMOKE: PASS ({bytes} bytes round-tripped)");
                Application.Quit(0);
            }
            else
            {
                SimLog.Bootstrap.ZLogError($"PERSISTENCE SMOKE: FAIL {failure}");
                Application.Quit(FailExitCode);
            }
        }

        private static string RoundTrip(ref int bytes)
        {
            var header = new SessionSaveHeader
            {
                BuildVersion = Application.version,
                SessionId = "smoke",
                MapFingerprint = 0x0123456789ABCDEFUL,
                Seed = 20261009UL,
                Tick = 3000,
                EntityIdCounter = 57,
            };
            byte[] headerBytes = SaveSerializer.Serialize(header);
            var headerBack = SaveSerializer.Deserialize<SessionSaveHeader>(headerBytes);
            if (headerBack == null || headerBack.SessionId != "smoke" || headerBack.BuildVersion != header.BuildVersion
                || headerBack.MapFingerprint != header.MapFingerprint || headerBack.Tick != 3000 || headerBack.EntityIdCounter != 57)
            {
                return "header did not round-trip";
            }

            var items = new[] { new ItemInstance(1, ItemType.Ration, 3, 5), new ItemInstance(2, ItemType.IronOre, 1, 8, quality: 1, durability: 60) };
            byte[] itemBytes = SaveSerializer.Serialize(items);
            var itemsBack = SaveSerializer.Deserialize<ItemInstance[]>(itemBytes);
            if (itemsBack == null || itemsBack.Length != 2 || !itemsBack[0].Equals(items[0]) || !itemsBack[1].Equals(items[1]))
            {
                return "unmanaged records did not round-trip";
            }

            bytes = headerBytes.Length + itemBytes.Length;
            return SessionRoundTrip(ref bytes);
        }

        /// <summary>The running session, through a save file and back, in a real player.</summary>
        private static string SessionRoundTrip(ref int bytes)
        {
            var container = Simpiens.Core.Bootstrapping.AppBootstrapper.Instance != null ? Simpiens.Core.Bootstrapping.AppBootstrapper.Instance.Container : null;
            if (container == null) return "no container";
            var session = VContainer.IObjectResolverExtensions.Resolve<Simpiens.Core.Session.ISessionPersistence>(container);

            byte[] file = SaveFile.Encode(session.Capture());
            if (!SaveFile.TryDecode(file, out var save, out string refusal)) return $"the save file was refused: {refusal}";
            if (!session.TryRestore(save, out refusal)) return $"the load was refused: {refusal}";

            byte[] again = SaveFile.Encode(session.Capture());
            if (again.Length != file.Length) return $"the reloaded session saves to {again.Length} bytes, not {file.Length}";
            for (int i = 0; i < file.Length; i++)
            {
                if (again[i] != file[i]) return $"the reloaded session saves differently at byte {i}";
            }

            SimLog.Bootstrap.ZLogInformation($"PERSISTENCE SMOKE: session t{save.Header.Tick}, {save.Crawlers.Length} crawlers, saved and reloaded ({file.Length} bytes)");
            bytes += file.Length;
            return null;
        }
    }
}
#endif
