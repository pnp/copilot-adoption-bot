using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Running;

namespace BenchmarkSuite
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var config = DefaultConfig.Instance
                .AddDiagnoser(MemoryDiagnoser.Default);
            var _ = BenchmarkRunner.Run(typeof(Program).Assembly, config, args);
        }
    }
}
