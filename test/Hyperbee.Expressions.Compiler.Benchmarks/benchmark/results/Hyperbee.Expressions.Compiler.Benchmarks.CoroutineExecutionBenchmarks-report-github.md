```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host] : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9 : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Job=.NET 9  Runtime=.NET 9.0  IterationCount=20  
LaunchCount=1  WarmupCount=8  

```
| Method                                               | Mean          | Error       | StdDev      | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Gen0   | Allocated |
|----------------------------------------------------- |--------------:|------------:|------------:|----------:|-------:|----------------:|-------------:|-------:|----------:|
| &#39;AsyncSuspending x16 | Hyperbee&#39;                     | 9,493.9407 ns | 836.0230 ns | 821.0864 ns |       N/A |    N/A |             N/A |          N/A |      - |     152 B |
| &#39;AsyncSuspending x16 (delegate MoveNext) | Hyperbee&#39; | 8,827.3804 ns | 187.2328 ns | 208.1088 ns |       N/A |    N/A |             N/A |          N/A | 0.0153 |     152 B |
| &#39;AsyncNoCapture | System&#39;                            |   859.5069 ns |  43.6162 ns |  46.6689 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0277 |     232 B |
| &#39;AsyncNoCapture | Hyperbee&#39;                          |    54.0294 ns |   3.3373 ns |   3.8433 ns |     0.06x |    N/A |           0.72x |          N/A | 0.0200 |     168 B |
| &#39;AsyncNoCapture (delegate MoveNext) | Hyperbee&#39;      |    64.4557 ns |   3.9446 ns |   4.3844 ns |       N/A |    N/A |             N/A |          N/A | 0.0200 |     168 B |
| &#39;AsyncCapture | System&#39;                              |   898.8913 ns |  78.0808 ns |  89.9179 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0286 |     240 B |
| &#39;AsyncCapture | Hyperbee&#39;                            |    55.7982 ns |   4.7166 ns |   5.2425 ns |     0.06x |    N/A |           0.50x |          N/A | 0.0143 |     120 B |
| &#39;EnumerableNoCapture | System&#39;                       |   816.1460 ns |  31.9541 ns |  35.5169 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0134 |     112 B |
| &#39;EnumerableNoCapture | Hyperbee&#39;                     |    36.2016 ns |   1.6077 ns |   1.7870 ns |     0.04x |    N/A |           0.43x |          N/A | 0.0057 |      48 B |
| &#39;EnumerableCapture | System&#39;                         |   918.6911 ns |  69.7268 ns |  80.2975 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0229 |     192 B |
| &#39;EnumerableCapture | Hyperbee&#39;                       |    44.6377 ns |   3.0216 ns |   3.3585 ns |     0.05x |    N/A |           0.38x |          N/A | 0.0086 |      72 B |
| &#39;NestedClosure | System&#39;                             |     0.6602 ns |   0.0850 ns |   0.0979 ns |     1.00x |    N/A |           1.00x |          N/A |      - |         - |
| &#39;NestedClosure | Hyperbee&#39;                           |     1.3510 ns |   0.1206 ns |   0.1389 ns |     2.05x |    N/A |           1.00x |          N/A |      - |         - |
