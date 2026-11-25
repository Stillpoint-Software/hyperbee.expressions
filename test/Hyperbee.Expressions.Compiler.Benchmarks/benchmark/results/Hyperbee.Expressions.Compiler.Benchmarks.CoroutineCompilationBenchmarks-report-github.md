```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.10075)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 11.0.100
  [Host]  : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v3
  .NET 10 : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v3
  .NET 11 : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  InvocationCount=1  IterationCount=25  
LaunchCount=1  UnrollFactor=1  WarmupCount=5  

```
| Method                                               | Job     | Toolchain | Mean        | Error      | StdDev     | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Allocated |
|----------------------------------------------------- |-------- |---------- |------------:|-----------:|-----------:|----------:|-------:|----------------:|-------------:|----------:|
| &#39;AsyncNoCapture | System&#39;                            | .NET 10 | Default   | 1,125.13 μs |  20.151 μs |  25.484 μs |     1.00x |    N/A |           1.00x |          N/A |  65.97 KB |
| &#39;AsyncNoCapture | Hyperbee&#39;                          | .NET 10 | Default   |   607.47 μs |  12.650 μs |  16.449 μs |     0.54x |    N/A |           0.97x |          N/A |  64.02 KB |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      | .NET 10 | Default   |   834.89 μs |  67.140 μs |  87.301 μs |       N/A |    N/A |             N/A |          N/A |  61.93 KB |
| &#39;AsyncCapture | System&#39;                              | .NET 10 | Default   | 1,210.51 μs |  24.444 μs |  32.632 μs |     1.00x |    N/A |           1.00x |          N/A |  66.44 KB |
| &#39;AsyncCapture | Hyperbee&#39;                            | .NET 10 | Default   | 1,022.71 μs | 216.640 μs | 289.208 μs |     0.84x |    N/A |           1.07x |          N/A |  70.81 KB |
| &#39;EnumerableNoCapture | System&#39;                       | .NET 10 | Default   |   943.32 μs | 113.852 μs | 148.040 μs |     1.00x |    N/A |           1.00x |          N/A |  44.09 KB |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     | .NET 10 | Default   |   338.16 μs |  24.029 μs |  30.389 μs |     0.36x |    N/A |           0.82x |          N/A |  36.25 KB |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; | .NET 10 | Default   |   591.28 μs |  24.913 μs |  33.258 μs |       N/A |    N/A |             N/A |          N/A |  42.03 KB |
| &#39;EnumerableCapture | System&#39;                         | .NET 10 | Default   |   829.35 μs |  10.031 μs |  12.319 μs |     1.00x |    N/A |           1.00x |          N/A |  45.26 KB |
| &#39;EnumerableCapture | Hyperbee&#39;                       | .NET 10 | Default   |   406.26 μs |  28.944 μs |  36.605 μs |     0.49x |    N/A |           0.95x |          N/A |  42.79 KB |
| &#39;NestedClosure | System&#39;                             | .NET 10 | Default   |   152.86 μs |   3.049 μs |   3.965 μs |     1.00x |    N/A |           1.00x |          N/A |   7.27 KB |
| &#39;NestedClosure | Hyperbee&#39;                           | .NET 10 | Default   |    37.28 μs |   4.436 μs |   5.768 μs |     0.24x |    N/A |           0.40x |          N/A |   2.94 KB |
| &#39;AsyncNoCapture | System&#39;                            | .NET 11 | .NET 11   | 1,097.05 μs |  68.161 μs |  88.628 μs |     1.00x |    N/A |           1.00x |          N/A |  66.63 KB |
| &#39;AsyncNoCapture | Hyperbee&#39;                          | .NET 11 | .NET 11   |   582.46 μs |  12.563 μs |  15.888 μs |     0.53x |    N/A |           0.96x |          N/A |   64.2 KB |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      | .NET 11 | .NET 11   |   764.04 μs |  24.773 μs |  33.071 μs |       N/A |    N/A |             N/A |          N/A |  62.12 KB |
| &#39;AsyncCapture | System&#39;                              | .NET 11 | .NET 11   | 1,192.76 μs |  34.186 μs |  43.234 μs |     1.00x |    N/A |           1.00x |          N/A |  66.45 KB |
| &#39;AsyncCapture | Hyperbee&#39;                            | .NET 11 | .NET 11   |   714.08 μs |  14.789 μs |  19.230 μs |     0.60x |    N/A |           1.07x |          N/A |  71.14 KB |
| &#39;EnumerableNoCapture | System&#39;                       | .NET 11 | .NET 11   |   829.25 μs |  24.445 μs |  31.786 μs |     1.00x |    N/A |           1.00x |          N/A |   44.2 KB |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     | .NET 11 | .NET 11   |   369.95 μs |  12.710 μs |  16.527 μs |     0.45x |    N/A |           0.82x |          N/A |  36.44 KB |
| &#39;EnumerableNoCapture (delegate MoveNext) | Hyperbee&#39; | .NET 11 | .NET 11   |   525.41 μs |  12.060 μs |  14.811 μs |       N/A |    N/A |             N/A |          N/A |  42.14 KB |
| &#39;EnumerableCapture | System&#39;                         | .NET 11 | .NET 11   |   857.32 μs |  41.971 μs |  56.030 μs |     1.00x |    N/A |           1.00x |          N/A |  45.36 KB |
| &#39;EnumerableCapture | Hyperbee&#39;                       | .NET 11 | .NET 11   |   382.40 μs |   5.541 μs |   7.008 μs |     0.45x |    N/A |           0.98x |          N/A |  44.29 KB |
| &#39;NestedClosure | System&#39;                             | .NET 11 | .NET 11   |   117.40 μs |   1.901 μs |   2.404 μs |     1.00x |    N/A |           1.00x |          N/A |   7.26 KB |
| &#39;NestedClosure | Hyperbee&#39;                           | .NET 11 | .NET 11   |    31.78 μs |   2.206 μs |   2.790 μs |     0.27x |    N/A |           0.40x |          N/A |   2.94 KB |
