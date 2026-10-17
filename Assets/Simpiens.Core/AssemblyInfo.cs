using System.Runtime.CompilerServices;

// The engine-free core (Epic CORE C7). Its internal seams are exercised by the test harness in both runtimes and used
// by the Unity layer and the .NET hosts.
[assembly: InternalsVisibleTo("Simpiens")]
[assembly: InternalsVisibleTo("Simpiens.Editor")]
[assembly: InternalsVisibleTo("Simpiens.Testing")]
[assembly: InternalsVisibleTo("Simpiens.Testing.Core")]
[assembly: InternalsVisibleTo("Simpiens.Tests.PlayMode")]
[assembly: InternalsVisibleTo("Simpiens.Hosting")]
[assembly: InternalsVisibleTo("Simpiens.Core.Tests")]
