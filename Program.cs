using Microsoft.Diagnostics.Runtime;

namespace SpanAnalyzer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine($"Usage: SpanAnalyzer <path to memory dump>");
                return;
            }

            Console.WriteLine($"Analayzing {args[0]}...");

            var dataTarget = DataTarget.LoadDump(args[0]);
            var runtime = dataTarget.ClrVersions[0].CreateRuntime();

            PrintTraces(runtime);
            PrintScopes(runtime);
        }

        private static void PrintScopes(ClrRuntime runtime)
        {
            var heap = runtime.Heap;

            foreach (var scopeManager in heap.GetProxies("Datadog.Trace.AsyncLocalScopeManager"))
            {
                Console.WriteLine($"Found scope manager at address {scopeManager:x2}");

                foreach (var (threadId, value) in AsyncLocalHelper.ExtractAsyncLocalValues(heap, (ulong)scopeManager._activeScope))
                {
                    if (value == null)
                    {
                        continue;
                    }

                    var spanId = value.Span.Context.SpanId;
                    var traceId = value.Span.Context.TraceId;

                    Console.WriteLine($"Span {spanId} (trace id: {traceId}) is active on thread {threadId}:");

                    var thread = runtime.Threads.FirstOrDefault(t => t.ManagedThreadId == threadId);

                    if (thread == null)
                    {
                        Console.WriteLine(@" - /!\ thread not found");
                        continue;
                    }

                    foreach (var frame in thread.EnumerateStackTrace())
                    {
                        Console.WriteLine($" - {frame}");
                    }
                }
            }
        }

        public static void PrintTraces(ClrRuntime runtime)
        {
            var heap = runtime.Heap;

            var traces = new Dictionary<ulong, Trace>();

            foreach (var spanProxy in heap.GetProxies("Datadog.Trace.Span"))
            {
                var span = new Span(spanProxy);

                if (!traces.TryGetValue(span.TraceId, out var trace))
                {
                    trace = new Trace { TraceId = span.TraceId };
                    traces.Add(trace.TraceId, trace);
                }

                trace.Spans.Add(span.SpanId, span);
            }

            Console.WriteLine($"Found {traces.Count} traces:");


            foreach (var trace in traces.Values)
            {
                trace.Print();
                Console.WriteLine();
                Console.WriteLine();
            }
        }

        public class Trace
        {
            public ulong TraceId;
            public Dictionary<ulong, Span> Spans = new();

            public void Print()
            {
                int unfinishedSpans = 0;

                var rootSpans = new List<Span>();

                foreach (var span in Spans.Values)
                {
                    if (!span.IsFinished)
                    {
                        unfinishedSpans++;
                    }

                    if (span.ParentSpanId == null)
                    {
                        rootSpans.Add(span);
                        continue;
                    }

                    if (!Spans.TryGetValue(span.ParentSpanId.Value, out var parent))
                    {
                        rootSpans.Add(span);
                        continue;
                    }

                    parent.Children.Add(span);
                }

                Console.WriteLine($"Trace {TraceId} ({Spans.Count} spans, {unfinishedSpans} unfinished)");
                Console.WriteLine();

                foreach (var rootSpan in rootSpans)
                {
                    if (rootSpan.ParentSpanId != null && !Spans.ContainsKey(rootSpan.ParentSpanId.Value))
                    {
                        Console.WriteLine(" - The parent span could not be found: " + rootSpan);
                        continue;
                    }

                    PrintSpan(rootSpan, 0);
                }
            }

            private void PrintSpan(Span span, int depth)
            {
                Console.WriteLine($"{new string(' ', depth * 4)} - {span}");

                foreach (var child in span.Children)
                {
                    PrintSpan(child, depth + 1);
                }
            }
        }

        public class Span
        {
            public Span(dynamic proxy)
            {
                Address = (ulong)proxy;
                OperationName = (string)proxy.OperationName;
                ResourceName = (string)proxy.ResourceName;
                IsFinished = (bool)proxy.IsFinished;
                StartTime = (DateTimeOffset)proxy.StartTime;

                var spanContext = proxy.Context;

                SpanId = spanContext.SpanId;
                TraceId = spanContext.TraceId;

                if (spanContext.Parent != null)
                {
                    ParentSpanId = spanContext.Parent.SpanId;
                }
            }

            public ulong Address { get; }

            public string OperationName { get; }
            public string ResourceName { get; }

            public DateTimeOffset StartTime { get; }

            public bool IsFinished { get; }

            public ulong SpanId { get; }

            public ulong TraceId { get; }

            public ulong? ParentSpanId { get; }

            public List<Span> Children { get; } = new();

            public override string ToString()
            {
                return $"Span id: {SpanId}, Trace id: {TraceId}, Operation name: {OperationName}, Start time: {StartTime}, Address: {Address:x2}, Finished: {IsFinished}";
            }
        }
    }
}