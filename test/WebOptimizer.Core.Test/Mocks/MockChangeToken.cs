using System;
using Microsoft.Extensions.Primitives;

namespace WebOptimizer.Core.Test.Mocks
{
    public class MockChangeToken : IChangeToken
    {
        public bool ActiveChangeCallbacks => false;

        public bool HasChanged => false;

        public IDisposable RegisterChangeCallback(Action<object> callback, object state)
        {
            return NoopDisposable.Instance;
        }

        private class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
