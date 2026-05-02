using System;
using System.Threading;

namespace RimChat.Persistence
{
    /// <summary>
    /// Dependencies: none.
    /// Responsibility: carry player message across prompt building layers via ThreadStatic scope.
    /// </summary>
    internal static class ExpandMemoryMatchContext
    {
        [ThreadStatic]
        private static string _playerMessage;

        internal static string PlayerMessage => _playerMessage;

        internal static IDisposable Push(string playerMessage)
        {
            string prev = _playerMessage;
            _playerMessage = playerMessage;
            return new Scope(prev);
        }

        private sealed class Scope : IDisposable
        {
            private readonly string _previous;
            private bool _disposed;

            internal Scope(string previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    _playerMessage = _previous;
                }
            }
        }
    }
}
