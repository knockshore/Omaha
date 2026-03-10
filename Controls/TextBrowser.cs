using SkiaSharp;
using System.Text.RegularExpressions;

namespace Omaha.Controls;

/// <summary>
/// A rich-text browser that renders the HTML diff report produced by
/// PDFCompare (generate_html_diff / generate_html_diff_pagewise).
///
/// Supported HTML constructs:
///   • &lt;div class="page-header"&gt;   — bold section header (dark-blue bg, white text)
///   • &lt;div class="page-summary"&gt;  — per-page summary line (light-grey bg)
///   • &lt;div class="summary-bar"&gt;   — overall summary (light-blue bg)
///   • &lt;tr style="background:#HEX"&gt; — data rows with inline background colour
///   • &lt;th&gt; header rows             — column headers (blue bg, white text)
///   • Three-column layout: Status (15 %) | Template (42.5 %) | Report (42.5 %)
///   • Plain text fallback when no structural elements are found
/// </summary>
public class TextBrowser : Control
{
    // ── Public API ─────────

    public float FontSize    { get; set; }
    public float ContentHeight { get; private set; }

    // ── Scroll state ──────────────────────────────────────────────────────────
    private float _scrollY;
    private bool  _thumbDragging;
    private float _thumbDragStartY;    // mouse Y when drag began
    private float _thumbDragStartScroll; // _scrollY when drag began

    public float ScrollY
    {
        get => _scrollY;
        set { _scrollY = ClampScroll(value); Invalidate(); }
    }

    private float MaxScroll => Math.Max(0, ContentHeight - Height);
    private float ClampScroll(float v) => Math.Clamp(v, 0, MaxScroll);

    private string _text = string.Empty;
    private string _html = string.Empty;
    // Replaced atomically by the parser; Render snapshots the reference before iterating
    // to avoid "Collection was modified" races between the RPC thread and the render thread.
    private volatile TextLine[] _lines = [];

    public string Text
    {
        get => _text;
        set { _text = value; RebuildLines(); Invalidate(); }
    }

    public string Html
    {
        get => _html;
        set { _html = value; ParseHtmlIntoLines(value); Invalidate(); }
    }

    public TextBrowser() { MinHeight = 50; }

    // ── patterns Regex ─────────────────────────────────────────────────

