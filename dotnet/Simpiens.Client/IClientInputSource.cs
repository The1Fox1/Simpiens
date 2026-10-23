using Simpiens.Presentation;

namespace Simpiens.Client
{
    /// <summary>Where the game's pointer and keys come from each update: the devices (<see cref="ClientInput"/>), or a script in tests.</summary>
    public interface IClientInputSource
    {
        void Read(out PointerInput pointer, out KeyInput keys);
    }
}
