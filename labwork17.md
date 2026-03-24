```markdown
Привет! Отлично, что хочешь нестандартный подход — это будет круто и выделится на фоне типичных лабораторных из Visual Studio с дизайнером. Я всё продумал на основе исследования (dotnet CLI + WinForms в VS Code, NuGet-пакеты для современного вида, обработка прав доступа в .NET 8+, работа с архивами и т.д.).

Мы сделаем полностью кодовый UI (без Form Designer), используем VS Code как основной «движок» разработки, добавим современную Material-тему (чтобы выглядело стильно, почти как современные приложения), а иконки возьмём через библиотеку + PNG из Google (как в задании, но с удобной загрузкой). Права доступа обработаем элегантно без лишних ошибок.

## 1. Почему этот подход нестандартный и правильный

- VS Code как движок: `dotnet new winforms` + расширение C#. Никакого тяжёлого Visual Studio. Всё в коде (`Dock`, `TableLayoutPanel`, `Anchor`).
- Современный вид: NuGet `MaterialSkin.2` — Material Design тема (тёмная, как VS Code).
- Иконки через библиотеку: основной набор PNG скачаем с <https://fonts.google.com/icons> (`folder`, `insert_drive_file`, `article`, `picture_as_pdf` и `default`). Загрузим в `ImageList`. Дополнительно можно подключить `Rop.Winforms.Icons.MaterialDesign` (или просто MaterialSkin), но базово хватит `ImageList` + `ExtractAssociatedIcon` для реальных системных иконок.
- Права доступа: `EnumerationOptions { IgnoreInaccessible = true }` — автоматически пропускает папки без прав (без `UnauthorizedAccessException`).
- Архивы: встроенный `System.IO.Compression` (`ZipFile`).
- Всё по заданию 5.1–5.5, но чище и современнее.

## 2. Настройка проекта в VS Code (5 минут)

1. Установи .NET 8 SDK (если нет).
2. В VS Code установи расширения: **C#** и **C# Dev Kit** от Microsoft.
3. Создай папку проекта → открой в VS Code.
4. В терминале выполни:  
   `dotnet new winforms -o FileManager --framework net8.0-windows`
5. `cd FileManager`
6. `dotnet add package MaterialSkin.2`
7. `dotnet restore`
8. Открой `FileManager.csproj` и убедись, что там `net8.0-windows`.

Готово! Теперь весь код будем писать вручную.

## 3. Структура UI (как расставить элементы управления)

В `Form1.cs` полностью перепишем `InitializeComponent` (или сделаем свой метод `SetupUI()`). Используем `TableLayoutPanel` для красивого размещения:

- Строка меню — `MenuStrip` (можно добавить пункты File/Вид, но по заданию хватит пустой или с Exit).
- Поле текущего каталога — `TextBox` + кнопка «Перейти» в `FlowLayoutPanel` (`Dock = Top`).
- Список файлов — `ListView` (`Dock = Fill`, `View = Details`, с колонками Иконка + Имя + Дата + Размер).
- Контекстное меню — `ContextMenuStrip` привязывается к `ListView`.

Пример кода (вставь в `public partial class Form1 : Form`):

```csharp
using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;
using MaterialSkin;
using MaterialSkin.Controls;

public partial class Form1 : MaterialForm  // ← MaterialSkin вместо обычного Form
{
    private string currentPath;
    private ListView listView;
    private TextBox pathTextBox;
    private ImageList imageList;
    private ContextMenuStrip contextMenu;

    public Form1()
    {
        InitializeMaterialTheme();  // Material Design
        SetupUI();
        currentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        pathTextBox.Text = currentPath;
        LoadDirectory(currentPath);
    }

    private void InitializeMaterialTheme()
    {
        var materialSkinManager = MaterialSkinManager.Instance;
        materialSkinManager.AddFormToManage(this);
        materialSkinManager.Theme = MaterialSkinManager.Themes.DARK;  // как VS Code
        materialSkinManager.ColorScheme = new ColorScheme(
            Primary.BlueGrey800, Primary.BlueGrey900,
            Primary.LightBlue500, Accent.LightBlue200, TextShade.WHITE);
    }

