using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.IO;
using System.Xml.Linq;
using VirtualClipBoard.Properties;

namespace VirtualClipBoard
{
    public partial class VirtualClipBoard : Form
    {
        private const String VirtualClipBoardName = "VirtualClipBoard"; // название программы
        public String VirtualClipBoardTarget; // последний значение текстового БО
        public String VirtualClipBoardDat; // путь к файлу истории
        Dictionary<int, string> _virtualClipBoardHistory = new Dictionary<int, string>(); // История нашего буфера
        Dictionary<int, int> _virtualClipBoardIndexListBox; // список индексов в связки с ключами истории буфера

        // Подключение библиотек WIN
        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);
        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

        // Наш Form
        public VirtualClipBoard()
        {
            InitializeComponent();
            LoadConfigs();

            _nextClipboardViewer = SetClipboardViewer(Handle);

            ReloadTray(); // Обноавляемменю в трее
            ReloadListClipboard(); // Обновляем ListBox

            _notifyIcon.Text = VirtualClipBoardName;
            _notifyIcon.MouseDoubleClick += NotifyIconMouseDoubleClick;
        }

        // Перезагрузка элементов в ListBox
        private void ReloadListClipboard()
        {
            _virtualClipBoardIndexListBox = new Dictionary<int, int>();
            var listTargetItem = 0; // индекс текущего элемента в ListBox
            var freeSlotToTray = Settings.Default.history_size;

            list_clipboard.Items.Clear(); // Очищаем список
            var list = _virtualClipBoardHistory.OrderByDescending(x => x.Key);
            
            foreach (var item in list)
            {
                var stringNameIte = item.Value.Length > 150 ?
                    item.Value.Replace("\n", "\t").Replace("\r", "\t").Substring(0, 60) :
                    item.Value.Replace("\n", "\t").Replace("\r", "\t");
                
                list_clipboard.Items.Add(stringNameIte);
                _virtualClipBoardIndexListBox.Add(listTargetItem, item.Key);
                
                if (freeSlotToTray == 1) 
                    break;

                freeSlotToTray--;
                listTargetItem++; // Увеличиваем индекс текущего элемента в ListBox
            }
        }

        // Выбор элемента в ListBox
        private void ListClipboardSelectedIndexChanged(object sender, EventArgs e)
        {
            if (list_clipboard.SelectedIndex >= 0 )
                Clipboard.SetText(_virtualClipBoardHistory[_virtualClipBoardIndexListBox[list_clipboard.SelectedIndex]]);
        }

