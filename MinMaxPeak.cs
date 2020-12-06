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

        private (float Min, float Max) GetMinMax() {
            var min = buffer[0];
            var max = buffer[0];
            foreach (var s in buffer) {
                if (s < min) min = s;
                else if (s > max) max = s;
            }
            return (min, max);
        }

        public void Add(float value) {
            buffer[bufferSize++] = value;
            if (bufferSize == buffer.Length) {
                values.Add(GetMinMax());
                Child?.Add(values.Last());
                bufferSize = 0;
            }
        }
    }
}
