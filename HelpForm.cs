using System;
using System.Drawing;
using System.Windows.Forms;

namespace MicVolumeGuard;

/// <summary>Окно справки: как пользоваться MicVolumeGuard.</summary>
internal sealed class HelpForm : Form
{
    public HelpForm()
    {
        Text = "MicVolumeGuard — справка";
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 420);

        // RichTextBox: корректно отображает абзацы и переносы строк (TextBox с ReadOnly
        // при некоторых DPI «сливает» \n), плюс WordWrap для длинных строк.
        var txt = new RichTextBox
        {
            ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = true,
            Text = HelpText
        };
        txt.TabStop = false; // чтобы не выделялся курсором при открытии
        txt.Cursor = Cursors.Default;

        var btnClose = new Button { Text = "Закрыть", DialogResult = DialogResult.Cancel, MinimumSize = new Size(100, 28) };
        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 40, ColumnCount = 2, RowCount = 1, Padding = new Padding(8, 6, 8, 6) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        bottom.Controls.Add(new Label(), 0, 0);
        bottom.Controls.Add(btnClose, 1, 0);
        btnClose.Click += (s, e) => Close();

        Controls.Add(txt);
        Controls.Add(bottom);
        CancelButton = btnClose;
    }

    private static string HelpText => """
        MICVOLUMEGUARD — СПРАВКА
        ══════════════════════════════════════════════════════════

        НАЗНАЧЕНИЕ
        Программа удерживает громкость микрофона на заданном уровне
        и мгновенно отменяет изменения, которые вносят другие программы
        (мессенджеры, игры, драйверы и т.д.).

        БЫСТРЫЙ СТАРТ
        1. В списке «Микрофон» выберите нужное устройство
           (или оставьте «По умолчанию (связь)» — его используют
           программы для звонков: Zoom, Discord, Teams…).
        2. Ползунком или числом задайте уровень громкости (0–100 %).
        3. Поставьте галочку «Блокировать уровень».
        4. Нажмите «Применить».
        Готово: иконка в трее покажет текущий процент.

        БЛОКИРОВКА УРОВНЯ
        Когда блокировка включена, любое изменение громкости извне
        откатывается мгновенно (через callback звуковой подсистемы
        Windows) и повторно проверяется каждые 1,5 секунды
        (watchdog — на случай, если уведомление было пропущено).
        Текущая иконка в трее:  синяя — блокировка включена,
        серая — выключена.

        БЫСТРОЕ ПЕРЕКЛЮЧЕНИЕ БЛОКИРОВКИ
        Правый клик по иконке в трее → пункт «Блокировка: вкл/выкл».
        Галочка у пункта = блокировка активна. Переключение действует
        сразу и сохраняется в настройках.

        УДЕРЖАНИЕ MUTE
        Дополнительно можно зафиксировать состояние «микрофон
        выключен/включён»: другие программы не смогут ни заглушить,
        ни включить микрофон. По умолчанию выключено.

        ЗАПУСК ВМЕСТЕ С WINDOWS
        Галочка регистрирует программу в реестре
        HKCU\Software\Microsoft\Windows\CurrentVersion\Run —
        запуск от имени текущего пользователя, без прав администратора.
        Снятие галочки убирает запись. Состояние галочки всегда
        отражает реальную запись в реестре.

        ЛОГИРОВАНИЕ (для диагностики)
        По умолчанию лог выключен. Включите «Вести журнал» и нажмите
        «Применить», затем повторите проблему — файл журнала:
        %APPDATA%\MicVolumeGuard\log.txt
        «Подробный» дополнительно пишет каждую установку громкости.
        Журнал автоматически урезается при превышении 512 КБ.

        ФАЙЛ НАСТРОЕК
        %APPDATA%\MicVolumeGuard\settings.json
        Можно отредактировать вручную (программа перечитывает файл
        при запуске).

        ИКОНКА ТРЕЯ
        • Левый клик — открыть настройки.
        • Правый клик — меню: настройки / блокировка / выход.
        • Число на иконке — текущая громкость в процентах.

        ЧАСТЫЕ ВОПРОСЫ
        В: Другая программа всё равно меняет громкость. Что делать?
        О: Проверьте, включена ли блокировка (синяя иконка). Если да —
           включите журнал и посмотрите log.txt: там видно, кто
           и когда изменил уровень.

        В: Программа не видит микрофон.
        О: Проверьте, что устройство записи включено в Windows
           (Параметры → Система → Звук → Ввод).

        В: Нужны ли права администратора?
        О: Нет. Программа работает от имени пользователя.
        """;
}