        // Перезагрузка элементов для трей
        private void ReloadTray()
        {
            var contextMenu = new ContextMenuStrip();
            ToolStripMenuItem menuItem;
            var freeSlotToTray = Settings.Default.size_tray;
            var list = _virtualClipBoardHistory.OrderByDescending(x => x.Key);
            
            foreach (var item in list)
            {
                menuItem = new ToolStripMenuItem
                {
                    Tag = item.Key,
                    Text =
                        item.Value.Length > 60
                            ? item.Value.Replace("\n", "\t").Replace("\r", "\t").Substring(0, 60)
                            : item.Value.Replace("\n", "\t").Replace("\r", "\t")
                };

                menuItem.Click += MenuItemClick;
                contextMenu.Items.Add(menuItem);
                
                if (freeSlotToTray == 1)
                    break;
                
                freeSlotToTray--;
            }

            // Разделитель
            contextMenu.Items.Add(new ToolStripSeparator());

            // Свернуть/Развернуть
            menuItem = new ToolStripMenuItem {Text = Resources.TrayMenu_Settings};
            menuItem.Click += MenuItemConfig;
            contextMenu.Items.Add(menuItem);

            // Выход из программы
            menuItem = new ToolStripMenuItem {Text = Resources.TrayMenu_Exit};
            menuItem.Click += ExitClick;
            contextMenu.Items.Add(menuItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        // Вызов окна настроек
        private void MenuItemConfig(object sender, EventArgs e)
        {
            // ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
        }

        // Событие по клику на элемент контекстного меню в трее
        private void MenuItemClick(object sender, EventArgs e)
        {
            // Console.WriteLine((int)(sender as ToolStripMenuItem).Tag);
            Clipboard.SetText(_virtualClipBoardHistory[(int)((ToolStripMenuItem)sender).Tag]);
        }

        // событие при клике мышкой по значку в трее
        private void NotifyIconMouseDoubleClick(object sender, MouseEventArgs e)
        {
            Console.WriteLine(WindowState);
            if (WindowState == FormWindowState.Normal || WindowState == FormWindowState.Maximized)
            {
                // ShowInTaskbar = false;
                Hide();
                WindowState = FormWindowState.Minimized;
            }
            else
            {
                // ShowInTaskbar = true;
                Show();
                WindowState = FormWindowState.Normal;
            }
        }

        // Установка путей к файлам конфигурации и истории
        private void LoadConfigs()
        {
            groupBox1.Text = Resources.TrayMenu_Settings;
            cbLanguage.Items.AddRange(new object[]
            {
                new CbItem {Text = Resources.Culture_Invariant, Value = ""},
                new CbItem {Text = Resources.Culture_Russian, Value = "ru-RU"}
            });
            if (!String.IsNullOrEmpty(Settings.Default.Culture))
               cbLanguage.SelectedIndex = 1;

            VirtualClipBoardDat = Application.UserAppDataPath + "\\history.dat";
            Console.WriteLine(Resources.Config_Files + VirtualClipBoardDat);
            history_size.Value = Settings.Default.history_size;
            Console.WriteLine(Resources.Config_LoadedHistorySize + Settings.Default.history_size);
            size_tray.Value = Settings.Default.size_tray;
            Console.WriteLine(Resources.Config_LoadedTrayElements + Settings.Default.size_tray);
            
            var reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run\", true);
            if (reg != null)
            {
                if (reg.GetValue(VirtualClipBoardName) != null)
                {
                    autoload.Checked = true;
                    Console.WriteLine(Resources.Config_AutoLoad);
                }
                reg.Close();
            }

            // Загружаем историю из файла
            var xmlString = "";
            xmlString += @"<items>";
            if (File.Exists(VirtualClipBoardDat))
            {
                var stream = new StreamReader(VirtualClipBoardDat);
                while (stream.Peek() > -1)
                {
                    xmlString += stream.ReadLine() + "\n";
                }
                stream.Close();
                xmlString += @"</items>";
                var indexNewHistory = 2;
                var doc = XDocument.Parse(xmlString);
                var element = doc.Element("items");
                if (element != null)
                {
                    var items = element.Elements("item");
                    foreach (var item in items)
                    {
                        _virtualClipBoardHistory.Add(indexNewHistory, item.Value);
                        indexNewHistory++; // увеличиваем индекс новому элементу
                    }
                }
            }
            // Чистим историю буфера
            if (_virtualClipBoardHistory.Count() > Settings.Default.history_size)
            {
                var clearItemsCount = _virtualClipBoardHistory.Count() - Settings.Default.history_size;
                var list = _virtualClipBoardHistory.Keys.ToList();
                list.Sort();
                foreach (var key in list)
                {
                    _virtualClipBoardHistory.Remove(key);
                    if (clearItemsCount == 1)
                        break;

                    clearItemsCount--;
                }
            }
            // Обновляем файл истории
            var writer = new StreamWriter(VirtualClipBoardDat, false, System.Text.Encoding.UTF8);
            var newList = _virtualClipBoardHistory.Keys.ToList();
            newList.Sort();
            foreach (var key in newList)
            {
                writer.WriteLine(@"<item>" + _virtualClipBoardHistory[key].Replace(@"<", @"&lt;").Replace(@">", @"&gt;") + @"</item>");
            }
            writer.Close();
            // Если элементов ноль, добавляем из буфера
            Console.WriteLine(_virtualClipBoardHistory.Count());
            if (!_virtualClipBoardHistory.Any())
            {
                VirtualClipBoardTarget = Clipboard.GetText();
                _virtualClipBoardHistory.Add(1, VirtualClipBoardTarget);
            }
            VirtualClipBoardTarget = _virtualClipBoardHistory.Last().Value;
        }

        // Событие изменения статуса флажка автозагрузки
        // Если флажок - прописываем в реестр на автозагрузку
        private void AutoloadCheckedChanged(object sender, EventArgs e)
        {
            const string regKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run\";

            var reg = Registry.LocalMachine.OpenSubKey(regKey, true);
            if (reg == null)
            {
                Console.WriteLine( Resources.AutoloadRegistryAccess, regKey);
                return;
            }

            if (reg.GetValue(VirtualClipBoardName) != null)
            {
                try
                {
                    reg.DeleteValue(VirtualClipBoardName);
                    Console.WriteLine(Resources.Info_AutoloadOff, VirtualClipBoardName);
                }
                catch
                {
                    Console.WriteLine(Resources.Info_AutoloadCheckedError, VirtualClipBoardName);
                }
            }
            
            if (autoload.Checked)
            {
                reg.SetValue(VirtualClipBoardName, Application.ExecutablePath);
                Console.WriteLine(Resources.Info_AutoloadOn, VirtualClipBoardName);
            }
            reg.Close();
        }

        // Завершение работы программы по закрытию через кнопку
        private void ExitClick(object sender, EventArgs e)
        {
            Application.Exit();
        }

        // изменение размера истории
        private void HistorySizeValueChanged(object sender, EventArgs e)
        {
            Settings.Default.history_size = (int)history_size.Value;
            Settings.Default.Save();
            Console.WriteLine(Resources.Info_HistorySizeChanged, Settings.Default.history_size);
            ReloadListClipboard(); // Обновляем ListBox
        }

        // изменение количества записей БО в трее
        private void SizeTrayValueChanged(object sender, EventArgs e)
        {
            Settings.Default.size_tray = (int)size_tray.Value;
            Settings.Default.Save();
            Console.WriteLine(Resources.Info_TraySizeChanged, Settings.Default.size_tray);
            ReloadTray(); // Обновляем Трей
        }

        // Реагируем на обновление буфераобмена
        private void ClipboardChanged()
        {
            if (!Clipboard.ContainsText() || Clipboard.GetText().Length <= 0 || VirtualClipBoardTarget == Clipboard.GetText())
                return;

            VirtualClipBoardTarget = Clipboard.GetText();

            // Записываем новый элемент в словарь
            _virtualClipBoardHistory.Add((_virtualClipBoardHistory.Last().Key + 1), VirtualClipBoardTarget);

            ReloadTray(); // Обноавляемменю в трее
            ReloadListClipboard(); // Обновляем ListBox

            // Отчистка словаря от лишних элементов
            if (_virtualClipBoardHistory.Count() > Settings.Default.history_size)
            {
                var clearItemsCount = _virtualClipBoardHistory.Count() - Settings.Default.history_size;
                var list = _virtualClipBoardHistory.Keys.ToList();
                list.Sort();
                foreach (var key in list)
                {
                    _virtualClipBoardHistory.Remove(key);
                    if (clearItemsCount == 1) break;
                    clearItemsCount--;
                }
            }

            // Записываем новый элемент в файл истории
            var writer = new StreamWriter(VirtualClipBoardDat, true, Encoding.UTF8);
            writer.WriteLine(@"<item>" + VirtualClipBoardTarget.Replace(@"<", @"&lt;").Replace(@">", @"&gt;") + @"</item>");
            writer.Close();
            Console.WriteLine(Resources.Info_HistoryAdded, VirtualClipBoardTarget);
        }

        // Затираем всю историю
        private void ClearClick(object sender, EventArgs e)
        {
            var writer = new StreamWriter(VirtualClipBoardDat, false, Encoding.Default);
            writer.Write("");
            writer.Close();

            _virtualClipBoardHistory = new Dictionary<int, string>();

            VirtualClipBoardTarget = Clipboard.GetText();
            _virtualClipBoardHistory.Add(1, VirtualClipBoardTarget);

            ReloadTray(); // Обноавляемменю в трее
            ReloadListClipboard(); // Обновляем ListBox
        }

        // Сворачивать в трей вместо закрытия программы
        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            //ShowInTaskbar = false;
            Hide();
            WindowState = FormWindowState.Minimized;
        }

        // дескриптор окна
        private IntPtr _nextClipboardViewer;

        // Константы
        public const int WM_DRAWCLIPBOARD = 0x308;
        public const int WM_CHANGECBCHAIN = 0x030D;

        // Метод для реагирование на изменение вбуфере обмена и т.д.
        protected override void WndProc(ref Message m)
        {
            // Console.WriteLine("WndProc");
            switch (m.Msg)
            {
                case WM_DRAWCLIPBOARD:
                    ClipboardChanged();
                    //Console.WriteLine("WM_DRAWCLIPBOARD ClipboardChanged();");
                    SendMessage(_nextClipboardViewer, WM_DRAWCLIPBOARD, m.WParam, m.LParam);
                    break;
                case WM_CHANGECBCHAIN:
                    if (m.WParam == _nextClipboardViewer)
                        _nextClipboardViewer = m.LParam;
                    else
                        SendMessage(_nextClipboardViewer, WM_CHANGECBCHAIN, m.WParam, m.LParam);
                    m.Result = IntPtr.Zero;
                    break;
                default:
                    base.WndProc(ref m);
                    break;
            }
        }

        private void cbLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbLanguage.SelectedIndex < 0) return;

            var item = (CbItem)cbLanguage.Items[cbLanguage.SelectedIndex];
            if (item.Value.Equals(Settings.Default.Culture)) return;

            Settings.Default.Culture = item.Value;
            Settings.Default.Save();
            MessageBox.Show(Resources.CultureChanged_Text, Resources.CultureChanged_Caption, MessageBoxButtons.OK);
        }
    }

    class CbItem
    {
        public string Text { get; set; }
        public string Value { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }
}
