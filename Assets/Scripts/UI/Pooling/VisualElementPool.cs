using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Simpiens.UI.Pooling
{
    /// <summary>
    /// Pure C# generic utility for pooling and recycling UI Toolkit VisualElements.
    /// Eliminates garbage collection allocations on high-frequency UI refresh loops (10 Hz)
    /// by reusing pre-allocated VisualElement instances inside a parent container.
    /// </summary>
    /// <typeparam name="TData">Model or struct type representing the item data.</typeparam>
    /// <typeparam name="TElement">VisualElement type to instantiate and recycle.</typeparam>
    public class VisualElementPool<TData, TElement> where TElement : VisualElement
    {
        private readonly VisualElement _parentContainer;
        private readonly Func<TElement> _factoryMethod;
        private readonly Action<TElement, TData, int> _bindAction;
        private readonly List<TElement> _pool;

        public int PoolSize => _pool.Count;

        public VisualElementPool(
            VisualElement parentContainer,
            Func<TElement> factoryMethod,
            Action<TElement, TData, int> bindAction,
            int initialCapacity = 16)
        {
            _parentContainer = parentContainer;
            _factoryMethod = factoryMethod ?? throw new ArgumentNullException(nameof(factoryMethod));
            _bindAction = bindAction ?? throw new ArgumentNullException(nameof(bindAction));
            _pool = new List<TElement>(initialCapacity > 0 ? initialCapacity : 16);
        }

        /// <summary>
        /// Synchronizes the pooled VisualElements with the provided data collection.
        /// Once warmed up to the peak data count, this method executes with 0 bytes of GC heap allocation.
        /// </summary>
        public void Sync(IReadOnlyList<TData> data)
        {
            int dataCount = data != null ? data.Count : 0;

            // Expand pool lazily only if incoming data exceeds current pool capacity
            if (dataCount > _pool.Count)
            {
                int needed = dataCount - _pool.Count;
                for (int i = 0; i < needed; i++)
                {
                    var newElement = _factoryMethod();
                    _pool.Add(newElement);
                    _parentContainer?.Add(newElement);
                }
            }

            // Bind active items
            for (int i = 0; i < dataCount; i++)
            {
                var element = _pool[i];
                element.style.display = DisplayStyle.Flex;
                _bindAction(element, data[i], i);
            }

            // Hide unused pooled items
            for (int i = dataCount; i < _pool.Count; i++)
            {
                _pool[i].style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Hides all pooled elements without destroying them.
        /// </summary>
        public void HideAll()
        {
            int count = _pool.Count;
            for (int i = 0; i < count; i++)
            {
                _pool[i].style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Clears all elements from the pool and removes them from the parent container.
        /// </summary>
        public void Clear()
        {
            int count = _pool.Count;
            for (int i = 0; i < count; i++)
            {
                _parentContainer?.Remove(_pool[i]);
            }
            _pool.Clear();
        }
    }
}
