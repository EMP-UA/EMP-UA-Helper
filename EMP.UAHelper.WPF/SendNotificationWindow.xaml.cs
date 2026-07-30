// =============================================================================
// EMP UA Helper — SendNotificationWindow.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA/EMP-UA-Helper)
// Підтримати / Donate: https://ko-fi.com/emp_ua
// Ліцензія / License: GPL-3.0
// =============================================================================
// UA: Код вікна надсилання сповіщення — приклади для обох платформ,
//     мініатюра, м'яке попередження про нерозпізнане посилання, а також
//     повністю ручний режим без шаблону (окремо Telegram/Discord)
// EN: Send-notification window code-behind — previews for both platforms,
//     thumbnail, soft warning for unrecognized links, and a fully manual
//     no-template mode (separate Telegram/Discord fields)
// =============================================================================
using EMP.UAHelper.Core.Models;
using EMP.UAHelper.Core.Services;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
// UA: Проєкт одночасно підключає WPF і WinForms (для іконки в треї) — тому
//     TextBox/Button існують в обох просторах імен і компілятор не може сам
//     вибрати, який мається на увазі. Явно фіксуємо WPF-варіант, як і в
//     SettingsWindow.xaml.cs/FirstRunWindow.xaml.cs.
// EN: The project references both WPF and WinForms (for the tray icon), so
//     TextBox/Button exist in both namespaces and the compiler can't pick one
//     on its own. Pin the WPF variant explicitly, matching the convention
//     already used in SettingsWindow.xaml.cs/FirstRunWindow.xaml.cs.
using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace EMP.UAHelper.WPF
{
    public partial class SendNotificationWindow : Window
    {
        private readonly YouTubeService? _youTubeService;
        private readonly NotificationDispatcher _dispatcher;
        private readonly TemplateService _templateService;
        private readonly AppSettings _settings;
        private readonly LocalizationService _loc;

        private List<ContentCacheEntry> _candidates = new();
        private bool _isReady;

        // UA: Яке текстове поле зараз "слухає" спільний піквер дати/часу —
        //     призначається перед відкриттям відповідного Popup
        // EN: Which text field the shared date/time picker is currently
        //     "listening" for — assigned right before opening the
        //     corresponding Popup
        private TextBox? _dateTarget;
        private TextBox? _timeTarget;

        private class CandidateItem
        {
            public ContentCacheEntry Source { get; init; } = null!;
            public BitmapImage? Thumbnail { get; init; }
            public string Title => Source.Title;
            public string SubLabel { get; init; } = string.Empty;
        }

        public SendNotificationWindow(
            YouTubeService? youTubeService,
            NotificationDispatcher dispatcher,
            TemplateService templateService,
            AppSettings settings,
            LocalizationService loc)
        {
            InitializeComponent();
            _youTubeService = youTubeService;
            _dispatcher = dispatcher;
            _templateService = templateService;
            _settings = settings;
            _loc = loc;
            _loc.LanguageChanged += ApplyLocalization;

            ApplyLocalization();

            // UA: Показуємо блоки прикладу лише для реально увімкнених платформ —
            //     стан платформ не змінюється, поки це вікно відкрите, тож
            //     достатньо встановити один раз тут
            // EN: Show preview blocks only for actually enabled platforms —
            //     platform state doesn't change while this window is open,
            //     so setting it once here is enough
            PanelPreviewTelegram.Visibility = _settings.UseTelegram ? Visibility.Visible : Visibility.Collapsed;
            PanelPreviewDiscord.Visibility = _settings.UseDiscord ? Visibility.Visible : Visibility.Collapsed;
            TxtPreviewEmpty.Visibility = (!_settings.UseTelegram && !_settings.UseDiscord)
                ? Visibility.Visible : Visibility.Collapsed;

            // UA: Ті самі правила видимості платформ — для полів повністю
            //     ручного режиму
            // EN: Same platform-visibility rules — for the fully manual
            //     mode fields
            PanelRawTelegram.Visibility = _settings.UseTelegram ? Visibility.Visible : Visibility.Collapsed;
            PanelRawDiscord.Visibility = _settings.UseDiscord ? Visibility.Visible : Visibility.Collapsed;
            TxtRawEmpty.Visibility = (!_settings.UseTelegram && !_settings.UseDiscord)
                ? Visibility.Visible : Visibility.Collapsed;

            TypeCombo.SelectedIndex = 0;

            // UA: Списки годин/хвилин для спливного пікера часу — 00-23 і
            //     00-59, один раз при побудові вікна
            // EN: Hour/minute lists for the time picker popup — 00-23 and
            //     00-59, built once when the window is constructed
            HourCombo.ItemsSource = Enumerable.Range(0, 24).Select(h => h.ToString("00")).ToList();
            MinuteCombo.ItemsSource = Enumerable.Range(0, 60).Select(m => m.ToString("00")).ToList();

            MentionTypeCombo.SelectedIndex = 0;

            if (_youTubeService != null)
            {
                ChkAutoLatest.Visibility = Visibility.Visible;
                PanelPickerRow.Visibility = Visibility.Collapsed;
                ChkAutoLatest.IsChecked = true;
                _isReady = true;
                _ = LoadCandidatesAsync();
            }
            else
            {
                _isReady = true;
                UpdatePreview();
            }
        }

        private void ApplyLocalization()
        {
            Title = _loc.Get("send.title");
            TxtHeader.Text = _loc.Get("send.header");
            TxtDescription.Text = _loc.Get("send.description");
            ChkAutoLatest.Content = _loc.Get("send.auto_checkbox");
            TxtPickerHint.Text = _loc.Get("send.picker.hint");
            TxtTitleLabel.Text = _loc.Get("send.title_label");
            TxtTypeLabel.Text = _loc.Get("send.type_label");
            TxtTypeHint.Text = _loc.Get("send.type_hint");
            TxtUrlLabel.Text = _loc.Get("send.url_label");
            TxtUrlHint.Text = _loc.Get("send.url_hint");
            TxtUrlWarning.Text = _loc.Get("send.url.warning");
            TxtPreviewTelegramLabel.Text = _loc.Get("send.preview.telegram");
            TxtPreviewDiscordLabel.Text = _loc.Get("send.preview.discord");
            TxtPreviewEmpty.Text = _loc.Get("send.preview.empty");
            TxtThumbnailNote.Text = _loc.Get("send.thumbnail.twitch_note");
            BtnSend.Content = _loc.Get("send.button");

            ChkRawMode.Content = _loc.Get("send.raw.toggle");
            TxtRawModeHint.Text = _loc.Get("send.raw.hint");
            TxtRawTelegramLabel.Text = _loc.Get("send.raw.telegram_label");
            TxtRawTelegramHint.Text = _loc.Get("send.raw.telegram_hint");
            TxtRawDiscordLabel.Text = _loc.Get("send.raw.discord_label");
            TxtRawDiscordHint.Text = _loc.Get("send.raw.discord_hint");
            TxtRawEmpty.Text = _loc.Get("send.raw.empty_platforms");

            TxtRawDateLabel.Text = _loc.Get("send.raw.scheduled.date_label");
            TxtRawTimeLabel.Text = string.Format(_loc.Get("send.raw.scheduled.time_label"), ZoneLabel());
            TxtRawScheduledHint.Text = _loc.Get("send.raw.scheduled.hint");
            TxtRawTelegramSnippetLabel.Text = _loc.Get("send.raw.scheduled.telegram_snippet");
            TxtRawDiscordSnippetLabel.Text = _loc.Get("send.raw.scheduled.discord_snippet");

            TxtRawMentionLabel.Text = _loc.Get("send.raw.mention.label");
            TxtRawMentionHint.Text = _loc.Get("send.raw.mention.hint");
            TxtRawMentionSnippetLabel.Text = _loc.Get("send.raw.mention.snippet");
            ItemMentionRole.Content = _loc.Get("send.raw.mention.role");
            ItemMentionUser.Content = _loc.Get("send.raw.mention.user");
            ItemMentionChannel.Content = _loc.Get("send.raw.mention.channel");

            ChkOverrideChannel.Content = _loc.Get("send.channel.override");
            TxtChannelHint.Text = _loc.Get("send.channel.hint");

            // UA: Список каналів перебудовується саме тут, а не в конструкторі:
            //     підписи безіменних каналів локалізовані, тож при перемиканні
            //     мови вони мають змінитись разом з рештою інтерфейсу
            // EN: The channel list is rebuilt here rather than in the
            //     constructor: labels for unnamed channels are localized, so
            //     they must change along with the rest of the UI on switch
            BuildChannelList();

            ItemLive.Content = _loc.Get("type.live");
            ItemUpcoming.Content = _loc.Get("type.upcoming");
            ItemVideo.Content = _loc.Get("type.video");
            ItemShort.Content = _loc.Get("type.short");

            if (BtnTogglePicker.Tag as string != "selected")
                BtnTogglePicker.Content = _loc.Get("send.picker.toggle");

            UpdateDateTimeLabels();

            var active = new SolidColorBrush(Color.FromRgb(0x8A, 0x46, 0xC1));
            var inactive = new SolidColorBrush(Color.FromRgb(0x1A, 0x14, 0x25));
            BtnUA.Background = _loc.Language == UiLanguage.UA ? active : inactive;
            BtnEN.Background = _loc.Language == UiLanguage.EN ? active : inactive;

            if (_isReady) UpdatePreview();
        }

        private void BtnUA_Click(object sender, RoutedEventArgs e)
            => _loc.SetLanguage(UiLanguage.UA);

        private void BtnEN_Click(object sender, RoutedEventArgs e)
            => _loc.SetLanguage(UiLanguage.EN);

        // UA: Перемикач повністю ручного режиму — приховує весь шаблонний
        //     блок (приклад, автопідбір, заголовок/тип/URL/дата) і показує
        //     сирі текстові поля окремо для Telegram і Discord
        // EN: Fully manual mode toggle — hides the entire template block
        //     (preview, auto-pick, title/type/url/date) and shows raw text
        //     fields separately for Telegram and Discord
        private void ChkRawMode_Changed(object sender, RoutedEventArgs e)
        {
            bool raw = ChkRawMode.IsChecked == true;
            PanelTemplateMode.Visibility = raw ? Visibility.Collapsed : Visibility.Visible;
            PanelRawMode.Visibility = raw ? Visibility.Visible : Visibility.Collapsed;
        }

        // UA: Опціональна дата/час для ручного режиму — лише генерує готові
        //     до вставки позначки часу (Telegram-рядок, Discord-тег), нічого
        //     не підставляє в текстові поля автоматично. Некоректна або
        //     порожня дата/час — блоки позначок просто ховаються, без помилки,
        //     бо поле необов'язкове
        // EN: Optional date/time for manual mode — only generates ready-to-
        //     paste timestamp snippets (Telegram string, Discord tag), never
        //     auto-inserts anything into the text fields. An invalid or
        //     empty date/time just hides the snippet blocks, no error, since
        //     the field is optional
        private void RawScheduled_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (RawTelegramSnippetBox == null || RawDiscordSnippetBox == null) return;

            var unix = ParseLocalTime(RawDateInput.Text, RawTimeInput.Text);
            if (unix.HasValue)
            {
                var timeZone = AppTimeZone.Resolve(_settings.TimeZoneId);
                RawTelegramSnippetBox.Text = TemplateService.FormatScheduledTelegram(unix.Value, timeZone);
                RawDiscordSnippetBox.Text = TemplateService.FormatScheduledDiscordSnippet(unix.Value);
                PanelRawTelegramSnippet.Visibility = _settings.UseTelegram ? Visibility.Visible : Visibility.Collapsed;
                PanelRawDiscordSnippet.Visibility = _settings.UseDiscord ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                PanelRawTelegramSnippet.Visibility = Visibility.Collapsed;
                PanelRawDiscordSnippet.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnCopyTelegramSnippet_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(RawTelegramSnippetBox.Text))
                System.Windows.Clipboard.SetText(RawTelegramSnippetBox.Text);
        }

        private void BtnCopyDiscordSnippet_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(RawDiscordSnippetBox.Text))
                System.Windows.Clipboard.SetText(RawDiscordSnippetBox.Text);
        }

        // =====================================================================
        // UA: Генератор тегу згадки — той самий принцип, що й у позначок часу
        //     вище: вводиться лише ID, генерується готовий код, який людина
        //     копіює й вставляє куди хоче. Автоматично в текст нічого не
        //     потрапляє, бо в ручному режимі відправляється рівно те, що
        //     набрано, і будь-яка "допомога" тут була б сюрпризом.
        // EN: Mention tag generator — the same principle as the timestamps
        //     above: only the ID is entered, the finished code is generated for
        //     the person to copy and paste wherever they want. Nothing lands in
        //     the text automatically, because manual mode sends exactly what
        //     was typed, and any "help" here would be a surprise.
        // =====================================================================
        //     Два окремих обробники з точними підписами замість одного спільного
        //     з RoutedEventArgs: формально C# дозволив би один метод (параметри
        //     делегата контраваріантні), але WPF генерує підписку в
        //     .g.cs-коді, і покладатись тут на тонкість правил перетворення
        //     груп методів заради економії трьох рядків — погана угода.
        //     Two separate handlers with exact signatures instead of one shared
        //     RoutedEventArgs handler: C# would formally allow a single method
        //     (delegate parameters are contravariant), but WPF generates the
        //     subscription in .g.cs code, and relying on a subtlety of method
        //     group conversion rules to save three lines is a bad trade.
        private void MentionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateMentionSnippet();

        private void MentionId_TextChanged(object sender, TextChangedEventArgs e)
            => UpdateMentionSnippet();

        private void UpdateMentionSnippet()
        {
            // UA: SelectionChanged комбобокса спрацьовує ще під час
            //     InitializeComponent, коли решта полів ще не створена
            // EN: The combo's SelectionChanged fires during InitializeComponent,
            //     while the remaining fields don't exist yet
            if (RawMentionSnippetBox == null || RawMentionIdInput == null) return;

            // UA: Дужки навколо "as string" не обов'язкові за правилами
            //     пріоритету, але без них рядок читається як "as (string switch
            //     ...)", що збиває з пантелику при читанні
            // EN: The parentheses around "as string" aren't required by
            //     precedence rules, but without them the line reads as
            //     "as (string switch ...)", which is confusing to read
            var tag = (MentionTypeCombo.SelectedItem as ComboBoxItem)?.Tag as string;

            var kind = tag switch
            {
                "user" => DiscordService.MentionKind.User,
                "channel" => DiscordService.MentionKind.Channel,
                _ => DiscordService.MentionKind.Role
            };

            var snippet = DiscordService.FormatMention(kind, RawMentionIdInput.Text);

            // UA: Порожній результат = ID ще не введений або введений не лише
            //     цифрами (наприклад, скопійований цілий тег). Блок просто
            //     ховається — це не помилка, поле необов'язкове
            // EN: An empty result = the ID isn't entered yet, or isn't
            //     digits-only (e.g. a whole tag was pasted). The block simply
            //     hides — this isn't an error, the field is optional
            RawMentionSnippetBox.Text = snippet;
            PanelRawMentionSnippet.Visibility = snippet.Length > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void BtnCopyMentionSnippet_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(RawMentionSnippetBox.Text))
                System.Windows.Clipboard.SetText(RawMentionSnippetBox.Text);
        }

        // =====================================================================
        // UA: Вибір Discord-каналу для цієї відправки.
        // EN: Discord channel selection for this send.
        // =====================================================================
        private void BuildChannelList()
        {
            // UA: Основний канал завжди перший і завжди в списку — навіть коли
            //     людина перемикається на інший, має бути видно, звідки вона
            //     пішла і куди повернутись
            // EN: The primary channel is always first and always in the list —
            //     even when switching away, it should be visible where you came
            //     from and where to return
            var channels = new List<DiscordWebhookTarget>();

            if (!string.IsNullOrWhiteSpace(_settings.DiscordWebhookUrl))
            {
                channels.Add(new DiscordWebhookTarget
                {
                    Name = string.IsNullOrWhiteSpace(_settings.DiscordWebhookName)
                        ? _loc.Get("send.channel.primary_default")
                        : _settings.DiscordWebhookName,
                    Url = _settings.DiscordWebhookUrl
                });
            }

            // UA: Записи без URL пропускаємо — порожній рядок у налаштуваннях
            //     не має ставати пунктом списку, який мовчки нікуди не надішле
            // EN: Entries without a URL are skipped — an empty row in settings
            //     shouldn't become a list item that silently sends nowhere
            channels.AddRange(_settings.DiscordExtraWebhooks
                .Where(w => !string.IsNullOrWhiteSpace(w.Url))
                .Select(w => new DiscordWebhookTarget
                {
                    Name = string.IsNullOrWhiteSpace(w.Name)
                        ? _loc.Get("send.channel.unnamed")
                        : w.Name,
                    Url = w.Url
                }));

            // UA: Перебудова не має скидати вже зроблений вибір (метод
            //     викликається й при перемиканні мови) — тому запам'ятовуємо
            //     URL і повертаємось на нього, якщо такий канал ще існує
            // EN: Rebuilding must not reset an existing choice (the method also
            //     runs on language switch) — so remember the URL and return to
            //     it if that channel still exists
            var previous = ChannelCombo.SelectedValue as string;

            ChannelCombo.ItemsSource = channels;
            ChannelCombo.SelectedIndex = 0;

            if (!string.IsNullOrEmpty(previous))
            {
                var match = channels.FindIndex(c => c.Url == previous);
                if (match >= 0) ChannelCombo.SelectedIndex = match;
            }

            // UA: Один канал — вибирати нема з чого, блок не показуємо взагалі
            // EN: One channel — there's nothing to choose, don't show the block
            PanelChannelPicker.Visibility = channels.Count > 1
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ChkOverrideChannel_Changed(object sender, RoutedEventArgs e)
        {
            if (ChannelCombo == null) return;

            bool custom = ChkOverrideChannel.IsChecked == true;
            ChannelCombo.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;

            // UA: Знімаючи галочку, повертаємось саме на основний канал, а не
            //     лишаємо останній обраний — інакше "вимкнув перевизначення",
            //     а надсилає все одно кудись убік
            // EN: Unticking returns to the primary channel rather than keeping
            //     the last selection — otherwise "override off" would still
            //     send somewhere else
            if (!custom) ChannelCombo.SelectedIndex = 0;
        }

        // UA: Канал для поточної відправки. null = типовий з налаштувань, тож
        //     сервіси не мусять знати про існування перемикача
        // EN: The channel for the current send. null = the default from
        //     settings, so the services need not know the toggle exists
        private string? SelectedWebhookUrl()
        {
            if (ChkOverrideChannel.IsChecked == true)
                return ChannelCombo.SelectedValue as string;

            // UA: Вироджений, але цілком можливий випадок: основний вебхук
            //     порожній (наприклад, у appsettings.json його стерли руками),
            //     а додаткові канали заведені. Повернути тут null означало б
            //     віддати сервісу порожній URL — той просто нічого не надішле,
            //     і людина побачить "надіслано" без жодного повідомлення в
            //     Discord. Тому підставляємо перший доступний канал.
            // EN: A degenerate but entirely possible case: the primary webhook
            //     is empty (e.g. hand-deleted from appsettings.json) while
            //     additional channels are configured. Returning null here would
            //     hand the service an empty URL — it would simply send nothing,
            //     and the person would see "sent" with no Discord message at
            //     all. So we fall back to the first available channel.
            if (string.IsNullOrWhiteSpace(_settings.DiscordWebhookUrl))
                return ChannelCombo.SelectedValue as string;

            return null;
        }

        private async Task LoadCandidatesAsync()
        {
            if (_youTubeService == null) return;

            ShowStatusLoading();
            BtnTogglePicker.Content = _loc.Get("send.picker.toggle");

            try
            {
                // UA: Помилка мережі більше не знищує список — сервіс поверне
                //     те, що є в локальному кеші, і окремо повідомить, що
                //     онлайн-оновитись не вдалося
                // EN: A network error no longer wipes the list — the service
                //     returns whatever is in the local cache and separately
                //     reports that the online refresh failed
                var (candidates, fetchError) = await _youTubeService.GetCandidatesWithStatusAsync();
                _candidates = candidates;
                BuildCandidateItems();

                if (fetchError != null)
                    await ReportLoadFailureAsync(fetchError);
                else
                    HideStatus();

                if (ChkAutoLatest.IsChecked == true)
                    AutoFillBestPick();
                else
                    UpdatePreview();
            }
            catch (Exception ex)
            {
                // UA: Сюди потрапляє лише те, що зламалось уже після отримання
                //     даних (наприклад, побудова списку) — сам мережевий збій
                //     обробляється вище через fetchError
                // EN: Only failures after the data was obtained land here (e.g.
                //     list building) — the network failure itself is handled
                //     above via fetchError
                await ReportLoadFailureAsync(ex);
                UpdatePreview();
            }
        }

        // =====================================================================
        // UA: Повідомлення про проблеми із завантаженням списку.
        //     Гібридна стратегія свідомо: модальне вікно з'являється ЛИШЕ коли
        //     працювати справді нічим (ані свіжих даних, ані кешу) — інакше
        //     людина отримувала б модалку при кожному відкритті вікна без
        //     інтернету, навіть коли список нормально показано з кешу. У
        //     "м'якому" випадку достатньо помітного банера в самому вікні.
        //     У будь-якому разі повний стектрейс іде в logs/ — рядок в UI
        //     пояснює ситуацію людині, лог потрібен для розбору причини.
        // EN: Reporting list-loading problems.
        //     The hybrid strategy is deliberate: a modal dialog appears ONLY
        //     when there's genuinely nothing to work with (neither fresh data
        //     nor cache) — otherwise the user would get a modal every time they
        //     open this window offline, even when the list is served fine from
        //     cache. In the "soft" case a visible in-window banner is enough.
        //     Either way the full stack trace goes to logs/ — the UI line
        //     explains the situation to a human, the log is for diagnosing it.
        // =====================================================================
        private async Task ReportLoadFailureAsync(Exception error)
        {
            await new CrashLogService().LogErrorAsync(
                "SendNotificationWindow: failed to refresh the YouTube candidate list",
                error);

            bool hasCache = _candidates.Count > 0;

            ShowStatus(
                icon: hasCache ? "⚠" : "⛔",
                text: _loc.Get(hasCache ? "send.picker.offline" : "send.picker.error"),
                foreground: hasCache ? "#F0C674" : "#F08A8A",
                border: hasCache ? "#7A5F1F" : "#7A2F2F",
                background: hasCache ? "#2A2213" : "#2A1414",
                allowRetry: true);

            if (hasCache) return;

            // UA: Кеш порожній — список порожній, вручну теж нічого не
            //     підставиться. Це вже блокує роботу, тож модалка виправдана
            // EN: Empty cache — the list is empty and nothing will be
            //     pre-filled either. That actually blocks the user, so a modal
            //     is warranted here
            System.Windows.MessageBox.Show(
                this,
                $"{_loc.Get("send.picker.error")}\n\n{error.Message}",
                _loc.Get("send.title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ShowStatusLoading() => ShowStatus(
            icon: "⏳",
            text: _loc.Get("send.picker.loading"),
            foreground: "#A89CB8",
            border: "#3D2B5E",
            background: "#1A1425",
            allowRetry: false);

        private void ShowStatus(string icon, string text, string foreground,
                                string border, string background, bool allowRetry)
        {
            TxtPickerStatusIcon.Text = icon;
            TxtPickerStatus.Text = text;

            var fg = Brush(foreground);
            TxtPickerStatusIcon.Foreground = fg;
            TxtPickerStatus.Foreground = fg;
            PanelPickerStatus.BorderBrush = Brush(border);
            PanelPickerStatus.Background = Brush(background);

            var retryVisibility = allowRetry ? Visibility.Visible : Visibility.Collapsed;
            BtnRetryLoad.Visibility = retryVisibility;

            // UA: Кнопка платного API-шляху живе рівно стільки ж, скільки й
            //     банер помилки: у нормальному стані її взагалі не існує, щоб
            //     ніхто не витратив квоту "просто так", не маючи проблеми
            // EN: The paid API-path button lives exactly as long as the error
            //     banner: in the normal state it doesn't exist at all, so nobody
            //     burns quota "just because" without actually having a problem
            BtnApiFallback.Visibility = retryVisibility;
            BtnApiFallback.ToolTip = _loc.Get("send.picker.api_fallback.tooltip");

            PanelPickerStatus.Visibility = Visibility.Visible;
        }

        private void HideStatus() => PanelPickerStatus.Visibility = Visibility.Collapsed;

        // UA: Хелпер, щоб не повторювати ColorConverter у кожному виклику
        // EN: Helper so ColorConverter isn't repeated at every call site
        //     (?? Colors.Transparent — щоб не отримати попередження про
        //     розпакування можливого null; на практиці всі hex тут константні)
        //     (?? Colors.Transparent — to avoid an unboxing-possible-null
        //     warning; in practice every hex here is a constant)
        private static SolidColorBrush Brush(string hex) =>
            new((Color)(ColorConverter.ConvertFromString(hex) ?? Colors.Transparent));

        // UA: Повторна спроба без перезапуску програми — той самий шлях, що й
        //     при відкритті вікна, тож окремої логіки не потрібно
        // EN: Retry without restarting the app — the same path as on window
        //     open, so no separate logic is needed
        private async void BtnRetryLoad_Click(object sender, RoutedEventArgs e)
        {
            BtnRetryLoad.IsEnabled = false;
            try
            {
                await LoadCandidatesAsync();
            }
            finally
            {
                BtnRetryLoad.IsEnabled = true;
            }
        }

        // =====================================================================
        // UA: Запасний шлях через YouTube Data API. Свідомо вимагає підтвердження
        //     і показує ціну в юнітах ДО запуску, а не після: на відміну від
        //     безкоштовного RSS цей шлях витрачає добову квоту, і людина має
        //     розуміти, за що платить, перш ніж натиснути. Ліміт скидається раз
        //     на добу за тихоокеанським часом, тож "перевитратити випадково" —
        //     цілком реальний сценарій, який коштував би решти дня без анонсів.
        // EN: The YouTube Data API fallback path. It deliberately asks for
        //     confirmation and shows the cost in units BEFORE running, not
        //     after: unlike the free RSS feed this path spends the daily quota,
        //     and the person should understand what they're paying before
        //     clicking. The limit resets once a day on Pacific time, so
        //     "accidentally overspending" is a very real scenario that would
        //     cost them the rest of the day without announcements.
        // =====================================================================
        private async void BtnApiFallback_Click(object sender, RoutedEventArgs e)
        {
            if (_youTubeService == null) return;

            var confirm = System.Windows.MessageBox.Show(
                this,
                string.Format(
                    _loc.Get("send.picker.api_fallback.confirm"),
                    Core.Services.YouTubeService.ApiFallbackQuotaCost),
                _loc.Get("send.title"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            BtnApiFallback.IsEnabled = false;
            BtnRetryLoad.IsEnabled = false;
            ShowStatusLoading();

            try
            {
                var (candidates, error) = await _youTubeService.GetCandidatesViaApiAsync();
                _candidates = candidates;
                BuildCandidateItems();

                if (error != null)
                {
                    await ReportLoadFailureAsync(error);
                }
                else
                {
                    // UA: Список свіжий, але отриманий платним шляхом — кажемо
                    //     про це прямо, щоб витрата квоти не була невидимою
                    // EN: The list is fresh but came via the paid path — say so
                    //     plainly, so the quota spend isn't invisible
                    ShowStatus(
                        icon: "✅",
                        text: string.Format(
                            _loc.Get("send.picker.api_fallback.done"),
                            Core.Services.YouTubeService.ApiFallbackQuotaCost),
                        foreground: "#8FD18F",
                        border: "#2F5A2F",
                        background: "#14220F",
                        allowRetry: false);
                }

                if (ChkAutoLatest.IsChecked == true)
                    AutoFillBestPick();
                else
                    UpdatePreview();
            }
            catch (Exception ex)
            {
                await ReportLoadFailureAsync(ex);
            }
            finally
            {
                BtnApiFallback.IsEnabled = true;
                BtnRetryLoad.IsEnabled = true;
            }
        }

        // UA: Дата/час для показу в списку кандидатів — пріоритет:
        //     заплановано (Upcoming) > реальний старт трансляції > публікація
        // EN: Date/time to display in the candidate list — priority:
        //     scheduled (Upcoming) > real broadcast start > publish date
        private static long? BestDisplayTime(ContentCacheEntry entry) =>
            entry.Type == VideoType.Upcoming
                ? entry.ScheduledStartTime
                : entry.ActualStartTime ?? entry.PublishedAt;

        private void BuildCandidateItems()
        {
            var items = new List<CandidateItem>();

            foreach (var entry in _candidates)
            {
                BitmapImage? thumbnail = null;
                if (!string.IsNullOrEmpty(entry.ThumbnailUrl))
                {
                    try { thumbnail = new BitmapImage(new Uri(entry.ThumbnailUrl, UriKind.Absolute)); }
                    catch { thumbnail = null; }
                }

                var typeLabel = entry.Type switch
                {
                    VideoType.Live => _loc.Get("type.live"),
                    VideoType.Upcoming => _loc.Get("type.upcoming"),
                    VideoType.Video => _loc.Get("type.video"),
                    VideoType.Short => _loc.Get("type.short"),
                    _ => "?"
                };

                var displayTime = BestDisplayTime(entry);
                string dateLabel;
                if (displayTime.HasValue)
                {
                    var (d, t) = FormatLocalTime(displayTime.Value);
                    dateLabel = $"{d} {t}";
                }
                else
                {
                    dateLabel = DateTimeOffset.FromUnixTimeSeconds(entry.DiscoveredAt)
                        .ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                }

                items.Add(new CandidateItem
                {
                    Source = entry,
                    Thumbnail = thumbnail,
                    SubLabel = $"{typeLabel} · {dateLabel}"
                });
            }

            CandidatesList.ItemsSource = items;
        }

        private void ChkAutoLatest_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelPickerRow == null) return;
            bool auto = ChkAutoLatest.IsChecked == true;
            PanelPickerRow.Visibility = auto ? Visibility.Collapsed : Visibility.Visible;

            if (auto)
            {
                PanelPickerList.Visibility = Visibility.Collapsed;
                AutoFillBestPick();
            }
        }

        private void BtnTogglePicker_Click(object sender, RoutedEventArgs e)
        {
            PanelPickerList.Visibility = PanelPickerList.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;
        }

        private void AutoFillBestPick()
        {
            // UA: Навіть якщо кандидатів немає — приклад повідомлення все одно
            //     треба відрендерити з шаблону (з порожніми {title}/{url}),
            //     інакше вікно виглядає повністю "мертвим" і незрозуміло, чи
            //     проблема в каналі, чи в шаблонах
            // EN: Even with no candidates, the preview must still be rendered
            //     from the template (with empty {title}/{url}), otherwise the
            //     window looks completely "dead" and it's unclear whether the
            //     problem is the channel or the templates
            if (_candidates.Count == 0)
            {
                UpdatePreview();
                return;
            }

            var pick = _candidates.FirstOrDefault(c => c.Type == VideoType.Live)
                ?? _candidates.FirstOrDefault(c => c.Type == VideoType.Upcoming)
                ?? _candidates.FirstOrDefault(c => c.Type is VideoType.Video or VideoType.Short);

            if (pick != null) FillForm(pick);
            else UpdatePreview();
        }

        private void CandidatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CandidatesList.SelectedItem is CandidateItem item)
            {
                FillForm(item.Source);
                PanelPickerList.Visibility = Visibility.Collapsed;
                BtnTogglePicker.Tag = "selected";
                BtnTogglePicker.Content = $"🎬 {item.Title} — {_loc.Get("send.picker.change")}";
            }
        }

        // UA: Підставити дані обраного кандидата у форму — мініатюра береться
        //     напряму з ContentCacheEntry (найнадійніше джерело), а не через
        //     розпізнавання URL
        // EN: Populate the form from the selected candidate — the thumbnail
        //     is taken directly from ContentCacheEntry (the most reliable
        //     source), not via URL pattern matching
        private void FillForm(ContentCacheEntry entry)
        {
            TitleInput.Text = entry.Title;

            TypeCombo.SelectedItem = entry.Type switch
            {
                VideoType.Live => ItemLive,
                VideoType.Upcoming => ItemUpcoming,
                VideoType.Video => ItemVideo,
                VideoType.Short => ItemShort,
                _ => ItemVideo
            };

            UrlInput.Text = entry.Url;

            var displayTime = BestDisplayTime(entry);
            if (displayTime.HasValue)
            {
                var (d, t) = FormatLocalTime(displayTime.Value);
                DateInput.Text = d;
                TimeInput.Text = t;
            }
            else
            {
                DateInput.Text = string.Empty;
                TimeInput.Text = string.Empty;
            }

            SetThumbnail(entry.ThumbnailUrl, isTwitch: false);
            UpdatePreview();
        }

        private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDateTimeLabels();
            UpdatePreview();
        }

        // UA: Підпис полів дати/часу залежить від типу: обов'язкова дата
        //     початку для Upcoming, довідкова дата публікації для решти
        // EN: Date/time field labels depend on type: required start date for
        //     Upcoming, informational publish date for everything else
        private void UpdateDateTimeLabels()
        {
            if (TypeCombo?.SelectedItem is not ComboBoxItem item) return;
            var type = Enum.Parse<VideoType>((string)item.Tag);

            if (type == VideoType.Upcoming)
            {
                TxtDateLabel.Text = _loc.Get("send.date_label");
                TxtTimeLabel.Text = string.Format(_loc.Get("send.time_label"), ZoneLabel());
            }
            else
            {
                TxtDateLabel.Text = _loc.Get("send.date_label.published");
                TxtTimeLabel.Text = _loc.Get("send.time_label.published");
            }
        }

        // UA: Коротка людяна назва поточної часової зони (з налаштувань,
        //     через AppTimeZone) — підставляється в підписи полів дати/часу,
        //     щоб ніде в UI не залишалось хардкодженого "Київ"
        // EN: A short human-readable name of the current timezone (from
        //     settings, via AppTimeZone) — substituted into date/time field
        //     labels so no UI text is left with a hardcoded "Kyiv"
        private string ZoneLabel() => AppTimeZone.Resolve(_settings.TimeZoneId).DisplayName;

        private void Field_TextChanged(object sender, TextChangedEventArgs e)
            => UpdatePreview();

        // UA: Окремий обробник для URL — окрім оновлення прикладу, намагається
        //     розпізнати платформу (для мініатюри й м'якого попередження) і,
        //     якщо посилання збігається з відомим кандидатом, підтягує решту
        //     полів автоматично (лише якщо заголовок ще порожній — щоб не
        //     затирати те, що користувач уже ввів вручну)
        // EN: A dedicated handler for the URL field — besides updating the
        //     preview, it tries to recognize the platform (for the thumbnail
        //     and soft warning) and, if the link matches a known candidate,
        //     auto-fills the rest of the fields (only if the title is still
        //     empty — to avoid overwriting something the user already typed)
        private void UrlInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isReady) return;

            var url = UrlInput.Text.Trim();

            TxtUrlWarning.Visibility = (!string.IsNullOrEmpty(url) && !IsRecognizedPlatformUrl(url))
                ? Visibility.Visible : Visibility.Collapsed;

            var ytId = TryExtractYouTubeVideoId(url);
            if (ytId != null)
            {
                var match = _candidates.FirstOrDefault(c => c.VideoId == ytId);
                if (match != null && string.IsNullOrWhiteSpace(TitleInput.Text))
                {
                    FillForm(match);
                    return;
                }

                SetThumbnail($"https://img.youtube.com/vi/{ytId}/mqdefault.jpg", isTwitch: false);
                UpdatePreview();
                return;
            }

            var twitchLogin = TryExtractTwitchLogin(url);
            if (twitchLogin != null)
            {
                // UA: Офіційного способу отримати превью Twitch-каналу без
                //     авторизації через Twitch API не існує (Helix API
                //     вимагає App Access Token). Перевірене на практиці
                //     неофіційне джерело (static-cdn.jtvnw.net) виявилось
                //     нестабільним/недоступним — тому прев'ю тут свідомо
                //     не показуємо, лише розпізнаємо посилання як коректне
                //     (знімаємо м'яке попередження про "непізнаний URL")
                // EN: There's no official way to get a Twitch channel
                //     thumbnail without Twitch API authorization (the Helix
                //     API requires an App Access Token). The unofficial
                //     source (static-cdn.jtvnw.net) tested in practice turned
                //     out unreliable/unavailable — so we deliberately don't
                //     show a thumbnail here, we just recognize the link as
                //     valid (clearing the "unrecognized URL" soft warning)
                SetThumbnail(null, isTwitch: false);
                UpdatePreview();
                return;
            }

            SetThumbnail(null, isTwitch: false);
            UpdatePreview();
        }

        // UA: Розпізнати YouTube video ID з будь-якого поширеного формату URL —
        //     не потребує API, превью будується напряму за ID
        // EN: Recognize a YouTube video ID from any common URL format —
        //     no API needed, the thumbnail is built directly from the ID
        private static string? TryExtractYouTubeVideoId(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var match = Regex.Match(url,
                @"(?:youtube\.com\/(?:watch\?v=|shorts\/|live\/)|youtu\.be\/)([A-Za-z0-9_-]{6,})");
            return match.Success ? match.Groups[1].Value : null;
        }

        // UA: Розпізнати логін каналу з посилання на Twitch
        // EN: Recognize a channel login from a Twitch link
        private static string? TryExtractTwitchLogin(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
            if (!uri.Host.EndsWith("twitch.tv", StringComparison.OrdinalIgnoreCase)) return null;
            if (uri.Host.StartsWith("clips.", StringComparison.OrdinalIgnoreCase)) return null;

            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length == 0 || string.IsNullOrWhiteSpace(segments[0])) return null;

            var reserved = new[] { "videos", "directory", "settings", "subscriptions", "p", "downloads", "jobs", "turbo", "prime", "wallet" };
            if (reserved.Contains(segments[0].ToLowerInvariant())) return null;

            return segments[0];
        }

        private static bool IsRecognizedPlatformUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return true;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            var host = uri.Host.ToLowerInvariant();
            return host.Contains("youtube.com") || host.Contains("youtu.be") || host.Contains("twitch.tv");
        }

        // UA: Встановити мініатюру безпечно — якщо посилання виявиться
        //     нечинним (особливо ймовірно для неофіційного Twitch-URL),
        //     просто ховаємо картинку без помилки
        // EN: Set the thumbnail safely — if the link turns out to be invalid
        //     (especially likely for the unofficial Twitch URL), just hide
        //     the image without an error
        private void SetThumbnail(string? url, bool isTwitch)
        {
            if (string.IsNullOrEmpty(url))
            {
                PreviewThumbnail.Source = null;
                PanelThumbnail.Visibility = Visibility.Collapsed;
                TxtThumbnailNote.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(url, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.DownloadFailed += (s, e) =>
                {
                    PreviewThumbnail.Source = null;
                    PanelThumbnail.Visibility = Visibility.Collapsed;
                    TxtThumbnailNote.Visibility = Visibility.Collapsed;
                };

                PreviewThumbnail.Source = bitmap;
                PanelThumbnail.Visibility = Visibility.Visible;
                TxtThumbnailNote.Visibility = isTwitch ? Visibility.Visible : Visibility.Collapsed;
            }
            catch
            {
                PreviewThumbnail.Source = null;
                PanelThumbnail.Visibility = Visibility.Collapsed;
                TxtThumbnailNote.Visibility = Visibility.Collapsed;
            }
        }

        // UA: Живий приклад для обох платформ одночасно — окремо для Telegram
        //     і Discord, кожен рендериться зі свого шаблону
        // EN: Live preview for both platforms at once — separately for
        //     Telegram and Discord, each rendered from its own template
        private void UpdatePreview()
        {
            if (!_isReady || TypeCombo.SelectedItem is not ComboBoxItem item) return;

            var type = Enum.Parse<VideoType>((string)item.Tag);
            long? scheduled = type == VideoType.Upcoming
                ? ParseLocalTime(DateInput.Text, TimeInput.Text)
                : null;

            var templates = _templateService.GetTemplates();
            var twitchUrl = _settings.UseTwitch ? _settings.TwitchUrl : string.Empty;
            var url = UrlInput.Text.Trim();
            var title = TitleInput.Text.Trim();
            var timeZone = AppTimeZone.Resolve(_settings.TimeZoneId);

            if (_settings.UseTelegram)
            {
                var template = type switch
                {
                    VideoType.Live => templates.Telegram.Live,
                    VideoType.Upcoming => templates.Telegram.Upcoming,
                    VideoType.Video => templates.Telegram.Video,
                    VideoType.Short => templates.Telegram.Short,
                    _ => string.Empty
                };
                PreviewTelegramBox.Text = _templateService.Apply(template, title, url, twitchUrl, timeZone, scheduled);
            }

            if (_settings.UseDiscord)
            {
                var titleTemplate = type switch
                {
                    VideoType.Live => templates.DiscordTitles.Live,
                    VideoType.Upcoming => templates.DiscordTitles.Upcoming,
                    VideoType.Video => templates.DiscordTitles.Video,
                    VideoType.Short => templates.DiscordTitles.Short,
                    _ => string.Empty
                };
                var bodyTemplate = type switch
                {
                    VideoType.Live => templates.Discord.Live,
                    VideoType.Upcoming => templates.Discord.Upcoming,
                    VideoType.Video => templates.Discord.Video,
                    VideoType.Short => templates.Discord.Short,
                    _ => string.Empty
                };
                var renderedTitle = _templateService.Apply(titleTemplate, title, url, twitchUrl, timeZone, scheduled);
                var renderedBody = _templateService.Apply(bodyTemplate, title, url, twitchUrl, timeZone, scheduled);
                PreviewDiscordBox.Text = $"{renderedTitle}\n\n{renderedBody}";
            }
        }

        // UA: Часова зона береться з поточних налаштувань через AppTimeZone —
        //     нічого не хардкодиться (раніше тут напряму стояло "FLE Standard
        //     Time"). Тому методи більше не static — їм потрібен доступ до _settings
        // EN: The timezone comes from the current settings via AppTimeZone —
        //     nothing is hardcoded here (this used to have "FLE Standard Time"
        //     inline). That's why these are no longer static — they need access to _settings
        private (string date, string time) FormatLocalTime(long unixSeconds)
        {
            var timeZone = AppTimeZone.Resolve(_settings.TimeZoneId);
            var localTime = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(unixSeconds), timeZone);
            return (
                localTime.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                localTime.ToString("HH:mm", CultureInfo.InvariantCulture)
            );
        }

        private long? ParseLocalTime(string dateText, string timeText)
        {
            if (!DateTime.TryParseExact(dateText.Trim(), "dd.MM.yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var datePart))
                return null;

            if (!TimeSpan.TryParseExact(timeText.Trim(), @"hh\:mm",
                    CultureInfo.InvariantCulture, out var timePart))
                return null;

            var timeZone = AppTimeZone.Resolve(_settings.TimeZoneId);
            var localDateTime = datePart.Date + timePart;
            var offset = timeZone.GetUtcOffset(localDateTime);
            return new DateTimeOffset(localDateTime, offset).ToUnixTimeSeconds();
        }

        // =====================================================================
        // UA: Пікери дати/часу — 📅/🕐 відкривають спільний спливний Popup,
        //     ⏱ одразу підставляє поточні дату/час (у зоні з налаштувань).
        //     Один Popup на календар і один на годинник обслуговують усі 4
        //     поля дати/часу в цьому вікні (шаблонний і ручний режими) —
        //     через _dateTarget/_timeTarget код-behind знає, куди саме писати
        //     результат вибору. Результат завжди записується в TextBox.Text —
        //     тобто йде тим самим шляхом, що й ручне введення (спрацьовує той
        //     самий TextChanged, та сама валідація)
        // EN: Date/time pickers — 📅/🕐 open a shared floating Popup, ⏱
        //     immediately fills in the current date/time (in the zone from
        //     settings). One Popup for the calendar and one for the clock
        //     serve all 4 date/time fields in this window (template and
        //     manual modes) — _dateTarget/_timeTarget tell the code-behind
        //     which field to write the picked value into. The result always
        //     goes through TextBox.Text — the same path as manual typing
        //     (same TextChanged, same validation)
        // =====================================================================

        private void BtnPickDate_Click(object sender, RoutedEventArgs e) => OpenDatePicker((Button)sender, DateInput);
        private void BtnPickDateRaw_Click(object sender, RoutedEventArgs e) => OpenDatePicker((Button)sender, RawDateInput);
        private void BtnPickTime_Click(object sender, RoutedEventArgs e) => OpenTimePicker((Button)sender, TimeInput);
        private void BtnPickTimeRaw_Click(object sender, RoutedEventArgs e) => OpenTimePicker((Button)sender, RawTimeInput);

        private void BtnDateNow_Click(object sender, RoutedEventArgs e) => SetDateNow(DateInput);
        private void BtnDateNowRaw_Click(object sender, RoutedEventArgs e) => SetDateNow(RawDateInput);
        private void BtnTimeNow_Click(object sender, RoutedEventArgs e) => SetTimeNow(TimeInput);
        private void BtnTimeNowRaw_Click(object sender, RoutedEventArgs e) => SetTimeNow(RawTimeInput);

        // UA: Відкрити календар біля конкретної кнопки, попередньо виділивши
        //     дату, яка вже введена в полі (якщо вона коректна) — щоб пікер
        //     не "забував" те, що людина вже ввела вручну
        // EN: Open the calendar next to a specific button, pre-selecting the
        //     date already typed into the field (if it's valid) — so the
        //     picker doesn't "forget" what the person already entered by hand
        private void OpenDatePicker(Button anchor, TextBox target)
        {
            _dateTarget = target;

            DatePickerCalendar.SelectedDate =
                DateTime.TryParseExact(target.Text.Trim(), "dd.MM.yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                    ? parsed
                    : null;

            DatePickerPopup.PlacementTarget = anchor;
            DatePickerPopup.IsOpen = true;
        }

        private void DatePickerCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            // UA: Умови розбито на окремі if — компілятор C# не завжди може
            //     довести, що pattern-змінна точно присвоєна, коли вона стоїть
            //     поруч з іншою умовою через "||" в одному виразі.
            // EN: Conditions are split into separate ifs — the C# compiler
            //     can't always prove a pattern variable is definitely assigned
            //     when it sits next to another condition via "||" in one expression.
            if (_dateTarget == null)
                return;
            if (DatePickerCalendar.SelectedDate is not DateTime picked)
                return;

            _dateTarget.Text = picked.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            DatePickerPopup.IsOpen = false;
        }

        // UA: Відкрити годинник біля конкретної кнопки, попередньо виставивши
        //     години/хвилини з поля (якщо воно коректне), інакше — з поточного
        //     часу в обраній зоні, щоб не стартувати з довільних 00:00
        // EN: Open the clock next to a specific button, pre-setting hours/
        //     minutes from the field (if it's valid), otherwise from the
        //     current time in the configured zone, so it doesn't start from
        //     an arbitrary 00:00
        private void OpenTimePicker(Button anchor, TextBox target)
        {
            _timeTarget = target;

            if (!TimeSpan.TryParseExact(target.Text.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var current))
            {
                var zone = AppTimeZone.Resolve(_settings.TimeZoneId);
                current = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).TimeOfDay;
            }

            HourCombo.SelectedItem = current.Hours.ToString("00");
            MinuteCombo.SelectedItem = current.Minutes.ToString("00");

            TimePickerPopup.PlacementTarget = anchor;
            TimePickerPopup.IsOpen = true;
        }

        private void BtnApplyTime_Click(object sender, RoutedEventArgs e)
        {
            // UA: Так само розбито на окремі if — з тієї ж причини, що й вище.
            // EN: Same split into separate ifs — same reason as above.
            if (_timeTarget == null)
                return;
            if (HourCombo.SelectedItem is not string h)
                return;
            if (MinuteCombo.SelectedItem is not string m)
                return;

            _timeTarget.Text = $"{h}:{m}";
            TimePickerPopup.IsOpen = false;
        }

        // UA: "Зараз" — читає системний годинник і конвертує в обрану в
        //     налаштуваннях зону (та сама AppTimeZone-логіка, що й усюди —
        //     жодного окремого хардкоду тут)
        // EN: "Now" — reads the system clock and converts it into the zone
        //     configured in settings (the same AppTimeZone logic used
        //     everywhere else — no separate hardcoding here)
        private void SetDateNow(TextBox target)
        {
            var zone = AppTimeZone.Resolve(_settings.TimeZoneId);
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
            target.Text = now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        }

        private void SetTimeNow(TextBox target)
        {
            var zone = AppTimeZone.Resolve(_settings.TimeZoneId);
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
            target.Text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (ChkRawMode.IsChecked == true)
            {
                await SendRawAsync();
                return;
            }

            var title = TitleInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                System.Windows.MessageBox.Show(
                    _loc.Get("send.validation.title"), _loc.Get("app.name"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var type = Enum.Parse<VideoType>((string)((ComboBoxItem)TypeCombo.SelectedItem).Tag);
            long? scheduled = null;

            if (type == VideoType.Upcoming)
            {
                scheduled = ParseLocalTime(DateInput.Text, TimeInput.Text);
                if (scheduled == null)
                {
                    System.Windows.MessageBox.Show(
                        _loc.Get("send.validation.datetime"), _loc.Get("app.name"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            var url = UrlInput.Text.Trim();

            // UA: Розпізнаємо YouTube ID з поточного URL, щоб Discord-embed
            //     отримав картинку — Discord, на відміну від Telegram, не тягне
            //     прев'ю сам за посиланням, а бере його явно з ThumbnailUrl,
            //     який будується саме з VideoId
            // EN: Recognize the YouTube ID from the current URL so the Discord
            //     embed gets an image — unlike Telegram, Discord doesn't fetch
            //     a preview from the link itself; it takes it explicitly from
            //     ThumbnailUrl, which is built from VideoId
            var videoId = TryExtractYouTubeVideoId(url) ?? string.Empty;

            var video = new VideoInfo
            {
                Title = title,
                Type = type,
                ScheduledStartTime = scheduled,
                VideoId = videoId,
                UrlOverride = string.IsNullOrWhiteSpace(url) ? null : url
            };

            BtnSend.IsEnabled = false;
            try
            {
                await _dispatcher.SendAsync(video, SelectedWebhookUrl());
                System.Windows.MessageBox.Show(
                    _loc.Get("send.sent"), _loc.Get("app.name"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    ex.Message, _loc.Get("app.error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSend.IsEnabled = true;
            }
        }

        // UA: Повністю ручна відправка — жодного шаблону, жодної підстановки.
        //     Кожна платформа отримує рівно той текст, що введений у своєму
        //     полі; порожнє поле для платформи означає "не надсилати туди"
        // EN: Fully manual send — no template, no substitution at all. Each
        //     platform gets exactly the text typed into its own field; an
        //     empty field for a platform means "don't send there"
        private async Task SendRawAsync()
        {
            var telegramText = _settings.UseTelegram ? RawTelegramInput.Text.Trim() : string.Empty;
            var discordText = _settings.UseDiscord ? RawDiscordInput.Text.Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(telegramText) && string.IsNullOrWhiteSpace(discordText))
            {
                System.Windows.MessageBox.Show(
                    _loc.Get("send.raw.validation.empty"), _loc.Get("app.name"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnSend.IsEnabled = false;
            try
            {
                await _dispatcher.SendRawAsync(telegramText, discordText, SelectedWebhookUrl());
                System.Windows.MessageBox.Show(
                    _loc.Get("send.sent"), _loc.Get("app.name"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    ex.Message, _loc.Get("app.error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSend.IsEnabled = true;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _loc.LanguageChanged -= ApplyLocalization;
            base.OnClosed(e);
        }
    }
}