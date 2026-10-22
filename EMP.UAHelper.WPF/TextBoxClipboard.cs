// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Копіювання, вирізання, вставка й "виділити все" для текстових полів:
//     комбінації Ctrl+C/X/V/A, Ctrl+Insert, Shift+Insert, Shift+Delete та
//     контекстне меню. Операції з буфером обміну виконуються явно, з
//     перехопленням помилок доступу до буфера (його може тимчасово тримати
//     інша програма) — збій пишеться в лог, а не губиться мовчки.
// EN: Copy, cut, paste and "select all" for text fields: Ctrl+C/X/V/A,
//     Ctrl+Insert, Shift+Insert, Shift+Delete and a context menu. Clipboard
//     operations are performed explicitly, with clipboard access errors
//     caught (another program may hold the clipboard briefly) — a failure is
//     written to the log instead of being lost silently.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EMP.UAHelper.Core.Services;
using Application = System.Windows.Application;
using ClipboardApi = System.Windows.Clipboard;
using ContextMenu = System.Windows.Controls.ContextMenu;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Controls.MenuItem;
using TextBox = System.Windows.Controls.TextBox;

namespace EMP.UAHelper.WPF
{
    internal static class TextBoxClipboard
    {
        // UA: Поле підключається один раз, навіть якщо Attach викликано повторно
        // EN: A field is attached once, even if Attach is called again
        private static readonly ConditionalWeakTable<TextBox, object> Attached = new();

        private const int ClipboardAttempts = 2;

        // UA: Підключає всі TextBox у логічному дереві (статичні поля вікна).
        //     normalizerFor — необов'язкова обробка тексту, що вставляється.
        // EN: Attaches every TextBox in the logical tree (the window's static
        //     fields). normalizerFor — optional processing of pasted text.
        public static void AttachAll(
            DependencyObject root,
            LocalizationService loc,
            Func<TextBox, Func<string, string>?>? normalizerFor = null,
            Action<string>? log = null)
        {
            foreach (var box in Descendants(root))
                Attach(box, loc, normalizerFor?.Invoke(box), log);
        }

        public static void Attach(
            TextBox box,
            LocalizationService loc,
            Func<string, string>? normalize = null,
            Action<string>? log = null)
        {
            if (Attached.TryGetValue(box, out _)) return;
            Attached.Add(box, new object());

            box.PreviewKeyDown += (_, e) => HandleKey(box, e, normalize, log);

            var menu = new ContextMenu();
            if (Application.Current?.TryFindResource("DarkContextMenu") is Style menuStyle)
                menu.Style = menuStyle;
            box.ContextMenu = menu;
            box.ContextMenuOpening += (_, _) => Populate(menu, box, loc, normalize, log);
        }

        // UA: Запис тексту в буфер обміну; false — не вдалось (причина в логі)
        // EN: Writes text to the clipboard; false — failed (the reason is logged)
        public static bool TrySetText(string text, Action<string>? log = null)
        {
            ExternalException? last = null;
            for (var attempt = 0; attempt < ClipboardAttempts; attempt++)
            {
                try
                {
                    ClipboardApi.SetText(text);
                    return true;
                }
                catch (ExternalException ex)
                {
                    last = ex;
                    Thread.Sleep(50);
                }
            }

            log?.Invoke($"[Clipboard] SetText failed: 0x{last?.HResult:X8} {last?.Message}");
            return false;
        }

        private static string? TryGetText(Action<string>? log)
        {
            ExternalException? last = null;
            for (var attempt = 0; attempt < ClipboardAttempts; attempt++)
            {
                try
                {
                    return ClipboardApi.ContainsText() ? ClipboardApi.GetText() : null;
                }
                catch (ExternalException ex)
                {
                    last = ex;
                    Thread.Sleep(50);
                }
            }

            log?.Invoke($"[Clipboard] GetText failed: 0x{last?.HResult:X8} {last?.Message}");
            return null;
        }

