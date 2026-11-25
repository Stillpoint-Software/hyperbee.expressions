```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.10075)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 11.0.100
  [Host]  : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v3
  .NET 10 : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v3
  .NET 11 : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  IterationCount=20  LaunchCount=1  
WarmupCount=8  

```
| Method                                               | Job     | Toolchain | Mean          | Error       | StdDev      | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Gen0   | Allocated |
|----------------------------------------------------- |-------- |---------- |--------------:|------------:|------------:|----------:|-------:|----------------:|-------------:|-------:|----------:|
| &#39;AsyncSuspending x16 | Hyperbee&#39;                     | .NET 10 | Default   | 8,936.2682 ns | 697.0073 ns | 802.6746 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncSuspending x16 (delegate MoveNext) | Hyperbee&#39; | .NET 10 | Default   | 9,582.9000 ns | 637.0835 ns | 733.6663 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     154 B |
| &#39;AsyncNoCapture | System&#39;                            | .NET 10 | Default   |   529.3237 ns |  31.0604 ns |  35.7692 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0277 |     232 B |
| &#39;AsyncNoCapture | Hyperbee&#39;                          | .NET 10 | Default   |    34.7937 ns |   2.8582 ns |   3.2915 ns |     0.07x |    N/A |           0.72x |          N/A | 0.0200 |     168 B |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      | .NET 10 | Default   |    39.2899 ns |   2.3090 ns |   2.6591 ns |       N/A |    N/A |             N/A |          N/A | 0.0200 |     168 B |
| &#39;AsyncCapture | System&#39;                              | .NET 10 | Default   |   521.9096 ns |  35.4151 ns |  39.3638 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0286 |     240 B |
| &#39;AsyncCapture | Hyperbee&#39;                            | .NET 10 | Default   |    32.4799 ns |   1.9654 ns |   2.1845 ns |     0.06x |    N/A |           0.50x |          N/A | 0.0143 |     120 B |
| &#39;EnumerableNoCapture | System&#39;                       | .NET 10 | Default   |   488.1386 ns |  36.7348 ns |  40.8306 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0143 |     120 B |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     | .NET 10 | Default   |    24.1413 ns |   1.6888 ns |   1.9448 ns |     0.05x |    N/A |           0.47x |          N/A | 0.0067 |      56 B |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; | .NET 10 | Default   |    29.0782 ns |   1.4899 ns |   1.7158 ns |       N/A |    N/A |             N/A |          N/A | 0.0067 |      56 B |
| &#39;EnumerableCapture | System&#39;                         | .NET 10 | Default   |   509.9723 ns |  31.5718 ns |  36.3581 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0238 |     200 B |
| &#39;EnumerableCapture | Hyperbee&#39;                       | .NET 10 | Default   |    27.1945 ns |   1.3490 ns |   1.4434 ns |     0.05x |    N/A |           0.40x |          N/A | 0.0095 |      80 B |
| &#39;NestedClosure | System&#39;                             | .NET 10 | Default   |     0.4371 ns |   0.1040 ns |   0.1113 ns |     1.00x |    N/A |           1.00x |          N/A |      - |         - |
| &#39;NestedClosure | Hyperbee&#39;                           | .NET 10 | Default   |     0.9383 ns |   0.0951 ns |   0.1096 ns |     2.15x |    N/A |           1.00x |          N/A |      - |         - |
| &#39;AsyncSuspending x16 | Hyperbee&#39;                     | .NET 11 | .NET 11   | 5,056.6012 ns |  49.1315 ns |  50.4544 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncSuspending x16 (delegate MoveNext) | Hyperbee&#39; | .NET 11 | .NET 11   | 5,357.1371 ns | 125.4094 ns | 144.4216 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncNoCapture | System&#39;                            | .NET 11 | .NET 11   |   561.5756 ns |  37.3518 ns |  43.0144 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0267 |     224 B |
| &#39;AsyncNoCapture | Hyperbee&#39;                          | .NET 11 | .NET 11   |    30.9632 ns |   1.9705 ns |   2.2692 ns |     0.06x |    N/A |           0.75x |          N/A | 0.0200 |     168 B |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      | .NET 11 | .NET 11   |    38.0818 ns |   1.7593 ns |   2.0260 ns |       N/A |    N/A |             N/A |          N/A | 0.0200 |     168 B |
| &#39;AsyncCapture | System&#39;                              | .NET 11 | .NET 11   |   510.5351 ns |  14.2025 ns |  14.5849 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0277 |     232 B |
| &#39;AsyncCapture | Hyperbee&#39;                            | .NET 11 | .NET 11   |    31.0486 ns |   2.2966 ns |   2.4574 ns |     0.06x |    N/A |           0.52x |          N/A | 0.0143 |     120 B |
| &#39;EnumerableNoCapture | System&#39;                       | .NET 11 | .NET 11   |   491.1657 ns |  14.8303 ns |  15.2296 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0134 |     112 B |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     | .NET 11 | .NET 11   |    20.8481 ns |   1.0510 ns |   1.2103 ns |     0.04x |    N/A |           0.50x |          N/A | 0.0067 |      56 B |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; | .NET 11 | .NET 11   |    28.3838 ns |   1.0811 ns |   1.2017 ns |       N/A |    N/A |             N/A |          N/A | 0.0067 |      56 B |
| &#39;EnumerableCapture | System&#39;                         | .NET 11 | .NET 11   |   552.7323 ns |  38.4124 ns |  44.2358 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0229 |     192 B |
| &#39;EnumerableCapture | Hyperbee&#39;                       | .NET 11 | .NET 11   |    27.7674 ns |   2.3189 ns |   2.6704 ns |     0.05x |    N/A |           0.42x |          N/A | 0.0095 |      80 B |
| &#39;NestedClosure | System&#39;                             | .NET 11 | .NET 11   |     0.4485 ns |   0.1448 ns |   0.1667 ns |     1.00x |    N/A |           1.00x |          N/A |      - |         - |
| &#39;NestedClosure | Hyperbee&#39;                           | .NET 11 | .NET 11   |     1.0717 ns |   0.1912 ns |   0.2202 ns |     2.39x |    N/A |           1.00x |          N/A |      - |         - |