    private void SetupUI()
    {
        this.SuspendLayout();
        this.Text = "Файловый менеджер";
        this.Size = new Size(900, 600);
        this.MinimumSize = new Size(600, 400);

        // Меню
        var menu = new MenuStrip();
        this.MainMenuStrip = menu;
        this.Controls.Add(menu);

        // Панель пути (верх)
        var pathPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, AutoSize = true };
        pathTextBox = new TextBox { Width = 600, Text = currentPath };
        var btnGo = new MaterialButton { Text = "Перейти" };
        btnGo.Click += (s, e) => ChangeDirectory(pathTextBox.Text);
        pathTextBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) ChangeDirectory(pathTextBox.Text); };
        pathPanel.Controls.Add(pathTextBox);
        pathPanel.Controls.Add(btnGo);
        this.Controls.Add(pathPanel);

        // Список
        listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = true
        };
        listView.Columns.Add("Имя", 300);
        listView.Columns.Add("Дата изменения", 150);
        listView.Columns.Add("Размер", 100);

        // Иконки (библиотека ImageList + PNG)
        imageList = new ImageList { ImageSize = new Size(24, 24) };
        // Скачай PNG с Google Icons и положи в папку проекта (Copy to Output Directory = Copy always)
        imageList.Images.Add("folder", Image.FromFile("icons/folder.png"));
        imageList.Images.Add("file", Image.FromFile("icons/file.png"));
        imageList.Images.Add("doc", Image.FromFile("icons/doc.png"));
        imageList.Images.Add("pdf", Image.FromFile("icons/pdf.png"));
        imageList.Images.Add("default", Image.FromFile("icons/default.png"));
        listView.SmallImageList = imageList;

        listView.DoubleClick += ListView_DoubleClick;
        listView.ItemActivate += ListView_ItemActivate; // для Enter тоже

        // Контекстное меню
        contextMenu = new ContextMenuStrip();
        var copyItem = new ToolStripMenuItem("Копировать");
        copyItem.Click += Copy_Click;
        var pasteItem = new ToolStripMenuItem("Вставить");
        pasteItem.Click += Paste_Click;
        var compressItem = new ToolStripMenuItem("Сжать в архив");
        compressItem.Click += Compress_Click;
        var extractItem = new ToolStripMenuItem("Извлечь из архива");
        extractItem.Click += Extract_Click;

        contextMenu.Items.AddRange(new ToolStripItem[] { copyItem, pasteItem, new ToolStripSeparator(), compressItem, extractItem });
        contextMenu.Opening += ContextMenu_Opening; // динамически включаем/выключаем пункты
        listView.ContextMenuStrip = contextMenu;

        this.Controls.Add(listView);
        this.ResumeLayout();
    }
}
```

## 4. Ключевые методы (по заданию)

```csharp
private void LoadDirectory(string path)
{
    listView.Items.Clear();
    currentPath = path;
    pathTextBox.Text = path;

    // Специальный ".."
    var upItem = new ListViewItem("..") { ImageKey = "folder", Tag = Directory.GetParent(path)?.FullName ?? path };
    listView.Items.Add(upItem);

    var options = new EnumerationOptions { IgnoreInaccessible = true }; // ← права доступа без ошибок!

    var dirInfo = new DirectoryInfo(path);
    foreach (var d in dirInfo.EnumerateDirectories("*", options))
    {
        var item = new ListViewItem(d.Name) { ImageKey = "folder", Tag = d.FullName };
        listView.Items.Add(item);
    }

    foreach (var f in dirInfo.EnumerateFiles("*", options))
    {
        string iconKey = GetIconKey(f.Extension.ToLower());
        var item = new ListViewItem(f.Name) { ImageKey = iconKey, Tag = f.FullName };
        item.SubItems.Add(f.LastWriteTime.ToString("dd.MM.yyyy HH:mm"));
        item.SubItems.Add(FormatSize(f.Length));
        listView.Items.Add(item);
    }
}

