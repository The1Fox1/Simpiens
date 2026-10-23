using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Simpiens.Core.Session
{
    /// <summary>
    /// The Unity side of the running session (Epic CORE C5): <see cref="SessionHost"/> owns the session and the
    /// simulation's entities; this draws them. It builds a view for every crawler and map entity the host spawns, shows
    /// and hides each with its entity's presence in the world, draws the map, and tears the views down with the world.
    /// It keeps the session API scenarios and the dashboard use, forwarding it to the host.
    /// </summary>
    public class SwarmSpawner : IStartable, System.IDisposable, ISessionSwitcher, ISessionPersistence
    {
        private readonly SessionHost _host;
        private readonly IWorldRegistry _registry;
        private readonly WorldSelection _selection;

        private readonly List<AutonomousAgent> _agents = new List<AutonomousAgent>(1000);
        private readonly Dictionary<EntityId, GameObject> _views = new Dictionary<EntityId, GameObject>(256);
        private GameObject _agentsRoot;
        private GameObject _propsRoot;

        [Inject]
        public SwarmSpawner(SessionHost host, IWorldRegistry registry, WorldSelection selection)
        {
            _host = host;
            _registry = registry;
            _selection = selection;
            _host.BuildVersion = Application.version;

            _host.WorldTearingDown += HandleWorldTearingDown;
            _host.MapLoaded += HandleMapLoaded;
            _host.PropSpawned += HandlePropSpawned;
            _host.CrawlerSpawned += HandleCrawlerSpawned;
            _host.CrawlerDeparted += HandleCrawlerDeparted;
            if (_registry != null)
            {
                _registry.EntityRegistered += HandleEntityRegistered;
                _registry.EntityUnregistered += HandleEntityUnregistered;
            }
        }

        /// <summary>The engine-free session this draws.</summary>
        public SessionHost Host => _host;

        /// <summary>The map loaded by the last respawn, with its spawned entity ids.</summary>
        public LoadedWorld World => _host.World;

        public IReadOnlyList<ISessionDefinition> Sessions => _host.Sessions;
        public ISessionDefinition Default => _host.Default;
        public ISessionDefinition Current => _host.Current;

        public event System.Action<ISessionDefinition> SessionChanged
        {
            add => _host.SessionChanged += value;
            remove => _host.SessionChanged -= value;
        }

        /// <summary>The crawlers' views, in spawn order (each bound to the host's crawler at the same index).</summary>
        public IReadOnlyList<AutonomousAgent> Agents => _agents;

        public void Start()
        {
            _host.Respawn();
        }

        public ISessionDefinition Find(string sessionId) => _host.Find(sessionId);
        public void SwitchTo(ISessionDefinition session) => _host.SwitchTo(session);
        public void Respawn() => _host.Respawn();
        public void Respawn(ISessionDefinition session) => _host.Respawn(session);
        public bool TryRestore(SessionSave save, out string refusal) => _host.TryRestore(save, out refusal);
        public string CheckRestorable(SessionSave save, out ISessionDefinition session) => _host.CheckRestorable(save, out session);
        public SessionSave Capture() => _host.Capture();

        private void HandleWorldTearingDown()
        {
            string[] rootNames = new[] { "[SwarmAgents]", "[MapEntities]", "[DynamicObstacles]" };
            for (int i = 0; i < rootNames.Length; i++)
            {
                var existing = GameObject.Find(rootNames[i]);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing);
                }
            }
            MapView.Clear();
            _agents.Clear();
            _views.Clear();
            _selection?.Clear(); // the selected crawler belonged to the old world
        }

        private void HandleMapLoaded(MapDefinition map)
        {
            MapView.Show(map, _host.Sectors, _selection);
            _agentsRoot = new GameObject("[SwarmAgents]");
            _propsRoot = new GameObject("[MapEntities]");
        }

        private void HandleCrawlerSpawned(AgentRuntime runtime, WorldEntity entity, string name)
        {
            GameObject agentGo = new GameObject(name);
            if (_agentsRoot != null) agentGo.transform.SetParent(_agentsRoot.transform);
            agentGo.transform.position = entity.Position.ToUnity();

            var sr = agentGo.AddComponent<SpriteRenderer>();
            sr.sprite = AgentSpriteLibrary.GetAgentSprite(AgentVisualState.Idle, 0);
            sr.sortingOrder = RenderOrder.Crawlers;
            agentGo.transform.localScale = Vector3.one * 0.7f; // Clear, readable icon size

            // A kinematic body and a collider, for clicking; the simulation moves the crawler, not physics
            var rb = agentGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;

            var col = agentGo.AddComponent<CircleCollider2D>();
            col.radius = SessionHost.CrawlerRadius;

            agentGo.AddComponent<NodeController>().Bind(entity);
            var agent = agentGo.AddComponent<AutonomousAgent>();
            agent.Bind(runtime);
            agent.SetTelemetryRegistry(_registry);

            // Attach AgentView for animated visual feedback
            agentGo.AddComponent<AgentView>();

            _agents.Add(agent);
        }

        // A drifter walked out by the Road Gate (Epic 10 G1): its view goes with it. Unity's views are frozen; this only
        // keeps them from drawing a crawler the simulation no longer has.
        private void HandleCrawlerDeparted(AgentRuntime runtime, WorldEntity entity)
        {
            for (int i = _agents.Count - 1; i >= 0; i--)
            {
                var agent = _agents[i];
                if (agent == null || agent.State != runtime.State) continue;
                _agents.RemoveAt(i);
                Object.Destroy(agent.gameObject);
            }
        }

        private void HandlePropSpawned(WorldEntity entity)
        {
            var go = new GameObject(entity.Name);
            if (_propsRoot != null) go.transform.SetParent(_propsRoot.transform);
            go.transform.position = entity.Position.ToUnity();

            var sr = go.AddComponent<SpriteRenderer>();
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Static;
            var col = go.AddComponent<CircleCollider2D>();

            switch (entity.Type)
            {
                case EntityType.Resource:
                    sr.sprite = AgentSpriteLibrary.GetResourceSprite(entity.ResourceKind);
                    sr.sortingOrder = RenderOrder.Props;
                    go.transform.localScale = Vector3.one * 0.8f;
                    col.radius = SessionHost.ResourceRadius;
                    break;
                case EntityType.Anchor:
                    sr.sprite = AgentSpriteLibrary.GetCampfireSprite(0);
                    go.transform.localScale = Vector3.one * 0.9f;
                    col.radius = SessionHost.HearthRadius;
                    go.AddComponent<CampfireView>();
                    break;
                case EntityType.Bunk:
                    sr.sprite = AgentSpriteLibrary.BunkSprite;
                    sr.sortingOrder = RenderOrder.Props; // under the sleeper
                    go.transform.localScale = Vector3.one * 0.9f;
                    // A trigger: the bunk stays walkable and crawlers never shove it around
                    col.isTrigger = true;
                    col.radius = SessionHost.BunkRadius;
                    break;
            }

            go.AddComponent<NodeController>().Bind(entity);
            _views[entity.Id] = go;
        }

        // A view shows while its entity is in the world: a depleted resource node hides, a regrown one shows again
        private void HandleEntityRegistered(WorldEntity entity)
        {
            if (_views.TryGetValue(entity.Id, out var go) && go != null) go.SetActive(true);
        }

        private void HandleEntityUnregistered(WorldEntity entity)
        {
            if (_views.TryGetValue(entity.Id, out var go) && go != null) go.SetActive(false);
        }

        public void Dispose()
        {
            _host.WorldTearingDown -= HandleWorldTearingDown;
            _host.MapLoaded -= HandleMapLoaded;
            _host.PropSpawned -= HandlePropSpawned;
            _host.CrawlerSpawned -= HandleCrawlerSpawned;
            _host.CrawlerDeparted -= HandleCrawlerDeparted;
            if (_registry != null)
            {
                _registry.EntityRegistered -= HandleEntityRegistered;
                _registry.EntityUnregistered -= HandleEntityUnregistered;
            }

        }
    }
}
