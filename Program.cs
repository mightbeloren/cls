namespace cls;

enum Mode { Normal, PendingDelete, Cut, Copy }

class Program
{
    static int cursor = 0;
    static string[] files = [];
    static string currentDir = Directory.GetCurrentDirectory();
    static HashSet<int> selectedLines = [];
    static Mode currentMode = Mode.Normal;
    static int pendingLine = -1;
    static List<string> clipboard = [];
    static Mode clipboardOp = Mode.Normal;
    static bool renamingMode = false;
    static string renameBuffer = "";

    static void Main(string[] args)
    {
        Console.Clear();
        Console.CursorVisible = false;
        LoadFiles();

        while (true)
        {
            Render();
            var key = Console.ReadKey(true);
            HandleKey(key);
        }
    }

    static void LoadFiles()
    {
        var dirs = Directory.GetDirectories(currentDir).Select(d => d + "/");
        var fs = Directory.GetFiles(currentDir);
        files = dirs.Concat(fs).ToArray();
        if (cursor >= files.Length) cursor = Math.Max(0, files.Length - 1);
    }

    static void Render()
    {
        Console.Clear();

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(currentDir);
        Console.ResetColor();
        Console.WriteLine();

        for (int i = 0; i < files.Length; i++)
        {
            string name = Path.GetFileName(files[i].TrimEnd('/')) + (files[i].EndsWith("/") ? "/" : "");
            bool isSelected = selectedLines.Contains(i);
            bool isCursor = i == cursor;
            bool isPending = currentMode == Mode.PendingDelete && (isSelected || (selectedLines.Count == 0 && i == pendingLine));
            bool isCutCopy = (currentMode == Mode.Cut || currentMode == Mode.Copy) && (isSelected || (selectedLines.Count == 0 && i == pendingLine));

            if (isCursor)
            {
                Console.BackgroundColor = ConsoleColor.White;
                Console.ForegroundColor = ConsoleColor.Black;
            }
            else if (isPending)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            else if (isCutCopy)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
            }
            else if (isSelected)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
            }

            if (renamingMode && isCursor)
                Console.WriteLine(("  > " + renameBuffer).PadRight(Console.WindowWidth));
            else
                Console.WriteLine(("  " + name).PadRight(Console.WindowWidth));

