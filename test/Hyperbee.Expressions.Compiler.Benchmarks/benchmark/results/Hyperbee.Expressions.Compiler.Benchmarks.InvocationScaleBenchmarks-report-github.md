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
| Method                      | Job     | Toolchain | Mean     | Error     | StdDev    | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Allocated |
|---------------------------- |-------- |---------- |---------:|----------:|----------:|----------:|-------:|----------------:|-------------:|----------:|
| &#39;Simple x1000 | System&#39;     | .NET 10 | Default   | 1.470 μs | 0.0626 μs | 0.0721 μs |     1.00x |  0.75x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | FEC&#39;        | .NET 10 | Default   | 1.949 μs | 0.0506 μs | 0.0562 μs |     1.33x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | Hyperbee&#39;   | .NET 10 | Default   | 1.988 μs | 0.0578 μs | 0.0666 μs |     1.35x |  1.02x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | System&#39;   | .NET 10 | Default   | 1.186 μs | 0.0311 μs | 0.0358 μs |     1.00x |  0.70x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | FEC&#39;      | .NET 10 | Default   | 1.684 μs | 0.0636 μs | 0.0732 μs |     1.42x |  1.00x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | Hyperbee&#39; | .NET 10 | Default   | 1.660 μs | 0.0474 μs | 0.0546 μs |     1.40x |  0.99x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | System&#39;     | .NET 10 | Default   | 2.286 μs | 0.1091 μs | 0.1256 μs |     1.00x |  0.80x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | FEC&#39;        | .NET 10 | Default   | 2.863 μs | 0.1303 μs | 0.1501 μs |     1.25x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | Hyperbee&#39;   | .NET 10 | Default   | 2.884 μs | 0.1384 μs | 0.1539 μs |     1.26x |  1.01x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | System&#39;     | .NET 11 | .NET 11   | 1.291 μs | 0.0553 μs | 0.0637 μs |     1.00x |  0.75x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | FEC&#39;        | .NET 11 | .NET 11   | 1.715 μs | 0.0380 μs | 0.0374 μs |     1.33x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Simple x1000 | Hyperbee&#39;   | .NET 11 | .NET 11   | 1.703 μs | 0.0638 μs | 0.0735 μs |     1.32x |  0.99x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | System&#39;   | .NET 11 | .NET 11   | 1.217 μs | 0.0570 μs | 0.0656 μs |     1.00x |  0.74x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | FEC&#39;      | .NET 11 | .NET 11   | 1.652 μs | 0.0457 μs | 0.0527 μs |     1.36x |  1.00x |           1.00x |        1.00x |         - |
| &#39;TryCatch x1000 | Hyperbee&#39; | .NET 11 | .NET 11   | 1.647 μs | 0.0485 μs | 0.0539 μs |     1.35x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | System&#39;     | .NET 11 | .NET 11   | 2.145 μs | 0.1288 μs | 0.1484 μs |     1.00x |  0.84x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | FEC&#39;        | .NET 11 | .NET 11   | 2.557 μs | 0.1024 μs | 0.1179 μs |     1.19x |  1.00x |           1.00x |        1.00x |         - |
| &#39;Switch x1000 | Hyperbee&#39;   | .NET 11 | .NET 11   | 2.515 μs | 0.0861 μs | 0.0992 μs |     1.17x |  0.98x |           1.00x |        1.00x |         - |
