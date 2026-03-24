using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;

namespace FileManager
{
    public class Form1 : Form
    {
        // === НАСТРОЙКА ИКОНОК ===
        // СКАЧАЙТЕ PNG-иконки с https://fonts.google.com/icons?icon.size=24&icon.color=%23e8eaed
        // Рекомендуемые иконки (размер 24x24):
        // 1. folder (для папок и "..")
        // 2. insert_drive_file (иконка файла по умолчанию)
        // 3. description (для .txt)
        // 4. code (для .exe)
        // 5. image (для .jpg/.png)
        //
        // ВСТАВЛЯЙТЕ СВОИ ПУТИ ТОЛЬКО ЗДЕСЬ:
        private readonly string[] iconPaths = new string[5]
        {
            @"C:\Icons\folder.png",          // 0 — папка
            @"C:\Icons\default_file.png",    // 1 — файл по умолчанию
            @"C:\Icons\text.png",            // 2 — .txt
            @"C:\Icons\exe.png",             // 3 — .exe
            @"C:\Icons\image.png"            // 4 — изображения
        };

        private string currentPath;
        private ListView listView1;
        private TextBox textBoxPath;
        private MenuStrip menuStrip1;
        private ContextMenuStrip contextMenuStrip1;
        private ImageList imageList1;

        public Form1()
        {
            InitializeUI();
            LoadIcons();
            LoadDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }

        private void InitializeUI()
        {
            this.Text = "Файловый менеджер — Лабораторная работа №17";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 10);
            this.BackColor = Color.White;
            this.MinimumSize = new Size(800, 600);

            // === Строка меню ===
            menuStrip1 = new MenuStrip
            {
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(245, 245, 245)
            };
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("Файл");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Выход", null, (s, e) => Application.Exit()));
            menuStrip1.Items.Add(fileMenu);
            this.Controls.Add(menuStrip1);

            // === Поле текущего каталога ===
            Panel pathPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(240, 240, 240)
            };

            Label lblPath = new Label
            {
                Text = "Путь:",
                Location = new Point(10, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            textBoxPath = new TextBox
            {
                Location = new Point(60, 8),
                Width = 780,
                Font = new Font("Consolas", 10),
                Text = ""
            };
            textBoxPath.KeyDown += TextBoxPath_KeyDown;

            Button btnGo = new Button
            {
                Text = "Перейти",
                Location = new Point(850, 7),
                Width = 90,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White
            };
            btnGo.Click += (s, e) => NavigateToPath();

            pathPanel.Controls.Add(lblPath);
            pathPanel.Controls.Add(textBoxPath);
            pathPanel.Controls.Add(btnGo);
            this.Controls.Add(pathPanel);

            // === Список файлов ===
            listView1 = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = true,
                SmallImageList = null, // будет присвоен позже
                Font = new Font("Segoe UI", 10),
                BackColor = Color.White,
                ForeColor = Color.Black
            };

            listView1.Columns.Add("Имя", 380);
            listView1.Columns.Add("Дата изменения", 160);
            listView1.Columns.Add("Размер", 120);

            listView1.DoubleClick += ListView1_DoubleClick;
            listView1.MouseClick += ListView1_MouseClick; // для контекстного меню

            this.Controls.Add(listView1);

            // === Контекстное меню ===
            contextMenuStrip1 = new ContextMenuStrip();
            contextMenuStrip1.Items.Add(new ToolStripMenuItem("Копировать", null, Copy_Click));
            contextMenuStrip1.Items.Add(new ToolStripMenuItem("Вставить", null, Paste_Click));
            contextMenuStrip1.Items.Add(new ToolStripSeparator());
            contextMenuStrip1.Items.Add(new ToolStripMenuItem("Сжать в архив", null, Compress_Click));
            contextMenuStrip1.Items.Add(new ToolStripMenuItem("Извлечь из архива", null, Extract_Click));

            listView1.ContextMenuStrip = contextMenuStrip1;
        }

        private void LoadIcons()
        {
            imageList1 = new ImageList
            {
                ImageSize = new Size(24, 24),
                ColorDepth = ColorDepth.Depth32Bit
            };

            try
            {
                for (int i = 0; i < iconPaths.Length; i++)
                {
                    if (File.Exists(iconPaths[i]))
                        imageList1.Images.Add(Image.FromFile(iconPaths[i]));
                    else
                        imageList1.Images.Add(new Bitmap(24, 24)); // заглушка
                }
            }
            catch
            {
                // если иконки не найдены — используем пустые
                for (int i = 0; i < 5; i++)
                    imageList1.Images.Add(new Bitmap(24, 24));
            }

            listView1.SmallImageList = imageList1;
        }

