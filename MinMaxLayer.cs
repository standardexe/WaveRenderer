using System.Collections.Generic;
using System.Linq;

namespace WaveRenderer {
    class MinMaxLayer : IMinMax {
        private readonly List<(float Min, float Max)> values = new List<(float, float)>();

        private readonly float[] bufferMin;
        private readonly float[] bufferMax;
        private int bufferSize;

        public MinMaxLayer(IMinMax parent, int factor) {
            bufferMin = new float[factor];
            bufferMax = new float[factor];
            bufferSize = 0;
            RelativeFactor = factor;
            AbsoluteFactor = parent.AbsoluteFactor * factor;
            parent.Child = this;
        }

        public double RelativeFactor { get; }

        public double AbsoluteFactor { get; }

        public MinMaxLayer Child { get; set; }

        public IReadOnlyList<(float Min, float Max)> Values => values;

        public void Add((float min, float max) value) {
            bufferMax[bufferSize] = value.max;
            bufferMin[bufferSize++] = value.min;
            if (bufferSize == bufferMax.Length) {
                values.Add((bufferMin.Min(), bufferMax.Max()));
                Child?.Add(values.Last());
                bufferSize = 0;
            }
        }
    }
}
