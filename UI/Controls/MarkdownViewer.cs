using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RiftVault.UI.Controls
{
    /// <summary>
    /// Lightweight, high-performance WPF Markdown Viewer that formats headers,
    /// markdown tables, keyboard shortcuts, inline code badges, bold/italic,
    /// bullet lists, and callouts into clean, modern UI components.
    /// </summary>
    public class MarkdownViewer : ContentControl
    {
        public static readonly DependencyProperty MarkdownProperty =
            DependencyProperty.Register(
                nameof(Markdown),
                typeof(string),
                typeof(MarkdownViewer),
                new PropertyMetadata(string.Empty, OnMarkdownChanged));

        public string Markdown
        {
            get => (string)GetValue(MarkdownProperty);
            set => SetValue(MarkdownProperty, value);
        }

        private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarkdownViewer viewer)
            {
                viewer.RenderMarkdown((string)e.NewValue ?? string.Empty);
            }
        }

        public MarkdownViewer()
        {
            Focusable = false;
            IsTabStop = false;
        }

        private void RenderMarkdown(string markdown)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };

            if (string.IsNullOrWhiteSpace(markdown))
            {
                Content = stack;
                return;
            }

            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            int i = 0;

            while (i < lines.Length)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    i++;
                    continue;
                }

                // ── 1. Code Block (```lang ... ```) ─────────────────
                if (trimmed.StartsWith("```"))
                {
                    string lang = trimmed.Substring(3).Trim();
                    var codeLines = new List<string>();
                    i++;
                    while (i < lines.Length && !lines[i].Trim().StartsWith("```"))
                    {
                        codeLines.Add(lines[i]);
                        i++;
                    }
                    if (i < lines.Length) i++; // consume closing ```

                    stack.Children.Add(CreateCodeBlockElement(string.Join(Environment.NewLine, codeLines), lang));
                    continue;
                }

                // ── 2. Markdown Table (| col | col |) ───────────────
                if (trimmed.StartsWith("|") && trimmed.EndsWith("|"))
                {
                    var tableLines = new List<string>();
                    while (i < lines.Length && lines[i].Trim().StartsWith("|") && lines[i].Trim().EndsWith("|"))
                    {
                        tableLines.Add(lines[i].Trim());
                        i++;
                    }

                    var tableElement = CreateTableElement(tableLines);
                    if (tableElement != null)
                    {
                        stack.Children.Add(tableElement);
                        continue;
                    }
                }

                // ── 3. Headings (#, ##, ###) ─────────────────────────
                if (trimmed.StartsWith("### "))
                {
                    stack.Children.Add(CreateHeadingElement(trimmed.Substring(4).Trim(), 3));
                    i++;
                    continue;
                }
                if (trimmed.StartsWith("## "))
                {
                    stack.Children.Add(CreateHeadingElement(trimmed.Substring(3).Trim(), 2));
                    i++;
                    continue;
                }
                if (trimmed.StartsWith("# "))
                {
                    stack.Children.Add(CreateHeadingElement(trimmed.Substring(2).Trim(), 1));
                    i++;
                    continue;
                }

                // ── 4. Callout / Alert (💡, ⚠️, ℹ️, > ) ───────────────
                if (trimmed.StartsWith("> ") || trimmed.StartsWith("💡") || trimmed.StartsWith("⚠️") || trimmed.StartsWith("ℹ️"))
                {
                    string calloutText = trimmed.StartsWith("> ") ? trimmed.Substring(2).Trim() : trimmed;
                    stack.Children.Add(CreateCalloutElement(calloutText));
                    i++;
                    continue;
                }

                // ── 5. Bullet List Items (- item, * item, 1. item) ───
                if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    string itemText = trimmed.Substring(2).Trim();
                    stack.Children.Add(CreateListItemElement(itemText, "•"));
                    i++;
                    continue;
                }

                var numMatch = Regex.Match(trimmed, @"^(\d+)\.\s+(.*)$");
                if (numMatch.Success)
                {
                    string num = numMatch.Groups[1].Value + ".";
                    string itemText = numMatch.Groups[2].Value.Trim();
                    stack.Children.Add(CreateListItemElement(itemText, num));
                    i++;
                    continue;
                }

                // ── 6. Regular Paragraph ──────────────────────────────
                stack.Children.Add(CreateParagraphElement(trimmed));
                i++;
            }

            Content = stack;
        }

        private UIElement CreateHeadingElement(string text, int level)
        {
            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI")
            };

            switch (level)
            {
                case 1:
                    tb.FontSize = 17.0;
                    tb.FontWeight = FontWeights.Bold;
                    tb.Margin = new Thickness(0, 10, 0, 5);
                    tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    break;
                case 2:
                    tb.FontSize = 15.0;
                    tb.FontWeight = FontWeights.Bold;
                    tb.Margin = new Thickness(0, 8, 0, 4);
                    tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    break;
                case 3:
                default:
                    tb.FontSize = 14.0;
                    tb.FontWeight = FontWeights.SemiBold;
                    tb.Margin = new Thickness(0, 7, 0, 4);
                    tb.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
                    break;
            }

            PopulateInlines(tb.Inlines, text);

            var border = new Border
            {
                Child = tb,
                Margin = new Thickness(0, 3, 0, 3)
            };

            return border;
        }

        private UIElement CreateParagraphElement(string text)
        {
            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13.5,
                LineHeight = 21.0,
                Margin = new Thickness(0, 3, 0, 4),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI")
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

            PopulateInlines(tb.Inlines, text);
            return tb;
        }

        private UIElement CreateListItemElement(string text, string bullet)
        {
            var grid = new Grid
            {
                Margin = new Thickness(2, 3, 0, 3)
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var bulletTb = new TextBlock
            {
                Text = bullet + " ",
                FontSize = 13.0,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Top,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI")
            };
            bulletTb.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            Grid.SetColumn(bulletTb, 0);
            grid.Children.Add(bulletTb);

            var contentTb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13.5,
                LineHeight = 20.0,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI")
            };
            contentTb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            PopulateInlines(contentTb.Inlines, text);
            Grid.SetColumn(contentTb, 1);
            grid.Children.Add(contentTb);

            return grid;
        }

        private UIElement CreateCalloutElement(string text)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(0, 8, 8, 0),
                BorderThickness = new Thickness(3.5, 0, 0, 0),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 6, 0, 6)
            };
            border.SetResourceReference(Border.BackgroundProperty, "PillBackgroundBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");

            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13.0,
                LineHeight = 19.5,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI")
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            PopulateInlines(tb.Inlines, text);

            border.Child = tb;
            return border;
        }

        private UIElement? CreateTableElement(List<string> tableLines)
        {
            if (tableLines.Count < 2) return null;

            // Separate header, separator, and data rows
            var parsedRows = new List<List<string>>();
            foreach (var line in tableLines)
            {
                var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToList();
                // Skip markdown separator row |:---|:---|
                if (cells.All(c => Regex.IsMatch(c, @"^:?-+:?$")))
                {
                    continue;
                }
                parsedRows.Add(cells);
            }

            if (parsedRows.Count == 0) return null;

            int colCount = parsedRows.Max(r => r.Count);
            if (colCount == 0) return null;

            var tableGrid = new Grid();
            for (int c = 0; c < colCount; c++)
            {
                tableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            for (int r = 0; r < parsedRows.Count; r++)
            {
                tableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            for (int r = 0; r < parsedRows.Count; r++)
            {
                bool isHeader = (r == 0);
                var rowCells = parsedRows[r];

                // Row background container
                var rowBorder = new Border
                {
                    Padding = new Thickness(8, 5, 8, 5),
                    BorderThickness = new Thickness(0, 0, 0, isHeader ? 1 : (r == parsedRows.Count - 1 ? 0 : 0.5))
                };
                rowBorder.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

                if (isHeader)
                {
                    rowBorder.SetResourceReference(Border.BackgroundProperty, "SurfaceRaisedBrush");
                }
                else if (r % 2 == 1)
                {
                    rowBorder.Background = new SolidColorBrush(Color.FromArgb(12, 128, 128, 128));
                }

                Grid.SetRow(rowBorder, r);
                Grid.SetColumnSpan(rowBorder, colCount);
                tableGrid.Children.Add(rowBorder);

                for (int c = 0; c < rowCells.Count; c++)
                {
                    var cellTb = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = isHeader ? 13.0 : 12.5,
                        FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
                        Margin = new Thickness(10, 6, 10, 6),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontFamily = new FontFamily("Segoe UI Variable, Segoe UI")
                    };

                    if (isHeader)
                    {
                        cellTb.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
                    }
                    else
                    {
                        cellTb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    }

                    PopulateInlines(cellTb.Inlines, rowCells[c]);
                    Grid.SetRow(cellTb, r);
                    Grid.SetColumn(cellTb, c);
                    tableGrid.Children.Add(cellTb);
                }
            }

            var outerBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 8, 0, 8),
                ClipToBounds = true,
                Child = tableGrid
            };
            outerBorder.SetResourceReference(Border.BackgroundProperty, "PillBackgroundBrush");
            outerBorder.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

            return outerBorder;
        }

        private UIElement CreateCodeBlockElement(string code, string lang)
        {
            var outer = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 8, 0, 8),
                Background = new SolidColorBrush(Color.FromRgb(22, 27, 34))
            };
            outer.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

            var stack = new StackPanel();

            // Header strip
            var headerGrid = new Grid
            {
                Margin = new Thickness(12, 8, 10, 4)
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var langTb = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(lang) ? "CODE" : lang.ToUpperInvariant(),
                FontFamily = new FontFamily("Cascadia Code, Consolas"),
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(langTb, 0);
            headerGrid.Children.Add(langTb);

            var copyBtn = new Button
            {
                Content = "📋 Copy",
                Padding = new Thickness(10, 3, 10, 3),
                FontSize = 10.5,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            copyBtn.SetResourceReference(StyleProperty, "GlassButton");
            copyBtn.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(code);
                    copyBtn.Content = "✓ Copied!";
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
                    timer.Tick += (ts, te) =>
                    {
                        copyBtn.Content = "📋 Copy";
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch { }
            };
            Grid.SetColumn(copyBtn, 1);
            headerGrid.Children.Add(copyBtn);

            stack.Children.Add(headerGrid);

            // Code content
            var codeTb = new TextBox
            {
                Text = code,
                FontFamily = new FontFamily("Cascadia Code, Consolas"),
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(225, 230, 235)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12, 6, 12, 10),
                Cursor = System.Windows.Input.Cursors.IBeam
            };
            stack.Children.Add(codeTb);

            outer.Child = stack;
            return outer;
        }

        /// <summary>
        /// Parses inline Markdown:
        /// - Keyboard shortcuts (Ctrl+C, Alt+A, Enter, F5, etc.) -> <kbd> badges
        /// - Inline code / file paths (`code`, C:\...) -> styled code badge
        /// - Bold (**text**) -> Bold inline
        /// - Italic (*text*) -> Italic inline
        /// </summary>
        private static void PopulateInlines(InlineCollection inlines, string rawText)
        {
            if (string.IsNullOrEmpty(rawText)) return;

            // Regex tokenizing:
            // 1. Backticks: `([^`]+)`
            // 2. Bold: \*\*([^*]+)\*\*
            // 3. Italic: \*([^*]+)\*
            // 4. Windows file path: (?:[a-zA-Z]:\\[\w\s.-]+(?:\\[\w\s.-]+)*)
            // 5. Keyboard shortcuts: (?:Ctrl|Alt|Shift|Cmd|Win)\+(?:[a-zA-Z0-9]+|Space|Enter|Del|Esc|Tab|F\d{1,2})
            string pattern = @"(`[^`]+`|\*\*[^*]+\*\*|\*[^*]+\*|(?:[a-zA-Z]:\\[^\s`\*\|]+)|(?:(?:Ctrl|Alt|Shift)\+[a-zA-Z0-9\+\s]+))";

            var parts = Regex.Split(rawText, pattern);

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                // ── Inline Code: `code` ──
                if (part.StartsWith("`") && part.EndsWith("`") && part.Length >= 2)
                {
                    string codeContent = part.Substring(1, part.Length - 2);
                    inlines.Add(CreateInlineCodePill(codeContent));
                    continue;
                }

                // ── Bold: **text** ──
                if (part.StartsWith("**") && part.EndsWith("**") && part.Length >= 4)
                {
                    string boldContent = part.Substring(2, part.Length - 4);
                    var bold = new Bold();
                    bold.Inlines.Add(new Run(boldContent));
                    inlines.Add(bold);
                    continue;
                }

                // ── Italic: *text* ──
                if (part.StartsWith("*") && part.EndsWith("*") && part.Length >= 2 && !part.StartsWith("**"))
                {
                    string italicContent = part.Substring(1, part.Length - 2);
                    var italic = new Italic();
                    italic.Inlines.Add(new Run(italicContent));
                    inlines.Add(italic);
                    continue;
                }

                // ── Windows Path: C:\... ──
                if (Regex.IsMatch(part, @"^[a-zA-Z]:\\"))
                {
                    inlines.Add(CreateInlinePathPill(part));
                    continue;
                }

                // ── Keyboard Shortcut: Ctrl+C, Alt+A, etc. ──
                if (Regex.IsMatch(part, @"^(?:Ctrl|Alt|Shift)\+"))
                {
                    inlines.Add(CreateKbdBadge(part));
                    continue;
                }

                // Plain text segment: scan for standalone shortcuts like Enter, Esc, F5, Tab
                PopulatePlainTextWithShortcuts(inlines, part);
            }
        }

        private static void PopulatePlainTextWithShortcuts(InlineCollection inlines, string text)
        {
            string kbdPattern = @"\b(Enter|Esc|Escape|Space|Tab|F1|F2|F3|F4|F5|F6|F7|F8|F9|F10|F11|F12|Delete)\b";
            var tokens = Regex.Split(text, kbdPattern);

            foreach (var token in tokens)
            {
                if (string.IsNullOrEmpty(token)) continue;

                if (Regex.IsMatch(token, @"^(Enter|Esc|Escape|Space|Tab|F\d{1,2}|Delete)$"))
                {
                    inlines.Add(CreateKbdBadge(token));
                }
                else
                {
                    inlines.Add(new Run(token));
                }
            }
        }

        private static InlineUIContainer CreateInlineCodePill(string code)
        {
            var tb = new TextBlock
            {
                Text = code,
                FontFamily = new FontFamily("Cascadia Code, Consolas"),
                FontSize = 12.0,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");

            var border = new Border
            {
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 1.5, 6, 1.5),
                Margin = new Thickness(2, 0, 2, 0),
                Child = tb
            };
            border.SetResourceReference(Border.BackgroundProperty, "PillBackgroundBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

            return new InlineUIContainer(border) { BaselineAlignment = BaselineAlignment.Center };
        }

        private static InlineUIContainer CreateInlinePathPill(string path)
        {
            var tb = new TextBlock
            {
                Text = path,
                FontFamily = new FontFamily("Cascadia Code, Consolas"),
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");

            var border = new Border
            {
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 1.5, 6, 1.5),
                Margin = new Thickness(2, 0, 2, 0),
                Child = tb
            };
            border.SetResourceReference(Border.BackgroundProperty, "PillBackgroundBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

            return new InlineUIContainer(border) { BaselineAlignment = BaselineAlignment.Center };
        }

        private static InlineUIContainer CreateKbdBadge(string shortcut)
        {
            var tb = new TextBlock
            {
                Text = shortcut,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                FontSize = 11.0,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

            var border = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1, 1, 1, 2), // Hardware 3D key effect
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(2, 0, 2, 0),
                Child = tb
            };
            border.SetResourceReference(Border.BackgroundProperty, "SurfaceRaisedBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

            return new InlineUIContainer(border) { BaselineAlignment = BaselineAlignment.Center };
        }
    }
}
