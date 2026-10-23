using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Simpiens.Cognition;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The views read frames, not the live simulation (review A3, plan step 3.3): no public member of the scene, the
    /// crawlers' appearance, the fog or the selection takes or gives a live entity, a crawler's state or the registry.
    /// A view that needs more of the world gets it through <see cref="WorldViewFrame"/>.
    /// </summary>
    public class ViewArchitectureTests
    {
        private static readonly Type[] Views = { typeof(WorldScene), typeof(CrawlerAppearance), typeof(FogOfWar), typeof(WorldSelection) };
        private static readonly Type[] Live = { typeof(WorldEntity), typeof(AgentState), typeof(IWorldRegistry) };

        [Fact]
        public void TheViewsTakeNoLiveSimulationObjects()
        {
            var offences = new List<string>();
            foreach (var view in Views)
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                foreach (var member in view.GetMembers(flags))
                {
                    foreach (var type in TypesOf(member))
                    {
                        if (Mentions(type)) offences.Add($"{view.Name}.{member.Name} uses {type.Name}");
                    }
                }
            }
            Assert.True(offences.Count == 0, string.Join("; ", offences));
        }

        private static IEnumerable<Type> TypesOf(MemberInfo member)
        {
            switch (member)
            {
                case MethodBase method:
                    foreach (var p in method.GetParameters()) yield return p.ParameterType;
                    if (method is MethodInfo info) yield return info.ReturnType;
                    break;
                case PropertyInfo property:
                    yield return property.PropertyType;
                    break;
                case FieldInfo field:
                    yield return field.FieldType;
                    break;
                case EventInfo e:
                    yield return e.EventHandlerType;
                    break;
            }
        }

        private static bool Mentions(Type type)
        {
            if (type == null) return false;
            if (type.HasElementType) return Mentions(type.GetElementType());
            if (Live.Any(l => l.IsAssignableFrom(type))) return true;
            return type.IsGenericType && type.GetGenericArguments().Any(Mentions);
        }
    }
}
