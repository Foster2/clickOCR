using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Windows.Foundation;
using Windows.Media.Ocr;
using Point = System.Drawing.Point;

namespace ClickOCR
{
    internal sealed class PickResult
    {
        internal string Text = "";
        internal string MatchedWord = "";
        internal Rectangle WordRect = Rectangle.Empty;
        internal double Distance = -1.0;
    }

    /// <summary>Turns an OCR result plus a click point into the text the user meant.</summary>
    internal static class TextPicker
    {
        private sealed class Token
        {
            internal string Text;
            internal Rectangle Rect;
            internal int Line;
        }

        internal static PickResult Pick(OcrResult result, Point point, GrabMode mode)
        {
            PickResult picked = new PickResult();
            if (result == null) return picked;

            List<Token> tokens = new List<Token>();
            List<string> lineTexts = new List<string>();

            foreach (OcrLine line in result.Lines)
            {
                int lineIndex = lineTexts.Count;
                lineTexts.Add(Normalize(line.Text));

                foreach (OcrWord word in line.Words)
                {
                    string text = (word.Text ?? "").Trim();
                    if (text.Length == 0) continue;

                    Rect r = word.BoundingRect;
                    Token token = new Token();
                    token.Text = text;
                    token.Rect = new Rectangle(
                        (int)Math.Round(r.X),
                        (int)Math.Round(r.Y),
                        Math.Max(1, (int)Math.Round(r.Width)),
                        Math.Max(1, (int)Math.Round(r.Height)));
                    token.Line = lineIndex;
                    tokens.Add(token);
                }
            }

            if (tokens.Count == 0)
            {
                if (mode == GrabMode.Block) picked.Text = Normalize(result.Text);
                return picked;
            }

            Token best = null;
            double bestDistance = double.MaxValue;

            foreach (Token token in tokens)
            {
                Rectangle loose = Inflate(token.Rect, 6, 4);
                if (loose.Contains(point))
                {
                    best = token;
                    bestDistance = 0.0;
                    break;
                }

                double distance = DistanceToRect(token.Rect, point);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = token;
                }
            }

            if (best == null) return picked;

            // Refuse to grab text that is far away from the click.
            double limit = Math.Max(28.0, best.Rect.Height * 1.25);
            if (bestDistance > limit)
            {
                if (mode == GrabMode.Block) picked.Text = Normalize(result.Text);
                return picked;
            }

            picked.MatchedWord = best.Text;
            picked.WordRect = best.Rect;
            picked.Distance = bestDistance;

            switch (mode)
            {
                case GrabMode.Word:
                    picked.Text = best.Text;
                    break;

                case GrabMode.Line:
                    picked.Text = best.Line < lineTexts.Count ? lineTexts[best.Line] : best.Text;
                    if (picked.Text.Length == 0) picked.Text = best.Text;
                    break;

                default:
                    picked.Text = JoinLines(lineTexts);
                    break;
            }

            return picked;
        }

        private static string JoinLines(List<string> lines)
        {
            StringBuilder builder = new StringBuilder();
            foreach (string line in lines)
            {
                if (line.Length == 0) continue;
                if (builder.Length > 0) builder.Append("\r\n");
                builder.Append(line);
            }
            return builder.ToString();
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder builder = new StringBuilder(text.Length);
            bool previousWasSpace = false;
            foreach (char c in text)
            {
                char ch = c;
                if (ch == '\t' || ch == '\r' || ch == '\n') ch = ' ';
                if (ch == ' ')
                {
                    if (previousWasSpace) continue;
                    previousWasSpace = true;
                }
                else
                {
                    previousWasSpace = false;
                }
                builder.Append(ch);
            }
            return builder.ToString().Trim();
        }

        private static Rectangle Inflate(Rectangle rect, int x, int y)
        {
            return new Rectangle(rect.X - x, rect.Y - y, rect.Width + x * 2, rect.Height + y * 2);
        }

        private static double DistanceToRect(Rectangle rect, Point point)
        {
            double dx = 0.0;
            double dy = 0.0;

            if (point.X < rect.Left) dx = rect.Left - point.X;
            else if (point.X > rect.Right) dx = point.X - rect.Right;

            if (point.Y < rect.Top) dy = rect.Top - point.Y;
            else if (point.Y > rect.Bottom) dy = point.Y - rect.Bottom;

            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