    // <div class="…">…</div>
    private static readonly Regex _divRx = new(
        @"<div\s[^>]*class=""([^""]*)""[^>]*>([\s\S]*?)</div>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    // <tr …>…</tr>
    private static readonly Regex _rowRx = new(
        @"<tr([^>]*)>([\s\S]*?)</tr>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    // <td …>…</td>  or  <th …>…</th>
    private static readonly Regex _cellRx = new(
        @"<t([dh])([^>]*)>([\s\S]*?)</t[dh]>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    // background:#HEX inside a style attribute
    private static readonly Regex _bgRx = new(
        @"background\s*:\s*(#[0-9a-fA-F]{3,6}|[a-zA-Z]+)",
        RegexOptions.IgnoreCase);

    // Strip all HTML tags
    private static readonly Regex _tagRx = new(@"<[^>]+>", RegexOptions.IgnoreCase);

    // ── Colour helpers ────────────────────────────────────────────────

    private static SKColor ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 3)
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
        if (hex.Length == 6 &&
            uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return new SKColor((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
        return SKColor.Empty;
    }

    private static SKColor ColorFromStyle(string attrs)
    {
        var m = _bgRx.Match(attrs);
        if (!m.Success) return SKColor.Empty;
        var v = m.Groups[1].Value;
        return v.StartsWith('#') ? ParseHex(v) : v.ToLowerInvariant() switch
        {
            "white"  => SKColors.White,
            "red"    => new SKColor(0xFF, 0xC7, 0xCE),
            "green"  => new SKColor(0xC6, 0xEF, 0xCE),
            "yellow" => new SKColor(0xFF, 0xEB, 0x9C),
            _        => SKColor.Empty,
        };
    }

    // Fixed palette
    private static readonly SKColor PageHeaderBg  = new(0x2C, 0x5A, 0xA0);
    private static readonly SKColor PageHeaderFg  = SKColors.White;
    private static readonly SKColor ColHeaderBg   = new(0x44, 0x72, 0xC4);
    private static readonly SKColor ColHeaderFg   = SKColors.White;
    private static readonly SKColor SummaryBg     = new(0xDD, 0xEA, 0xF5);
    private static readonly SKColor SummarySepBg  = new(0xF0, 0xF4, 0xFF);

    // ── Column layout helpers ─────────────
    // Status 15 % | Template 42.5 % | Report 42.5 %

    private static (float x, float w)[] CalcCols(float totalW, float pad)
    {
        float avail = totalW - pad * 2;
        return
        [
            (pad,                      avail * 0.15f),
            (pad + avail * 0.15f,      avail * 0.425f),
            (pad + avail * 0.575f,     avail * 0.425f),
        ];
    }

    // ── HTML parser ───────

    private void ParseHtmlIntoLines(string html)
    {
        if (string.IsNullOrEmpty(html)) { ContentHeight = 0; _lines = []; return; }

        float fs   = FontSize > 0 ? FontSize : Theme.FontSizeNormal;
        float lh   = fs + 6f;
        float pad  = Theme.Padding;
        float y    = pad;

        // Remove <style> / <script> blocks
        var work = Regex.Replace(html,
            @"<style[^>]*>[\s\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
        work = Regex.Replace(work,
            @"<script[^>]*>[\s\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);

        // Collect events in source order
        var events = new List<(int pos, string kind, string attrs, string inner)>();

        foreach (Match m in _divRx.Matches(work))
            events.Add((m.Index, "div", m.Groups[1].Value.ToLowerInvariant(), m.Groups[2].Value));

        foreach (Match m in _rowRx.Matches(work))
            events.Add((m.Index, "tr", m.Groups[1].Value, m.Groups[2].Value));

        events.Sort((a, b) => a.pos.CompareTo(b.pos));

        System.Diagnostics.Debug.WriteLine(
            $"[TextBrowser] ParseHtmlIntoLines: {html.Length} chars, {events.Count} events");

        // Build into a local list, then assign atomically so Render never sees a
        // partially-built collection (fixes "Collection was modified" crash).
        var built = new List<TextLine>();

        if (events.Count == 0)
        {
            // Plain-text fallback
            foreach (var rawLine in StripHtml(html).Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(rawLine))
                {
                    built.Add(new TextLine { Segs = [new Seg(rawLine, SKColor.Empty)], Y = y });
                    y += lh;
                }
            }
            ContentHeight = y + pad;
            _lines = built.ToArray();
            return;
        }

        foreach (var (_, kind, attrs, inner) in events)
        {
            if (kind == "div")
            {
                var text = Decode(_tagRx.Replace(inner, " ")).Trim();
                text = Regex.Replace(text, @"\s{2,}", " ");
                if (string.IsNullOrWhiteSpace(text)) continue;

                SKColor bg   = SKColor.Empty;
                SKColor fg   = Theme.TextPrimary;
                bool    bold = false;

                if (attrs.Contains("page-header"))
                {
                    bg   = PageHeaderBg;
                    fg   = PageHeaderFg;
                    bold = true;
                    y   += lh * 0.4f;   // small spacer before each page section
                }
                else if (attrs.Contains("summary-bar"))
                {
                    bg = SummarySepBg;
                }
                else if (attrs.Contains("page-summary"))
                {
                    bg = SummaryBg;
                }

                built.Add(new TextLine
                {
                    Segs            = [new Seg(text, fg)],
                    Y               = y,
                    BackgroundColor = bg,
                    IsBold          = bold,
                    IsFullWidth     = true,
                });
                y += lh;
            }
            else // "tr"
            {
                var rowBg    = ColorFromStyle(attrs);
                bool isHeader = false;
                var  segs    = new List<Seg>();

                foreach (Match cell in _cellRx.Matches(inner))
                {
                    bool isTh     = cell.Groups[1].Value.Equals("h", StringComparison.OrdinalIgnoreCase);
                    if (isTh) isHeader = true;

                    var cellText = Decode(_tagRx.Replace(cell.Groups[3].Value, " ")).Trim();
                    cellText = Regex.Replace(cellText, @"\s{2,}", " ");
                    segs.Add(new Seg(cellText, isTh ? ColHeaderFg : SKColor.Empty));
                }

                if (segs.Count == 0) continue;
                while (segs.Count < 3) segs.Add(new Seg(string.Empty, SKColor.Empty));
                if (isHeader) rowBg = ColHeaderBg;

                built.Add(new TextLine
                {
                    Segs            = segs,
                    Y               = y,
                    BackgroundColor = rowBg,
                    IsBold          = isHeader,
                    IsFullWidth     = false,
                });
                y += lh;
            }
        }

        ContentHeight = y + pad;
        _lines = built.ToArray(); // atomic swap — Render always sees a complete array
    }

    private static string Decode(string s) => System.Net.WebUtility.HtmlDecode(s);

    // ── Input handling ────────────────────────────────────────────────────────

    public override bool HandleEvent(Omaha.Events.OmahaEvent evt)
    {
        if (!IsVisible || !IsEnabled) return false;

        switch (evt)
        {
            case Omaha.Events.MouseScrollEvent scroll when HitTest(scroll.X, scroll.Y):
                _scrollY = ClampScroll(_scrollY - scroll.DeltaY * 40f);
                Invalidate();
                return true;

            case Omaha.Events.MouseButtonEvent btn:
                if (btn.IsPressed && ThumbRect() is SKRect thumb && thumb.Contains(btn.X - AbsoluteX, btn.Y - AbsoluteY))
                {
                    _thumbDragging       = true;
                    _thumbDragStartY     = btn.Y;
                    _thumbDragStartScroll = _scrollY;
                    return true;
                }
                if (!btn.IsPressed) _thumbDragging = false;
                break;

            case Omaha.Events.MouseMoveEvent move:
                if (_thumbDragging)
                {
                    float trackH = Height - ScrollbarWidth * 2;  // usable track
                    float ratio  = trackH > 0 ? ContentHeight / trackH : 1f;
                    _scrollY = ClampScroll(_thumbDragStartScroll + (move.Y - _thumbDragStartY) * ratio);
                    Invalidate();
                    return true;
                }
                break;
        }

        return base.HandleEvent(evt);
    }

    // ── Plain-text (non-HTML) path ──────────────────

    private void RebuildLines()
    {
        float fs    = FontSize > 0 ? FontSize : Theme.FontSizeNormal;
        float lh    = fs + 6f;
        float y     = Theme.Padding;
        var   built = new List<TextLine>();

        foreach (var raw in _text.Split('\n'))
        {
            built.Add(new TextLine { Segs = [new Seg(raw, SKColor.Empty)], Y = y });
            y += lh;
        }
        ContentHeight = y;
        _lines = built.ToArray(); // atomic swap
    }

    // Rendering // ── ──

    public override void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;

        canvas.Save();
        canvas.Translate(X, Y);
        canvas.ClipRect(LocalBounds);

        // Panel background
        using var bgPaint = new SKPaint { Color = Theme.PanelBackground };
        canvas.DrawRoundRect(new SKRoundRect(LocalBounds, Theme.BorderRadius), bgPaint);

        // Border
        using var borderPaint = new SKPaint
        {
            Color       = Theme.Border,
            Style       = SKPaintStyle.Stroke,
            StrokeWidth = Theme.BorderWidth,
            IsAntialias = true,
        };
        canvas.DrawRoundRect(new SKRoundRect(LocalBounds, Theme.BorderRadius), borderPaint);

        float fs   = FontSize > 0 ? FontSize : Theme.FontSizeNormal;
        float lh   = fs + 6f;
        float pad  = Theme.Padding;
        // Reserve right edge for scrollbar when content overflows
        float sbW  = ContentHeight > Height ? ScrollbarWidth : 0f;
        var   cols = CalcCols(Width - sbW, pad);

        using var tp = Theme.CreateTextPaint(fs, Theme.TextPrimary);

        // Snapshot the lines array once — the parser may swap _lines on the RPC
        // thread at any moment; holding a local reference keeps the array stable
        // for the duration of this frame (fixes "Collection was modified" crash).
        var lines = _lines;

        // Scroll offset — content draws in a translated sub-canvas
        canvas.Save();
        canvas.Translate(0, -_scrollY);

        foreach (var line in lines)
        {
            if (line.Y + lh < _scrollY) continue;          // above viewport
            if (line.Y - _scrollY > Height) break;          // below viewport

            // Row background
            if (line.BackgroundColor != SKColor.Empty)
            {
                using var lbp = new SKPaint { Style = SKPaintStyle.Fill, Color = line.BackgroundColor };
                canvas.DrawRect(SKRect.Create(0, line.Y, Width, lh), lbp);
            }

            tp.FakeBoldText = line.IsBold;

            if (line.IsFullWidth || line.Segs.Count <= 1)
            {
                var s = line.Segs.Count > 0 ? line.Segs[0] : new Seg(string.Empty, SKColor.Empty);
                tp.Color = s.Fg != SKColor.Empty ? s.Fg : Theme.TextPrimary;
                DrawClipped(canvas, tp, s.Text, pad, line.Y + fs, Width - pad * 2);
            }
            else
            {
                for (int c = 0; c < Math.Min(line.Segs.Count, cols.Length); c++)
                {
                    var s = line.Segs[c];
                    tp.Color = s.Fg != SKColor.Empty ? s.Fg : Theme.TextPrimary;
                    DrawClipped(canvas, tp, s.Text, cols[c].x + 2f, line.Y + fs, cols[c].w - 4f);
                }

                // Vertical dividers between columns
                using var dp = new SKPaint { Color = new SKColor(0xCC, 0xCC, 0xCC), StrokeWidth = 1f };
                for (int c = 1; c < cols.Length; c++)
                    canvas.DrawLine(cols[c].x, line.Y, cols[c].x, line.Y + lh, dp);
            }
        }

        canvas.Restore(); // end scroll translation

        // ── Vertical scrollbar ──────────────────────────────────────────────
        if (ContentHeight > Height)
        {
            // Track
            using var trackP = new SKPaint { Color = Theme.ScrollbarTrack };
            canvas.DrawRect(Width - ScrollbarWidth, 0, ScrollbarWidth, Height, trackP);

            // Thumb
            if (ThumbRect() is SKRect tr)
            {
                using var thumbP = new SKPaint { Color = _thumbDragging ? Theme.ScrollbarThumb : Theme.ScrollbarThumb, IsAntialias = true };
                canvas.DrawRoundRect(new SKRoundRect(tr, 4), thumbP);
            }
        }

        canvas.Restore();
    }

    private const float ScrollbarWidth = 12f;

    /// <summary>Returns the thumb rectangle in local coords, or null when no scrollbar needed.</summary>
    private SKRect? ThumbRect()
    {
        if (ContentHeight <= Height) return null;
        float trackH = Height;
        float ratio  = trackH / ContentHeight;
        float thumbH = Math.Max(24f, trackH * ratio);
        float thumbY = ((_scrollY / (ContentHeight - Height)) * (trackH - thumbH));
        return new SKRect(Width - ScrollbarWidth, thumbY, Width, thumbY + thumbH);
    }

    private static void DrawClipped(SKCanvas cv, SKPaint paint, string text,
        float x, float baseline, float maxW)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (maxW <= 0) return;

        if (paint.MeasureText(text) > maxW)
        {
            const string ell = "…";
            float ew = paint.MeasureText(ell);
            while (text.Length > 1 && paint.MeasureText(text) + ew > maxW)
                text = text[..^1];
            text += ell;
        }
        cv.DrawText(text, x, baseline, paint);
    }

    // ── helper Strip-HTML ────────────

    private static string StripHtml(string html)
    {
        var t = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"<script[^>]*>[\s\S]*?</script>",      string.Empty, RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"<(br|/tr|/p|/div)[^>]*>", "\n",       RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"<[^>]+>", string.Empty);
        t = System.Net.WebUtility.HtmlDecode(t);
        return Regex.Replace(t, @"\n{3,}", "\n\n").Trim();
    }

    // ── model Data ───────────────────────────────────────────────

    private readonly record struct Seg(string Text, SKColor Fg);

    private sealed class TextLine
    {
        public List<Seg> Segs            { get; init; } = [];
        public float     Y               { get; init; }
        public SKColor   BackgroundColor { get; init; } = SKColor.Empty;
        public bool      IsBold          { get; init; }
        /// <summary>True for div section headers; false for multi-column table rows.</summary>
        public bool      IsFullWidth     { get; init; }
    }
}