        private static void HandleKey(TextBox box, KeyEventArgs e, Func<string, string>? normalize, Action<string>? log)
        {
            var modifiers = Keyboard.Modifiers;
            var ctrl = (modifiers & ModifierKeys.Control) != 0 && (modifiers & (ModifierKeys.Alt | ModifierKeys.Shift)) == 0;
            var shift = (modifiers & ModifierKeys.Shift) != 0 && (modifiers & (ModifierKeys.Alt | ModifierKeys.Control)) == 0;

            if ((ctrl && e.Key == Key.C) || (ctrl && e.Key == Key.Insert))
                Copy(box, log);
            else if ((ctrl && e.Key == Key.X) || (shift && e.Key == Key.Delete))
                Cut(box, log);
            else if ((ctrl && e.Key == Key.V) || (shift && e.Key == Key.Insert))
                Paste(box, normalize, log);
            else if (ctrl && e.Key == Key.A)
                box.SelectAll();
            else
                return;

            e.Handled = true;
        }

        private static void Copy(TextBox box, Action<string>? log)
        {
            if (box.SelectionLength > 0)
                TrySetText(box.SelectedText, log);
        }

        private static void Cut(TextBox box, Action<string>? log)
        {
            if (box.SelectionLength == 0) return;

            if (TrySetText(box.SelectedText, log) && !box.IsReadOnly)
                box.SelectedText = string.Empty;
        }

        private static void Paste(TextBox box, Func<string, string>? normalize, Action<string>? log)
        {
            if (box.IsReadOnly) return;

            var text = TryGetText(log);
            if (string.IsNullOrEmpty(text)) return;

            if (normalize != null) text = normalize(text);

            // UA: В однорядкових полях розриви рядків замінюються пробілами
            // EN: In single-line fields, line breaks are replaced with spaces
            if (!box.AcceptsReturn)
                text = text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');

            var start = box.SelectionStart;
            box.SelectedText = text;
            box.Select(start + text.Length, 0);
        }

        // UA: Пункти меню перебудовуються перед кожним показом — так мова й
        //     стан (є виділення, поле лише для читання) завжди актуальні.
        // EN: The menu items are rebuilt before each display — so the language
        //     and state (selection present, read-only field) are always current.
        private static void Populate(
            ContextMenu menu,
            TextBox box,
            LocalizationService loc,
            Func<string, string>? normalize,
            Action<string>? log)
        {
            menu.Items.Clear();

            var hasSelection = box.SelectionLength > 0;
            var canEdit = !box.IsReadOnly;

            AddItem(menu, box, loc.Get("clipboard.cut"), "Ctrl+X", canEdit && hasSelection, () => Cut(box, log));
            AddItem(menu, box, loc.Get("clipboard.copy"), "Ctrl+C", hasSelection, () => Copy(box, log));
            AddItem(menu, box, loc.Get("clipboard.paste"), "Ctrl+V", canEdit, () => Paste(box, normalize, log));

            var separator = new Separator();
            if (Application.Current?.TryFindResource("DarkMenuSeparator") is Style separatorStyle)
                separator.Style = separatorStyle;
            menu.Items.Add(separator);

            AddItem(menu, box, loc.Get("clipboard.select_all"), "Ctrl+A", box.Text.Length > 0, box.SelectAll);
        }

        private static void AddItem(ContextMenu menu, TextBox box, string header, string gesture, bool enabled, Action action)
        {
            var item = new MenuItem { Header = header, InputGestureText = gesture, IsEnabled = enabled };
            if (Application.Current?.TryFindResource("DarkMenuItem") is Style itemStyle)
                item.Style = itemStyle;

            item.Click += (_, _) =>
            {
                box.Focus();
                action();
            };
            menu.Items.Add(item);
        }

        private static IEnumerable<TextBox> Descendants(DependencyObject parent)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(parent))
            {
                if (child is TextBox box) yield return box;

                if (child is DependencyObject node)
                    foreach (var nested in Descendants(node))
                        yield return nested;
            }
        }
    }
}
