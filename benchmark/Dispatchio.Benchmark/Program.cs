using BenchmarkDotNet.Running;
using Dispatchio.Benchmark;

BenchmarkSwitcher
    .FromAssembly(typeof(PublishBenchmarks).Assembly)
    .Run(args);