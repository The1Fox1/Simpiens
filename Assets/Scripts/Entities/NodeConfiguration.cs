namespace Simpiens.Entities
{
    /// <summary>
    /// Pure C# struct to hold configuration data for a Node.
    /// This replaces the need for [SerializeField] tuning in the Unity Inspector.
    /// Structs are allocated on the stack or in-line within objects, reducing GC pressure.
    /// </summary>
    public readonly struct NodeConfiguration
    {
        public readonly float Speed;
        
        // We can add sprite references (Addressables keys) or other data here in the future.
        // public readonly string SpriteAddressableKey;

        public NodeConfiguration(float speed)
        {
            Speed = speed;
        }
        
        public static NodeConfiguration Default => new NodeConfiguration(2f);
    }
}
