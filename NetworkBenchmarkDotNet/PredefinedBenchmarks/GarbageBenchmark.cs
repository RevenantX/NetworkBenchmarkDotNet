﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GcBenchmark.cs">
//   Copyright (c) 2020 Johannes Deml. All rights reserved.
// </copyright>
// <author>
//   Johannes Deml
//   public@deml.io
// </author>
// --------------------------------------------------------------------------------------------------------------------

using BenchmarkDotNet.Attributes;

namespace NetworkBenchmark
{
	[Config(typeof(GarbageBenchmarkConfig))]
	public class GarbageBenchmark : APredefinedBenchmark
	{
		[ParamsAllValues]
		public NetworkLibrary Library { get; set; }

		protected override BenchmarkMode Mode => BenchmarkMode.Garbage;
		public override int MessageTarget => 10000;
		public override int ClientCount => 10;
		protected override NetworkLibrary LibraryTarget => Library;

		[GlobalSetup(Target = nameof(Garbage))]
		public void PrepareGarbageBenchmark()
		{
			BenchmarkCoordinator.ApplyPredefinedConfiguration();
			var config = BenchmarkCoordinator.Config;

			config.ParallelMessages = 10;
			config.MessageByteSize = 128;
			PrepareBenchmark();
		}

		[Benchmark]
		public long Garbage()
		{
			return RunBenchmark();
		}

		public override string ToString()
		{
			return "GarbageBenchmark";
		}
	}
}