        private void LoadDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                MessageBox.Show("Указанный каталог не существует!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            currentPath = path;
            textBoxPath.Text = currentPath;
            listView1.Items.Clear();

            // Специальный элемент «..»
            ListViewItem upItem = new ListViewItem("..")
            {
                ImageIndex = 0,
                Tag = "UP"
            };
            listView1.Items.Add(upItem);

            try
            {
                // Папки
                foreach (string dir in Directory.GetDirectories(path).OrderBy(d => Path.GetFileName(d)))
                {
                    DirectoryInfo di = new DirectoryInfo(dir);
                    ListViewItem item = new ListViewItem(Path.GetFileName(dir))
                    {
                        ImageIndex = 0,
                        Tag = dir
                    };
                    item.SubItems.Add(di.LastWriteTime.ToString("dd.MM.yyyy HH:mm"));
                    item.SubItems.Add("");
                    listView1.Items.Add(item);
                }

                // Файлы
                foreach (string file in Directory.GetFiles(path).OrderBy(f => Path.GetFileName(f)))
                {
                    FileInfo fi = new FileInfo(file);
                    int imgIndex = GetImageIndex(fi.Name);

                    ListViewItem item = new ListViewItem(fi.Name)
                    {
                        ImageIndex = imgIndex,
                        Tag = file
                    };
                    item.SubItems.Add(fi.LastWriteTime.ToString("dd.MM.yyyy HH:mm"));
                    item.SubItems.Add(FormatFileSize(fi.Length));
                    listView1.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки каталога:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private int GetImageIndex(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".txt" => 2,
                ".exe" or ".dll" => 3,
                ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" => 4,
                _ => 1 // файл по умолчанию
            };
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 Б";
            string[] sizes = { "Б", "КБ", "МБ", "ГБ" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        private void ListView1_DoubleClick(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0) return;

            ListViewItem item = listView1.SelectedItems[0];
            string name = item.Text;

            if (name == "..")
            {
                DirectoryInfo parent = Directory.GetParent(currentPath);
                if (parent != null)
                    LoadDirectory(parent.FullName);
                return;
            }

            string fullPath = item.Tag?.ToString();
            if (string.IsNullOrEmpty(fullPath)) return;

            if (Directory.Exists(fullPath))
            {
                LoadDirectory(fullPath);
            }
            else if (File.Exists(fullPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось открыть файл:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void TextBoxPath_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                NavigateToPath();
        }

        private void NavigateToPath()
        {
            string newPath = textBoxPath.Text.Trim();
            if (Directory.Exists(newPath))
                LoadDirectory(newPath);
            else
                MessageBox.Show("Каталог не существует!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ListView1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Контекстное меню уже привязано к listView1
            }
        }

        // ====================== КОПИРОВАТЬ ======================
        private void Copy_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0) return;

            StringCollection files = new StringCollection();

            foreach (ListViewItem item in listView1.SelectedItems)
            {
                if (item.Text != "..")
                {
                    string path = item.Tag?.ToString();
                    if (!string.IsNullOrEmpty(path))
                        files.Add(path);
                }
            }

            if (files.Count > 0)
            {
                Clipboard.SetFileDropList(files);
                // Небольшая подсказка
                ToolStripMenuItem menuItem = sender as ToolStripMenuItem;
                if (menuItem != null) menuItem.Text = "Копировать ✓";
                System.Threading.Tasks.Task.Delay(800).ContinueWith(_ =>
                    this.Invoke(new Action(() => { if (menuItem != null) menuItem.Text = "Копировать"; })));
            }
        }

        // ====================== ВСТАВИТЬ ======================
        private void Paste_Click(object sender, EventArgs e)
        {
            if (!Clipboard.ContainsFileDropList())
            {
                MessageBox.Show("В буфере обмена нет файлов или папок!", "Вставить", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            StringCollection files = Clipboard.GetFileDropList();
            bool anySuccess = false;

            foreach (string src in files)
            {
                try
                {
                    string name = Path.GetFileName(src);
                    string target = Path.Combine(currentPath, name);

                    if (Directory.Exists(src))
                    {
                        if (Directory.Exists(target))
                        {
                            MessageBox.Show($"Папка уже существует:\n{target}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            continue;
                        }
                        CopyDirectory(src, target);
                        anySuccess = true;
                    }
                    else if (File.Exists(src))
                    {
                        if (File.Exists(target))
                        {
                            MessageBox.Show($"Файл уже существует:\n{target}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            continue;
                        }
                        File.Copy(src, target, false);
                        anySuccess = true;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при вставке {Path.GetFileName(src)}:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            if (anySuccess)
                LoadDirectory(currentPath);
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, destFile, false);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string destDir = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
            }
        }

        // ====================== СЖАТЬ В АРХИВ ======================
        private void Compress_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count != 1)
            {
                MessageBox.Show("Выберите одну папку для сжатия!", "Сжать в архив", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ListViewItem item = listView1.SelectedItems[0];
            if (item.Text == "..") return;

            string fullPath = item.Tag?.ToString();
            if (!Directory.Exists(fullPath))
            {
                MessageBox.Show("Выберите каталог!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string folderName = Path.GetFileName(fullPath);
            string zipPath = Path.Combine(currentPath, folderName + ".zip");

            if (File.Exists(zipPath))
            {
                MessageBox.Show("Архив с таким именем уже существует!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                ZipFile.CreateFromDirectory(fullPath, zipPath);
                MessageBox.Show($"Архив успешно создан:\n{zipPath}", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadDirectory(currentPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания архива:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ====================== ИЗВЛЕЧЬ ИЗ АРХИВА ======================
        private void Extract_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count != 1)
            {
                MessageBox.Show("Выберите один ZIP-файл!", "Извлечь из архива", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ListViewItem item = listView1.SelectedItems[0];
            if (item.Text == "..") return;

            string fullPath = item.Tag?.ToString();
            if (!File.Exists(fullPath) || !fullPath.ToLower().EndsWith(".zip"))
            {
                MessageBox.Show("Выберите файл с расширением .zip!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string extractPath = Path.Combine(currentPath, Path.GetFileNameWithoutExtension(fullPath));

            if (Directory.Exists(extractPath))
            {
                MessageBox.Show("Папка с таким именем уже существует!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                ZipFile.ExtractToDirectory(fullPath, extractPath);
                MessageBox.Show($"Файлы успешно извлечены в:\n{extractPath}", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadDirectory(currentPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка извлечения архива:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
