```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                | Mean     | Error     | StdDev    | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Allocated |
|-------------------------------------- |---------:|----------:|----------:|----------:|-------:|----------------:|-------------:|----------:|
| &#39;a+b x1000 | open static delegate&#39;    | 4.108 μs | 0.9230 μs | 0.0506 μs |       N/A |    N/A |             N/A |          N/A |         - |
| &#39;a+b x1000 | closed over leading arg&#39; | 3.027 μs | 0.8430 μs | 0.0462 μs |       N/A |    N/A |             N/A |          N/A |         - |
