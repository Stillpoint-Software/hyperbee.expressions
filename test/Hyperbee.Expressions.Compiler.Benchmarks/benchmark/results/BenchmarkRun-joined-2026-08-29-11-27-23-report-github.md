```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Type                           | Method                           | Mean               | Error              | StdDev            | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Gen0    | Gen1   | Allocated |
|------------------------------- |--------------------------------- |-------------------:|-------------------:|------------------:|----------:|-------:|----------------:|-------------:|--------:|-------:|----------:|
| CoroutineCompilationBenchmarks | &#39;AsyncNoCapture | System&#39;        |    464,375.5208 ns |    190,580.1139 ns |    10,446.3392 ns |     1.00x |    N/A |           1.00x |          N/A |  2.9297 | 1.9531 |   28598 B |
| CoroutineCompilationBenchmarks | &#39;AsyncNoCapture | Hyperbee&#39;      |     11,056.4738 ns |      2,371.4125 ns |       129.9851 ns |     0.02x |    N/A |           0.23x |          N/A |  0.7324 | 0.6714 |    6535 B |
| CoroutineCompilationBenchmarks | &#39;AsyncCapture | System&#39;          |    560,530.3060 ns |    280,441.1344 ns |    15,371.9249 ns |     1.00x |    N/A |           1.00x |          N/A |  2.9297 | 1.9531 |   29328 B |
| CoroutineCompilationBenchmarks | &#39;AsyncCapture | Hyperbee&#39;        |  5,213,374.7396 ns | 15,931,114.0147 ns |   873,238.1198 ns |     9.30x |    N/A |           2.29x |          N/A |  7.8125 | 1.9531 |   67165 B |
| CoroutineCompilationBenchmarks | &#39;EnumerableNoCapture | System&#39;   |  6,824,228.2552 ns | 12,706,048.0944 ns |   696,461.3735 ns |     1.00x |    N/A |           1.00x |          N/A |  3.9063 |      - |   52871 B |
| CoroutineCompilationBenchmarks | &#39;EnumerableNoCapture | Hyperbee&#39; | 24,572,848.4375 ns | 62,167,652.5932 ns | 3,407,618.8278 ns |     3.60x |    N/A |           2.27x |          N/A |  7.8125 |      - |  120154 B |
| CoroutineCompilationBenchmarks | &#39;EnumerableCapture | System&#39;     |  5,464,946.7448 ns |  8,390,141.9471 ns |   459,891.9932 ns |     1.00x |    N/A |           1.00x |          N/A |  3.9063 |      - |   52758 B |
| CoroutineCompilationBenchmarks | &#39;EnumerableCapture | Hyperbee&#39;   | 23,609,782.8125 ns | 84,070,546.4563 ns | 4,608,190.3533 ns |     4.32x |    N/A |           2.63x |          N/A | 15.6250 |      - |  138631 B |
| CoroutineCompilationBenchmarks | &#39;NestedClosure | System&#39;         |     48,484.6303 ns |     47,259.0013 ns |     2,590.4253 ns |     1.00x |    N/A |           1.00x |          N/A |  0.8545 | 0.7935 |    7320 B |
| CoroutineCompilationBenchmarks | &#39;NestedClosure | Hyperbee&#39;       |      6,340.4241 ns |      4,831.0036 ns |       264.8036 ns |     0.13x |    N/A |           0.49x |          N/A |  0.3967 | 0.3662 |    3566 B |
| CoroutineExecutionBenchmarks   | &#39;AsyncNoCapture | System&#39;        |        725.2668 ns |        196.7408 ns |        10.7840 ns |     1.00x |    N/A |           1.00x |          N/A |  0.0277 |      - |     232 B |
| CoroutineExecutionBenchmarks   | &#39;AsyncNoCapture | Hyperbee&#39;      |         54.2955 ns |         16.1304 ns |         0.8842 ns |     0.07x |    N/A |           0.72x |          N/A |  0.0200 |      - |     168 B |
| CoroutineExecutionBenchmarks   | &#39;AsyncCapture | System&#39;          |        759.3935 ns |        828.2259 ns |        45.3979 ns |     1.00x |    N/A |           1.00x |          N/A |  0.0286 |      - |     240 B |
| CoroutineExecutionBenchmarks   | &#39;AsyncCapture | Hyperbee&#39;        |         53.5409 ns |         14.9422 ns |         0.8190 ns |     0.07x |    N/A |           0.50x |          N/A |  0.0143 |      - |     120 B |
| CoroutineExecutionBenchmarks   | &#39;EnumerableNoCapture | System&#39;   |        703.8596 ns |        649.7221 ns |        35.6135 ns |     1.00x |    N/A |           1.00x |          N/A |  0.0134 |      - |     112 B |
| CoroutineExecutionBenchmarks   | &#39;EnumerableNoCapture | Hyperbee&#39; |         31.1442 ns |         36.1643 ns |         1.9823 ns |     0.04x |    N/A |           0.43x |          N/A |  0.0057 |      - |      48 B |
| CoroutineExecutionBenchmarks   | &#39;EnumerableCapture | System&#39;     |        719.8261 ns |        252.0096 ns |        13.8135 ns |     1.00x |    N/A |           1.00x |          N/A |  0.0229 |      - |     192 B |
| CoroutineExecutionBenchmarks   | &#39;EnumerableCapture | Hyperbee&#39;   |        781.3572 ns |        375.0737 ns |        20.5591 ns |     1.09x |    N/A |           1.25x |          N/A |  0.0286 |      - |     240 B |
| CoroutineExecutionBenchmarks   | &#39;NestedClosure | System&#39;         |          0.9309 ns |          1.5990 ns |         0.0876 ns |     1.00x |    N/A |           1.00x |          N/A |       - |      - |         - |
| CoroutineExecutionBenchmarks   | &#39;NestedClosure | Hyperbee&#39;       |          1.3945 ns |          0.1469 ns |         0.0081 ns |     1.50x |    N/A |           1.00x |          N/A |       - |      - |         - |
