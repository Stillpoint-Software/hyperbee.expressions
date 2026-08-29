```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  InvocationCount=1  
IterationCount=25  LaunchCount=1  UnrollFactor=1  
WarmupCount=5  

```
| Method                                          | Mean     | Error     | StdDev    | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Allocated |
|------------------------------------------------ |---------:|----------:|----------:|----------:|-------:|----------------:|-------------:|----------:|
| &#39;AsyncNoCapture | System&#39;                       | 1.897 ms | 0.2656 ms | 0.3546 ms |     1.00x |    N/A |           1.00x |          N/A |  67.09 KB |
| &#39;AsyncNoCapture | Hyperbee&#39;                     | 1.485 ms | 0.2355 ms | 0.3143 ms |     0.78x |    N/A |           0.95x |          N/A |  63.49 KB |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39; | 1.411 ms | 0.1616 ms | 0.1861 ms |       N/A |    N/A |             N/A |          N/A |  63.75 KB |
| &#39;AsyncCapture | System&#39;                         | 1.715 ms | 0.1641 ms | 0.2075 ms |     1.00x |    N/A |           1.00x |          N/A |  67.73 KB |
| &#39;AsyncCapture | Hyperbee&#39;                       | 1.527 ms | 0.2387 ms | 0.3104 ms |     0.89x |    N/A |           1.05x |          N/A |   71.1 KB |
