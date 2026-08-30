```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method            | Mean     | Error    | StdDev  | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Allocated |
|------------------ |---------:|---------:|--------:|----------:|-------:|----------------:|-------------:|----------:|
| &#39;Loop | System&#39;   | 117.3 ns | 32.65 ns | 1.79 ns |     1.00x |  0.99x |           1.00x |        1.00x |         - |
| &#39;Loop | FEC&#39;      | 118.1 ns | 54.65 ns | 3.00 ns |     1.01x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Loop | Hyperbee&#39; | 106.1 ns | 31.78 ns | 1.74 ns |     0.90x |  0.90x |           1.00x |        1.00x |         - |
