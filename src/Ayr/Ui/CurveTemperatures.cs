using System.Collections.Generic;
using System.Linq;
using Ayr.Controllers;

namespace Ayr.Ui
{
    /// <summary>The temperature each fan curve step starts at, as chart labels: "&lt;55°" for the first, "55°" onwards.</summary>
    public sealed class CurveTemperatures
    {
        private readonly IReadOnlyList<string> _cpu;
        private readonly IReadOnlyList<string> _gpu;

        public CurveTemperatures(IReadOnlyList<int> cpu, IReadOnlyList<int> gpu)
        {
            _cpu = Labels(cpu);
            _gpu = Labels(gpu);
        }

        public IReadOnlyList<string> For(FanKind fan) => fan == FanKind.Cpu ? _cpu : _gpu;

        private static IReadOnlyList<string> Labels(IReadOnlyList<int> temperatures) =>
            temperatures.Select((temperature, point) => point == 0 ? "<" + temperatures[1] + "°" : temperature + "°").ToList();
    }
}
