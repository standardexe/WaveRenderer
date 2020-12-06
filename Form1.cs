using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WaveRenderer {
    public partial class Form1 : Form {
        private MinMaxPeak pyramid;
        private WaveProvider reader;
        private double zoomFactor;
        private LinearGradientBrush gradientBrush;
        private Pen gradientPen;
        private (bool Active, int Start, int End) selection;

        public Form1() {
            InitializeComponent();

            if (openFileDialog1.ShowDialog() != DialogResult.OK) {
                Application.Exit();
            }
            reader = new WaveProvider(System.IO.File.Open(openFileDialog1.FileName, System.IO.FileMode.Open));

            // build a zoom level pyramid data structure
            var zoomLevels = new[] { 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
            pyramid = BuildPyramid(zoomLevels);

            // render channel 0
            pyramid.Add(reader.Select(frame => frame[0]));

            // resource/control initialization
            pictureBox1_Resize(this, new EventArgs { });
            hScrollBarPos.Maximum = (int)reader.SampleCount;
            hScrollBarPos.SmallChange = reader.Samplerate / 1000;
            hScrollBarPos.LargeChange = reader.Samplerate / 100;

            comboBoxZoom.SelectedIndex = 0;
            comboBoxZoom_SelectionChangeCommitted(this, new EventArgs { });

            this.MouseWheel += Form1_MouseWheel;
        }

        private double ZoomFactor {
            get => zoomFactor;
            set {
                zoomFactor = value;
                comboBoxZoom.Text = $"{Math.Round(100 * zoomFactor / reader.SampleCount * pictureBox1.Width, 3)}%";
            }
        }

        private void Form1_MouseWheel(object sender, MouseEventArgs e) {
            const float scale_per_delta = 0.1f / 120;
            var pdash = hScrollBarPos.Value + ZoomFactor * e.X;
            ZoomFactor = Math.Max(1, ZoomFactor * Math.Pow(10, e.Delta * scale_per_delta));
            hScrollBarPos.Value = Math.Max(0, (int)(pdash - e.X * ZoomFactor));
            pictureBox1.Invalidate();
        }

        private MinMaxPeak BuildPyramid(int[] factors) {
            MinMaxPeak peak = new MinMaxPeak(factors[0]);
            IMinMax node = peak;
            for (int i = 1; i < factors.Length; i++) {
                node = new MinMaxLayer(node, factors[i] / factors[i - 1]);
            }
            return peak;
        }

        private static void Draw(float[] values, Pen pen, Graphics g, int height, int width) {
            if (values.Length == 0) return;

            var y = height / 2;

            // sampleStep is x coordinate increment in screen space.
            var sampleStep = width / (float)values.Count();

            var line = values.Select((v, i) => new PointF(i * sampleStep, y - v * y)).ToArray();
            g.DrawLines(pen, line);
        }

        private static void Draw(IMinMax layer, int position, double zoomFactor, Brush brush, Graphics g, int height, int width) {
            var x = 0.0f;
            var y = height / 2;

            // the MinMaxLayer's data is already "zoomed out". Find out the relative zoom factor
            // between the MinMaxLayer's zoom and the target zoom. That way we can calculate the
            // number of samples required from the layer for the current zoom level.
            var sampleCount = (int)(zoomFactor / layer.AbsoluteFactor * width);
            if (sampleCount == 0) return;

            // position needs to go from "absolute samples" to "MinMaxLayer samples".
            var layerPosition = (int)(position / layer.AbsoluteFactor);

            // sampleStep is x coordinate increment in screen space.
            var sampleStep = width / (float)sampleCount;

            // Build a closed curve where all values upside of the middle are in the first half of
            // the curve array and all values downside of the middle are in the second half of the
            // curve array, but in reversed order, like this:
            //
            // a^ -> b^ -> c^ -> d^ -> e^ .
            //                            |
            //                            v
            // a_ <- b_ <- c_ <- d_ <- e_
            //
            var hull = new PointF[Math.Min(layer.Values.Count - layerPosition, sampleCount) * 2];
            var i = 0;
            foreach (var (Min, Max) in layer.Values.Skip(layerPosition).Take(sampleCount)) {
                hull[i]                   = new PointF(x, y - Max * y);
                hull[hull.Length - i - 1] = new PointF(x, y - Min * y);
                i++;
                x += sampleStep;
            }

            g.FillClosedCurve(brush, hull);
        }

        private void DrawTimeBars((TimeSpan start, TimeSpan end) window, Graphics g) {
            var timeDiff = window.Duration();
            var timeStep = TimeSpan.FromSeconds(1);

            // Key: maximum window length
            // Value: Tuple of | roundTo: the nearest interval beginning to round to (in seconds)
            //                 | step: interval size (in seconds)
            var lookup = new Dictionary<double, (int roundTo, double step)>() {
                [  0.1] = ( 1,  0.01),
                [  0.6] = ( 1,  0.05),
                [  1.2] = ( 1,  0.1),
                [  2.0] = ( 1,  0.5),
                [ 10.0] = ( 1,  1),
                [ 15.0] = ( 5,  5),
                [ 40.0] = (10, 10),
                [ 60.0] = (15, 15),
                [120.0] = (30, 30),
                [double.MaxValue] = (60, 60)
            };

            // find the lowest maximum window length in the lookup
            foreach (var kvp in lookup.OrderBy(kvp => kvp.Key)) {
                if (timeDiff.TotalSeconds < kvp.Key) {
                    // round the window beginning
                    window.start = TimeSpan.FromSeconds(Math.Floor(window.start.TotalSeconds / kvp.Value.roundTo) * kvp.Value.roundTo);
                    timeStep = TimeSpan.FromSeconds(kvp.Value.step);
                    break;
                }
            }

            while (window.start < window.end) {
                var screenPos = (float)((TimeToSamples(window.start) - hScrollBarPos.Value) / ZoomFactor);
                g.DrawLine(Pens.LightGray, screenPos, 0, screenPos, pictureBox1.Height);
                g.DrawString(window.start.ToString(@"m\:ss\.ff"), SystemFonts.CaptionFont, Brushes.Black, screenPos, 3);
                window.start = window.start + timeStep;
            }
        }

        private static void DrawSelection((TimeSpan Start, TimeSpan End) window, (TimeSpan Start, TimeSpan end) selection, int height, int width, Graphics g) {
            var xStart = (selection.Start - window.Start).TotalSeconds / window.Duration().TotalSeconds * width;
            var xEnd   = (selection.end   - window.Start).TotalSeconds / window.Duration().TotalSeconds * width;
            g.FillRectangle(Brushes.Yellow, (int)xStart, 0, (int)(xEnd - xStart), height);
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e) {
            e.Graphics.Clear(Color.White);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var window = (start: SamplesToTime(hScrollBarPos.Value),
                          end:   SamplesToTime(hScrollBarPos.Value) + SamplesToTime(ZoomFactor * pictureBox1.Width));

            if (selection.Active) {
                var selectStartTime = SamplesToTime(selection.Start);
                var selectEndTime   = SamplesToTime(selection.End);
                DrawSelection(window, (selectStartTime, selectEndTime), pictureBox1.Height, pictureBox1.Width, e.Graphics);
            }

            DrawTimeBars(window, e.Graphics);

            e.Graphics.ScaleTransform(1, -1);
            e.Graphics.TranslateTransform(0, -pictureBox1.Height);

            if (ZoomFactor < pyramid.RelativeFactor) {
                reader.CurrentTime = window.start;
                var sampleCount = TimeToSamples(window.Duration());
                var values = reader.Take(sampleCount).Select(frame => frame[0]).ToArray();
                Draw(values, gradientPen, e.Graphics, pictureBox1.Height, pictureBox1.Width);
            } else {
                var layer = (IMinMax)pyramid;
                while (layer.Child != null) {
                    if (layer.Child.AbsoluteFactor > ZoomFactor) break;
                    layer = layer.Child;
                }

                Draw(layer, hScrollBarPos.Value, ZoomFactor, gradientBrush, e.Graphics, pictureBox1.Height, pictureBox1.Width);
            }
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e) {
            selection.Start = selection.End = (int)(hScrollBarPos.Value + e.Location.X * ZoomFactor);
            selection.Active = true;
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                selection.End = (int)(hScrollBarPos.Value + e.Location.X * ZoomFactor);
                pictureBox1.Invalidate();
            }
        }

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e) {
            selection.Active = false;
            if (e.Button == MouseButtons.Left) {
                ZoomFactor = Math.Max(1, (selection.End - selection.Start) / (double)pictureBox1.Width);
                hScrollBarPos.Value = selection.Start;
            }
        }

        private void hScrollBarPos_ValueChanged(object sender, EventArgs e) {
            var time = TimeSpan.FromSeconds(hScrollBarPos.Value / reader.Samplerate);
            labelTime.Text = $"Time ({time.ToString("m\\:ss")})";
            pictureBox1.Invalidate();
        }

        private void pictureBox1_Resize(object sender, EventArgs e) {
            var y = pictureBox1.Height / 2;

            gradientBrush?.Dispose();
            gradientPen?.Dispose();

            gradientBrush = new LinearGradientBrush(
                new Point(0, y + y * 3 / 2),
                new Point(0, y - y * 3 / 2),
                Color.FromArgb(255, 255, 0, 0),
                Color.FromArgb(255, 0, 0, 255));

            gradientPen = new Pen(gradientBrush);
        }

        // TimeSpan will only give us enough precision if constructed FromTicks!
        private TimeSpan SamplesToTime(double sampleCount) => 
            TimeSpan.FromTicks((long)(sampleCount / reader.Samplerate * TimeSpan.TicksPerSecond));

        private TimeSpan SamplesToTime(int sampleCount) => 
            TimeSpan.FromTicks((long)(sampleCount / (double)reader.Samplerate * TimeSpan.TicksPerSecond));

        private int TimeToSamples(TimeSpan time) => (int)(time.TotalSeconds * reader.Samplerate);

        private void comboBoxZoom_SelectionChangeCommitted(object sender, EventArgs e) {
            var text = comboBoxZoom.SelectedItem as string;
            ZoomFactor = int.Parse(text.Substring(0, text.Length - 1)) / 100.0 * reader.SampleCount / pictureBox1.Width;
            pictureBox1.Invalidate();
        }
    }

    static class Extensions {
        public static TimeSpan Duration(this (TimeSpan lower, TimeSpan upper) interval) {
            return interval.upper - interval.lower;
        }
    }
}
