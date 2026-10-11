using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Simpiens.Core.Bootstrapping
{
    /// <summary>
    /// Lets optional assemblies add registrations to the composition root without the production assembly
    /// referencing them. The test harness (Simpiens.Testing, editor/test builds only) registers its PlayMode services
    /// here from a RuntimeInitializeOnLoadMethod; player builds simply have no installers.
    /// </summary>
    public static class BootstrapExtensions
    {
        public delegate void Installer(IContainerBuilder builder, GameObject scopeObject);

        private static readonly List<Installer> Installers = new List<Installer>(2);

        public static void Register(Installer installer)
        {
            if (installer != null && !Installers.Contains(installer)) Installers.Add(installer);
        }

        internal static void InstallAll(IContainerBuilder builder, GameObject scopeObject)
        {
            for (int i = 0; i < Installers.Count; i++) Installers[i](builder, scopeObject);
        }
    }
}
