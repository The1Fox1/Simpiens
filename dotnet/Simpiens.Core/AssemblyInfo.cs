using System.Runtime.CompilerServices;

// The engine-free core (Epic CORE C7). Its internal seams are exercised by the test harness and used by the hosts.
[assembly: InternalsVisibleTo("Simpiens.Testing.Core")]
[assembly: InternalsVisibleTo("Simpiens.Hosting")]
[assembly: InternalsVisibleTo("Simpiens.Core.Tests")]
