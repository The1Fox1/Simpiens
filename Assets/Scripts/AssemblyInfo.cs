using System.Runtime.CompilerServices;

// Domain validators (Simpiens.Testing) and PlayMode tests exercise internal simulation seams directly
[assembly: InternalsVisibleTo("Simpiens.Testing")]
[assembly: InternalsVisibleTo("Simpiens.Tests.PlayMode")]