private string GetIconKey(string ext)
{
    return ext switch
    {
        ".doc" or ".docx" => "doc",
        ".pdf" => "pdf",
        _ => "default"  // или "file"
    };
}

private string FormatSize(long bytes)
{
    string[] sizes = { "Б", "КБ", "МБ", "ГБ" };
    int order = 0;
    while (bytes >= 1024 && order < sizes.Length - 1)
    {
        order++;
        bytes /= 1024;
    }
    return $"{bytes:F2} {sizes[order]}";
}

private void ListView_DoubleClick(object sender, EventArgs e)
{
    if (listView.SelectedItems.Count == 0) return;
    var item = listView.SelectedItems;
    var tagPath = item.Tag as string;

    if (item.Text == "..")
    {
        var parent = Directory.GetParent(currentPath)?.FullName;
        if (parent != null) LoadDirectory(parent);
    }
    else if (Directory.Exists(tagPath))
        LoadDirectory(tagPath);
    else if (File.Exists(tagPath))
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tagPath) { UseShellExecute = true });
}
```

Остальное (буфер обмена + архивы):

- `ContextMenu_Opening` — проверяем выделенное: если папка → `Compress` enabled, если `.zip` → `Extract`, если что-то выделено → `Copy`.
- Копировать: `Clipboard.SetFileDropList(new StringCollection { fullPaths })`.
- Вставить: `var files = Clipboard.GetFileDropList();` → `File.Copy` / рекурсивное копирование папок (простая функция на ~10 строк).
- Сжать: `ZipFile.CreateFromDirectory(selectedDir, Path.Combine(currentPath, name + ".zip"));`
- Извлечь: `ZipFile.ExtractToDirectory(zipPath, Path.Combine(currentPath, Path.GetFileNameWithoutExtension(zipPath)));`
- Все ошибки: `try { ... } catch (Exception ex) { MessageBox.Show(ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }`

## 5. Что дальше

- Положи папку `icons` рядом с `.exe` (или в проект с `Copy always`).
- Для ещё более крутых иконок можешь добавить `dotnet add package Rop.Winforms.Icons.MaterialDesign` и использовать Material-иконки как картинки.
- Запуск: `dotnet run` прямо в VS Code.

Если нужно — могу дать полный готовый код `Form1.cs` (или отдельные методы) или помочь с рекурсивным копированием папок. Просто скажи, на каком этапе застрял.

Это полностью соответствует заданию, но выглядит и разрабатывается современно. Удачи с лабой — должно зайти преподавателю! 🚀
```

Источники
[1] Самописный дизайнер форм (WinForms) для VS Code https://myseldon.com/ru/news/index/342742735
[2] Среда разработки Visual Studio и другие https://edu.mmcs.sfedu.ru/mod/page/view.php?id=30399
[3] Самописный дизайнер форм (WinForms) для VS Code / Habr https://habr.com/ru/amp/publications/1006240/
[4] Самописный дизайнер форм (WinForms) для VS Code - Habr https://habr.com/ru/articles/1006240/
[5] Знакомство с Visual Studio Code https://csharp.webdelphi.ru/znakomstvo-s-visual-studio-code/
[6] Установка, настройка visual studio code  + Net Core + C# + GIT https://www.youtube.com/watch?v=E722DIpUhlY
[7] Сведения о пакете разработки для Visual Studio Code - Contributor guide https://learn.microsoft.com/ru-ru/contribute/content/how-to-write-docs-auth-pack
[8] Шаблон метаданных и Markdown для документации .NET https://learn.microsoft.com/ru-ru/contribute/content/dotnet/dotnet-style-guide
[9] products.aspose.com › cells › net › create › md https://products.aspose.com/cells/ru/net/create/md/
[10] Текстовый редактор https://www.cyberforum.ru/windows-forms/thread2385172.html
