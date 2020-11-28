using System.Collections.Generic;

namespace WaveRenderer {
    interface IMinMax {
        MinMaxLayer Child { get; set; }
        double RelativeFactor { get; }
        double AbsoluteFactor { get; }
        IReadOnlyList<(float Min, float Max)> Values { get; }
    }
}
