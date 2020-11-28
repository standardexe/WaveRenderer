using System;
using System.Collections;
using System.Collections.Generic;

namespace WaveRenderer {
    class WaveProvider : IDisposable, IEnumerable<float[]> {
        private readonly NAudio.Wave.WaveFileReader reader;

        public WaveProvider(System.IO.Stream fileStream) {
            reader = new NAudio.Wave.WaveFileReader(fileStream);
        }

        public long SampleCount => reader.SampleCount;

        public TimeSpan CurrentTime { get => reader.CurrentTime; set => reader.CurrentTime = value; }

        public TimeSpan TotalTime => reader.TotalTime;

        public int Samplerate => reader.WaveFormat.SampleRate;

        public void Dispose() {
            reader.Dispose();
        }

        public IEnumerator<float[]> GetEnumerator() {
            while (true) {
                var sample = reader.ReadNextSampleFrame();
                if (sample == null) yield break;
                yield return sample;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return this.GetEnumerator();
        }
    }
}
