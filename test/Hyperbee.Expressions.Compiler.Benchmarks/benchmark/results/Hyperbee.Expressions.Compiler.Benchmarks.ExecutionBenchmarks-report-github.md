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
| Method                | Job     | Toolchain | Mean       | Error      | StdDev    | vs System | vs Fec | Alloc vs System | Alloc vs Fec | Gen0   | Allocated |
|---------------------- |-------- |---------- |-----------:|-----------:|----------:|----------:|-------:|----------------:|-------------:|-------:|----------:|
| &#39;Simple | System&#39;     | .NET 10 | Default   |  0.4046 ns |  0.8485 ns | 0.0465 ns |     1.00x |  0.35x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | FEC&#39;        | .NET 10 | Default   |  1.1619 ns |  4.2048 ns | 0.2305 ns |     2.87x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | Hyperbee&#39;   | .NET 10 | Default   |  0.9865 ns |  3.3097 ns | 0.1814 ns |     2.44x |  0.85x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | System&#39;    | .NET 10 | Default   |  0.3953 ns |  1.7181 ns | 0.0942 ns |     1.00x |  0.45x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | FEC&#39;       | .NET 10 | Default   |  0.8841 ns |  1.4407 ns | 0.0790 ns |     2.24x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | Hyperbee&#39;  | .NET 10 | Default   |  0.8381 ns |  1.3364 ns | 0.0733 ns |     2.12x |  0.95x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | System&#39;   | .NET 10 | Default   |  0.4642 ns |  2.4359 ns | 0.1335 ns |     1.00x |  0.44x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | FEC&#39;      | .NET 10 | Default   |  1.0610 ns |  4.5087 ns | 0.2471 ns |     2.29x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | Hyperbee&#39; | .NET 10 | Default   |  1.1005 ns |  2.0355 ns | 0.1116 ns |     2.37x |  1.04x |           1.00x |        1.00x |      - |         - |
| &#39;Complex | System&#39;    | .NET 10 | Default   | 26.2068 ns | 70.5615 ns | 3.8677 ns |     1.00x |  1.03x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | FEC&#39;       | .NET 10 | Default   | 25.5234 ns | 25.0679 ns | 1.3741 ns |     0.97x |  1.00x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | Hyperbee&#39;  | .NET 10 | Default   | 24.8413 ns | 25.0346 ns | 1.3722 ns |     0.95x |  0.97x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Loop | System&#39;       | .NET 10 | Default   | 29.7922 ns | 15.7547 ns | 0.8636 ns |     1.00x |  0.93x |           1.00x |        1.00x |      - |         - |
| &#39;Loop | FEC&#39;          | .NET 10 | Default   | 32.1825 ns | 35.9237 ns | 1.9691 ns |     1.08x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Loop | Hyperbee&#39;     | .NET 10 | Default   | 34.1602 ns | 61.9971 ns | 3.3983 ns |     1.15x |  1.06x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | System&#39;     | .NET 10 | Default   |  1.5275 ns |  3.9289 ns | 0.2154 ns |     1.00x |  0.75x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | FEC&#39;        | .NET 10 | Default   |  2.0297 ns |  4.1184 ns | 0.2257 ns |     1.33x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | Hyperbee&#39;   | .NET 10 | Default   |  1.4364 ns |  2.5319 ns | 0.1388 ns |     0.94x |  0.71x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | System&#39;     | .NET 11 | .NET 11   |  0.5147 ns |  2.0320 ns | 0.1114 ns |     1.00x |  0.49x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | FEC&#39;        | .NET 11 | .NET 11   |  1.0410 ns |  1.6254 ns | 0.0891 ns |     2.02x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Simple | Hyperbee&#39;   | .NET 11 | .NET 11   |  1.0466 ns |  1.1007 ns | 0.0603 ns |     2.03x |  1.01x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | System&#39;    | .NET 11 | .NET 11   |  0.4686 ns |  2.3344 ns | 0.1280 ns |     1.00x |  0.32x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | FEC&#39;       | .NET 11 | .NET 11   |  1.4760 ns |  5.7491 ns | 0.3151 ns |     3.15x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Closure | Hyperbee&#39;  | .NET 11 | .NET 11   |  1.2963 ns |  4.1864 ns | 0.2295 ns |     2.77x |  0.88x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | System&#39;   | .NET 11 | .NET 11   |  0.5086 ns |  3.4199 ns | 0.1875 ns |     1.00x |  0.71x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | FEC&#39;      | .NET 11 | .NET 11   |  0.7213 ns |  3.6326 ns | 0.1991 ns |     1.42x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;TryCatch | Hyperbee&#39; | .NET 11 | .NET 11   |  0.9280 ns |  2.5367 ns | 0.1390 ns |     1.82x |  1.29x |           1.00x |        1.00x |      - |         - |
| &#39;Complex | System&#39;    | .NET 11 | .NET 11   | 26.3610 ns | 40.3963 ns | 2.2143 ns |     1.00x |  0.96x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | FEC&#39;       | .NET 11 | .NET 11   | 27.5161 ns | 23.1384 ns | 1.2683 ns |     1.04x |  1.00x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Complex | Hyperbee&#39;  | .NET 11 | .NET 11   | 23.6266 ns | 55.9277 ns | 3.0656 ns |     0.90x |  0.86x |           1.00x |        1.00x | 0.0038 |      32 B |
| &#39;Loop | System&#39;       | .NET 11 | .NET 11   | 31.1220 ns | 13.5847 ns | 0.7446 ns |     1.00x |  0.94x |           1.00x |        1.00x |      - |         - |
| &#39;Loop | FEC&#39;          | .NET 11 | .NET 11   | 33.2089 ns | 52.1578 ns | 2.8589 ns |     1.07x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Loop | Hyperbee&#39;     | .NET 11 | .NET 11   | 31.6303 ns | 13.0417 ns | 0.7149 ns |     1.02x |  0.95x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | System&#39;     | .NET 11 | .NET 11   |  2.4550 ns |  8.9919 ns | 0.4929 ns |     1.00x |  0.91x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | FEC&#39;        | .NET 11 | .NET 11   |  2.6914 ns |  7.1233 ns | 0.3905 ns |     1.10x |  1.00x |           1.00x |        1.00x |      - |         - |
| &#39;Switch | Hyperbee&#39;   | .NET 11 | .NET 11   |  2.1863 ns |  6.5301 ns | 0.3579 ns |     0.89x |  0.81x |           1.00x |        1.00x |      - |         - |
