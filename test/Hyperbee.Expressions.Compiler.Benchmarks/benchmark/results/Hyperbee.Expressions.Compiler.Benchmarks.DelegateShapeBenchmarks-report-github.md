```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.10075)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 11.0.100
  [Host]  : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v3
  .NET 10 : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v3
  .NET 11 : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                                | Job     | Toolchain | Mean     | Error     | StdDev    | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Allocated |
|-------------------------------------- |-------- |---------- |---------:|----------:|----------:|----------:|-------:|----------------:|-------------:|----------:|
| &#39;a+b x1000 | open static delegate&#39;    | .NET 10 | Default   | 2.468 μs | 1.3901 μs | 0.0762 μs |       N/A |    N/A |             N/A |          N/A |         - |
| &#39;a+b x1000 | closed over leading arg&#39; | .NET 10 | Default   | 2.075 μs | 0.3376 μs | 0.0185 μs |       N/A |    N/A |             N/A |          N/A |         - |
| &#39;a+b x1000 | open static delegate&#39;    | .NET 11 | .NET 11   | 2.510 μs | 1.0566 μs | 0.0579 μs |       N/A |    N/A |             N/A |          N/A |         - |
| &#39;a+b x1000 | closed over leading arg&#39; | .NET 11 | .NET 11   | 2.002 μs | 0.2513 μs | 0.0138 μs |       N/A |    N/A |             N/A |          N/A |         - |
