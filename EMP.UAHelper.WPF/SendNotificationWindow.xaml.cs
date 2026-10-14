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
using System.IO;
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
// UA: Той самий конфлікт WPF/WinForms, що описаний вище — OpenFileDialog
//     також існує в обох просторах імен (System.Windows.Forms і
//     Microsoft.Win32). Явно фіксуємо WPF-варіант.
// EN: The same WPF/WinForms conflict described above — OpenFileDialog also
//     exists in both namespaces (System.Windows.Forms and Microsoft.Win32).
//     Pin the WPF variant explicitly.
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

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

        // UA: Список каналів Discord, що зараз стоїть у ChannelCombo — сам
        //     ComboBox прив'язаний лише до назв (рядків), тож саме тут
        //     тримаємо відповідність "індекс у списку -> реальний URL".
        //     Детальніше — коментар у BuildChannelList().
        // EN: The list of Discord channels currently in ChannelCombo — the
        //     ComboBox itself is bound only to the names (strings), so this
        //     is where "list index -> real URL" is tracked. See the comment
        //     in BuildChannelList() for why.
        private List<DiscordWebhookTarget> _channels = new();

        // UA: Збережені ролі Discord (⚙️ Налаштування → Discord → Ролі) —
        //     для пікера в генераторі згадок. Та сама ідея, що й у _channels
        //     вище: тут лише ті записи, в яких реально є ID, без порожніх
        // EN: Saved Discord roles (⚙️ Settings → Discord → Roles) — for the
        //     picker in the mention generator. Same idea as _channels above:
        //     only entries that actually have an ID, no empty ones
        private List<DiscordRoleTarget> _roles = new();

        // UA: Локальні файли зображень, прикріплені в ручному режимі —
        //     спільні для Telegram і Discord (одна галерея, обидві
        //     платформи). Шляхи, не байти: файл читається лише в момент
        //     фактичної відправки (DiscordService/TelegramService), тут
        //     тримаємо мінімум пам'яті — самі лише рядки-шляхи
        // EN: Local image files attached in manual mode — shared by
        //     Telegram and Discord (one gallery, both platforms). Paths, not
        //     bytes: the file is only read at actual send time
        //     (DiscordService/TelegramService), here we keep the minimum —
        //     just the path strings
        private readonly List<string> _attachedImagePaths = new();

        // UA: Яке текстове поле зараз "слухає" спільний піквер дати/часу —
        //     призначається перед відкриттям відповідного Popup
        // EN: Which text field the shared date/time picker is currently
        //     "listening" for — assigned right before opening the
        //     corresponding Popup
        private TextBox? _dateTarget;
        private TextBox? _timeTarget;

        // UA: Результат і "відбиток" вмісту останньої спроби відправки в
        //     цьому вікні — потрібні для захисту від дублів: якщо людина
        //     натискає "Надіслати" ще раз БЕЗ зміни вмісту, платформи, що
        //     вже підтвердили успіх минулого разу, не повинні мовчки
        //     отримати повідомлення вдруге. null = ще жодної спроби не було
        //     в цьому вікні.
        // EN: The result and content "fingerprint" of the last send attempt
        //     in this window — needed for duplicate protection: if the
        //     person presses "Send" again WITHOUT changing the content,
        //     platforms that already confirmed success last time shouldn't
        //     silently receive the message a second time. null = no attempt
        //     has been made yet in this window.
        private SendResult? _lastSendResult;
        private string? _lastSendSignature;

        // UA: Результат останньої перевірки з'єднання (GetMe/GetChat для
        //     Telegram, GET вебхука для Discord) — читається з
        //     ApplyLocalization під час зміни мови, щоб перекласти вже
        //     показаний результат, а не повторювати мережевий запит.
        //     _connChecked лишається false, доки перша перевірка не
        //     завершиться хоч раз.
        // EN: The result of the last connection check (GetMe/GetChat for
        //     Telegram, webhook GET for Discord) — read back by
        //     ApplyLocalization on a language switch, to translate the
        //     already-shown result instead of repeating the network call.
        //     _connChecked stays false until the first check has completed
        //     at least once.
        private SendResult? _lastConnResult;
        private bool _connChecked;

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
            //     00-59, один раз під час побудови вікна
            // EN: Hour/minute lists for the time picker popup — 00-23 and
            //     00-59, built once when the window is constructed
            HourCombo.ItemsSource = Enumerable.Range(0, 24).Select(h => h.ToString("00")).ToList();
            MinuteCombo.ItemsSource = Enumerable.Range(0, 60).Select(m => m.ToString("00")).ToList();

            MentionTypeCombo.SelectedIndex = 0;
            UpdateCharCounters();

            ApplyTextFilter(TitleInput);
            ApplyTextFilter(RawTelegramInput);
            ApplyTextFilter(RawDiscordInput);

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

            // UA: Перевірка з'єднання стартує одразу під час відкриття вікна —
            //     fire-and-forget, той самий підхід, що й у LoadCandidatesAsync
            //     вище: вікно не блокується в очікуванні мережевого запиту
            // EN: Connection check starts right when the window opens —
            //     fire-and-forget, same approach as LoadCandidatesAsync
            //     above: the window isn't blocked waiting on the network call
            _ = RunConnectionCheckAsync();
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

            BtnRecheckConnection.ToolTip = _loc.Get("send.conn.recheck_tooltip");
            // UA: Текст рядків з'єднання оновлюється лише якщо перевірка вже
            //     бодай раз відбулась — інакше перший виклик ApplyLocalization
            //     (у конструкторі) перезапише щойно встановлений "checking..."
            //     ще до того, як запит навіть пішов. _connChecked встановлюється
            //     в RunConnectionCheckAsync.
            // EN: Only refresh the connection-line text if a check has
            //     already happened at least once — otherwise the very first
            //     ApplyLocalization call (from the constructor) would
            //     overwrite the just-set "checking..." text before the
            //     request has even gone out. _connChecked is set in
            //     RunConnectionCheckAsync.
            if (_connChecked)
            {
                SetConnectionLine(TxtConnTelegram, "Telegram", _lastConnResult?.Telegram);
                SetConnectionLine(TxtConnDiscord, "Discord", _lastConnResult?.Discord);
            }

            ChkRawMode.Content = _loc.Get("send.raw.toggle");
            TxtRawModeHint.Text = _loc.Get("send.raw.hint");
            TxtRawTelegramLabel.Text = _loc.Get("send.raw.telegram_label");
            TxtRawTelegramHint.Text = _loc.Get("send.raw.telegram_hint");
            TxtRawDiscordLabel.Text = _loc.Get("send.raw.discord_label");
            TxtRawDiscordHint.Text = _loc.Get("send.raw.discord_hint");
            TxtRawEmpty.Text = _loc.Get("send.raw.empty_platforms");

            // UA: Підказки кнопок форматування — та сама пара ключів для
            //     обох платформ (Telegram і Discord), бо суть позначки
            //     однакова, лише синтаксис різний (уже видно з самого коду,
            //     що вставляється)
            // EN: Formatting button tooltips — the same pair of keys for
            //     both platforms (Telegram and Discord), since the meaning of
            //     each mark is identical, only the syntax differs (already
            //     visible from the inserted code itself)
            BtnFmtTgBold.ToolTip = BtnFmtDcBold.ToolTip = _loc.Get("send.raw.format.bold");
            BtnFmtTgItalic.ToolTip = BtnFmtDcItalic.ToolTip = _loc.Get("send.raw.format.italic");
            BtnFmtTgUnderline.ToolTip = BtnFmtDcUnderline.ToolTip = _loc.Get("send.raw.format.underline");
            BtnFmtTgStrike.ToolTip = BtnFmtDcStrike.ToolTip = _loc.Get("send.raw.format.strike");
            BtnFmtTgCode.ToolTip = BtnFmtDcCode.ToolTip = _loc.Get("send.raw.format.code");
            BtnFmtTgSpoiler.ToolTip = BtnFmtDcSpoiler.ToolTip = _loc.Get("send.raw.format.spoiler");
            BtnFmtTgLink.ToolTip = BtnFmtDcLink.ToolTip = _loc.Get("send.raw.format.link");

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

            TxtRawAttachLabel.Text = _loc.Get("send.raw.attach.label");
            TxtRawAttachHint.Text = _loc.Get("send.raw.attach.hint");
            BtnAttachImages.Content = _loc.Get("send.raw.attach.button");
            TxtAttachSizeWarning.Text = _loc.Get("send.raw.attach.size_warning");

            ChkOverrideChannel.Content = _loc.Get("send.channel.override");
            TxtChannelHint.Text = _loc.Get("send.channel.hint");

            // UA: Список каналів перебудовується саме тут, а не в конструкторі:
            //     підписи безіменних каналів локалізовані, тож коли мова
            //     перемикається, вони мають змінитись разом з рештою інтерфейсу
            // EN: The channel list is rebuilt here rather than in the
            //     constructor: labels for unnamed channels are localized, so
            //     they must change along with the rest of the UI on switch
            BuildChannelList();

            // UA: Та сама причина, що й у BuildChannelList() вище — підпис
            //     "ввести вручну" локалізований, тож список ролей теж
            //     перебудовується тут, а не лише в конструкторі
            // EN: Same reason as BuildChannelList() above — the "enter
            //     manually" label is localized, so the role list is also
            //     rebuilt here, not just in the constructor
            TxtRolePickerLabel.Text = _loc.Get("send.raw.role_picker.label");
            BuildRoleList();
            UpdateRolePickerVisibility();

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
        {
            UpdateMentionSnippet();
            UpdateRolePickerVisibility();
        }

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
            //     ...)", що збиває з пантелику під час читання
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
        // UA: Пікер збереженої ролі — альтернатива ручному введенню ID у
        //     RawMentionIdInput. Пункт 0 завжди "ввести вручну" (нічого не
        //     підставляє, не чіпає поточне значення поля ID) — так початковий
        //     стан (без жодної ролі ще не обраної) не виглядає так, ніби вже
        //     щось вибрано.
        // EN: Saved-role picker — an alternative to typing the ID by hand
        //     into RawMentionIdInput. Item 0 is always "enter manually"
        //     (inserts nothing, leaves the ID field's current value alone) —
        //     so the initial state (no role picked yet) doesn't look like
        //     something was already selected.
        // =====================================================================
        private void BuildRoleList()
        {
            // UA: Список будується не лише зі "Збережених ролей Discord", а й
            //     включає основну роль сповіщень (DiscordRoleId з "⚙️
            //     Налаштування" → Discord) — її не треба дублювати в окремий
            //     список, щоб вона з'явилась тут. Без цього кроку людина, що
            //     задала роль лише для автоматичних сповіщень, взагалі не
            //     бачила жодного вибору в цьому пікері — саме так і сталось.
            //     HashSet дедуплікує за ID, щоб та сама роль не з'явилась
            //     двічі, якщо її випадково завели в обох місцях.
            // EN: The list isn't built only from "Saved Discord roles" — it
            //     also includes the primary notification role (DiscordRoleId
            //     from "⚙️ Settings" → Discord) — it doesn't need to be
            //     duplicated into the separate list to show up here. Without
            //     this, someone who only ever set a role for automatic
            //     notifications saw no choice at all in this picker — which
            //     is exactly what happened.
            //     The HashSet deduplicates by ID, so the same role doesn't
            //     appear twice if it was accidentally entered in both places.
            var seenIds = new HashSet<string>();
            var roles = new List<DiscordRoleTarget>();

            if (!string.IsNullOrWhiteSpace(_settings.DiscordRoleId) && seenIds.Add(_settings.DiscordRoleId))
            {
                roles.Add(new DiscordRoleTarget
                {
                    Name = _loc.Get("send.raw.role_picker.default_role"),
                    Id = _settings.DiscordRoleId
                });
            }

            foreach (var role in _settings.DiscordRoles)
            {
                if (string.IsNullOrWhiteSpace(role.Id)) continue;
                if (!seenIds.Add(role.Id)) continue;
                roles.Add(role);
            }

            _roles = roles;

            var items = new List<string> { _loc.Get("send.raw.role_picker.manual") };
            items.AddRange(_roles.Select(r => string.IsNullOrWhiteSpace(r.Name) ? r.Id : r.Name));

            RawRoleCombo.ItemsSource = items;
            RawRoleCombo.SelectedIndex = 0;
        }

        // UA: Пікер показується лише для типу "Роль" і лише якщо є з чого
        //     обирати — для User/Channel чи порожнього списку ролей він
        //     просто не займає місця на екрані
        // EN: Only show the picker for the "Role" type, and only if there's
        //     something to pick — for User/Channel or an empty role list it
        //     simply takes up no screen space
        private void UpdateRolePickerVisibility()
        {
            if (PanelRoleQuickPick == null) return;

            var tag = (MentionTypeCombo.SelectedItem as ComboBoxItem)?.Tag as string;
            PanelRoleQuickPick.Visibility = (tag == "role" && _roles.Count > 0)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void RawRoleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var i = RawRoleCombo.SelectedIndex;
            if (i <= 0 || i - 1 >= _roles.Count) return;

            RawMentionIdInput.Text = _roles[i - 1].Id;
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
            //     викликається й під час перемикання мови) — тому URL (за
            //     старим _channels/SelectedIndex) запам'ятовується, і вибір
            //     повертається на нього, якщо такий канал ще існує
            // EN: Rebuilding must not reset an existing choice (the method also
            //     runs on language switch) — so remember the URL (via the old
            //     _channels/SelectedIndex) and return to it if that channel
            //     still exists
            string? previousUrl = null;
            if (ChannelCombo.SelectedIndex >= 0 && ChannelCombo.SelectedIndex < _channels.Count)
                previousUrl = _channels[ChannelCombo.SelectedIndex].Url;

            _channels = channels;

            // UA: ComboBox прив'язуємо до самих назв (List<string>), а не до
            //     об'єктів DiscordWebhookTarget напряму. Причина: кастомний
            //     ControlTemplate у Styles/DarkComboBox.xaml (ComboField) не
            //     синхронізує DisplayMemberPath із закритим станом
            //     комбобокса — замість "Name" показує повний .ToString()
            //     об'єкта, тобто "EMP.UAHelper.Core.Services.DiscordWebhookTarget".
            //     У рядків такої проблеми немає: ToString() рядка — він сам.
            //     Реальний URL для вибраного пункту беремо з _channels за
            //     тим самим індексом (SelectedIndexChanged тут не потрібен).
            // EN: The ComboBox is bound to the plain names (List<string>),
            //     not directly to DiscordWebhookTarget objects. Reason: the
            //     custom ControlTemplate in Styles/DarkComboBox.xaml
            //     (ComboField) doesn't sync DisplayMemberPath with the
            //     closed-box state of the combo — instead of "Name" it shows
            //     the object's raw .ToString(), i.e.
            //     "EMP.UAHelper.Core.Services.DiscordWebhookTarget". Strings
            //     don't have that problem: a string's ToString() is itself.
            //     The actual URL for the selected entry is read from
            //     _channels at the same index (no SelectedIndexChanged needed).
            ChannelCombo.ItemsSource = channels.Select(c => c.Name).ToList();
            ChannelCombo.SelectedIndex = 0;

            if (!string.IsNullOrEmpty(previousUrl))
            {
                var match = channels.FindIndex(c => c.Url == previousUrl);
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
                return SelectedChannelUrl();

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
                return SelectedChannelUrl();

            return null;
        }

        // UA: ChannelCombo зберігає лише назви (рядки) — див. коментар у
        //     BuildChannelList(). Реальний URL береться з _channels за тим
        //     самим індексом.
        // EN: ChannelCombo only holds names (strings) — see the comment in
        //     BuildChannelList(). The real URL is taken from _channels at
        //     the same index.
        private string? SelectedChannelUrl()
        {
            int i = ChannelCombo.SelectedIndex;
            return (i >= 0 && i < _channels.Count) ? _channels[i].Url : null;
        }

        // =====================================================================
        // UA: Прикріплення зображень у ручному режимі — спільна галерея для
        //     Telegram і Discord. Ліміт кількості (10) — не довільне рішення,
        //     а жорстка межа Telegram-медіагрупи (TelegramService.MaxAttachmentsPerMessage);
        //     Discord дозволяє більше, але єдина межа для обох платформ
        //     зрозуміліша, ніж дві різні.
        // EN: Image attachments in manual mode — one shared gallery for
        //     Telegram and Discord. The count limit (10) isn't an arbitrary
        //     choice — it's Telegram's hard media-group limit
        //     (TelegramService.MaxAttachmentsPerMessage); Discord allows more,
        //     but one shared limit for both platforms is clearer than two
        //     different ones.
        // =====================================================================

        // UA: М'який поріг попередження про розмір — не блокує відправку,
        //     лише попереджає, бо реальний ліміт залежить від платформи й
        //     навіть від рівня буста Discord-сервера (детальніше — коментарі
        //     в DiscordService/TelegramService)
        // EN: Soft size-warning threshold — doesn't block sending, just
        //     warns, since the real limit depends on the platform and even on
        //     the Discord server's boost level (see comments in
        //     DiscordService/TelegramService for detail)
        private const long WarnFileSizeBytes = 10 * 1024 * 1024;

        private void BtnAttachImages_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = _loc.Get("send.raw.attach.filter")
            };

            if (dialog.ShowDialog() != true) return;

            foreach (var path in dialog.FileNames)
            {
                if (_attachedImagePaths.Contains(path)) continue;
                if (_attachedImagePaths.Count >= TelegramService.MaxAttachmentsPerMessage) break;
                _attachedImagePaths.Add(path);
            }

            RebuildAttachmentGallery();
        }

        private void RebuildAttachmentGallery()
        {
            PanelAttachGallery.Children.Clear();
            var oversized = false;

            // UA: for з індексом, а не foreach — потрібен для стрілок
            //     порядку (яка з них активна залежить від позиції) і для
            //     drag-and-drop (куди саме "впало" перетягнуте зображення)
            // EN: for with an index, not foreach — needed for the order
            //     arrows (which one is enabled depends on position) and for
            //     drag-and-drop (exactly where a dragged image was "dropped")
            for (var index = 0; index < _attachedImagePaths.Count; index++)
            {
                var path = _attachedImagePaths[index];
                var capturedIndex = index;
                var capturedPath = path;

                var item = new StackPanel { Width = 76, Margin = new Thickness(0, 0, 8, 8) };

                var border = new Border
                {
                    Width = 72,
                    Height = 72,
                    BorderBrush = Brush("#3D2B5E"),
                    BorderThickness = new Thickness(1),
                    ClipToBounds = true,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    AllowDrop = true
                };

                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path, UriKind.Absolute);
                    // UA: Локальні файли можуть бути значно більшими за
                    //     будь-яке YouTube-прев'ю — обмежуємо декодований
                    //     розмір, щоб мініатюра не з'їдала пам'ять і диск
                    //     повністю на кожен скріншот у галереї
                    // EN: Local files can be far larger than any YouTube
                    //     thumbnail — cap the decoded size so a thumbnail
                    //     doesn't eat memory/disk in full for every
                    //     screenshot in the gallery
                    bitmap.DecodePixelWidth = 144;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    var image = new System.Windows.Controls.Image
                    {
                        Source = bitmap,
                        Stretch = Stretch.UniformToFill,
                        ToolTip = Path.GetFileName(path)
                    };
                    border.Child = image;
                }
                catch
                {
                    // UA: Файл видалили/пошкодили між вибором і рендером —
                    //     показуємо позначку замість падіння вікна
                    // EN: The file was removed/corrupted between selection and
                    //     render — show a marker instead of crashing the window
                    border.Child = new TextBlock
                    {
                        Text = "⚠",
                        FontSize = 24,
                        Foreground = Brush("#FFA500"),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }

                if (TryGetFileSize(path) is long size && size > WarnFileSizeBytes)
                    oversized = true;

                // UA: Клік vs перетягування на одній і тій самій мініатюрі —
                //     відстежуються точка натискання й поріг зсуву миші
                //     (SystemParameters.MinimumXxxDragDistance, системне
                //     значення, а не довільне число). Якщо зсув перевищує
                //     поріг — це перетягування (DragDrop.DoDragDrop, курсор
                //     "відпускається" лише над іншою мініатюрою). Якщо ні —
                //     це звичайний клік, що відкриває перегляд повного розміру.
                // EN: Click vs drag on the same thumbnail — track the press
                //     point and the mouse-move threshold
                //     (SystemParameters.MinimumXxxDragDistance, a system
                //     value, not an arbitrary number). If the movement
                //     exceeds the threshold, it's a drag (DragDrop.DoDragDrop,
                //     "dropped" only over another thumbnail). Otherwise it's
                //     a plain click that opens the full-size preview.
                System.Windows.Point? dragStart = null;

                border.PreviewMouseLeftButtonDown += (_, args) =>
                    dragStart = args.GetPosition(null);

                border.PreviewMouseMove += (_, args) =>
                {
                    if (dragStart is not { } start) return;
                    if (args.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;

                    var current = args.GetPosition(null);
                    if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                        Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
                        return;

                    // UA: Поріг перевищено — це перетягування, а не клік;
                    //     dragStart скидається, щоб наступний MouseLeftButtonUp
                    //     не відкрив прев'ю замість завершення drag'у
                    // EN: Threshold exceeded — this is a drag, not a click;
                    //     reset dragStart so the following
                    //     MouseLeftButtonUp doesn't open the preview instead
                    //     of finishing the drag
                    dragStart = null;
                    // UA: System.Windows.DragDropEffects вказано явно — у
                    //     проєкті підключено й System.Windows.Forms (через
                    //     OpenFileDialog), де є однойменний тип, тож без
                    //     повного шляху компілятор не може визначити, який
                    //     саме DragDropEffects мається на увазі (CS0104)
                    // EN: Explicitly qualify System.Windows.DragDropEffects —
                    //     the project also references System.Windows.Forms
                    //     (for OpenFileDialog), which has a type of the same
                    //     name, so without the full path the compiler can't
                    //     tell which DragDropEffects is meant (CS0104)
                    DragDrop.DoDragDrop(border, capturedIndex, System.Windows.DragDropEffects.Move);
                };

                border.PreviewMouseLeftButtonUp += (_, _) =>
                {
                    // UA: dragStart все ще заданий — отже, перетягування не
                    //     відбулось, і це був звичайний клік
                    // EN: dragStart is still set — so no drag happened, and
                    //     this was a plain click
                    if (dragStart != null)
                        ShowImagePreview(capturedPath);
                    dragStart = null;
                };

                border.Drop += (_, args) =>
                {
                    if (!args.Data.GetDataPresent(typeof(int))) return;
                    var sourceIndex = (int)args.Data.GetData(typeof(int));
                    MoveAttachment(sourceIndex, capturedIndex);
                };

                var moveLeftButton = new Button
                {
                    Content = "◀",
                    Style = (Style)FindResource("FormatToolButton"),
                    Margin = new Thickness(0, 4, 0, 0),
                    IsEnabled = index > 0,
                    ToolTip = _loc.Get("send.raw.attach.move_left")
                };
                moveLeftButton.Click += (_, _) => MoveAttachment(capturedIndex, capturedIndex - 1);

                var moveRightButton = new Button
                {
                    Content = "▶",
                    Style = (Style)FindResource("FormatToolButton"),
                    Margin = new Thickness(4, 4, 0, 0),
                    IsEnabled = index < _attachedImagePaths.Count - 1,
                    ToolTip = _loc.Get("send.raw.attach.move_right")
                };
                moveRightButton.Click += (_, _) => MoveAttachment(capturedIndex, capturedIndex + 1);

                var moveRow = new StackPanel
                {
                    // UA: Те саме, що й з DragDropEffects вище — System.Windows.Forms
                    //     теж має свій Orientation, тож тут потрібен повний шлях
                    // EN: Same issue as DragDropEffects above — System.Windows.Forms
                    //     also has its own Orientation, so the full path is needed here
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                };
                moveRow.Children.Add(moveLeftButton);
                moveRow.Children.Add(moveRightButton);

                var removeButton = new Button
                {
                    Content = "✕",
                    Style = (Style)FindResource("LangButton"),
                    Margin = new Thickness(0, 4, 0, 0),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    ToolTip = _loc.Get("send.raw.attach.remove")
                };
                removeButton.Click += (_, _) =>
                {
                    _attachedImagePaths.Remove(capturedPath);
                    RebuildAttachmentGallery();
                };

                item.Children.Add(border);
                item.Children.Add(moveRow);
                item.Children.Add(removeButton);
                PanelAttachGallery.Children.Add(item);
            }

            BtnAttachImages.IsEnabled = _attachedImagePaths.Count < TelegramService.MaxAttachmentsPerMessage;
            TxtAttachSizeWarning.Visibility = oversized ? Visibility.Visible : Visibility.Collapsed;

            // UA: Вкладення впливають на ліміт Telegram (підпис коротший за
            //     звичайний текст) — тож лічильник треба оновити і тут, а не
            //     лише коли змінюється сам текст
            // EN: Attachments affect Telegram's limit (a caption is shorter
            //     than plain text) — so the counter must be refreshed here
            //     too, not only when the text itself changes
            UpdateCharCounters();
        }

        // UA: Переставляє вкладення в списку й перебудовує галерею. Єдина
        //     точка зміни порядку — використовується і стрілками ◀▶, і
        //     drag-and-drop, тож обидва способи гарантовано поводяться
        //     однаково
        // EN: Moves an attachment within the list and rebuilds the gallery.
        //     The single point where order changes — used by both the ◀▶
        //     arrows and drag-and-drop, so the two input methods are
        //     guaranteed to behave identically
        private void MoveAttachment(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex) return;
            if (fromIndex < 0 || fromIndex >= _attachedImagePaths.Count) return;
            if (toIndex < 0 || toIndex >= _attachedImagePaths.Count) return;

            var path = _attachedImagePaths[fromIndex];
            _attachedImagePaths.RemoveAt(fromIndex);
            _attachedImagePaths.Insert(toIndex, path);

            RebuildAttachmentGallery();
        }

        // UA: Перегляд зображення в повному розмірі — окреме модальне вікно
        //     поверх цього, без власного XAML-файлу (повністю з коду, як і
        //     решта елементів галереї). Esc або клік за межами закриває.
        //     Якщо файл зник з диска між вибором і кліком — жодної дії,
        //     мініатюра вже й так показує позначку ⚠ для цього випадку
        // EN: Full-size image preview — a separate modal window on top of
        //     this one, with no XAML file of its own (built entirely from
        //     code, like the rest of the gallery). Esc or clicking outside
        //     closes it. If the file vanished from disk between selection
        //     and the click — silently do nothing, the thumbnail already
        //     shows a ⚠ marker for that case
        //
        //     UA: Раніше зображення примусово стискалось до MaxWidth=900/
        //     MaxHeight=700 — на моніторах з меншою роздільною здатністю, ніж
        //     оригінал (напр. 1080p), це давало помітно зменшену картинку з
        //     рамками по боках і без жодного способу роздивитись деталі.
        //     Тепер зображення показується в оригінальному розмірі (Stretch
        //     .None) всередині ScrollViewer, вікно підлаштовується під розмір
        //     картинки, але не більше ~92% робочої area екрана. Масштаб
        //     керується Ctrl+колесо миші (як у браузерах) або кнопками
        //     ➖ / 100% / ➕ зверху — для зображень, більших за екран, чи
        //     навпаки, щоб роздивитись дрібні деталі ближче.
        //     EN: Previously the image was forced down to MaxWidth=900/
        //     MaxHeight=700 — on monitors with a lower resolution than the
        //     original (e.g. 1080p), this produced a visibly shrunk image
        //     with side bars and no way to inspect detail. The image is now
        //     shown at its native size (Stretch.None) inside a ScrollViewer;
        //     the window sizes itself to the image but caps at ~92% of the
        //     screen's work area. Zoom is controlled via Ctrl+mouse wheel
        //     (the same gesture browsers use) or the ➖ / 100% / ➕ buttons
        //     above the image — for images bigger than the screen, or to
        //     zoom in on fine detail.
        private void ShowImagePreview(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                var scaleTransform = new ScaleTransform(1.0, 1.0);

                var image = new System.Windows.Controls.Image
                {
                    Source = bitmap,
                    Stretch = Stretch.None,
                    LayoutTransform = scaleTransform
                };

                var scrollViewer = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = image
                };

                // UA: Ctrl+колесо миші змінює масштаб; без Ctrl колесо й далі
                //     прокручує вміст як завжди (стандартна поведінка
                //     ScrollViewer не чіпається)
                // EN: Ctrl+wheel changes the zoom; without Ctrl the wheel
                //     keeps scrolling the content as usual (the default
                //     ScrollViewer behavior is left untouched)
                scrollViewer.PreviewMouseWheel += (_, args) =>
                {
                    if (System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.Control) return;
                    args.Handled = true;
                    var factor = args.Delta > 0 ? 1.15 : 1 / 1.15;
                    var newScale = Math.Clamp(scaleTransform.ScaleX * factor, 0.1, 6.0);
                    scaleTransform.ScaleX = scaleTransform.ScaleY = newScale;
                };

                var zoomLabel = new TextBlock
                {
                    Text = "100%", Foreground = Brush("#E5DFF0"),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Width = 44, TextAlignment = System.Windows.TextAlignment.Center
                };

                void ApplyZoom(double scale)
                {
                    scaleTransform.ScaleX = scaleTransform.ScaleY = scale;
                    zoomLabel.Text = $"{scale * 100:0}%";
                }

                var btnZoomOut = new Button
                {
                    Content = "➖", Style = (Style)FindResource("FormatToolButton"),
                    ToolTip = _loc.Get("send.preview.zoom_out")
                };
                btnZoomOut.Click += (_, _) => ApplyZoom(Math.Max(scaleTransform.ScaleX / 1.25, 0.1));

                var btnZoomReset = new Button
                {
                    Content = "⟲", Style = (Style)FindResource("FormatToolButton"),
                    ToolTip = _loc.Get("send.preview.zoom_reset")
                };
                btnZoomReset.Click += (_, _) => ApplyZoom(1.0);

                var btnZoomIn = new Button
                {
                    Content = "➕", Style = (Style)FindResource("FormatToolButton"),
                    ToolTip = _loc.Get("send.preview.zoom_in")
                };
                btnZoomIn.Click += (_, _) => ApplyZoom(Math.Min(scaleTransform.ScaleX * 1.25, 6.0));

                var toolbar = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                toolbar.Children.Add(btnZoomOut);
                toolbar.Children.Add(zoomLabel);
                toolbar.Children.Add(btnZoomIn);
                toolbar.Children.Add(btnZoomReset);

                var root = new DockPanel();
                DockPanel.SetDock(toolbar, Dock.Top);
                root.Children.Add(toolbar);
                root.Children.Add(scrollViewer);

                // UA: Вікно підлаштовується під реальний розмір картинки
                //     (+невеликий запас на панель інструментів і можливі
                //     смуги прокрутки), але не більше ~92% робочої area
                //     екрана — щоб вікно саме не вилізло за межі монітора
                //     на 4K/8K зображеннях
                // EN: The window sizes itself to the image's real dimensions
                //     (plus a small allowance for the toolbar and potential
                //     scrollbars), capped at ~92% of the screen's work area —
                //     so the window itself doesn't overflow the monitor on
                //     4K/8K images
                var workArea = SystemParameters.WorkArea;
                var preview = new Window
                {
                    Title = $"{Path.GetFileName(path)} ({bitmap.PixelWidth}×{bitmap.PixelHeight})",
                    Content = root,
                    Width = Math.Min(bitmap.PixelWidth + 40, workArea.Width * 0.92),
                    Height = Math.Min(bitmap.PixelHeight + 90, workArea.Height * 0.92),
                    ResizeMode = ResizeMode.CanResize,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = Brush("#0D0A14")
                };

                preview.PreviewKeyDown += (_, args) =>
                {
                    if (args.Key == System.Windows.Input.Key.Escape) preview.Close();
                };

                preview.ShowDialog();
            }
            catch
            {
                // UA: Файл зник/пошкоджений — нічого не показується, це не
                //     критична дія
                // EN: The file vanished/is corrupted — show nothing, this
                //     isn't a critical action
            }
        }

        // =====================================================================
        // UA: Реальні ліміти символів для ручного режиму.
        //     Telegram: 4096 символів для звичайного тексту (sendMessage),
        //     але лише 1024 для підпису (sendPhoto/sendMediaGroup) — тому межа
        //     залежить від того, чи є вкладення.
        //     Discord: content завжди обмежений 2000 символами, незалежно від
        //     вкладень.
        //     Лічильник під полем показує ці цифри в реальному часі; окремо
        //     від нього CheckCharLimits нижче блокує фактичну відправку на
        //     платформу, текст для якої перевищує її ліміт.
        // EN: Real character limits for manual mode.
        //     Telegram: 4096 characters for plain text (sendMessage), but
        //     only 1024 for a caption (sendPhoto/sendMediaGroup) — so the
        //     limit depends on whether there's an attachment.
        //     Discord: content is always capped at 2000 characters,
        //     regardless of attachments.
        //     The counter under each field shows these numbers live;
        //     separately, CheckCharLimits below blocks the actual send to
        //     any platform whose text exceeds its limit.
        // =====================================================================
        private const int TelegramTextLimit = 4096;
        private const int TelegramCaptionLimit = 1024;
        private const int DiscordContentLimit = 2000;

        // UA: Перевірка лімітів ПЕРЕД відправкою — на відміну від лічильника
        //     (який лише показує числа), перевищення тут знімає платформу зі
        //     списку адресатів. Якщо лишається хоча б одна платформа без
        //     перевищення, користувач явно підтверджує часткову відправку
        //     лише туди; якщо валідної платформи не лишається — відправка
        //     скасовується повністю, без варіанту "відправити частково".
        // EN: A limit check BEFORE sending — unlike the counter (which only
        //     displays numbers), exceeding the limit here removes that
        //     platform from the recipient list. If at least one platform
        //     without an overage remains, the user explicitly confirms a
        //     partial send to just that platform; if no valid platform
        //     remains, the send is cancelled entirely, with no "send
        //     partially" option.
        private (bool SendTelegram, bool SendDiscord) CheckCharLimits(
            string telegramText, string discordText, bool hasImages, bool wantTelegram, bool wantDiscord)
        {
            var telegramLimit = hasImages ? TelegramCaptionLimit : TelegramTextLimit;
            var telegramOver = wantTelegram && telegramText.Length > telegramLimit;
            var discordOver = wantDiscord && discordText.Length > DiscordContentLimit;

            if (!telegramOver && !discordOver)
                return (wantTelegram, wantDiscord);

            var lines = new List<string>();
            if (telegramOver)
                lines.Add(string.Format(_loc.Get("send.raw.limit.line"), "Telegram", telegramText.Length, telegramLimit));
            if (discordOver)
                lines.Add(string.Format(_loc.Get("send.raw.limit.line"), "Discord", discordText.Length, DiscordContentLimit));
            var body = string.Join("\n", lines);

            var telegramViable = wantTelegram && !telegramOver;
            var discordViable = wantDiscord && !discordOver;

            if (!telegramViable && !discordViable)
            {
                System.Windows.MessageBox.Show(
                    body + "\n\n" + _loc.Get("send.raw.limit.blocked_suffix"),
                    _loc.Get("app.name"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return (false, false);
            }

            var viableName = telegramViable ? "Telegram" : "Discord";
            var exceededName = telegramOver ? "Telegram" : "Discord";
            var confirmed = System.Windows.MessageBox.Show(
                body + "\n\n" + string.Format(_loc.Get("send.raw.limit.confirm_partial"), viableName, exceededName),
                _loc.Get("app.name"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

            return confirmed ? (telegramViable, discordViable) : (false, false);
        }

        private void RawTelegramInput_TextChanged(object sender, TextChangedEventArgs e)
            => UpdateCharCounters();

        private void RawDiscordInput_TextChanged(object sender, TextChangedEventArgs e)
            => UpdateCharCounters();

        // =====================================================================
        // UA: Панель форматування — кнопка застосовує код до ВИДІЛЕНОГО
        //     тексту, або, якщо нічого не виділено, вставляє пару
        //     тегів/символів на місці курсора й ставить курсор МІЖ ними, щоб
        //     одразу можна було друкувати. Жодної магії з парсингом —
        //     користувач і далі бачить і редагує сирий текст з тегами, так
        //     само, як і раніше під час ручного набору.
        // EN: A formatting toolbar — a button applies the markup to the
        //     SELECTED text, or, if nothing is selected, inserts the pair
        //     of tags/symbols at the caret and places the caret BETWEEN
        //     them so typing can continue right away. No parsing magic —
        //     the user still sees and edits the raw
        //     tagged text, exactly as when typing it by hand before.
        // =====================================================================
        private void FormatTelegram_Click(object sender, RoutedEventArgs e)
        {
            var (prefix, suffix) = TelegramFormatFor((sender as Button)?.Tag as string);
            if (prefix.Length > 0) ApplyFormatting(RawTelegramInput, prefix, suffix);
        }

        private void FormatDiscord_Click(object sender, RoutedEventArgs e)
        {
            var (prefix, suffix) = DiscordFormatFor((sender as Button)?.Tag as string);
            if (prefix.Length > 0) ApplyFormatting(RawDiscordInput, prefix, suffix);
        }

        private static (string Prefix, string Suffix) TelegramFormatFor(string? tag) => tag switch
        {
            "b" => ("<b>", "</b>"),
            "i" => ("<i>", "</i>"),
            "u" => ("<u>", "</u>"),
            "s" => ("<s>", "</s>"),
            "code" => ("<code>", "</code>"),
            "spoiler" => ("<tg-spoiler>", "</tg-spoiler>"),
            // UA: URL — заготовка для ручного редагування, як і решта кнопок:
            //     жодного діалогу вводу, користувач сам дописує адресу замість
            //     буквального "URL" у вставленому тексті
            // EN: URL — a placeholder for manual editing, same as the other
            //     buttons: no input dialog, the user replaces the literal
            //     "URL" in the inserted text with the real address
            "link" => ("<a href=\"URL\">", "</a>"),
            _ => (string.Empty, string.Empty)
        };

        private static (string Prefix, string Suffix) DiscordFormatFor(string? tag) => tag switch
        {
            "b" => ("**", "**"),
            "i" => ("*", "*"),
            "u" => ("__", "__"),
            "s" => ("~~", "~~"),
            "code" => ("`", "`"),
            "spoiler" => ("||", "||"),
            "link" => ("[", "](URL)"),
            _ => (string.Empty, string.Empty)
        };

        // UA: Текст змінюється напряму (а не через SelectedText), щоб точно
        //     контролювати, де опиниться курсор після зміни — WPF не
        //     гарантує зручну позицію курсора після запису в SelectedText
        // EN: The text is changed directly (not via SelectedText) to fully
        //     control where the caret ends up afterward — WPF doesn't
        //     guarantee a convenient caret position after writing to
        //     SelectedText
        private static void ApplyFormatting(TextBox target, string prefix, string suffix)
        {
            var start = target.SelectionStart;
            var length = target.SelectionLength;
            var text = target.Text;
            var selected = text.Substring(start, length);

            target.Text = text[..start] + prefix + selected + suffix + text[(start + length)..];

            if (length == 0)
            {
                // UA: Нічого не виділено — курсор стає рівно між тегами
                // EN: Nothing selected — the caret lands exactly between the tags
                target.SelectionStart = start + prefix.Length;
                target.SelectionLength = 0;
            }
            else
            {
                // UA: Було виділено текст — виділення лишається (тепер
                //     разом з тегами видно весь результат одразу)
                // EN: Text was selected — keep it selected (the whole result,
                //     tags included, is now visible at once)
                target.SelectionStart = start + prefix.Length;
                target.SelectionLength = length;
            }

            target.Focus();
        }

        private void UpdateCharCounters()
        {
            // UA: Обробники підписуються ще під час InitializeComponent, коли
            //     ці елементи ще не створені
            // EN: Handlers get wired up during InitializeComponent, while
            //     these elements don't exist yet
            if (TxtTelegramCharCount == null || TxtDiscordCharCount == null) return;

            var hasImages = _attachedImagePaths.Count > 0;
            var telegramLimit = hasImages ? TelegramCaptionLimit : TelegramTextLimit;

            SetCharCount(TxtTelegramCharCount, RawTelegramInput.Text.Length, telegramLimit);
            SetCharCount(TxtDiscordCharCount, RawDiscordInput.Text.Length, DiscordContentLimit);
        }

        // UA: Червоний (не жовтий) навмисно — перевищення тут більше не
        //     просто попередження, а реальна причина, через яку CheckCharLimits
        //     заблокує відправку на цю платформу
        // EN: Red (not yellow) on purpose — exceeding the limit here is no
        //     longer just a warning, it's the real reason CheckCharLimits
        //     will block sending to this platform
        private static void SetCharCount(TextBlock target, int length, int limit)
        {
            target.Text = $"{length} / {limit}";
            target.Foreground = length > limit
                ? Brush("#E0605A")
                : Brush("#B8A8D0");
        }

        // UA: Розмір файлу для м'якого попередження — файл міг зникнути з
        //     диска між вибором і перебудовою галереї (той самий захист, що
        //     й у catch вище), тому null замість винятку
        // EN: File size for the soft warning — the file may have vanished
        //     from disk between selection and gallery rebuild (the same
        //     guard as the catch above), so null instead of an exception
        private static long? TryGetFileSize(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return null; }
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
        //     людина отримувала б модалку щоразу під час відкриття вікна без
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

        // UA: Нормалізація тексту для полів, що йдуть у фактичне повідомлення
        // EN: Text normalization for fields that become the actual message
        private static readonly char[] FilteredChars = { 'ы', 'Ы', 'ъ', 'Ъ', 'э', 'Э', 'ё', 'Ё' };

        private static string FilterText(string? text) =>
            string.IsNullOrEmpty(text)
                ? string.Empty
                : new string(text.Where(c => Array.IndexOf(FilteredChars, c) < 0).ToArray());

        private static void FilterTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (e.Text.IndexOfAny(FilteredChars) >= 0)
                e.Handled = true;
        }

        // UA: System.Windows.* кваліфіковано явно — той самий конфлікт
        //     WPF/WinForms, що й з DragDropEffects/Orientation вище (CS0104)
        // EN: System.Windows.* qualified explicitly — the same WPF/WinForms
        //     conflict as DragDropEffects/Orientation above (CS0104)
        private static void FilterPastedText(object sender, System.Windows.DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(typeof(string))) return;
            var pasted = (string)e.DataObject.GetData(typeof(string));
            e.DataObject = new System.Windows.DataObject(FilterText(pasted));
        }

        private static void ApplyTextFilter(TextBox box)
        {
            box.PreviewTextInput += FilterTextInput;
            System.Windows.DataObject.AddPastingHandler(box, FilterPastedText);
        }

        // UA: Повторна спроба без перезапуску програми — той самий шлях, що й
        //     під час відкриття вікна, тож окремої логіки не потрібно
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
            TitleInput.Text = FilterText(entry.Title);

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

            var title = FilterText(TitleInput.Text.Trim());
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

            var webhookUrl = SelectedWebhookUrl();
            var signature = ComputeSignature("template", title, type, scheduled, url, webhookUrl);
            var (sendTelegram, sendDiscord) = ResolveResendScope(
                signature, _settings.UseTelegram, _settings.UseDiscord);

            // UA: Користувач відмовився повторно надсилати туди, де вже й так
            //     підтверджено успіх, і нічого іншого надсилати не було
            //     заплановано — немає що робити
            // EN: The person declined to resend where success was already
            //     confirmed, and nothing else was scheduled to send — nothing
            //     left to do
            if (!sendTelegram && !sendDiscord) return;

            PanelSendStatus.Visibility = Visibility.Collapsed;
            BtnSend.IsEnabled = false;
            try
            {
                var result = await _dispatcher.SendAsync(video, webhookUrl, sendTelegram, sendDiscord);
                result = MergeSkippedPlatforms(signature, result);
                _lastSendResult = result;
                _lastSendSignature = signature;
                ShowSendResult(result);

                // UA: Вікно закривається лише якщо дійсно все, що мало піти,
                //     пішло — часткова невдача мусить лишатись на екрані,
                //     щоб людина це побачила, а не дізналась про це вже з
                //     Telegram/Discord
                // EN: Only close the window if everything that was supposed
                //     to go out actually went out — a partial failure must
                //     stay on screen so the person sees it here, not find
                //     out from Telegram/Discord afterward
                if (result.AllAttemptsSucceeded)
                    Close();
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
            var telegramText = _settings.UseTelegram ? FilterText(RawTelegramInput.Text.Trim()) : string.Empty;
            var discordText = _settings.UseDiscord ? FilterText(RawDiscordInput.Text.Trim()) : string.Empty;

            if (string.IsNullOrWhiteSpace(telegramText) && string.IsNullOrWhiteSpace(discordText))
            {
                System.Windows.MessageBox.Show(
                    _loc.Get("send.raw.validation.empty"), _loc.Get("app.name"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var wantTelegram = !string.IsNullOrWhiteSpace(telegramText);
            var wantDiscord = !string.IsNullOrWhiteSpace(discordText);

            (wantTelegram, wantDiscord) = CheckCharLimits(
                telegramText, discordText, _attachedImagePaths.Count > 0, wantTelegram, wantDiscord);
            if (!wantTelegram && !wantDiscord) return;

            var webhookUrl = SelectedWebhookUrl();
            var signature = ComputeSignature("raw", telegramText, discordText, webhookUrl, _attachedImagePaths);
            var (sendTelegram, sendDiscord) = ResolveResendScope(signature, wantTelegram, wantDiscord);

            if (!sendTelegram && !sendDiscord) return;

            PanelSendStatus.Visibility = Visibility.Collapsed;
            BtnSend.IsEnabled = false;
            try
            {
                var result = await _dispatcher.SendRawAsync(
                    telegramText, discordText, webhookUrl, _attachedImagePaths, sendTelegram, sendDiscord);
                result = MergeSkippedPlatforms(signature, result);
                _lastSendResult = result;
                _lastSendSignature = signature;
                ShowSendResult(result);

                if (result.AllAttemptsSucceeded)
                    Close();
            }
            finally
            {
                BtnSend.IsEnabled = true;
            }
        }

        // =====================================================================
        // UA: Захист від дублів + реальний статус відправки (замість одного
        //     спільного MessageBox "Відправлено"/"Помилка", який ховав, що
        //     саме сталось з КОЖНОЮ платформою окремо — без цього немає
        //     способу дізнатись, чи варто натискати "Надіслати" ще раз).
        // EN: Duplicate-send protection + a real send status (instead of one
        //     shared "Sent"/"Error" MessageBox, which hid what actually
        //     happened to EACH platform separately — without this there's
        //     no way to tell whether pressing "Send" again is even needed).
        // =====================================================================

        // UA: "Відбиток" вмісту конкретної спроби відправки — не криптографічний
        //     хеш, просто конкатенація значень через розділювач, що не
        //     зустрічається в тексті (U+0001). Призначення єдине: порівняти
        //     "це та сама спроба чи інша" між двома натисканнями "Надіслати" в
        //     межах ОДНОГО відкритого вікна. IEnumerable<string> (шляхи
        //     зображень) розгортається в рядок через кому, решта — звичайний
        //     ToString().
        // EN: A content "fingerprint" for one send attempt — not a
        //     cryptographic hash, just values joined by a separator that
        //     won't appear in real text (U+0001). Single purpose: tell
        //     "the same attempt or a different one" apart between two
        //     "Send" clicks within ONE open window. IEnumerable<string>
        //     (image paths) is flattened to a comma-joined string, everything
        //     else uses plain ToString().
        private static string ComputeSignature(params object?[] parts) =>
            string.Join("\u0001", parts.Select(FormatSignaturePart));

        private static string FormatSignaturePart(object? part) => part switch
        {
            null => string.Empty,
            IEnumerable<string> list => string.Join(",", list),
            _ => part.ToString() ?? string.Empty
        };

        // UA: Вирішує, чи дійсно відправляти на кожну з платформ ПРИ ЦЬОМУ
        //     натисканні "Надіслати".
        //     - Якщо вміст змінився з минулого разу (інший "відбиток") —
        //       надсилається все, що просить людина: це нова спроба,
        //       жодних підстав щось пропускати.
        //     - Якщо вміст той самий і ЖОДНА платформа ще не підтвердила
        //       успіх — теж надсилається все: звичайний повтор після
        //       невдачі чи першої спроби.
        //     - Якщо вміст той самий і ХОЧ ОДНА платформа вже підтвердила
        //       успіх — запитується окреме підтвердження (саме та вимога, яку
        //       поставив користувач), перш ніж повторно надсилати туди.
        //       Відмова просто пропускає ці платформи, лишаючи решту (ще не
        //       відправлені чи ті, що впали минулого разу).
        // EN: Decides whether to actually send to each platform on THIS
        //     "Send" click.
        //     - If the content changed since last time (a different
        //       "fingerprint") — send to everything the person asked for:
        //       this is a new attempt, no reason to skip anything.
        //     - If the content is the same and NO platform has confirmed
        //       success yet — also send to everything: a normal retry after
        //       a failure or the first attempt.
        //     - If the content is the same and AT LEAST ONE platform already
        //       confirmed success — ask for a separate confirmation (exactly
        //       the requirement the user set) before sending there again.
        //       Declining simply skips those platforms, keeping the rest
        //       (not yet sent, or failed last time).
        private (bool SendTelegram, bool SendDiscord) ResolveResendScope(
            string signature, bool wantTelegram, bool wantDiscord)
        {
            if (_lastSendResult == null || _lastSendSignature != signature)
                return (wantTelegram, wantDiscord);

            var alreadyOkTelegram = wantTelegram && _lastSendResult.Telegram.Succeeded;
            var alreadyOkDiscord = wantDiscord && _lastSendResult.Discord.Succeeded;

            if (!alreadyOkTelegram && !alreadyOkDiscord)
                return (wantTelegram, wantDiscord);

            var confirmedPlatforms = new List<string>();
            if (alreadyOkTelegram) confirmedPlatforms.Add("Telegram");
            if (alreadyOkDiscord) confirmedPlatforms.Add("Discord");

            var resendAnyway = System.Windows.MessageBox.Show(
                string.Format(_loc.Get("send.status.resend_confirm"), string.Join(", ", confirmedPlatforms)),
                _loc.Get("app.name"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

            return resendAnyway
                ? (wantTelegram, wantDiscord)
                : (wantTelegram && !alreadyOkTelegram, wantDiscord && !alreadyOkDiscord);
        }

        // UA: Коли ResolveResendScope пропускає платформу (бо вона вже
        //     підтвердила успіх і людина відмовилась дублювати), дispatcher
        //     повертає для неї Attempted=false — адже на ЦЕЙ раз її справді
        //     не викликали. Без цього кроку збереження "успіху" з минулого
        //     разу загубилось би: _lastSendResult перезаписався б на
        //     Attempted=false/Succeeded=false, і вже за ТРЕТЬОЇ спроби захист
        //     від дублів вирішив би, що платформа ще нічого не отримувала, й
        //     мовчки надіслав би туди вдруге без жодного питання. Тут
        //     "довершальний" результат для пропущеної платформи береться з
        //     попереднього збереженого стану (той самий відбиток вмісту),
        //     а не з щойно отриманого.
        // EN: When ResolveResendScope skips a platform (because it already
        //     confirmed success and the person declined to duplicate), the
        //     dispatcher returns Attempted=false for it — it genuinely wasn't
        //     called this time. Without this step, last time's "success"
        //     would be lost: _lastSendResult would get overwritten with
        //     Attempted=false/Succeeded=false, and on a THIRD attempt the
        //     duplicate-send guard would conclude the platform never received
        //     anything yet and silently send there again with no question
        //     asked. Here, the "carried-forward" result for a skipped
        //     platform is taken from the previously stored state (same
        //     content fingerprint), not from the result just received.
        private SendResult MergeSkippedPlatforms(string signature, SendResult current)
        {
            if (_lastSendResult == null || _lastSendSignature != signature)
                return current;

            var telegram = (!current.Telegram.Attempted && _lastSendResult.Telegram.Succeeded)
                ? _lastSendResult.Telegram
                : current.Telegram;

            var discord = (!current.Discord.Attempted && _lastSendResult.Discord.Succeeded)
                ? _lastSendResult.Discord
                : current.Discord;

            return new SendResult { Telegram = telegram, Discord = discord };
        }

        // UA: Показує реальний результат по кожній платформі окремо. Рядок
        //     для платформи, яку не увімкнено в налаштуваннях, просто
        //     ховається — немає сенсу показувати "—" для того, чого й бути
        //     не могло
        // EN: Shows the real result for each platform separately. The line
        //     for a platform that isn't enabled in settings is simply
        //     hidden — no point showing "—" for something that couldn't
        //     have happened anyway
        private void ShowSendResult(SendResult result)
        {
            PanelSendStatus.Visibility = Visibility.Visible;

            TxtSendStatusTelegram.Visibility = _settings.UseTelegram ? Visibility.Visible : Visibility.Collapsed;
            TxtSendStatusDiscord.Visibility = _settings.UseDiscord ? Visibility.Visible : Visibility.Collapsed;

            SetStatusLine(TxtSendStatusTelegram, "Telegram", result.Telegram);
            SetStatusLine(TxtSendStatusDiscord, "Discord", result.Discord);
        }

        private void SetStatusLine(TextBlock target, string platformLabel, PlatformSendResult r)
        {
            if (!r.Attempted)
            {
                target.Text = $"{platformLabel}: {_loc.Get("send.status.skipped")}";
                target.Foreground = Brush("#B8A8D0");
                return;
            }

            if (r.Succeeded)
            {
                target.Text = $"{platformLabel}: {_loc.Get("send.status.ok")}";
                target.Foreground = Brush("#55C57A");
            }
            else
            {
                target.Text = $"{platformLabel}: {_loc.Get("send.status.failed")} — {r.Error?.Message}";
                target.Foreground = Brush("#E0605A");
            }
        }

        // UA: Перевірка з'єднання з увімкненими платформами — ДО фактичної
        //     відправки. Викликається і автоматично (з конструктора, при
        //     відкритті вікна), і вручну (кнопка 🔄) — тому зроблена
        //     ідемпотентною, без побічних ефектів поза цими двома рядками.
        //     На відміну від SendAsync/RunAsync, тут НІЧОГО не пишеться в
        //     logs/ — це лише діагностика досяжності платформи, а не спроба
        //     відправки, і лог з написом "не вдалось відправити" був би тут
        //     відвертою брехнею.
        // EN: Checks connectivity with the enabled platforms — BEFORE an
        //     actual send. Called both automatically (from the constructor,
        //     on window open) and manually (the 🔄 button) — kept
        //     idempotent, with no side effects beyond these two lines.
        //     Unlike SendAsync/RunAsync, NOTHING is written to logs/ here —
        //     this is only reachability diagnostics, not a send
        //     attempt, and a log entry saying "send failed" would be an
        //     outright lie here.
        private async Task RunConnectionCheckAsync()
        {
            TxtConnTelegram.Visibility = _settings.UseTelegram ? Visibility.Visible : Visibility.Collapsed;
            TxtConnDiscord.Visibility = _settings.UseDiscord ? Visibility.Visible : Visibility.Collapsed;

            if (!_settings.UseTelegram && !_settings.UseDiscord)
            {
                PanelConnectionCheck.Visibility = Visibility.Collapsed;
                return;
            }

            PanelConnectionCheck.Visibility = Visibility.Visible;

            if (_settings.UseTelegram)
            {
                TxtConnTelegram.Text = $"Telegram: {_loc.Get("send.conn.checking")}";
                TxtConnTelegram.Foreground = Brush("#B8A8D0");
            }

            if (_settings.UseDiscord)
            {
                TxtConnDiscord.Text = $"Discord: {_loc.Get("send.conn.checking")}";
                TxtConnDiscord.Foreground = Brush("#B8A8D0");
            }

            BtnRecheckConnection.IsEnabled = false;
            try
            {
                var result = await _dispatcher.CheckConnectionsAsync(SelectedWebhookUrl());
                _lastConnResult = result;
                _connChecked = true;

                SetConnectionLine(TxtConnTelegram, "Telegram", result.Telegram);
                SetConnectionLine(TxtConnDiscord, "Discord", result.Discord);
            }
            finally
            {
                BtnRecheckConnection.IsEnabled = true;
            }
        }

        private void SetConnectionLine(TextBlock target, string platformLabel, PlatformSendResult? r)
        {
            if (r == null) return;

            if (!r.Attempted)
            {
                target.Text = $"{platformLabel}: {_loc.Get("send.status.skipped")}";
                target.Foreground = Brush("#B8A8D0");
                return;
            }

            if (r.Succeeded)
            {
                target.Text = $"{platformLabel}: {_loc.Get("send.conn.ok")}";
                target.Foreground = Brush("#55C57A");
            }
            else
            {
                target.Text = $"{platformLabel}: {_loc.Get("send.conn.failed")} — {r.Error?.Message}";
                target.Foreground = Brush("#E0605A");
            }
        }

        private async void BtnRecheckConnection_Click(object sender, RoutedEventArgs e) =>
            await RunConnectionCheckAsync();

        protected override void OnClosed(EventArgs e)
        {
            _loc.LanguageChanged -= ApplyLocalization;
            base.OnClosed(e);
        }
    }
}