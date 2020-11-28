using System.Collections.Generic;
using System.Linq;

namespace WaveRenderer {
    class MinMaxPeak : IMinMax {
        private readonly List<(float Min, float Max)> values = new List<(float, float)>();

        private readonly float[] buffer;
        private int bufferSize;

        public MinMaxPeak(int factor) {
            buffer = new float[factor];
            bufferSize = 0;
            RelativeFactor = factor;
        }

        public double RelativeFactor { get; }

        public double AbsoluteFactor => RelativeFactor;

        public MinMaxLayer Child { get; set; }

        public IReadOnlyList<(float Min, float Max)> Values => values;

        public void Add(IEnumerable<float> values) {
            foreach (var value in values) {
                Add(value);
            }
        }

        public void Add(float value) {
            buffer[bufferSize++] = value;
            if (bufferSize == buffer.Length) {
                values.Add((buffer.Min(), buffer.Max()));
                Child?.Add(values.Last());
                bufferSize = 0;
            }
        }
    }
}
