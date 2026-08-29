```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9278/25H2/2025Update/HudsonValley2)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.111
  [Host]    : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  .NET 9    : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3
  MediumRun : .NET 9.0.12 (9.0.12, 9.0.1225.60609), X64 RyuJIT x86-64-v3

Runtime=.NET 9.0  

```
| Method                           | Job       | IterationCount | LaunchCount | WarmupCount | Mean        | Error         | StdDev     | Median      | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Gen0   | Allocated |
|--------------------------------- |---------- |--------------- |------------ |------------ |------------:|--------------:|-----------:|------------:|----------:|-------:|----------------:|-------------:|-------:|----------:|
| &#39;AsyncNoCapture | System&#39;        | .NET 9    | 3              | 1           | 3           | 528.0237 ns |   361.6659 ns | 19.8241 ns | 530.5567 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0277 |     232 B |
| &#39;AsyncNoCapture | Hyperbee&#39;      | .NET 9    | 3              | 1           | 3           |  42.2338 ns |    54.6483 ns |  2.9955 ns |  42.7108 ns |     0.08x |    N/A |           0.72x |          N/A | 0.0200 |     168 B |
| &#39;AsyncCapture | System&#39;          | .NET 9    | 3              | 1           | 3           | 576.4517 ns | 1,476.9322 ns | 80.9556 ns | 545.6754 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0286 |     240 B |
| &#39;AsyncCapture | Hyperbee&#39;        | .NET 9    | 3              | 1           | 3           | 559.7563 ns |   903.5586 ns | 49.5271 ns | 535.3719 ns |     0.97x |    N/A |           1.10x |          N/A | 0.0315 |     264 B |
| &#39;EnumerableNoCapture | System&#39;   | .NET 9    | 3              | 1           | 3           | 586.4254 ns |   473.0846 ns | 25.9314 ns | 597.7931 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0134 |     112 B |
| &#39;EnumerableNoCapture | Hyperbee&#39; | .NET 9    | 3              | 1           | 3           |  22.8838 ns |    28.8981 ns |  1.5840 ns |  22.4743 ns |     0.04x |    N/A |           0.43x |          N/A | 0.0057 |      48 B |
| &#39;EnumerableCapture | System&#39;     | .NET 9    | 3              | 1           | 3           | 545.4181 ns | 1,205.9621 ns | 66.1029 ns | 513.2337 ns |     1.00x |    N/A |           1.00x |          N/A | 0.0229 |     192 B |
| &#39;EnumerableCapture | Hyperbee&#39;   | .NET 9    | 3              | 1           | 3           | 497.6159 ns |    41.1703 ns |  2.2567 ns | 498.4658 ns |     0.91x |    N/A |           1.12x |          N/A | 0.0257 |     216 B |
| &#39;NestedClosure | System&#39;         | .NET 9    | 3              | 1           | 3           |   0.6205 ns |     3.8971 ns |  0.2136 ns |   0.6730 ns |     1.00x |    N/A |           1.00x |          N/A |      - |         - |
| &#39;NestedClosure | Hyperbee&#39;       | .NET 9    | 3              | 1           | 3           |   8.9203 ns |    19.1941 ns |  1.0521 ns |   8.5893 ns |    14.37x |    N/A |               ∞ |          N/A | 0.0029 |      24 B |
| &#39;AsyncNoCapture | System&#39;        | MediumRun | 15             | 2           | 10          | 543.9846 ns |    31.1025 ns | 46.5528 ns | 541.4843 ns |     1.03x |    N/A |           1.00x |          N/A | 0.0277 |     232 B |
| &#39;AsyncNoCapture | Hyperbee&#39;      | MediumRun | 15             | 2           | 10          |  41.8549 ns |     2.4380 ns |  3.6491 ns |  40.2757 ns |     0.08x |    N/A |           0.72x |          N/A | 0.0200 |     168 B |
| &#39;AsyncCapture | System&#39;          | MediumRun | 15             | 2           | 10          | 564.7298 ns |    39.7363 ns | 59.4754 ns | 562.8368 ns |     0.98x |    N/A |           1.00x |          N/A | 0.0286 |     240 B |
| &#39;AsyncCapture | Hyperbee&#39;        | MediumRun | 15             | 2           | 10          | 547.7377 ns |    33.5959 ns | 47.0968 ns | 522.1474 ns |     0.95x |    N/A |           1.10x |          N/A | 0.0315 |     264 B |
| &#39;EnumerableNoCapture | System&#39;   | MediumRun | 15             | 2           | 10          | 482.0097 ns |    14.1553 ns | 19.3759 ns | 473.5492 ns |     0.82x |    N/A |           1.00x |          N/A | 0.0134 |     112 B |
| &#39;EnumerableNoCapture | Hyperbee&#39; | MediumRun | 15             | 2           | 10          |  22.2070 ns |     1.3042 ns |  1.9117 ns |  21.7147 ns |     0.04x |    N/A |           0.43x |          N/A | 0.0057 |      48 B |
| &#39;EnumerableCapture | System&#39;     | MediumRun | 15             | 2           | 10          | 549.3840 ns |    32.7755 ns | 49.0569 ns | 542.3422 ns |     1.01x |    N/A |           1.00x |          N/A | 0.0229 |     192 B |
| &#39;EnumerableCapture | Hyperbee&#39;   | MediumRun | 15             | 2           | 10          | 559.5536 ns |    28.8087 ns | 43.1195 ns | 565.3195 ns |     1.03x |    N/A |           1.12x |          N/A | 0.0257 |     216 B |
| &#39;NestedClosure | System&#39;         | MediumRun | 15             | 2           | 10          |   0.4440 ns |     0.1357 ns |  0.2031 ns |   0.4431 ns |     0.72x |    N/A |           1.00x |          N/A |      - |         - |
| &#39;NestedClosure | Hyperbee&#39;       | MediumRun | 15             | 2           | 10          |   9.9884 ns |     0.4572 ns |  0.6409 ns |  10.1963 ns |    16.10x |    N/A |               ∞ |          N/A | 0.0029 |      24 B |
