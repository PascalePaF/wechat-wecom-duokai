using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace WechatDuokai.Presentation
{
    internal static class ThemedDialogService
    {
        internal static void Show(Window owner, string title, string message, bool warning = false)
        {
            var dialog = new ThemedDialogWindow(title, message, false, warning);
            SetOwner(dialog, owner);
            dialog.ShowDialog();
        }

        internal static bool Confirm(Window owner, string title, string message,
            string confirmText = "确认继续", bool warning = false)
        {
            var dialog = new ThemedDialogWindow(title, message, true, warning, confirmText);
            SetOwner(dialog, owner);
            return dialog.ShowDialog() == true;
        }

        internal static void SetOwner(Window dialog, Window owner)
        {
            if (owner != null && owner.IsLoaded)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }
    }

    internal sealed class ThemedDialogWindow : Window
    {
        internal ThemedDialogWindow(string title, string message, bool confirmation,
            bool warning, string confirmText = "确定")
        {
            Title = title;
            Width = 490;
            MinWidth = 440;
            MinHeight = 240;
            MaxHeight = SystemParameters.WorkArea.Height * 0.82;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ShowInTaskbar = false;
            FontFamily = (FontFamily)Application.Current.FindResource("AppFont");
            UseLayoutRounding = true;
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            SourceInitialized += (sender, args) => WindowChromeHelper.Apply(this);

            var root = new Grid();
            root.SetResourceReference(Panel.BackgroundProperty, "WindowBackgroundBrush");
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(82) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(76) });

            var header = new Border { Padding = new Thickness(24, 16, 24, 14) };
            header.SetResourceReference(Border.BackgroundProperty, "WindowTopBrush");
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var glyph = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(12) };
            glyph.SetResourceReference(Border.BackgroundProperty,
                warning ? "DangerSurfaceBrush" : "SelectionBrush");
            var glyphText = new TextBlock
            {
                Text = warning ? "!" : "i",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            glyphText.SetResourceReference(TextBlock.ForegroundProperty,
                warning ? "DangerBrush" : "AccentBrush");
            glyph.Child = glyphText;
            headerGrid.Children.Add(glyph);
            var titleStack = new StackPanel { Margin = new Thickness(13, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var titleText = new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.SemiBold };
            var subtitle = new TextBlock
            {
                Text = warning ? "请确认后继续" : "微窗助手",
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0)
            };
            subtitle.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush");
            titleStack.Children.Add(titleText);
            titleStack.Children.Add(subtitle);
            Grid.SetColumn(titleStack, 1);
            headerGrid.Children.Add(titleStack);
            header.Child = headerGrid;
            root.Children.Add(header);

            var bodyCard = new Border
            {
                Margin = new Thickness(24, 18, 24, 14),
                Padding = new Thickness(18),
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1)
            };
            bodyCard.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
            bodyCard.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            var bodyScroll = new ScrollViewer
            {
                MaxHeight = 360,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            bodyScroll.Resources.Add(typeof(ScrollBar),
                Application.Current.FindResource("PremiumVerticalScrollBarStyle"));
            var body = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.WrapWithOverflow,
                FontSize = 12.5,
                LineHeight = 23
            };
            body.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            bodyScroll.Content = body;
            bodyCard.Child = bodyScroll;
            Grid.SetRow(bodyCard, 1);
            root.Children.Add(bodyCard);

            var footer = new Border { Padding = new Thickness(24, 14, 24, 16), BorderThickness = new Thickness(0, 1, 0, 0) };
            footer.SetResourceReference(Border.BackgroundProperty, "WindowTopBrush");
            footer.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            if (confirmation)
            {
                var cancel = MakeButton("取消", "SecondaryButtonStyle", 96);
                cancel.IsDefault = true;
                cancel.IsCancel = true;
                cancel.Click += (sender, args) => DialogResult = false;
                buttons.Children.Add(cancel);
            }
            var accept = MakeButton(confirmText, warning && confirmation ? "DangerButtonStyle" : "PrimaryButtonStyle", 112);
            accept.Margin = new Thickness(10, 0, 0, 0);
            accept.IsDefault = !confirmation;
            accept.Click += (sender, args) => DialogResult = true;
            buttons.Children.Add(accept);
            footer.Child = buttons;
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
            Content = root;
        }

        private static Button MakeButton(string label, string styleName, double width)
        {
            return new Button
            {
                Content = label,
                Width = width,
                Height = 38,
                Style = (Style)Application.Current.FindResource(styleName)
            };
        }
    }

    internal enum PickerMode
    {
        File,
        Folder
    }

    internal sealed class ThemedPathPicker : Window
    {
        private const int PageSize = 6;
        private readonly PickerMode _mode;
        private readonly HashSet<string> _allowedFiles;
        private readonly TextBox _pathBox;
        private readonly TextBox _folderNameBox;
        private readonly StackPanel _entriesPanel;
        private readonly TextBlock _status;
        private readonly TextBlock _pageText;
        private readonly Button _previousPage;
        private readonly Button _nextPage;
        private readonly Button _selectButton;
        private List<PickerEntry> _entries = new List<PickerEntry>();
        private string _currentDirectory;
        private string _selectedFile;
        private int _page;

        internal string SelectedPath { get; private set; }

        internal static string SelectFile(Window owner, string title,
            string initialDirectory, params string[] allowedFiles)
        {
            var picker = new ThemedPathPicker(PickerMode.File, title, initialDirectory, allowedFiles);
            ThemedDialogService.SetOwner(picker, owner);
            return picker.ShowDialog() == true ? picker.SelectedPath : null;
        }

        internal static string SelectFolder(Window owner, string title, string initialDirectory)
        {
            var picker = new ThemedPathPicker(PickerMode.Folder, title, initialDirectory, null);
            ThemedDialogService.SetOwner(picker, owner);
            return picker.ShowDialog() == true ? picker.SelectedPath : null;
        }

        internal ThemedPathPicker(PickerMode mode, string title, string initialDirectory,
            IEnumerable<string> allowedFiles)
        {
            _mode = mode;
            _allowedFiles = new HashSet<string>(allowedFiles ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            Title = title;
            Width = 660;
            Height = 580;
            MinWidth = 600;
            MinHeight = 540;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            FontFamily = (FontFamily)Application.Current.FindResource("AppFont");
            UseLayoutRounding = true;
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            SourceInitialized += (sender, args) => WindowChromeHelper.Apply(this);

            var root = new Grid();
            root.SetResourceReference(Panel.BackgroundProperty, "WindowBackgroundBrush");
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(78) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(mode == PickerMode.Folder ? 104 : 74) });

            var header = new Border { Padding = new Thickness(24, 14, 24, 14) };
            header.SetResourceReference(Border.BackgroundProperty, "WindowTopBrush");
            var heading = new StackPanel();
            heading.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.SemiBold });
            var help = new TextBlock
            {
                Text = mode == PickerMode.Folder
                    ? "选择专用文件夹；也可在当前目录新建文件夹"
                    : "只显示可选的官方程序文件，选定后仍会验证腾讯数字签名",
                FontSize = 11,
                Margin = new Thickness(0, 5, 0, 0)
            };
            help.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush");
            heading.Children.Add(help);
            header.Child = heading;
            root.Children.Add(header);

            var navigation = new Grid { Margin = new Thickness(24, 11, 24, 7) };
            navigation.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            navigation.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            navigation.ColumnDefinitions.Add(new ColumnDefinition());
            var drives = MakeButton("磁盘", 62);
            drives.Click += (sender, args) => Navigate(null);
            navigation.Children.Add(drives);
            var up = MakeButton("上一级", 64);
            up.Click += (sender, args) => Navigate(_currentDirectory == null
                ? null : Directory.GetParent(_currentDirectory)?.FullName);
            Grid.SetColumn(up, 1);
            navigation.Children.Add(up);
            _pathBox = new TextBox
            {
                Height = 38,
                Padding = new Thickness(12, 8, 12, 6),
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 12,
                Style = (Style)Application.Current.FindResource("PremiumTextBoxStyle")
            };
            _pathBox.KeyDown += (sender, args) =>
            {
                if (args.Key == Key.Enter)
                {
                    NavigateTypedPath();
                    args.Handled = true;
                }
            };
            AutomationProperties.SetName(_pathBox, "当前文件夹路径；按回车进入");
            Grid.SetColumn(_pathBox, 2);
            navigation.Children.Add(_pathBox);
            Grid.SetRow(navigation, 1);
            root.Children.Add(navigation);

            var listCard = new Border
            {
                Margin = new Thickness(24, 4, 24, 14),
                Padding = new Thickness(10, 9, 10, 9),
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1)
            };
            listCard.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
            listCard.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            var listGrid = new Grid();
            listGrid.RowDefinitions.Add(new RowDefinition());
            listGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            _entriesPanel = new StackPanel();
            listGrid.Children.Add(_entriesPanel);
            var paging = new Grid { Margin = new Thickness(3, 4, 3, 0) };
            paging.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            paging.ColumnDefinitions.Add(new ColumnDefinition());
            paging.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            _previousPage = MakeButton("上一页", 74);
            _previousPage.Height = 28;
            _previousPage.Click += (sender, args) => { _page--; RenderPage(); };
            paging.Children.Add(_previousPage);
            _pageText = new TextBlock { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _pageText.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush");
            Grid.SetColumn(_pageText, 1);
            paging.Children.Add(_pageText);
            _nextPage = MakeButton("下一页", 74);
            _nextPage.Height = 28;
            _nextPage.Click += (sender, args) => { _page++; RenderPage(); };
            Grid.SetColumn(_nextPage, 2);
            paging.Children.Add(_nextPage);
            Grid.SetRow(paging, 1);
            listGrid.Children.Add(paging);
            listCard.Child = listGrid;
            Grid.SetRow(listCard, 2);
            root.Children.Add(listCard);

            var footer = new Border { Padding = new Thickness(24, 8, 24, 12), BorderThickness = new Thickness(0, 1, 0, 0) };
            footer.SetResourceReference(Border.BackgroundProperty, "WindowTopBrush");
            footer.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            var footerGrid = new Grid();
            if (mode == PickerMode.Folder)
            {
                footerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            }
            footerGrid.RowDefinitions.Add(new RowDefinition());
            _folderNameBox = new TextBox
            {
                Width = 180,
                Height = 30,
                Padding = new Thickness(8, 4, 8, 4),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "输入要创建的文件夹名称",
                Style = (Style)Application.Current.FindResource("PremiumTextBoxStyle")
            };
            AutomationProperties.SetName(_folderNameBox, "新文件夹名称");
            if (mode == PickerMode.Folder)
            {
                var createRow = new StackPanel { Orientation = Orientation.Horizontal };
                createRow.Children.Add(new TextBlock { Text = "新文件夹名", VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0), FontSize = 11 });
                createRow.Children.Add(_folderNameBox);
                var create = MakeButton("创建", 62);
                create.Height = 30;
                create.Margin = new Thickness(8, 0, 0, 0);
                create.Click += (sender, args) => CreateFolder();
                createRow.Children.Add(create);
                footerGrid.Children.Add(createRow);
            }
            var actionRow = new Grid();
            actionRow.ColumnDefinitions.Add(new ColumnDefinition());
            actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _status = new TextBlock
            {
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            _status.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush");
            actionRow.Children.Add(_status);
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            var cancel = MakeButton("取消", 82);
            cancel.IsCancel = true;
            cancel.Click += (sender, args) => DialogResult = false;
            actions.Children.Add(cancel);
            _selectButton = new Button
            {
                Content = mode == PickerMode.Folder ? "选择此文件夹" : "选择程序",
                Width = 120,
                Height = 38,
                Margin = new Thickness(10, 0, 0, 0),
                Style = (Style)Application.Current.FindResource("PrimaryButtonStyle")
            };
            _selectButton.Click += (sender, args) => AcceptSelection();
            actions.Children.Add(_selectButton);
            Grid.SetColumn(actions, 1);
            actionRow.Children.Add(actions);
            Grid.SetRow(actionRow, mode == PickerMode.Folder ? 1 : 0);
            footerGrid.Children.Add(actionRow);
            footer.Child = footerGrid;
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);
            Content = root;

            Navigate(FindExistingDirectory(initialDirectory));
        }

        private static Button MakeButton(string label, double width)
        {
            return new Button
            {
                Content = label,
                Width = width,
                Height = 36,
                Style = (Style)Application.Current.FindResource("SecondaryButtonStyle")
            };
        }

        private static string FindExistingDirectory(string path)
        {
            try
            {
                var directory = new DirectoryInfo(Path.GetFullPath(path));
                while (directory != null && !directory.Exists) directory = directory.Parent;
                return directory?.FullName;
            }
            catch (Exception)
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }
        }

        private void NavigateTypedPath()
        {
            var entered = _pathBox.Text.Trim().Trim('"');
            if (Directory.Exists(entered))
            {
                Navigate(entered);
            }
            else if (_mode == PickerMode.File && File.Exists(entered))
            {
                _selectedFile = Path.GetFullPath(entered);
                if (_allowedFiles.Contains(Path.GetFileName(_selectedFile)))
                {
                    SetStatus("已选中 " + Path.GetFileName(_selectedFile), false);
                    RenderPage();
                }
                else
                {
                    _selectedFile = null;
                    SetStatus("请选择允许的官方程序文件。", true);
                }
            }
            else
            {
                SetStatus("路径不存在；请进入已有文件夹，或在下方新建文件夹。", true);
            }
        }

        private void Navigate(string directory)
        {
            try
            {
                var nextDirectory = directory == null ? null : Path.GetFullPath(directory);
                var nextEntries = nextDirectory == null
                    ? DriveInfo.GetDrives().Select(drive => new PickerEntry(drive.Name, drive.Name, true)).ToList()
                    : Directory.EnumerateDirectories(nextDirectory)
                        .Select(path => new PickerEntry(Path.GetFileName(path), path, true))
                        .Concat(_mode == PickerMode.File
                            ? Directory.EnumerateFiles(nextDirectory)
                                .Where(path => _allowedFiles.Contains(Path.GetFileName(path)))
                                .Select(path => new PickerEntry(Path.GetFileName(path), path, false))
                            : Enumerable.Empty<PickerEntry>())
                        .OrderByDescending(entry => entry.IsDirectory)
                        .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList();
                _currentDirectory = nextDirectory;
                _entries = nextEntries;
                _selectedFile = null;
                _page = 0;
                _pathBox.Text = _currentDirectory ?? "此电脑 · 选择磁盘";
                SetStatus(_mode == PickerMode.Folder ? "选择当前位置或进入下级文件夹" : "选择官方程序文件", false);
                RenderPage();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                        ex is ArgumentException)
            {
                SetStatus("无法读取此位置：" + ex.Message, true);
            }
        }

        private void RenderPage()
        {
            _entriesPanel.Children.Clear();
            var pages = Math.Max(1, (_entries.Count + PageSize - 1) / PageSize);
            _page = Math.Max(0, Math.Min(_page, pages - 1));
            foreach (var entry in _entries.Skip(_page * PageSize).Take(PageSize))
            {
                var row = new Button
                {
                    Height = 38,
                    Margin = new Thickness(0, 0, 0, 3),
                    Padding = new Thickness(12, 0, 12, 0),
                    Tag = entry,
                    Style = (Style)Application.Current.FindResource("PickerRowButtonStyle"),
                    Content = (entry.IsDirectory ? "▸   " : "●   ") + entry.Name
                };
                if (string.Equals(_selectedFile, entry.Path, StringComparison.OrdinalIgnoreCase))
                    row.SetResourceReference(Button.BackgroundProperty, "SelectionBrush");
                AutomationProperties.SetName(row, (entry.IsDirectory ? "打开文件夹 " : "选择程序 ") + entry.Name);
                row.Click += (sender, args) =>
                {
                    if (entry.IsDirectory)
                    {
                        Navigate(entry.Path);
                    }
                    else
                    {
                        _selectedFile = entry.Path;
                        _pathBox.Text = entry.Path;
                        SetStatus("已选中 " + entry.Name, false);
                        RenderPage();
                    }
                };
                _entriesPanel.Children.Add(row);
            }
            if (_entries.Count == 0)
            {
                var empty = new TextBlock
                {
                    Text = _mode == PickerMode.File ? "这里没有符合条件的程序文件。" : "这里还没有子文件夹。",
                    Margin = new Thickness(12, 18, 0, 0), FontSize = 12
                };
                empty.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush");
                _entriesPanel.Children.Add(empty);
            }
            _pageText.Text = $"第 {_page + 1} / {pages} 页";
            _previousPage.IsEnabled = _page > 0;
            _nextPage.IsEnabled = _page + 1 < pages;
            _selectButton.IsEnabled = _mode == PickerMode.Folder ? _currentDirectory != null : _selectedFile != null;
        }

        private void CreateFolder()
        {
            if (_currentDirectory == null)
            {
                SetStatus("请先进入一个磁盘或文件夹。", true);
                return;
            }
            var name = _folderNameBox.Text.Trim();
            if (name.Length == 0 || name == "." || name == ".." ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.Contains("\\") || name.Contains("/"))
            {
                SetStatus("请输入有效的单层文件夹名称。", true);
                return;
            }
            try
            {
                var path = Path.Combine(_currentDirectory, name);
                if (Directory.Exists(path) || File.Exists(path))
                {
                    SetStatus("此名称已存在，请直接进入或换一个名称。", true);
                    return;
                }
                Directory.CreateDirectory(path);
                _folderNameBox.Text = string.Empty;
                Navigate(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                        ex is ArgumentException)
            {
                SetStatus("无法创建文件夹：" + ex.Message, true);
            }
        }

        private void AcceptSelection()
        {
            if (_mode == PickerMode.Folder)
            {
                if (_currentDirectory == null || !Directory.Exists(_currentDirectory))
                {
                    SetStatus("请先选择一个已有文件夹。", true);
                    return;
                }
                SelectedPath = _currentDirectory;
            }
            else
            {
                var path = _selectedFile ?? _pathBox.Text.Trim().Trim('"');
                if (!File.Exists(path) || !_allowedFiles.Contains(Path.GetFileName(path)))
                {
                    SetStatus("请选择列表中的官方程序文件。", true);
                    return;
                }
                SelectedPath = Path.GetFullPath(path);
            }
            DialogResult = true;
        }

        private void SetStatus(string message, bool error)
        {
            _status.Text = message;
            _status.SetResourceReference(TextBlock.ForegroundProperty,
                error ? "DangerBrush" : "TextTertiaryBrush");
        }

        private sealed class PickerEntry
        {
            internal PickerEntry(string name, string path, bool isDirectory)
            {
                Name = name;
                Path = path;
                IsDirectory = isDirectory;
            }

            internal string Name { get; }
            internal string Path { get; }
            internal bool IsDirectory { get; }
        }
    }
}
