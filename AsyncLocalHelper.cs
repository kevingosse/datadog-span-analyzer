using Microsoft.CSharp.RuntimeBinder;
using Microsoft.Diagnostics.Runtime;

namespace SpanAnalyzer
{
    internal class AsyncLocalHelper
    {
        private static bool _useNewThreadId;
        private static bool _useNewLocalValues;

        public static IEnumerable<(int threadId, dynamic value)> ExtractAsyncLocalValues(ClrHeap heap, ulong address)
        {
            foreach (var thread in heap.GetProxies<Thread>())
            {
                int threadId = GetThreadId(thread);
                var localValues = GetLocalValues(thread);

                foreach ((dynamic key, dynamic value) pair in ReadAsyncLocalStorage(localValues))
                {
                    if ((ulong)pair.key == address)
                    {
                        yield return (threadId, pair.value);
                        break;
                    }
                }
            }
        }

        private static int GetThreadId(dynamic thread)
        {
            if (!_useNewThreadId)
            {
                try
                {
                    return thread.m_ManagedThreadId;
                }
                catch (RuntimeBinderException)
                {
                    _useNewThreadId = true;
                }
            }

            return thread._managedThreadId;
        }

        private static dynamic? GetLocalValues(dynamic thread)
        {
            if (!_useNewLocalValues)
            {
                try
                {
                    return thread.m_ExecutionContext?._localValues;
                }
                catch (RuntimeBinderException)
                {
                    _useNewLocalValues = true;
                }
            }

            return thread._executionContext?.m_localValues;
        }

        private static IEnumerable<(dynamic key, dynamic value)> ReadAsyncLocalStorage(dynamic storage)
        {
            if (storage == null)
            {
                return Enumerable.Empty<(dynamic key, dynamic value)>();
            }

            ClrType type = storage.GetClrType();

            return type.Name switch
            {
                "System.Threading.AsyncLocalValueMap+EmptyAsyncLocalValueMap" => Enumerable.Empty<(dynamic key, dynamic value)>(),
                "System.Threading.AsyncLocalValueMap+OneElementAsyncLocalValueMap" => ReadOneElementAsyncLocalValueMap(storage),
                "System.Threading.AsyncLocalValueMap+TwoElementAsyncLocalValueMap" => ReadTwoElementAsyncLocalValueMap(storage),
                "System.Threading.AsyncLocalValueMap+ThreeElementAsyncLocalValueMap" => ReadThreeElementAsyncLocalValueMap(storage),
                "System.Threading.AsyncLocalValueMap+MultiElementAsyncLocalValueMap" => ReadMultiElementAsyncLocalValueMap(storage),
                "System.Threading.AsyncLocalValueMap+ManyElementAsyncLocalValueMap" => ReadManyElementAsyncLocalValueMap(storage),
                "System.Collections.Generic.Dictionary<System.Threading.IAsyncLocal, System.Object>" => ReadDictionary(storage),
                _ => throw new InvalidOperationException($"Unexpected asynclocal storage type: {type.Name}")
            };
        }

        private static IEnumerable<(dynamic key, dynamic value)> ReadOneElementAsyncLocalValueMap(dynamic storage)
        {
            yield return (storage._key1, storage._value1);
        }

        private static IEnumerable<(dynamic key, dynamic value)> ReadTwoElementAsyncLocalValueMap(dynamic storage)
        {
            yield return (storage._key1, storage._value1);
            yield return (storage._key2, storage._value2);
        }

        private static IEnumerable<(dynamic key, dynamic value)> ReadThreeElementAsyncLocalValueMap(dynamic storage)
        {
            yield return (storage._key1, storage._value1);
            yield return (storage._key2, storage._value2);
            yield return (storage._key3, storage._value3);
        }

        private static IEnumerable<(dynamic key, dynamic value)> ReadMultiElementAsyncLocalValueMap(dynamic storage)
        {
            foreach (var kvp in storage._keyValues)
            {
                yield return (kvp.key, kvp.value);
            }
        }

        private static IEnumerable<(dynamic key, dynamic value)> ReadManyElementAsyncLocalValueMap(dynamic storage)
        {
            foreach (var entry in storage._entries)
            {
                if (entry.key != null)
                {
                    yield return (entry.key, entry.value);
                }
            }
        }

        private static IEnumerable<(dynamic key, dynamic value)> ReadDictionary(dynamic storage)
        {
            foreach (var entry in storage.entries)
            {
                if (entry.key != null)
                {
                    yield return (entry.key, entry.value);
                }
            }
        }
    }
}
