// Author: EMP_UA | https://github.com/EMP-UA/EMP-UA-Helper
// Donate: https://ko-fi.com/emp_ua
// UA: Вікно результату відправки. Отримує вже підготовлені рядки
//     (SendResultRow) і лише відображає їх: на кожну платформу картка з
//     кольоровою рамкою, великим статусом, приміткою та ID повідомлення.
// EN: The send-result window. It receives ready-made rows (SendResultRow)
//     and only displays them: one card per platform with a colored border,
//     a large status, a note and the message ID.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Orientation = System.Windows.Controls.Orientation;

namespace EMP.UAHelper.WPF
{
    // UA: Один рядок результату: платформа, статус, успіх, примітка/помилка, ID
    // EN: One result row: platform, status, success flag, note/error, ID
    internal sealed record SendResultRow(
        string Platform,
        string Status,
        bool Success,
        string? Detail,
        string? IdText);

    public partial class SendResultWindow : Window
    {
        private static readonly SolidColorBrush Green = Make("#55C57A");
        private static readonly SolidColorBrush Red = Make("#E0605A");

        internal SendResultWindow(
            Window owner,
            string title,
            string header,
            bool allOk,
            IReadOnlyList<SendResultRow> rows,
            string? sourceNote,
            string okText)
        {
            InitializeComponent();

            Owner = owner;
            Title = title;
            BtnOk.Content = okText;

            TxtHeader.Text = header;
            TxtHeader.Foreground = allOk ? Green : Red;

            foreach (var row in rows)
                PanelRows.Children.Add(BuildCard(row));

            if (!string.IsNullOrEmpty(sourceNote))
            {
                TxtSource.Text = $"ℹ {sourceNote}";
                TxtSource.Visibility = Visibility.Visible;
            }

            Loaded += (_, _) => BtnOk.Focus();
        }

        // UA: Картка платформи: у першому рядку назва та статус, далі —
        //     примітка/помилка та ID. Відступи між рядками збільшені для читабельності.
        // EN: A platform card: name and status on the first line, then the
        //     note/error and the ID. Line spacing is increased for readability.
        private static Border BuildCard(SendResultRow row)
        {
            var accent = row.Success ? Green : Red;

            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(new TextBlock
            {
                Text = row.Platform,
                FontSize = 19,
                FontWeight = FontWeights.Bold,
                Foreground = Make("#E8E0F0"),
                Margin = new Thickness(0, 0, 14, 0)
            });
            head.Children.Add(new TextBlock
            {
                Text = row.Status,
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = accent
            });

            var body = new StackPanel();
            body.Children.Add(head);

            if (!string.IsNullOrEmpty(row.Detail))
                body.Children.Add(new TextBlock
                {
                    Text = row.Success ? $"ℹ {row.Detail}" : row.Detail,
                    FontSize = 15,
                    Foreground = row.Success ? Make("#C989F3") : Make("#F0A09B"),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 22,
                    Margin = new Thickness(0, 8, 0, 0)
                });

            if (!string.IsNullOrEmpty(row.IdText))
                body.Children.Add(new TextBlock
                {
                    Text = row.IdText,
                    FontSize = 14,
                    Foreground = Make("#B8A8D0"),
                    Margin = new Thickness(0, 8, 0, 0)
                });

            return new Border
            {
                Background = Make("#1A1425"),
                BorderBrush = accent,
                BorderThickness = new Thickness(3, 1, 1, 1),
                Padding = new Thickness(18, 14, 18, 16),
                Margin = new Thickness(0, 0, 0, 12),
                Child = body
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e) => Close();

        // UA: Esc і Enter закривають вікно
        // EN: Esc and Enter close the window
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.Enter) Close();
        }

        private static SolidColorBrush Make(string hex)
        {
            var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }
}