            Console.ResetColor();
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        string status = currentMode switch
        {
            Mode.PendingDelete => "[d] confirm delete | [Esc] cancel",
            Mode.Cut => "[p] paste to move | [Esc] cancel",
            Mode.Copy => "[p] paste to copy | [Esc] cancel",
            _ => "[j/k] move  [Tab] select  [dd] delete  [x] cut  [c] copy  [p] paste  [r] rename  [..] parent  [q] quit"
        };
        if (renamingMode) status = $"Rename: {renameBuffer}_ | [Enter] confirm  [Esc] cancel";
        // Console.WriteLine(status);
        Console.ResetColor();
    }

    static void HandleKey(ConsoleKeyInfo key)
    {
        if (renamingMode)
        {
            HandleRenameInput(key);
            return;
        }

        switch (key.Key)
        {
            case ConsoleKey.Escape:
                Reset();
                break;

            case ConsoleKey.Tab:
                if (selectedLines.Contains(cursor))
                    selectedLines.Remove(cursor);
                else
                    selectedLines.Add(cursor);
                break;

            case ConsoleKey.Enter:
                OpenOrEnter();
                break;

            default:
                HandleCharKey(key);
                break;
        }
    }

    static void HandleCharKey(ConsoleKeyInfo key)
    {
        switch (key.KeyChar)
        {
            case 'j':
                if (cursor < files.Length - 1) cursor++;
                break;

            case 'k':
                if (cursor > 0) cursor--;
                break;

            case 'd':
                if (currentMode == Mode.PendingDelete)
                {
                    var targets = GetTargets();
                    foreach (var path in targets)
                    {
                        if (Directory.Exists(path))
                            Directory.Delete(path, true);
                        else if (File.Exists(path))
                            File.Delete(path);
                    }
                    Reset();
                    LoadFiles();
                }
                else if (currentMode == Mode.Normal)
                {
                    currentMode = Mode.PendingDelete;
                    pendingLine = cursor;
                }
                break;

            case 'x':
                if (currentMode == Mode.Normal)
                {
                    currentMode = Mode.Cut;
                    pendingLine = cursor;
                    clipboard = GetTargets();
                    clipboardOp = Mode.Cut;
                }
                break;

            case 'c':
                if (currentMode == Mode.Normal)
                {
                    currentMode = Mode.Copy;
                    pendingLine = cursor;
                    clipboard = GetTargets();
                    clipboardOp = Mode.Copy;
                }
                break;

            case 'p':
                if (clipboard.Count > 0)
                {
                    foreach (var src in clipboard)
                    {
                        string name = Path.GetFileName(src.TrimEnd('/'));
                        string dest = Path.Combine(currentDir, name);
                        if (clipboardOp == Mode.Cut)
                        {
                            if (Directory.Exists(src))
                                Directory.Move(src, dest);
                            else if (File.Exists(src))
                                File.Move(src, dest);
                        }
                        else if (clipboardOp == Mode.Copy)
                        {
                            if (Directory.Exists(src))
                                CopyDirectory(src, dest);
                            else if (File.Exists(src))
                                File.Copy(src, dest, overwrite: false);
                        }
                    }
                    clipboard.Clear();
                    clipboardOp = Mode.Normal;
                    currentMode = Mode.Normal;
                    selectedLines.Clear();
                    LoadFiles();
                }
                break;

            case 'r':
                if (files.Length > 0)
                {
                    renamingMode = true;
                    renameBuffer = Path.GetFileName(files[cursor].TrimEnd('/'));
                }
                break;

            case '.':
                if (key.KeyChar == '.')
                {
                    var parent = Directory.GetParent(currentDir)?.FullName;
                    if (parent != null)
                    {
                        currentDir = parent;
                        cursor = 0;
                        selectedLines.Clear();
                        LoadFiles();
                    }
                }
                break;

            case 'q':
                Console.CursorVisible = true;
                Console.Clear();
                Environment.Exit(0);
                break;
        }
    }

    static void HandleRenameInput(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            renamingMode = false;
            renameBuffer = "";
            return;
        }

        if (key.Key == ConsoleKey.Enter)
        {
            string oldPath = files[cursor].TrimEnd('/');
            string newPath = Path.Combine(currentDir, renameBuffer);
            if (oldPath != newPath)
            {
                if (Directory.Exists(oldPath))
                    Directory.Move(oldPath, newPath);
                else if (File.Exists(oldPath))
                    File.Move(oldPath, newPath);
            }
            renamingMode = false;
            renameBuffer = "";
            LoadFiles();
            return;
        }

        if (key.Key == ConsoleKey.Backspace && renameBuffer.Length > 0)
        {
            renameBuffer = renameBuffer[..^1];
            return;
        }

        if (!char.IsControl(key.KeyChar))
            renameBuffer += key.KeyChar;
    }

    static void OpenOrEnter()
    {
        if (files.Length == 0) return;
        string path = files[cursor].TrimEnd('/');

        if (Directory.Exists(path))
        {
            currentDir = path;
            cursor = 0;
            selectedLines.Clear();
            currentMode = Mode.Normal;
            LoadFiles();
        }
        else if (File.Exists(path))
        {
            Console.CursorVisible = true;
            Console.Clear();
            var p = System.Diagnostics.Process.Start("xdg-open", path);
            Console.CursorVisible = false;
        }
    }

    static List<string> GetTargets()
    {
        if (selectedLines.Count > 0)
            return selectedLines.Select(i => files[i].TrimEnd('/')).ToList();
        return new List<string> { files[cursor].TrimEnd('/') };
    }

    static void Reset()
    {
        currentMode = Mode.Normal;
        selectedLines.Clear();
        pendingLine = -1;
        renamingMode = false;
        renameBuffer = "";
    }

    static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
        foreach (var dir in Directory.GetDirectories(src))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }
}
