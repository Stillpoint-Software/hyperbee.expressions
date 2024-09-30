﻿using BenchmarkDotNet.Running;

namespace Hyperbee.AsyncExpressions.Benchmark;

internal class Program
{
    static void Main( string[] args )
    {
        BenchmarkSwitcher.FromAssembly( typeof( Program ).Assembly ).Run( args, new BenchmarkConfig.Config() );
    }
}
