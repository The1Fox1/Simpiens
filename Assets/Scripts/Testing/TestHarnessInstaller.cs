using UnityEngine;
using Simpiens.Core.Bootstrapping;
using Simpiens.Core.Session;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Environment;
using Simpiens.Testing.Sessions;
using VContainer;
using VContainer.Unity;

namespace Simpiens.Testing
{
    /// <summary>
    /// Registers the PlayMode test harness services with the production composition root (AppBootstrapper) through
    /// BootstrapExtensions. Lives in Simpiens.Testing, which only compiles for the editor and test builds.
    /// </summary>
    public static class TestHarnessInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook()
        {
            // Validator output goes to Unity's console (TestLog is framework-free, Epic CORE C6)
            TestLog.InfoSink = message => Debug.Log(message);
            TestLog.WarningSink = message => Debug.LogWarning(message);
            TestLog.ErrorSink = message => Debug.LogError(message);

            BootstrapExtensions.Register(Install);
        }

        private static void Install(IContainerBuilder builder, GameObject scopeObject)
        {
            // The Dev Map: the pen canaries, reachable through the session switch (never the boot session)
            builder.Register<CanaryPenSession>(Lifetime.Singleton).As<ISessionDefinition>();

            builder.Register<EdgeCaseInjector>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<VisualAnomalyDetector>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.RegisterComponent(scopeObject.AddComponent<VisualAnomalyRecorderView>());
            builder.RegisterEntryPoint<BatchPlaymodeBootstrap>();
        }
    }

    /// <summary>
    /// Headless batch runs that are not test runs get the gossip-detection batch controller
    /// (previously created by SimulationStarter).
    /// </summary>
    public sealed class BatchPlaymodeBootstrap : IStartable
    {
        public void Start()
        {
            if (!Application.isBatchMode) return;

            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("-runTests", System.StringComparison.OrdinalIgnoreCase)) return;
            }

            var batchGo = new GameObject("[BatchPlaymodeController]");
            batchGo.AddComponent<BatchPlaymodeController>();
        }
    }
}
